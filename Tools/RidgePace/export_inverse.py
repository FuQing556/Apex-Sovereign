"""Calculate the Ridge drift plan and export a standalone, time-scrubbable ghost."""
import csv
import hashlib
import json
import re
import numpy as np
from scipy.spatial import cKDTree
from scipy.spatial.transform import Rotation
from inverse_drift import (ROOT, OUT, LOGS, SOURCE, HOLDOUT, DT, road, car, setup, geometry,
                           problem, identify, solve, sectors, unit)
from drift_controls import digital_plan, realize


def table(name,headers,rows):
    with (OUT/name).open("w",encoding="utf-8-sig",newline="") as stream:
        writer=csv.writer(stream)
        writer.writerow(headers)
        for row in rows:
            writer.writerow([round(float(v),5) if isinstance(v,(float,np.floating)) else v for v in row])


def frames(g,p,state,t):
    clock=np.r_[np.arange(0,t[-1]-1e-9,DT),t[-1]]
    lo=np.minimum(np.searchsorted(t,clock,side="right")-1,len(t)-2)
    phase=(clock-t[lo])/(t[lo+1]-t[lo])
    v0,v1=state[lo,0],state[lo+1,0]
    distance_fraction=(2*v0*phase+(v1-v0)*phase**2)/(v1+v0)
    station=p["s"][lo]+distance_fraction*(p["s"][lo+1]-p["s"][lo])
    local=station%g["length"]
    base=g["line"](local)
    tangent=g["line"](local,1)
    speed,beta,yaw,delta,force,offset,h=(state[lo]+(state[lo+1]-state[lo])*phase[:,None]).T
    center=base+unit(np.cross([0,1,0],tangent))*offset[:,None]
    direction=np.unwrap(np.arctan2(tangent[:,0],tangent[:,2]))+h
    heading=direction-beta
    return dict(t=clock,s=station,v=speed,b=np.degrees(beta),r=np.degrees(yaw),d=np.degrees(delta),
                u=force,pos=center,heading=heading,chi=direction)


def geometric_checks(g,f):
    mesh=g["mesh"]
    centers=f["pos"]
    tree=cKDTree(mesh[:,[0,2]])
    edge_errors=[]
    for sign in [-1,1]:
        edge=mesh+sign*road["halfWidth"]*g["side"]
        reference=np.genfromtxt(ROOT/"Logs/RidgeSearch/road.csv",delimiter=",",skip_header=1)
        edge_errors.append(np.max(np.linalg.norm(edge-reference[:,0:3] if sign<0 else edge-reference[:,3:6],axis=1)))
    # Signed distance from each physical collision-box corner to both surveyed asphalt edges.
    ahead=np.column_stack([np.sin(f["heading"]),np.cos(f["heading"])])
    side=np.column_stack([ahead[:,1],-ahead[:,0]])
    prefab=(ROOT/"Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab").read_text(encoding="utf-8-sig")
    hull=re.search(r"m_Size: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}",prefab)
    hw=float(hull[1])/2;hl=float(hull[3])/2
    margin=np.full(len(centers),np.inf)
    for longitudinal in [-hl,hl]:
        for lateral in [-hw,hw]:
            pt=centers[:,[0,2]]+longitudinal*ahead+lateral*side
            nearest=tree.query(pt)[1]
            candidates=(nearest[:,None]+np.arange(-3,3))%len(mesh)
            for sign in [-1,1]:
                edge=(mesh+sign*road["halfWidth"]*g["side"])[:,[0,2]]
                a=edge[candidates]; ab=edge[(candidates+1)%len(mesh)]-a
                ap=pt[:,None,:]-a
                t=np.clip(np.sum(ap*ab,axis=2)/np.sum(ab**2,axis=2),0,1)
                displacement=ap-t[:,:,None]*ab
                distances=np.linalg.norm(displacement,axis=2)
                best=distances.argmin(axis=1)
                which=np.arange(len(pt))
                d=displacement[which,best];v=ab[which,best]
                inner=np.sign(-sign*(d[:,0]*v[:,1]-d[:,1]*v[:,0]))
                margin=np.minimum(margin,distances[which,best]*inner)
    # Use the actual gate transforms and lateral limits serialized in Ridge.unity.
    scene=(ROOT/"Assets/_Project/Scenes/Ridge.unity").read_text(encoding="utf-8-sig")
    ids=re.findall(r"fileID: (\d+)",scene.split("  gates:\n",1)[1].split("  gateLateralLimits:",1)[0])
    limits=[list(map(float,pair)) for pair in re.findall(r"- \{x: ([-\d.]+), y: ([-\d.]+)\}",scene.split("  gateLateralLimits:\n",1)[1].split("  laps:",1)[0])]
    height=float(re.search(r"  gateHalfHeight: ([\d.]+)",scene)[1])
    gates=[]
    for ident,limit in zip(ids,limits):
        chunk=scene.split("--- !u!4 &"+ident+"\n",1)[1].split("--- !u!",1)[0]
        def vector(name):
            return [float(q) for q in re.findall(r"[xyzw]: ([^,}]+)",re.search(name+r": \{([^}]+)",chunk)[1])]
        gates.append((np.array(vector("m_LocalPosition")),Rotation.from_quat(vector("m_LocalRotation")),limit))
    rows=[];start=0
    for order in range(1,2*len(gates)+1):
        j=order%len(gates); origin,rot,limits=gates[j]
        xyz=rot.inv().apply(centers-origin)
        depth=xyz[:,2]
        crossing=np.flatnonzero((depth[:-1]<0)&(depth[1:]>=-1e-6)&(np.arange(len(depth)-1)>=start))
        for i in crossing:
            ratio=np.clip(-depth[i]/(depth[i+1]-depth[i]),0,1)
            hit=xyz[i]+ratio*(xyz[i+1]-xyz[i])
            if limits[0] <= hit[0] <= limits[1] and abs(hit[1])<=height:
                when=f["t"][i]+ratio*(f["t"][i+1]-f["t"][i])
                rows.append([order,j+1,when,hit[0],hit[1]]);start=i+1
                break
        else: break
    return margin,rows,float(max(edge_errors))


