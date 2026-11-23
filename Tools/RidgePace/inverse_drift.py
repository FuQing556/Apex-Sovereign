"""Offline inverse design for two laps of Ridge. No Unity process or ghost inputs."""
from pathlib import Path
import csv
import json
import re
import numpy as np
from scipy.interpolate import CubicSpline
from scipy.ndimage import gaussian_filter1d, minimum_filter1d
from scipy.optimize import least_squares, lsq_linear
from scipy.sparse import coo_matrix, eye
from scipy.sparse.linalg import spsolve
from scipy.spatial import cKDTree
import yaml

ROOT = Path(__file__).resolve().parents[2]
VEH = ROOT / "Assets/_Project/Vehicles/SHIBATA31"
OUT = ROOT / "毕设资料/05-试验记录/山脊双圈漂移逆解"
LOGS = ROOT / "Logs/DriveTelemetry"
SOURCE = "20261123-022232-410-Ridge-01c464b3.csv"
HOLDOUT = "20261123-023447-742-Ridge-73a0ea1d.csv"
G = 9.81
DT = .02


def asset(path):
    return yaml.safe_load(path.read_text(encoding="utf-8").split("MonoBehaviour:", 1)[1])


car = asset(VEH / "Okuibuki.asset")
setup = asset(VEH / "OkuibukiDynamics.asset")
power = asset(VEH / "VR38_1300hp_estimated.asset")
road = asset(ROOT / "Assets/_Project/Tracks/Ridge.asset")
mass = car["mass"]
wheelbase = car["wheelbase"]
front_arm = wheelbase * (1 - car["frontWeight"])
rear_arm = wheelbase - front_arm
inertia_per_kg = 2325.74 / mass
rho = setup["airDensity"]
gears = np.array(car["gears"]) * car["finalDrive"]
power_points = power["fullThrottlePower"]["m_Curve"]
brake_force = []
for axle, fraction, radius in [("front", setup["frontPushrodFraction"], car["radius"]),
                              ("rear", 1-setup["frontPushrodFraction"], car["rearRadius"])]:
    pressure = setup["pedalForce"]*setup["pedalRatio"]*fraction / (np.pi*(setup[axle+"MasterMm"]/2000)**2)
    brake_force.append(4*pressure*setup[axle+"PistonArea"]*1e-6*setup[axle+"DiscRadius"]*setup["padFriction"]/radius)
brake_split = brake_force[0]/sum(brake_force)
brake_limit = sum(brake_force)/mass


def sectors():
    folder=ROOT/"Assets/_Project/Editor"
    def numbers(file, name):
        match=re.search(name+r"=\{([^}]+)",(folder/file).read_text())
        return np.array([int(x) for x in match[1].split(",")])
    return numbers("RidgeCornerPace.cs","ApexM"), numbers("RidgeLineSearch.cs","side")


def nominal_drive(v, gear):
    ratio = gears[gear]
    rpm = np.clip(v * ratio * 60/(2*np.pi*car["rearRadius"]), setup["launchRpm"], car["upshift"])
    power_frac = np.interp(rpm, [p["time"] for p in power_points], [p["value"] for p in power_points])
    torque = power_frac*power["peakMechanicalHp"]*745.699872/(rpm*2*np.pi/60)
    return np.minimum(8.2, torque*ratio*car["efficiency"]/(car["rearRadius"]*mass))


thrust_map=None
rpm_centers=np.arange(0,9501,500)
angle_centers=np.arange(0,51,10)


def drive_limit(v,gear,beta=0.):
    if thrust_map is None: return nominal_drive(v,gear)
    rpm=v*gears[gear]*60/(2*np.pi*car["rearRadius"])
    x=np.clip(rpm/500,0,len(rpm_centers)-1.00001)
    y=np.clip(abs(np.degrees(beta))/10,0,len(angle_centers)-1.00001)
    i,j=x.astype(int),np.asarray(y).astype(int)
    x=x-i;y=y-j
    return ((1-x)*((1-y)*thrust_map[gear,i,j]+y*thrust_map[gear,i,j+1])
            +x*((1-y)*thrust_map[gear,i+1,j]+y*thrust_map[gear,i+1,j+1]))


