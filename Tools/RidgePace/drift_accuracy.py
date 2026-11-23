"""Independent finite-horizon check of the fitted model against raw player positions."""
import csv
import numpy as np
from scipy.ndimage import gaussian_filter1d
from inverse_drift import (LOGS, SOURCE, HOLDOUT, DT, G, rho, mass, car, setup,
                           wheelbase, front_arm, rear_arm, inertia_per_kg, drive_limit, tire,
                           brake_limit, brake_split, gears)
from drift_controls import steer_step, pedal_step


def derivative(q,delta,thrust,grade,vertical,params):
    speed,beta,yaw,psi,_,_=q
    bz,bx=speed*np.cos(beta),speed*np.sin(beta)
    load=np.maximum(4,G+vertical*speed**2+.5*rho*setup["downforceArea"]*bz*bz/mass)
    front_load=load*car["frontWeight"]-.32/wheelbase*thrust
    fx=brake_split*np.minimum(thrust,0)
    front=tire(delta-np.arctan2(bx+front_arm*yaw,bz),fx,front_load,params[0])
    rear=tire(-np.arctan2(bx-rear_arm*yaw,bz),thrust-fx,load-front_load,params[1])
    fy=front*np.cos(delta)+fx*np.sin(delta)
    ay=fy+rear-.5*rho*setup["sideDragArea"]*bx*abs(bx)/mass
    ax=thrust-fx+fx*np.cos(delta)-front*np.sin(delta)-.5*rho*setup["dragArea"]*bz*abs(bz)/mass
    return np.array([ax*np.cos(beta)+ay*np.sin(beta)-G*(grade+setup["rollingLossCoefficient"]),
                     (ay*np.cos(beta)-ax*np.sin(beta))/np.maximum(speed,1)-yaw,
                     (front_arm*fy-rear_arm*rear)/inertia_per_kg,yaw,
                     speed*np.sin(psi+beta),speed*np.cos(psi+beta)])


def check(name,params,anchor=False):
    with (LOGS/name).open(encoding="utf-8-sig") as f: rows=list(csv.DictReader(f))
    get=lambda k:np.array([float(row[k]) for row in rows])
    clock=get("sim_s");v=get("speed_mps");b=np.radians(get("signed_drift_deg"));r=np.radians(get("yaw_deg_s"))
    x,y,z=get("x"),get("y"),get("z")
    angle=np.unwrap(np.arctan2(get("front_axle_x")-get("rear_axle_x"),get("front_axle_z")-get("rear_axle_z")))
    road_y=gaussian_filter1d(y,4)
    grade=np.clip(np.gradient(road_y,DT)/np.maximum(v,4),-.16,.16)
    vertical=np.clip(np.gradient(np.gradient(road_y,DT),DT),-4,4)/np.maximum(v*v,16)
    gear=get("gear").astype(int)-1
    valid=(get("W")>0)&(get("S")==0)&(get("space")==0)&(get("contact_body")==0)&(v>15)
    valid&=(abs(get("FL_steer_deg"))<=.25)&(abs(get("FR_steer_deg"))<=.25)
    valid&=np.array([all(row[w+"_cover"]=="沥青" for w in ("FL","FR","RL","RR")) for row in rows])
    if anchor:
        windows=[(np.argmin(abs(clock-146.5599)),np.argmin(abs(clock-150.3199)))]
    else:
        windows=[]
        i=0
        while i+50<len(v):
            if valid[i:i+51].all() and .1396<=abs(b[i])<=.52 and np.all(gear[i:i+51]==gear[i]):
                windows.append((i,i+50));i+=51
            else: i+=1
    def deriv(q,i):
        thrust=drive_limit(np.array(q[0]),gear[i],q[1])
        return derivative(q,0,thrust,grade[i],vertical[i],params)
    cases=[]
    for i,j in windows:
        q=np.array([v[i],b[i],r[i],angle[i],x[i],z[i]])
        for k in range(i,j):
            q+=DT*deriv(q+.5*DT*deriv(q,k),k)
        actual_turn=np.degrees(angle[j]+b[j]-angle[i]-b[i])
        prediction=np.degrees(q[3]+q[1]-angle[i]-b[i])
        cases.append(dict(start_s=clock[i],duration_s=clock[j]-clock[i],
                          observed_turn_deg=actual_turn,predicted_turn_deg=prediction,
                          end_xz_error_m=np.hypot(q[4]-x[j],q[5]-z[j]),
                          end_speed_error_kph=(q[0]-v[j])*3.6))
    return cases


