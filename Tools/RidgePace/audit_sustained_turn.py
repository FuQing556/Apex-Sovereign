"""Replay the recorded Ridge turn, comparing the existing predictor with Unity at 50 Hz."""
import csv
import hashlib
import json
from itertools import groupby

import matplotlib
import numpy as np
from scipy.ndimage import gaussian_filter1d

import inverse_drift as model
from drift_accuracy import derivative
from drift_controls import steer_step

matplotlib.use("Agg")
from matplotlib import pyplot as plt


def road_inputs(height, speed):
    y = gaussian_filter1d(height, 4)
    climb = np.gradient(y,model.DT)
    grade = np.clip(climb/np.maximum(speed,4), -.16,.16)
    vertical = np.clip(np.gradient(climb,model.DT),-4,4)/np.maximum(speed**2,16)
    return grade, vertical


def rates(q, gear, grade, vertical, params):
    drive = model.drive_limit(np.asarray(q[0]),gear,q[1])
    return derivative(q,0.,drive,grade,vertical,params)


def advance(q, gear, grade, vertical, params):
    args = gear,grade,vertical,params
    return q + model.DT*rates(q + .5*model.DT*rates(q,*args),*args)


def independent_turns(target, params):
    with (model.LOGS/model.HOLDOUT).open(encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    def col(k):
        return np.array([float(r[k]) for r in rows])
    speed = col("speed_mps")
    grade, vertical = road_inputs(col("y"),speed)
    beta, yaw = np.radians(col("signed_drift_deg")), np.radians(col("yaw_deg_s"))
    psi = np.unwrap(np.arctan2(col("front_axle_x")-col("rear_axle_x"),col("front_axle_z")-col("rear_axle_z")))
    x,z = col("x"),col("z")
    out = []
    def valid(i):
        row = rows[i]
        return (row["phase"]=="Driving" and row["W"]=="1" and speed[i]>15
                and all(float(row[k])==0 for k in ("A","D","space","S","contact_body","FL_steer_deg","FR_steer_deg"))
                and all(row[w+"_cover"]=="沥青" and row[w+"_ground"]=="1" for w in ("FL","FR","RL","RR")))
    for permitted, group in groupby(range(len(rows)),key=valid):
        if not permitted:
            continue
        indices = list(group)
        start,end = indices[0],indices[-1]
        if len(indices)<101 or abs(np.degrees(beta[start]))<8 or len({rows[i]["gear"] for i in indices})!=1:
            continue
        q = np.array([speed[start],beta[start],yaw[start],psi[start],x[start],z[start]])
        for i in indices:
            if i>start:
                q = advance(q,int(rows[i-1]["gear"])-1,grade[i-1],vertical[i-1],params)
            obs_turn = np.degrees(psi[i]+beta[i]-psi[start]-beta[start])
            calc_turn = np.degrees(q[3]+q[1]-psi[start]-beta[start])
            out.append(dict(start_sim_s=rows[start]["sim_s"],sim_s=rows[i]["sim_s"],elapsed_s=(i-start)*model.DT,
                            obs_turn_deg=obs_turn,calc_turn_deg=calc_turn,obs_yaw_deg_s=np.degrees(yaw[i]),
                            calc_yaw_deg_s=np.degrees(q[2]),obs_beta_deg=np.degrees(beta[i]),calc_beta_deg=np.degrees(q[1]),
                            obs_kph=speed[i]*3.6,calc_kph=q[0]*3.6,xz_err_m=np.hypot(q[4]-x[i],q[5]-z[i])))
        print("independent",out[-1])
    if not out:
        raise ValueError("No independent sustained-turn windows")
    with (target/"独立驾驶记录逐帧对照.csv").open("w",encoding="utf-8-sig",newline="") as f:
        writer=csv.DictWriter(f,fieldnames=out[0])
        writer.writeheader()
        writer.writerows(out)


def run():
    target = model.OUT.parent / "持续拐弯逐帧复算"
    target.mkdir(exist_ok=True)
    baseline = json.loads((model.OUT / "计算核对.json").read_text(encoding="utf-8-sig"))
    for filename, expected in baseline["telemetry"].items():
        assert hashlib.sha256((model.LOGS / filename).read_bytes()).hexdigest() == expected
    params, _ = model.identify()
    for axle, previous in zip(params, baseline["calibration"]):
        np.testing.assert_allclose(axle, previous["parameters"], rtol=1e-4)
    with (model.LOGS / model.SOURCE).open(encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    def col(key):
        return np.array([float(r[key]) for r in rows])
    t = col("sim_s")
    def at(seconds):
        return int(np.argmin(abs(t - seconds)))
    start, anchor, end = map(at, (145.2199, 146.5599, 150.3199))
    dt = model.DT
    np.testing.assert_allclose(np.diff(t[start:end+1]), dt, atol=2e-4)
    window = slice(anchor, end+1)
    assert all(col(k)[window].max() == 0 for k in ("A","D","space","S","steer_cmd","contact_body","FL_steer_deg","FR_steer_deg"))
    assert col("W")[start:end+1].min() == 1
    assert all(all(r[w+"_cover"] == "沥青" and r[w+"_ground"] == "1" for w in ("FL","FR","RL","RR")) for r in rows[start:end+1])
    speed, beta, yaw = col("speed_mps"), np.radians(col("signed_drift_deg")), np.radians(col("yaw_deg_s"))
    x, y, z = col("x"), col("y"), col("z")
    psi = np.unwrap(np.arctan2(col("front_axle_x")-col("rear_axle_x"), col("front_axle_z")-col("rear_axle_z")))
    # Tangent at each timestamp from adjacent actual positions, including just outside the window.
    tangent = np.unwrap(np.arctan2(np.gradient(x), np.gradient(z)))
    horizontal_speed = np.hypot(np.gradient(x),np.gradient(z))/dt
    horizontal_normal_accel = horizontal_speed*np.gradient(tangent,dt)
    grade, vertical = road_inputs(y,speed)
    gear = col("gear").astype(int)-1
    actual = np.stack([speed,beta,yaw,psi,x,z],axis=1)
    def degrees(rad):
        return float(np.degrees(rad))
    measured_fl, measured_fr = col("FL_steer_deg"), col("FR_steer_deg")
    keys = col("steer_cmd")
    assert keys[start-1] == measured_fl[start-1] == measured_fr[start-1] == 0
    steering, key_rate = 0., 0.
    q = actual[anchor].copy()
    center_xz = []
    path_angles = []
    records = []
    for i in range(start,end+1):
        r = rows[i]
        steering, key_rate = steer_step(steering,key_rate,keys[i],speed[i],degrees(beta[i]),degrees(yaw[i]))
        radius = np.inf if abs(steering)<=.001 else model.wheelbase/np.tan(np.radians(abs(steering)))
        offset = .5*model.car["track"]*model.setup["ackermannFraction"]
        def wheel_angle(left):
            inner = left == (steering < 0)
            return np.sign(steering)*np.degrees(np.arctan2(model.wheelbase,radius+(-offset if inner else offset)))
        record = dict(tick=int(r["tick"]),sim_s=t[i],from_zero_s=round((i-anchor)*dt,2),
                      W=r["W"],A=r["A"],D=r["D"],S=r["S"],space=r["space"],steer_cmd=keys[i],gear=gear[i]+1,
                      FL_calc_deg=wheel_angle(True),FL_obs_deg=measured_fl[i],FR_calc_deg=wheel_angle(False),FR_obs_deg=measured_fr[i],
                      obs_x_m=x[i],obs_y_m=y[i],obs_z_m=z[i],obs_kph=speed[i]*3.6,
                      obs_beta_deg=degrees(beta[i]),obs_yaw_deg_s=degrees(yaw[i]),
                      obs_body_turn_deg=degrees(psi[i]-psi[anchor]),obs_XZ_tangent_turn_deg=degrees(tangent[i]-tangent[anchor]),
                      obs_psi_beta_turn_deg=degrees(psi[i]+beta[i]-psi[anchor]-beta[anchor]))
        for w in ("FL","FR","RL","RR"):
            for field in ("drive_nm","brake_nm","rpm","load_n","forward_slip","side_slip","cover","tyre_c"):
                record[w+"_"+field] = r[w+"_"+field]
        # Condition on observed axle torque. This checks its split, not its generation.
        axle = float(r["RL_drive_nm"])+float(r["RR_drive_nm"])
        inertia = model.car["wheelMass"]*model.car["rearRadius"]**2
        limit = model.setup["differentialPreloadNm"]+abs(axle)*model.setup["powerLock" if float(r["clutch_nm"])>=0 else "coastLock"]
        transfer = .5*np.clip((float(r["RL_rpm"])-float(r["RR_rpm"]))*np.pi/30*inertia/(4*dt),-limit,limit)
        record.update(RL_split_calc_nm=axle/2-transfer,RR_split_calc_nm=axle/2+transfer,
                      obs_xz_normal_accel_mps2=horizontal_normal_accel[i])
        if i >= anchor:
            # Reduced-model forces at the observed state, delta=0 and full throttle.
            u = model.drive_limit(np.asarray(speed[i]),gear[i],beta[i])
            bz,bx = speed[i]*np.cos(beta[i]),speed[i]*np.sin(beta[i])
            load = max(4,model.G+vertical[i]*speed[i]**2+.5*model.rho*model.setup["downforceArea"]*bz*bz/model.mass)
            front_load = load*model.car["frontWeight"]-.32/model.wheelbase*u
            af, ar = -np.arctan2(bx+model.front_arm*yaw[i],bz), -np.arctan2(bx-model.rear_arm*yaw[i],bz)
            fyf = model.tire(af,0,front_load,params[0])
            fyr = model.tire(ar,u,load-front_load,params[1])
            accel = (model.front_arm*fyf-model.rear_arm*fyr)/model.inertia_per_kg
            np.testing.assert_allclose(accel,rates(actual[i],gear[i],grade[i],vertical[i],params)[2],atol=1e-9)
            record.update(model_at_obs_thrust_N=u*model.mass,model_at_obs_front_alpha_deg=degrees(af),
                          model_at_obs_rear_alpha_deg=degrees(ar),model_at_obs_front_lat_N=fyf*model.mass,
                          model_at_obs_rear_lat_N=fyr*model.mass,model_at_obs_yaw_accel_deg_s2=degrees(accel))
            if i > anchor:
                args = gear[i-1],grade[i-1],vertical[i-1],params
                q = advance(q,*args)
                one = advance(actual[i-1],*args)
                record.update(step_obs_dyaw_deg_s=degrees(yaw[i]-yaw[i-1]),step_calc_dyaw_deg_s=degrees(one[2]-yaw[i-1]),
                              step_yaw_err_deg_s=degrees(one[2]-yaw[i]),step_speed_err_kph=(one[0]-speed[i])*3.6)
            predicted_turn = degrees(q[3]+q[1]-psi[anchor]-beta[anchor])
            center_xz.append(q[4:6].copy())
            path_angles.append(predicted_turn)
            record.update(grade=grade[i],vertical_curvature_per_m=vertical[i],calc_x_m=q[4],calc_z_m=q[5],
                          calc_kph=q[0]*3.6,calc_yaw_deg_s=degrees(q[2]),calc_beta_deg=degrees(q[1]),
                          calc_body_turn_deg=degrees(q[3]-psi[anchor]),calc_psi_beta_turn_deg=predicted_turn,
                          xz_err_m=np.hypot(q[4]-x[i],q[5]-z[i]),speed_err_kph=(q[0]-speed[i])*3.6,
                          turn_err_deg=predicted_turn-record["obs_psi_beta_turn_deg"])
        records.append(record)
    columns = list(dict.fromkeys(key for row in records for key in row))
    with (target/"逐帧计算与实测.csv").open("w",encoding="utf-8-sig",newline="") as f:
        writer = csv.DictWriter(f,fieldnames=columns)
        writer.writeheader()
        for record in records:
            writer.writerow({k:round(float(v),6) if isinstance(v,(float,np.floating)) else v for k,v in record.items()})
    measured_turn = np.degrees(psi[window]+beta[window]-psi[anchor]-beta[anchor])
    clock = np.arange(end-anchor+1)*dt
    pos = np.asarray(center_xz)
    time = np.arange(start,end+1)*dt-anchor*dt
    plt.rcParams["font.family"] = ["Microsoft YaHei", "DejaVu Sans"]
    fig, axes = plt.subplots(1,3,figsize=(13,4.4),layout="constrained")
    axes[0].plot(time,measured_fl[start:end+1],label="左前轮实测")
    axes[0].plot(time,np.degrees(yaw[start:end+1]),label="车身横摆角速度")
    axes[0].plot(clock,[a["calc_yaw_deg_s"] for a in records[anchor-start:]],"--",label="预测横摆角速度")
    axes[0].axvline(0,color="gray",ls=":",label="双前轮归零")
    axes[0].set(xlabel="相对归零时间（秒）",ylabel="前轮度数 / 横摆度每秒",title="给舵、回正与继续旋转")
    axes[1].plot(x[window]-x[anchor],z[window]-z[anchor],label="Unity 质心")
    axes[1].plot(pos[:,0]-x[anchor],pos[:,1]-z[anchor],"--",label="旧公式前推")
    axes[1].set(xlabel="相对 X（米）",ylabel="相对 Z（米）",title="同一初态的水平轨迹",aspect="equal")
    axes[2].plot(clock,np.degrees(tangent[window]-tangent[anchor]),label="实测坐标切线")
    axes[2].plot(clock,measured_turn,":",label="实测车头角＋侧滑角")
    axes[2].plot(clock,path_angles,"--",label="旧公式前推")
    axes[2].set(xlabel="归零后秒数",ylabel="行进方向累计改变（度）",title="实际过弯量与预测")
    for ax in axes:
        ax.grid(alpha=.25)
        ax.legend(fontsize=8)
    fig.savefig(target/"持续拐弯对照.png",dpi=155)
    plt.close(fig)
    event = records[anchor-start:]
    for offset in (0,1,10,25,50,100,150,len(event)-1):
        rec=event[offset]
        print({k:round(rec[k],4) for k in ("from_zero_s","obs_kph","calc_kph","obs_yaw_deg_s","calc_yaw_deg_s","obs_XZ_tangent_turn_deg","obs_psi_beta_turn_deg","calc_psi_beta_turn_deg","xz_err_m")})
    for key,limit in (("xz_err_m",.1),("xz_err_m",1),("turn_err_deg",1),("turn_err_deg",5),("speed_err_kph",5)):
        print("first",key,limit,next((a["from_zero_s"] for a in event if abs(a[key])>=limit),None))
    errors = [a["step_yaw_err_deg_s"] for a in event[1:]]
    print("step yaw error mean/rms",np.mean(errors),np.sqrt(np.mean(np.square(errors))))
    print("steering max abs error", max(abs(a[w+"_calc_deg"]-a[w+"_obs_deg"]) for a in records for w in ("FL","FR")))
    print("LSD split max abs error",max(abs(a[w+"_split_calc_nm"]-float(a[w+"_drive_nm"])) for a in records for w in ("RL","RR")))
    print("first/last D",[(a["sim_s"],a["FL_obs_deg"]) for a in (next(a for a in records if a["D"]=="1"),next(a for a in reversed(records) if a["D"]=="1"))])
    print("step one", {k:event[1][k] for k in ("step_obs_dyaw_deg_s","step_calc_dyaw_deg_s","step_speed_err_kph")})
    independent_turns(target,params)


if __name__ == "__main__":
    run()