def automatic(v):
    gear = 0
    result = []
    for speed in v:
        rpm = speed*gears[gear]*60/(2*np.pi*car["rearRadius"])
        if rpm > car["upshift"] and gear < len(gears)-1:
            gear += 1
        elif rpm < car["downshift"] and gear > 0:
            gear -= 1
        result.append(gear)
    return np.array(result)


def tire(alpha, long_acc, load, params):
    peak, slope, fall, combined = params
    fraction = long_acc / np.maximum(load, 2)
    return (load*peak*np.tanh(slope*alpha)*(1-fall*np.tanh(2.4*alpha)**2)
            /np.sqrt(1 + combined*fraction**2))


def observations(name):
    with (LOGS/name).open(encoding="utf-8-sig") as stream:
        rows = list(csv.DictReader(stream))
    def column(k):
        return np.array([float(r[k]) for r in rows])
    def smooth(x):
        return gaussian_filter1d(x, 3)
    valid = np.array([r["phase"]=="Driving" and r["contact_body"]=="0" and r["hand"]=="0"
                      and r["setup_revision"]=="8" and r["differential_part"]=="club-lsd"
                      and r["rear_wheel_part"]=="original" and all(r[w+"_cover"]=="沥青" for w in ("FL","FR","RL","RR"))
                      for r in rows])
    v, z, x = (smooth(column(k)) for k in ("speed_mps","body_forward_mps","body_lateral_mps"))
    yaw = smooth(np.radians(column("yaw_deg_s")))
    delta = smooth(np.radians((column("FL_steer_deg")+column("FR_steer_deg"))/2))
    b = np.arctan2(x,z)
    ay = np.gradient(smooth(column("y")), DT)
    grade = np.clip(ay/np.maximum(v,4), -.2,.2)
    # Differentiate body-frame velocity with transport terms. X is body-right.
    lateral = np.gradient(x, DT) + yaw*z + G*grade*np.sin(b) + .5*rho*setup["sideDragArea"]/mass*x*abs(x)
    long = np.gradient(z, DT) - yaw*x + G*grade*np.cos(b) + .5*rho*setup["dragArea"]/mass*z*abs(z)+G*setup["rollingLossCoefficient"]
    front = (rear_arm*lateral + inertia_per_kg*np.gradient(yaw,DT))/wheelbase
    rear = lateral-front
    fx = -smooth(column("FL_brake_nm")+column("FR_brake_nm"))/(mass*car["radius"])
    fy = (front-fx*np.sin(delta))/np.cos(delta)
    rx = long - (fx*np.cos(delta)-fy*np.sin(delta))
    normal = np.clip(G+np.gradient(ay,DT)+.5*rho*setup["downforceArea"]*v*v/mass, 5,16)
    front_load = normal*car["frontWeight"] - .32/wheelbase*(fx+rx)
    af = delta - np.arctan2(x+front_arm*yaw,z)
    ar = -np.arctan2(x-rear_arm*yaw,z)
    valid = minimum_filter1d(valid.astype(int),size=23)>0
    valid &= (z>7)&(abs(b)<np.radians(41))&(abs(rx)<14)&(abs(fy)<17)&(abs(rear)<17)
    return [(af,fx,front_load,fy), (ar,rx,normal-front_load,rear)],valid,column("sim_s")