def checks(params):
    return {"anchor":check(SOURCE,params,True),"independent_1s":check(HOLDOUT,params)}


def refine_inputs(params,f,pr,corners,turn,drive,gear,length,neutral):
    """Discrete coordinate search on pre-entry keys. No steering feedback or route snapping."""
    s,t=f["s"],f["t"]
    grade=np.gradient(f["pos"][:,1],t)/np.maximum(f["v"],1)
    vertical=np.interp(s,(pr["s"][:-1]+pr["s"][1:])/2,pr["vertical"])
    prior_turn=turn.copy();prior_drive=drive.copy()
    actuator=np.zeros(5);cursor=0
    for row in corners:
        apex=(row[0]-1)*length+row[3]
        start=np.searchsorted(s,apex-max(row[5],35))
        mid=np.searchsorted(s,apex)
        end=np.searchsorted(s,apex+16)
        for i in range(cursor,start):
            actuator[:2]=steer_step(*actuator[:2],turn[i],f["v"][i],f["b"][i],f["r"][i])
            actuator[2:]=pedal_step(*actuator[2:],drive[i])
        cursor=start
        base=np.array([turn[start:end],drive[start:end]])
        original=np.array([prior_turn[start:end],prior_drive[start:end]])
        changes=np.flatnonzero(np.diff(gear[:start+1]))+1
        shift_age=t[start]-t[changes[-1]] if len(changes) else 5.
        def cost(candidates):
            size=len(candidates)
            q=np.tile(np.array([f["v"][start],np.radians(f["b"][start]),np.radians(f["r"][start]),
                                f["heading"][start],*f["pos"][start,[0,2]]])[:,None],(1,size))
            a,w,p,h,c=np.tile(actuator[:,None],(1,size))
            g=np.full(size,gear[start]-1);since=np.full(size,shift_age)
            error=np.zeros(size)
            for k,i in enumerate(range(start,end)):
                a,w=steer_step(a,w,candidates[:,0,k],q[0],np.degrees(q[1]),np.degrees(q[2]))
                p,h,c=pedal_step(p,h,c,candidates[:,1,k])
                rpm=q[0]*gears[g]*60/(2*np.pi*car["rearRadius"])
                new_g=g+((rpm>car["upshift"])&(g<len(gears)-1)).astype(int)-((rpm<car["downshift"])&(g>0))
                since=np.where(g!=new_g,0,since);g=new_g
                delivery=np.clip((since-setup["shiftOpenSeconds"])/setup["clutchEngagementSeconds"],0,1)
                overrun=setup["overrunNm"]*np.clip((rpm-car["idle"])/(car["upshift"]-car["idle"]),0,1)*gears[g]/(car["rearRadius"]*mass*car["efficiency"])
                u=delivery*(drive_limit(q[0],g,q[1])*p*(setup["minimumBoostTorqueFraction"]+(1-setup["minimumBoostTorqueFraction"])*c)-overrun*(1-p))-h*brake_limit
                args=(np.radians(a),u,grade[i],vertical[i],params)
                q+=DT*derivative(q+.5*DT*derivative(q,*args),*args);since+=DT
                nxt=i+1
                if nxt>=mid-16 and (nxt==mid or nxt==end or nxt%8==0):
                    dx,dz=q[4:]-f["pos"][nxt,[0,2],None]
                    chi=f["chi"][nxt]
                    cross=dx*np.cos(chi)-dz*np.sin(chi)
                    along=dz*np.cos(chi)+dx*np.sin(chi)
                    loss=(cross/.60)**2+(along/1.4)**2+((np.degrees(q[1])-f["b"][nxt])/4)**2
                    loss+=((np.degrees(q[2])-f["r"][nxt])/10)**2+((q[0]-f["v"][nxt])/1.6)**2
                    error+=loss*(1. if nxt in (mid,end) else .12)
            return error+.008*(candidates!=original).sum(axis=(1,2))+.025*np.count_nonzero(np.diff(candidates,axis=2),axis=(1,2))
        blocks=[i-start for i in range(start+(-start)%3,mid-2,3)
                if not np.any(neutral[i:i+3]) and s[i]<apex-10]
        for _ in range(7):
            variants=[base]
            for axis in (0,1):
                for k in blocks:
                    for span in (3,6):
                        if span==6 and k+3 not in blocks:continue
                        for key in (-1,0,1):
                            if np.all(base[axis,k:k+span]==key):continue
                            altered=base.copy();altered[axis,k:k+span]=key;variants.append(altered)
            scores=cost(np.stack(variants));which=np.argmin(scores)
            if which==0 or scores[0]-scores[which]<.025:break
            base=variants[which]
        turn[start:end],drive[start:end]=base
    return turn,drive