def spans(mask):
    edges=np.diff(np.r_[False,mask,False].astype(int))
    return list(zip(np.flatnonzero(edges==1),np.flatnonzero(edges==-1)))


def corner_stats(f,length,turn,actual):
    clock,s=f["t"],f["s"]
    apex,signs=sectors()
    rows=[]
    interp=lambda x,series:float(np.interp(x,s,series))
    for j,(peak,sign) in enumerate(zip(np.r_[apex,apex+length],np.tile(signs,2))):
        apex_i=np.searchsorted(s,peak)
        window=abs(s-peak)<=26
        good=window&(-sign*f["b"]>=12)&(-sign*f["b"]<=45)&(sign*f["r"]>=5)&(f["v"]*np.cos(np.radians(f["b"]))>8)
        best=max((clock[min(b,len(s)-1)]-clock[a] for a,b in spans(good)),default=0)
        free=(turn==0)&(abs(actual)<=1)
        containing=[(a,b) for a,b in spans(free) if a<=apex_i<b]
        if containing:
            a,b=containing[0];b=min(b-1,len(s)-1)
        else: a=b=apex_i
        previous=(apex[(j-1)%16] if j%16 else apex[-1]-length)+(j//16)*length
        begin=max(0,(peak+previous)/2)
        before=(s>=begin)&(s<peak)&(np.arange(len(s))<a)
        pulses=[(i,j) for i,j in spans(turn==sign) if begin<=(s[i]+s[min(j,len(s)-1)])/2<peak and j<=a]
        # Identify the main same-direction pulse, also retain every tap in the key sheet.
        if pulses:
            first,last=max(pulses,key=lambda ab:clock[ab[1]]-clock[ab[0]])
            lead=peak-s[first];duration=clock[last]-clock[first]
        else:lead=duration=0.
        max_steer=np.max(sign*actual[before],initial=0)
        vel_deg=np.degrees(f["chi"][b]-f["chi"][a])
        body_deg=np.degrees(f["heading"][b]-f["heading"][a])
        last_input=np.flatnonzero((np.arange(len(s))<apex_i)&(turn!=0))
        release=min(last_input[-1]+1,apex_i) if len(last_input) else 0
        rows.append([j//16+1,j%16+1,"右" if sign==1 else "左",peak%length,
                     interp(peak,clock),lead,duration,max_steer,peak-s[a],s[b]-s[a],clock[b]-clock[a],
                     vel_deg,body_deg,f["b"][b]-f["b"][a],interp(peak,actual),interp(peak,f["b"]),
                     interp(peak-26,f["v"])*3.6,interp(peak,f["v"])*3.6,interp(peak+26,f["v"])*3.6,
                     peak-s[release],interp(peak,clock)-clock[release],best])
    return rows


CORNER_HEADERS="圈 弯 方向 弯心站距m 弯心时刻s 主同向按键起点距弯心m 主同向按键持续s 提前同向峰值前轮度 回正段起点距弯心m 回正段站距m 回正段时长s 回正段速度方向变化度 回正段车头变化度 回正段侧滑变化度 弯心键盘前轮度 弯心侧滑度 入窗速度kmh 弯心速度kmh 出窗速度kmh 最后松方向键距弯心m 松方向键至弯心s 连续合格漂移s".split()


def report(recalculate=True):
    OUT.mkdir(exist_ok=True,parents=True)
    g=geometry();p=problem(g);params,fit=identify()
    candidates=[]
    scratch=ROOT/"Logs/PhysicsAudit/RidgeInverseCandidates"
    scratch.mkdir(parents=True,exist_ok=True)
    best=None
    for core,time_weight in [(window,w) for window in [(-13,21),(-16,28),(-22,34)] for w in (0.,1.8,3.8)]:
        cache=scratch/f"finish-{core[1]}-time-{time_weight}.npz"
        if recalculate or not cache.exists():
            a,t,e=solve(p,params,core,time_weight)
            np.savez_compressed(cache,state=a,t=t,err=e)
        with np.load(cache) as z: a,t,e=z["state"],z["t"],z["err"]
        planned=frames(g,p,a,t)
        margin,gates,mesh_error=geometric_checks(g,planned)
        windows=corner_stats(planned,g["length"],np.zeros(len(planned["s"]),int),planned["d"])
        drift_count=int(sum(row[-1]>=.4 for row in windows))
        peak_error=np.max(abs(e),axis=0)
        eligible=(len(gates)==68 and min(margin)>1.0 and drift_count==32
                  and peak_error[0]<.15 and peak_error[1]<.16 and peak_error[2]<.15)
        candidates.append(dict(neutral_window_m=core,time_weight=time_weight,seconds=t[-1],
                               max_collocation_error=peak_error.tolist(),gates=len(gates),drifts=drift_count,
                               min_margin_m=float(min(margin)),eligible=bool(eligible)))
        if eligible and (best is None or t[-1]<best[1][-1]):
            best=(a,t,e,core)
    if best is None: raise ValueError("No eligible two-lap solution: "+str(candidates))
    a,t,e,core=best
    f=frames(g,p,a,t)
    margin,gates,mesh_error=geometric_checks(g,f)
    centers=np.r_[sectors()[0],sectors()[0]+g["length"]]
    gap=f["s"]-centers[np.argmin(abs(f["s"][:,None]-centers),axis=1)]
    neutral=(gap>=core[0])&(gap<=core[1])
    turn,drive,actual,thrust,gear=digital_plan(f["v"],f["b"],f["r"],f["d"],f["u"],neutral)
    from drift_accuracy import checks,corner_forecasts,refine_inputs
    initial_corners=corner_stats(f,g["length"],turn,actual)
    prior=corner_forecasts(params,f,p,initial_corners,turn,drive,gear,g["length"])
    turn,drive=refine_inputs(params,f,p,initial_corners,turn,drive,gear,g["length"],neutral)
    actual,thrust=realize(f["v"],f["b"],f["r"],turn,drive)
    mask=(drive==1).astype(int)+2*(turn<0)+4*(turn>0)+8*(drive<0)
    corners=corner_stats(f,g["length"],turn,actual)
    table("逐弯计算.csv",CORNER_HEADERS,corners)
    table("计数门.csv",["顺序","游戏门号","通过时刻s","门内横坐标m","门内高度m"],gates)
    table("操作与轨迹.csv","t_s station_m x_m road_y_m z_m speed_kph body_yaw_deg velocity_heading_deg beta_deg yaw_rate_deg_s target_steer_deg keyboard_steer_deg required_thrust_mps2 keyboard_thrust_mps2 auto_gear keys_mask body_asphalt_clearance_m".split(),
          zip(f["t"],f["s"],*f["pos"].T,f["v"]*3.6,np.degrees(f["heading"]),np.degrees(f["chi"]),
              f["b"],f["r"],f["d"],actual,f["u"],thrust,gear,mask,margin))
    events=np.r_[0,np.flatnonzero(np.diff(mask))+1,len(mask)]
    def keys(m):return "+".join(k for bit,k in [(1,"W"),(2,"A"),(4,"D"),(8,"S")] if m&bit) or "松开"
    table("按键时序.csv",["绿灯后起始s","结束s","起始站距m","按键"],(
        [f["t"][i],f["t"][min(j,len(mask)-1)],f["s"][i],keys(mask[i])] for i,j in zip(events,events[1:])))
    measurements=dict()
    for name,difference in [("keyboard_steer_deg",actual-f["d"]),("keyboard_thrust_mps2",thrust-f["u"])]:
        measurements[name]={"rms":float(np.sqrt(np.mean(difference**2))),"p95_abs":float(np.percentile(abs(difference),95)),
                            "max_abs":float(np.max(abs(difference)))}
    basis_check=checks(params)
    local_check=corner_forecasts(params,f,p,initial_corners,turn,drive,gear,g["length"])
    table("弯前按键局部复核.csv","圈 弯 弯前到弯心s 横向落点差m 平面落点差m 速度差kmh 有符号侧滑差deg 修正前横向落点差m".split(),
          [post+[pre[3]] for pre,post in zip(prior,local_check)])
    primary=basis_check["anchor"][0]
    independent=basis_check["independent_1s"]
    data=dict(model="离线双轴侧滑、横摆、纵向速度与赛道坐标联合配点，固定起点和冲线平面",telemetry={n:hashlib.sha256((LOGS/n).read_bytes()).hexdigest() for n in (SOURCE,HOLDOUT)},
              calibration=fit,candidates=candidates,chosen_window_m=core,lap_seconds=[t[p["n"]],t[-1]-t[p["n"]]],total_s=t[-1],
              course_3d_centerline_m=g["length"],path_horizontal_m=float(np.sum(np.linalg.norm(np.diff(f["pos"][:,[0,2]],axis=0),axis=1))),
              road_export_max_discrepancy_m=mesh_error,gates_geometric=len(gates),min_asphalt_margin_m=float(min(margin)),
              drift_windows_qualified=int(sum(row[-1]>=.4 for row in corners)),keyboard_error=measurements,
              max_apex_keyboard_steer_deg=max(abs(row[14]) for row in corners),min_neutral_s=min(row[10] for row in corners),
              max_neutral_s=max(row[10] for row in corners),step_s=DT,anchor_and_validation=basis_check,
              fraction_planned_in_extrapolated_gears=float(np.mean(gear>2)),
              fraction_planned_above_training_speed=float(np.mean(f["v"]*3.6>fit[-1]["observed_speed_range_kph"][1])),
              peak_kph=float(np.max(f["v"])*3.6),
              local_key_forecast_abs_lateral_m=dict(median=float(np.median(np.abs(np.array(local_check)[:,3]))),
                                                     max=float(np.max(np.abs(np.array(local_check)[:,3])))))
    (OUT/"计算核对.json").write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding="utf-8")
    from drift_preview import preview
    preview(g,f,actual,mask,corners,margin)
    note=f"""山脊大奖赛道 · 双圈提前给舵漂移

直接打开“双圈漂移幽灵.html”，选一个弯可跳到弯前。操作表从发车起计时。计数表使用游戏内门号，发车后的第一道是 2 号门，每圈以正向穿过 1 号门收尾。

计算依据
车辆为柴田 31 号 GR86，配置版本 8、club-lsd、原装后轮、干沥青。赛道几何取自 Ridge.asset，重建的左右沥青边缘同此前从 Unity 网格导出的数据最大相差 {mesh_error*1000:.2f} 毫米。分别读取 Ridge.unity 的门姿态、有效左右边界和高度，以及车辆预制件的 1.78 × 4.18 米车身碰撞盒。净距是车身四角到沥青边缘的水平投影距离。

车辆和优化方法
用车轮前后轴距和静态轴荷分配，以及 1350 千克质量、2325.74 千克·平方米横摆惯量，将记录的车身加速度和横摆加速度分配到前后轴。前后轮侧向力随轮胎侧偏角增长并饱和，纵向力参与折减。系数用玩家干沥青记录拟合。全油门有效后轴推力按道路转速、挡位和侧滑角拟合。未采到的挡位用相邻挡位和估算发动机功率曲线外推。坡度、垂向曲率、气动阻力和滚阻计入受力，胎温、悬架瞬态、离合器和每个轮子的滑转均未单独积分。

在赛道里程上同时求纵向速度、侧滑角、横摆角速度、前轮等效舵角、轮上推力、横向位置及速度航向。相邻节点满足纵向和侧向力平衡、横摆力矩平衡，以及路径和车身运动学。车头角等于速度方向角减去有符号侧滑角。两圈共用同一套连续状态，首圈从静止起步，第二圈带速。CubicSpline 参考线只提供起始坐标，优化器仍能调整横向位置与航向。物理配点间隔约 4.8 米。表中 0.02 秒是输出和按键换算间隔。

比较了三种弯内松舵范围和三种时间权重。九个候选的时间、动力学残差和筛选结果在“计算核对.json”。选中的轨迹总用时 {t[-1]:.2f} 秒。筛选条件为依次通过 68 道门，32 个弯区均有至少 0.40 秒连续合格漂移，车身净距大于 1 米，以及纵向加速度、法向加速度、横摆角加速度配点最大残差分别小于 0.15、0.16、0.15（前二项 m/s²，末项 rad/s²）。漂移窗口沿用项目标准，在弯心前后 26 米内，向外侧滑 12–45°、同向横摆超过 5°/s、车身前向速度超过 8 m/s。接地采用连续干沥青假设。

按键和读数
20 毫秒步长复算加舵、回中和反打，按键位以至少 60 毫秒的块搜索，弯心窗口要求松开 A 和 D。油门与制动按相应升降时间换算，同时近似处理自动换挡动力中断、增压和发动机制动。按键掩码 W=1，A=2，D=4，S=8，全程未使用手刹。推力列是前后轮纵向力合计除以质量，单位 m/s²，车辆切向加速度还受到侧滑和阻力影响。前轮角正值向右，车身侧滑角是车体坐标下横向与前向速度的 atan2。

“逐弯计算.csv”列出弯前最大的同向按键脉冲，以及该弯准备区内的同向最大前轮角，两项可能来自不同脉冲。回正段同时满足无 A/D 和键盘换算的前轮角绝对值不超过 1°，并取包含弯心的连续段。最后松键的位置与车轮回正的位置分别列出。入窗和出窗速度分别在弯心前后 26 米读取。

误差和可复现范围
三项区分记录。“操作与轨迹.csv”的位置、速度及侧滑来自连续动力学逆解，键盘舵角与推力是用离散按键再计算的结果，游戏里的真实落点需另行验证。超过训练最高车速的时间占 {data["fraction_planned_above_training_speed"]:.1%}，三挡及以上占 {data["fraction_planned_in_extrapolated_gears"]:.1%}。两圈没有单独标定热衰退。模型的配点残差只衡量方程内部的一致性。九个候选中的最短可行时间不能证明游戏里的全局最快圈速。

将你实测的 3.76 秒纯 W 长漂起始状态代入，从头积分整个片段，中途仅提供沿途路面坡度和垂向曲率。模型预测速度方向转过 {primary["predicted_turn_deg"]:.1f}°，原始记录用车头角加侧滑角为 {primary["observed_turn_deg"]:.1f}°，终点平面位置差 {primary["end_xz_error_m"]:.2f} 米。该段参与了拟合。独立的第二份玩家日志中，按预定条件找到 {len(independent)} 个互不重叠的一秒窗口，终点位置误差中位数 {np.median([x["end_xz_error_m"] for x in independent]):.2f} 米。每个试片的数据和源文件 SHA-256 见 JSON。

“弯前按键局部复核.csv”还单独检查了每个弯的输入。将车辆置于该弯的预测初态，从初版同向主脉冲或弯前 35 米中较早的一处开始。先计算原时序落点，再只在提前打舵阶段调整 60 或 120 毫秒的方向和油刹键位，用弯心及其后 16 米的横向和纵向位置、横摆、速度与侧滑误差选择时序。保持弯心松舵约束，用相同的窗口重新检查最终按键。积分中重新计算横摆、侧滑、前轮回正、传动和车速，不使用路线反馈。只输入预定路段的坡度、垂向曲率。32 次按键预测的弯心横向位置误差中位数 {data["local_key_forecast_abs_lateral_m"]["median"]:.2f} 米，最大 {data["local_key_forecast_abs_lateral_m"]["max"]:.2f} 米。每个弯从各自初态开始，不能把这些局部结果拼成连续两圈通过的实测证明。

复算入口为 Tools/RidgePace/export_inverse.py。依赖 Python、NumPy、SciPy、PyYAML 和 Matplotlib。默认从当前配置与原始遥测重新拟合、求解和导出，独立于 Unity 编辑器。候选解缓存放在 Logs/PhysicsAudit/RidgeInverseCandidates。
"""
    (OUT/"计算说明.txt").write_text(note,encoding="utf-8-sig")
    print(json.dumps(data,ensure_ascii=False,indent=2))
    return data


if __name__=="__main__":
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--use-cache",action="store_true")
    args=parser.parse_args()
    report(recalculate=not args.use_cache)