def identify():
    train, valid, clock = observations(SOURCE)
    test, held, _ = observations(HOLDOUT)
    result, report = [], []
    for tr, te in zip(train,test):
        anchor = valid & (clock>=146.5598)&(clock<=150.3201)
        weight = np.where(anchor[valid],1.7,1.)
        p = least_squares(lambda p: (tire(*(a[valid] for a in tr[:3]),p)-tr[3][valid])*weight,
                          [1.5,8,.1,.5], bounds=([.6,2,0,.01],[2.3,35,.55,8]),loss="soft_l1",f_scale=.6).x
        result.append(p)
        def rmse(data,mask):
            return float(np.sqrt(np.mean((tire(*(a[mask] for a in data[:3]),p)-data[3][mask])**2)))
        report.append(dict(parameters=p.tolist(),train_n=int(valid.sum()),test_n=int(held.sum()),
                           train_rmse_mps2=rmse(tr,valid),holdout_rmse_mps2=rmse(te,held),anchor_rmse_mps2=rmse(tr,anchor)))
    def full_w(name,obs,mask):
        with (LOGS/name).open(encoding="utf-8-sig") as stream: rows=list(csv.DictReader(stream))
        num=lambda k:np.array([float(z[k]) for z in rows])
        v=num("speed_mps");gear=num("gear").astype(int)-1
        uninterrupted=minimum_filter1d((num("W")*(1-num("S"))*(1-num("space"))).astype(int),size=33)>0
        constant=minimum_filter1d(gear,size=23)==-minimum_filter1d(-gear,size=23)
        include=mask&uninterrupted&constant
        return v[include],gear[include],abs(num("signed_drift_deg")[include]),obs[1][1][include],include
    v,gear,angle,force,included=full_w(SOURCE,train,valid)
    rpm=v*gears[gear]*60/(2*np.pi*car["rearRadius"])
    global thrust_map
    thrust_map=np.empty((len(gears),len(rpm_centers),len(angle_centers)))
    for g,ratio in enumerate(gears):
        for i,reference in enumerate(rpm_centers):
            for j,slip in enumerate(angle_centers):
                # Sparse gears borrow the same road-referenced rpm cell, with the gear ratio applied.
                weights=np.exp(-.5*((rpm-reference)/560)**2-.5*((angle-slip)/8)**2)*np.where(gear==g,1,.10)
                prior=nominal_drive(reference*2*np.pi*car["rearRadius"]/(60*ratio),g)
                thrust_map[g,i,j]=np.clip((4*prior+weights@np.clip(force*ratio/gears[gear],.2,11))/(4+weights.sum()),.3,9.5)
    wv,wg,wb,measured,_=full_w(HOLDOUT,test,held)
    estimate=drive_limit(wv,wg,np.radians(wb))
    anchor=(clock[included]>=146.5598)&(clock[included]<=150.3201)
    report.append(dict(name="持续全油门时反推后轴纵向推力除以质量",train_frames=len(v),holdout_frames=len(wv),
                       training_gear_counts=np.bincount(gear+1,minlength=7).tolist(),
                       observed_speed_range_kph=[min(v)*3.6,max(v)*3.6],
                       anchor_estimate=float(np.mean(drive_limit(v[anchor],gear[anchor],np.radians(angle[anchor])))),
                       anchor_observed=float(np.mean(force[anchor])),holdout_rmse_mps2=float(np.mean((estimate-measured)**2)**.5),
                       nominal_only_holdout_rmse_mps2=float(np.mean((nominal_drive(wv,wg)-measured)**2)**.5)))
    return result, report


def unit(x):
    return x/np.linalg.norm(x,axis=-1,keepdims=True)