def corner_forecasts(params,f,pr,corners,turn,drive,gear,length):
    clock,s=f["t"],f["s"]
    grade=np.gradient(f["pos"][:,1],clock)/np.maximum(f["v"],1)
    vertical=np.interp(s,(pr["s"][:-1]+pr["s"][1:])/2,pr["vertical"])
    state=np.zeros((len(s)+1,5))
    for i in range(len(s)):
        a,w,p,h,c=state[i]
        a,w=steer_step(a,w,turn[i],f["v"][i],f["b"][i],f["r"][i])
        p,h,c=pedal_step(p,h,c,drive[i])
        state[i+1]=a,w,p,h,c
    out=[]
    for row in corners:
        apex=(row[0]-1)*length+row[3]
        start=np.searchsorted(s,apex-max(row[5],35))
        end=np.searchsorted(s,apex)
        a,w,p,h,c=state[start]
        q=np.array([f["v"][start],np.radians(f["b"][start]),np.radians(f["r"][start]),
                    f["heading"][start],*f["pos"][start,[0,2]]])
        g=gear[start]-1
        changed=np.flatnonzero(np.diff(gear[:start+1]))+1
        since=clock[start]-clock[changed[-1]] if len(changed) else 5.
        for i in range(start,end):
            a,w=steer_step(a,w,turn[i],q[0],np.degrees(q[1]),np.degrees(q[2]))
            p,h,c=pedal_step(p,h,c,drive[i])
            rpm=q[0]*gears[g]*60/(2*np.pi*car["rearRadius"])
            next_gear=g+(1 if rpm>car["upshift"] and g<len(gears)-1 else -1 if rpm<car["downshift"] and g>0 else 0)
            if next_gear!=g:since=0
            g=next_gear
            delivery=np.clip((since-setup["shiftOpenSeconds"])/setup["clutchEngagementSeconds"],0,1)
            overrun=setup["overrunNm"]*np.clip((rpm-car["idle"])/(car["upshift"]-car["idle"]),0,1)*gears[g]/(car["rearRadius"]*mass*car["efficiency"])
            throttle=p*(setup["minimumBoostTorqueFraction"]+(1-setup["minimumBoostTorqueFraction"])*c)
            u=delivery*(throttle*drive_limit(np.array(q[0]),g,q[1])-overrun*(1-p))-brake_limit*h
            args=(np.radians(a),u,grade[i],vertical[i],params)
            q+=DT*derivative(q+.5*DT*derivative(q,*args),*args)
            since+=DT
        dx,dz=q[4:]-f["pos"][end,[0,2]]
        angle=f["chi"][end]
        out.append([row[0],row[1],clock[end]-clock[start],dx*np.cos(angle)-dz*np.sin(angle),
                    np.hypot(dx,dz),(q[0]-f["v"][end])*3.6,np.degrees(q[1])-f["b"][end]])
    return out