def geometry():
    knots = np.array([[k[c] for c in "xyz"] for k in road["knots"]])
    stride = road["samplesPerSpan"]
    a,b,c,d = [np.roll(knots,-i,axis=0)[:,None,:] for i in (-1,0,1,2)]
    t = np.arange(stride)[None,:,None]/stride
    mesh = (.5*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t**3)).reshape(-1,3)
    tangent = unit(np.roll(mesh,-1,axis=0)-np.roll(mesh,1,axis=0))
    side = unit(np.cross([0,1,0],tangent))
    ring = np.vstack([mesh,mesh[0]])
    station = np.r_[0,np.cumsum(np.linalg.norm(np.diff(ring,axis=0),axis=1))]
    length = station[-1]
    n = int(round(length/2))
    s = np.arange(n)*length/n
    center = CubicSpline(station,ring,bc_type="periodic")(s)
    normal = unit(np.cross([0,1,0],np.roll(center,-1,axis=0)-np.roll(center,1,axis=0)))
    count = round(length/13)
    basis = CubicSpline(np.linspace(0,length,count+1),np.vstack([np.eye(count),np.eye(count)[0]]),bc_type="periodic")(s)
    move = normal[:,:,None]*basis[:,None,:]
    def second(x):
        return np.roll(x,1,axis=0)+np.roll(x,-1,axis=0)-2*x
    # Minimise bending and abrupt curvature changes subject to a whole-car margin.
    pinned = np.ones(count,dtype=bool)
    pinned[[0,1,-1,2,-2]] = False
    A, target = second(move), -second(center)
    def difference(x):
        return np.roll(x,-1,axis=0)-x
    matrix = np.vstack([A.reshape(-1,count),6*difference(A).reshape(-1,count),.008*basis])
    rhs = np.r_[target.ravel(),6*difference(target).ravel(),np.zeros(n)]
    coeff = np.zeros(count)
    apex,sign=sectors()
    near=np.argmin(abs(s[:,None]-apex),axis=1)
    inside=abs(s-apex[near])<18
    signed=sign[near,None]*np.einsum("ijk,ij->ik",A,normal)/(length/n)**2
    baseline=-sign[near]*np.einsum("ij,ij->i",target,normal)/(length/n)**2
    for _ in range(10):
        active=inside & (baseline+signed@coeff < .0155)
        weight=14
        m=np.vstack([matrix,weight*signed[active]])
        y=np.r_[rhs,weight*(.0155-baseline[active])]
        next_coeff=lsq_linear(m[:,pinned],y,bounds=(-6.4,6.4),method="bvls").x
        coeff[pinned]=.35*coeff[pinned]+.65*next_coeff
    line = center + normal*(basis@coeff)[:,None]
    line3 = CubicSpline(np.r_[s,length],np.vstack([line,line[0]]),bc_type="periodic")
    offset=CubicSpline(np.r_[s,length],np.r_[basis@coeff,(basis@coeff)[0]],bc_type="periodic")
    return dict(mesh=mesh,side=side,tangent=tangent,station=station,length=length,line=line3,offset=offset)


def problem(geo,step=4.8):
    length = geo["length"]
    n = round(length/step)
    lap_s = np.sort(np.r_[np.linspace(0,length,n+1),.14,.45,1.2,2.5])
    n=len(lap_s)-1
    s=np.r_[lap_s,lap_s[1:]+length]
    path = geo["line"]
    local = s%length
    midpoint = ((s[1:]+s[:-1])/2)%length
    def geom(q):
        p1,p2 = path(q,1),path(q,2)
        h = np.hypot(p1[:,0],p1[:,2])
        return ((p1[:,2]*p2[:,0]-p1[:,0]*p2[:,2])/h**3, p1[:,1]/h,
                (p2[:,1]-p1[:,1]*(p2[:,0]*p1[:,0]+p2[:,2]*p1[:,2])/h**2)/h**2)
    k,grade,vertical = geom(midpoint)
    # Segment lengths by Simpson integration of horizontal speed per station.
    h = lambda q: np.linalg.norm(path(q,1)[:,[0,2]],axis=1)
    ds = np.diff(s)*(h(local[:-1])+4*h(midpoint)+h(local[1:]))/6
    kn = geom(local)[0]
    v = np.minimum(40.,np.sqrt(13.6/np.maximum(abs(kn),.0001)))
    for i in range(len(ds)-1,-1,-1):
        v[i] = min(v[i],np.sqrt(v[i+1]**2+2*5.4*ds[i]))
    v[0] = 0
    for i in range(len(ds)):
        v[i+1] = min(v[i+1],np.sqrt(v[i]**2+2*4.4*ds[i]))
    apex,directions=sectors()
    # Smooth drift demand in each of the project's 16 named sectors, with room to unwind.
    beta=np.zeros(len(s))
    for peak,sign in zip(np.r_[apex,apex+length],np.tile(directions,2)):
        beta-=sign*np.radians(15)*np.exp(-.5*((s-peak)/20)**2)
    beta+=-np.radians(8)*np.tanh(kn/.012)
    beta*=np.clip(s/55,0,1)
    return dict(s=s,n=n,k=k,grade=grade,vertical=vertical,ds=ds,target_v=v,target_b=beta,apex=apex,
                offset=geo["offset"](local))


def solve(pr, params, core=(-13,21), time_weight=0.):
    s,ds,k=pr["s"],pr["ds"],pr["k"]
    want_v,want_b=pr["target_v"],pr["target_b"]
    count=len(s)
    # Node variables: horizontal speed, body sideslip, yaw rate, road-wheel steer, net wheel thrust / mass.
    scale=np.array([30,.35,.65,.32,5.,3.,.12])
    state=np.column_stack([want_v,want_b,np.r_[k,k[-1]]*want_v,
                           want_b + np.arctan(wheelbase*np.r_[k,k[-1]]),np.gradient(want_v**2/2)/np.r_[ds,ds[-1]],
                           np.zeros((count,2))])
    state[0]=0
    state/=scale
    tag=np.arange(count-1)
    gear=automatic(want_v)
    apex,directions=sectors()
    centers=np.r_[apex,apex+s[pr["n"]]]
    mid_s=(s[:-1]+s[1:])*.5
    which=np.argmin(abs(mid_s[:,None]-centers),axis=1)
    sign=np.tile(directions,2)[which]
    in_corner=abs(mid_s-centers[which])<12.8
    gap=mid_s-centers[which]
    preload=(gap>=-36)&(gap<=-22)
    neutral_core=(gap>=core[0])&(gap<=core[1])
    def residual(left,right,w,details=False):
        v,b,r,d,u,e,h=((left+right)*.5*scale).T
        v0,b0,r0,d0,u0,e0,h0=(left*scale).T
        v1,b1,r1,d1,u1,e1,h1=(right*scale).T
        stretch=(1-k*e)/np.cos(h)
        dt=ds*stretch/np.maximum(v,.005)
        curvature=(k+(h1-h0)/ds)/stretch
        z,x=v*np.cos(b),v*np.sin(b)
        load=np.maximum(4,G+pr["vertical"]*v*v+.5*rho*setup["downforceArea"]*z*z/mass)
        lf=load*car["frontWeight"]-.32/wheelbase*u
        fx=brake_split*np.minimum(0,u)
        rx=u-fx
        fy=tire(d-np.arctan2(x+front_arm*r,np.maximum(z,1)),fx,lf,params[0])
        ry=tire(-np.arctan2(x-rear_arm*r,np.maximum(z,1)),rx,load-lf,params[1])
        front_y=fy*np.cos(d)+fx*np.sin(d)
        body_y=front_y+ry-.5*rho*setup["sideDragArea"]*x*abs(x)/mass
        body_z=rx+fx*np.cos(d)-fy*np.sin(d)-.5*rho*setup["dragArea"]*z*abs(z)/mass
        along=body_z*np.cos(b)+body_y*np.sin(b)-G*(pr["grade"]/stretch+setup["rollingLossCoefficient"])
        normal=body_y*np.cos(b)-body_z*np.sin(b)
        yaw_acc=(front_arm*front_y-rear_arm*ry)/inertia_per_kg
        errors=[(v1-v0)/dt-along,v*v*curvature-normal,(r1-r0)/dt-yaw_acc,(b1-b0)/dt+r-v*curvature,
                (e1-e0)/dt-v*np.sin(h)]
        if details:
            return errors,dt
        power_cap=drive_limit(np.maximum(v,0),gear[:-1],b)
        target=(want_v[:-1]+want_v[1:])/2
        beta_target=(want_b[:-1]+want_b[1:])/2
        direction=np.sign(d1-d0)
        acceleration=np.interp(v*3.6,[40,95],[95,10])
        degrees=np.degrees(d)
        keyboard_rate=np.sqrt(4.5**2+2*acceleration*abs(degrees))
        catch=np.clip(direction*np.degrees(b),0,18)*np.clip(-direction*np.degrees(r)/30,0,1)
        keyboard_rate+=np.maximum(0,catch-direction*degrees)/.10
        center_rate=np.where(direction*d<0,4+abs(degrees)/.1,0)
        allowed=np.radians(np.minimum(165,np.maximum(keyboard_rate,center_rate)))
        # Steer and thrust are continuous across intervals, including the lap seam.
        # The last terms select one drift-rich solution of the underdetermined inverse problem.
        parts=[errors[0],errors[1],2*errors[2],12*errors[3],4*errors[4],
               4*np.maximum(0,u-power_cap),4*np.maximum(0,-u-brake_limit),
               5*np.maximum(0,abs(d)-np.radians(55)),10*np.minimum(0,v0),
               w*(v-target)*.38,w*(b-beta_target)*9,
               w*(d1-d0)/dt*1.6,w*(u1-u0)/dt*.075,w*d*.4,
               w*(b1-b0)/dt*.65,w*(v1-v0)/dt*.055,
               6*np.maximum(0,abs(b1)-np.radians(34)),
               in_corner*15*np.maximum(0,np.radians(14)+sign*b),
               in_corner*4*np.maximum(0,np.radians(8)-sign*r),
               2*np.maximum(0,u1-drive_limit(np.maximum(v1,0),gear[1:],b1)),
               2*np.maximum(0,-u1-brake_limit),
               w*.09*(u1-u0), w*.23*(d1-d0)/dt,
               2*np.maximum(0,abs(d1-d0)/dt-allowed),
               4*np.maximum(0,abs(pr["offset"][1:]+e1)-7.30),
               w*.20*e,w*1.4*h,.02*(u1-u0)/dt,
               .18*(d1-d0)/dt, np.maximum(0,abs(h1)-.36)*8,
               neutral_core*24*abs(d),
               neutral_core*9*abs(d1-d0)/dt,
               preload*2*np.maximum(0,np.radians(2)-sign*d),
               .06*(v-target), .025*e, time_weight*np.sqrt(np.maximum(dt,.0001)),
               neutral_core*(.10+.18*w)*(u-power_cap)]
        return np.array(parts).T
    # A block-banded Gauss-Newton solve: each interval touches exactly two nodes.
    # Finite differences for all left/right nodes of one variable are independent here.
    for weight,iterations in [(1.,85),(.28,80),(.09,90),(.026,120)]:
        damping=.15
        for iteration in range(iterations):
            if iteration%8==0:
                gear=automatic(state[:,0]*scale[0])
            left,right=state[:-1],state[1:]
            f=residual(left,right,weight)
            rows,cols,vals=[],[],[]
            eqs=f.shape[1]
            rr=(tag[:,None]*eqs+np.arange(eqs)).ravel()
            for side in range(2):
                for j in range(len(scale)):
                    shift=np.zeros(len(scale));shift[j]=1e-5
                    df=(residual(left+(shift if side==0 else 0),right+(shift if side else 0),weight)-f)/1e-5
                    rows.append(rr);cols.append(np.repeat((tag+side)*len(scale)+j,eqs));vals.append(df.ravel())
            free=np.ones((count,len(scale)),bool)
            free[0]=False
            free[-1,5]=False  # Exactly cross the baked finish plane at the origin.
            free=free.ravel()
            J=coo_matrix((np.concatenate(vals),(np.concatenate(rows),np.concatenate(cols))),
                         shape=(f.size,count*len(scale))).tocsr()[:,free]
            normal=(J.T@J).tocsc()
            direction=spsolve(normal+damping*eye(normal.shape[0],format="csc"),-J.T@f.ravel())
            change=np.zeros_like(state)
            change.ravel()[free]=direction
            cost=np.sum(f*f)
            accepted=False
            for step_size in (1,.5,.25,.125,.0625,.03125):
                candidate=state+change*step_size
                trial=np.sum(residual(candidate[:-1],candidate[1:],weight)**2)
                if trial<cost:
                    state=candidate;damping=max(.00015,damping*.8)
                    accepted=True
                    break
            if not accepted:
                damping*=5
                if damping>1e6: break
            elif (cost-trial)<1e-7*max(cost,1): break
        print("stage",weight,"iterations",iteration+1,"cost",round(cost,4),flush=True)
    errors,dt=residual(state[:-1],state[1:],0,True)
    return state*scale,np.r_[0,np.cumsum(dt)],np.array(errors).T
