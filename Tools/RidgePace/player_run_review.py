"""Compare a finished v9 Ridge keyboard run with the recorded rack and the frozen v8 predictor."""
import argparse
import csv
import math
from pathlib import Path

import matplotlib
import numpy as np
from scipy.spatial.transform import Rotation

import inverse_drift as model
from audit_sustained_turn import road_inputs
from drift_accuracy import derivative

matplotlib.use("Agg")
from matplotlib import pyplot as plt

WHEELS = ("FL", "FR", "RL", "RR")


def runs(mask):
    changes = np.diff(np.r_[0, mask, 0].astype(int))
    return list(zip(np.flatnonzero(changes == 1), np.flatnonzero(changes == -1)))


def clamp(x, low, high):
    return max(low, min(high, x))


def move(x, target, step):
    return x + clamp(target - x, -step, step)


def steering(rows, col, recovery):
    cfg, car = model.setup, model.car
    locks = {p["value"] for p in car["steeringBySpeed"]["m_Curve"]}
    assert len(locks) == 1 and car["slipSteerGain"] == 0
    lock = locks.pop()
    pose = Rotation.from_quat(np.array([col("body_q" + c) for c in "xyzw"]).T)
    normal = sum(np.column_stack([col(w + "_normal_" + axis) for axis in "xyz"])
                 * col(w + "_ground")[:, None] for w in WHEELS)
    length = np.linalg.norm(normal, axis=1)
    airborne = length == 0
    normal[~airborne] /= length[~airborne, None]
    normal[airborne] = pose.apply([0, 1, 0])[airborne]
    forward = pose.apply([0, 0, 1])
    forward -= np.sum(forward * normal, axis=1)[:, None] * normal
    forward /= np.maximum(np.linalg.norm(forward, axis=1)[:, None], 1e-12)
    velocity = np.column_stack([col("world_v_" + c) for c in "xyz"])
    along = np.sum(velocity * forward, axis=1)
    across = np.sum(velocity * np.cross(normal, forward), axis=1)
    speed = np.hypot(along, across) * 3.6
    omega = np.column_stack([col("world_omega_" + c) for c in "xyz"])
    yaw = np.degrees(np.sum(omega * normal, axis=1))
    grounded = sum(col(w + "_ground") for w in WHEELS)
    angle = rate = latched = age = follow = 0.
    result = []
    gain, preview = cfg["recoveryCounterGain"], cfg["recoveryYawSeconds"]
    response, center = cfg["recoveryResponseSeconds"], cfg["keyboardCenterSeconds"]
    start, maximum = cfg["keyboardStartDegreesPerSecond"], cfg["keyboardMaxDegreesPerSecond"]
    precision, acceleration = cfg["keyboardPrecisionKph"], cfg["keyboardAcceleration"]
    dt_values, keys, slips = col("next_step_s"), col("steer_cmd"), col("signed_drift_deg")
    for i, row in enumerate(rows):
        if i and row["reset_generation"] != rows[i-1]["reset_generation"]:
            angle = rate = latched = age = follow = 0.
        key, beta, dt = keys[i], slips[i], dt_values[i]
        mode = "ordinary"
        assist = (recovery and row["keyboard_recovery_enabled"] == "1" and row["hand"] == "0"
                  and along[i] > car["driftMinSpeed"] and grounded[i] >= 3)
        if not assist or key != latched:
            latched = age = 0.
        follow = max(0., follow - dt) if assist else 0.
        target = clamp(gain * beta - preview * yaw[i], -lock, lock)
        blend = clamp((speed[i]-precision["x"])/(precision["y"]-precision["x"]), 0., 1.)
        accel = acceleration["x"] + blend * (acceleration["y"] - acceleration["x"])
        if not key:
            rate = 0.
            demand = clamp((abs(beta)-cfg["recoveryNeutralSlip"])/cfg["recoveryCounterSlip"], 0., 1.)
            demand = max(demand, min(1., follow/response)) if assist else 0.
            rest = target * demand
            returning = cfg["keyboardCenterDegreesPerSecond"] + abs(angle)/center
            if assist:
                returning = max(returning, abs(rest-angle)/response)
            angle = move(angle, rest, returning*dt)
            if demand > 0:
                mode = "released"
        elif assist and (key == latched or (key*beta > cfg["recoveryCounterSlip"]
                                            and -key*yaw[i] > cfg["recoveryYawRate"])):
            latched, age, follow = key, age+dt, cfg["recoveryFollowSeconds"]
            held = max(0., age-cfg["recoveryHoldSeconds"])
            desired = clamp(target+key*held*(start+.5*held*accel), -lock, lock)
            angle += clamp((desired-angle)*(1-math.exp(-dt/response)), -maximum*dt, maximum*dt)
            rate, mode = 0., "counter"
        else:
            incoming = max(start, key*rate)
            outgoing = move(incoming, maximum, accel*dt)
            rate = key*outgoing
            catch = 0.
            if grounded[i] >= 3 and along[i] > 0:
                catch = (clamp(key*beta, 0., cfg["keyboardCatchDegrees"])
                         * clamp(-key*yaw[i]/cfg["keyboardCatchYawRate"], 0., 1.)
                         * clamp(along[i]/car["driftMinSpeed"], 0., 1.))
            unwind = max(0., catch-key*angle)*(1-math.exp(-dt/center))
            angle = move(angle, key*lock, min(maximum*dt, .5*(incoming+outgoing)*dt+unwind))
        radius = car["wheelbase"]/math.tan(math.radians(abs(angle))) if abs(angle) > .001 else math.inf
        offset = cfg["ackermannFraction"] * car["track"] / 2
        wheels = [math.copysign(math.degrees(math.atan2(car["wheelbase"], radius+
                  (-offset if left == (angle < 0) else offset))), angle) for left in (True, False)]
        result.append((angle, *wheels, mode))
    return np.array([r[:3] for r in result]), np.array([r[3] for r in result])


def write_table(path, entries):
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=entries[0].keys())
        writer.writeheader()
        writer.writerows(entries)


def review(source):
    with source.open(encoding="utf-8-sig") as stream:
        rows = list(csv.DictReader(stream))
    def col(key):
        return np.array([float(r[key]) for r in rows])
    out = model.ROOT / "Logs/HandlingChecks" / ("player-" + source.stem.split("-")[-1])
    out.mkdir(parents=True, exist_ok=True)
    driving = np.array([r["phase"] == "Driving" for r in rows])
    first, finish = runs(driving)[0]
    assert rows[finish]["phase"] == "Results" and len(runs(driving)) == 1
    assert all(r["setup_revision"] == "9" and r["vehicle"] == "Okuibuki"
               and r["driver"] == "LocalDriver" and float(r["pad_x"]) == 0 for r in rows[first:finish])
    clock = (np.arange(len(rows))-first)*model.DT
    np.testing.assert_allclose(col("sim_s")-col("sim_s")[first], clock, atol=.00021)
    v, b, yaw = col("speed_mps"), col("signed_drift_deg"), col("yaw_deg_s")
    x, z = col("x"), col("z")
    key, hand = col("steer_cmd"), col("hand")
    contact, reset = col("contact_body") > 0, col("reset_generation")
    ground = sum(col(w+"_ground") for w in WHEELS)
    actual_wheels = np.column_stack([col(w+"_steer_deg") for w in WHEELS[:2]])
    on, mode = steering(rows, col, True)
    off, _ = steering(rows, col, False)
    errors = np.max(abs(on[:,1:]-actual_wheels),axis=1)
    print("wheels abs per-frame max, median/p95/max", np.percentile(errors[driving],[50,95,100]),
          "worst t",clock[np.argmax(errors*driving)])
    print("modes seconds",[(m,round(np.sum(driving & (mode==m))*.02,2)) for m in np.unique(mode)])
    drift = driving & (abs(b)>=12) & (abs(b)<=45) & (abs(yaw)>8) & (v*3.6>30) & (ground==4) & ~contact
    longest = max(runs(drift),key=lambda ab:ab[1]-ab[0])
    enter,leave = longest
    print("drift total/longest",np.sum(drift)*.02,clock[enter],clock[leave],
          "kph first/last",v[[enter,leave-1]]*3.6,"beta range",b[enter:leave].min(),b[enter:leave].max())
    print("reset",clock[np.flatnonzero(np.diff(reset)!=0)+1])
    for lap in (1,2):
        mask = driving & (col("lap")==lap)
        print("lap",lap,"avg/max kph",v[mask].mean()*3.6,v[mask].max()*3.6,
              "hand seconds/presses",np.sum(mask*(hand>0))*.02,len(runs(mask&(hand>0))),
              "drift",np.sum(mask & drift)*.02,"contact seconds",np.sum(mask&contact)*.02)
    phase = ["countdown" if i<first else r["phase"] for i,r in enumerate(rows)]
    frames = [dict(race_s=round(clock[i],2), sim_s=r["sim_s"], lap=r["lap"], gate=r["next_gate"],
                   phase=phase[i], mode=mode[i], W=r["W"], steer_cmd=key[i], hand=hand[i], beta_deg=b[i],
                   body_yaw_dps=yaw[i], kph=round(v[i]*3.6,3), FL_Unity=actual_wheels[i,0],
                   FL_calc=round(on[i,1],6), FR_Unity=actual_wheels[i,1], FR_calc=round(on[i,2],6),
                   FL_off_same_state=round(off[i,1],6), FR_off_same_state=round(off[i,2],6))
              for i,r in enumerate(rows) if first<=i<=finish]
    write_table(out/"steering.csv",frames)

    # Supply the actual front wheel input to the old predictor to isolate downstream error.
    tangents = np.tan(np.radians(actual_wheels))
    total = tangents.sum(axis=1)
    rack = np.arctan(np.divide(2*tangents.prod(axis=1),total,out=np.zeros_like(total),where=total!=0))
    heading = np.unwrap(np.arctan2(col("front_axle_x")-col("rear_axle_x"),col("front_axle_z")-col("rear_axle_z")))
    course = np.unwrap(np.arctan2(col("world_v_x"),col("world_v_z")))
    grade, vertical = road_inputs(col("y"),v)
    gear = col("gear").astype(int)-1
    dry = np.array([all(r[w+"_cover"]=="沥青" for w in WHEELS) for r in rows])
    valid = (driving & dry & ~contact & (ground==4) & (v>15) & (col("W")==1) & (col("brake")==0)
             & (hand==0) & (col("hand_bar")==0) & (col("boost")==0) & (col("reverse")==0))
    params,_ = model.identify()  # Recreate the original thrust map from its v8 training file only.
    out_windows = []
    for horizon in (50,100):
        for a,end in runs(valid):
            i = a+20  # settle driver pedal and pressure for 0.4 s
            while i+horizon < end:
                j=i+horizon
                if reset[i]!=reset[j] or np.any(gear[i-20:j+1]!=gear[i]):
                    i+=1
                    continue
                q=np.array([v[i],np.radians(b[i]),np.radians(yaw[i]),heading[i],x[i],z[i]])
                for k in range(i,j):
                    def rates(state):
                        thrust=model.drive_limit(np.asarray(state[0]),gear[k],state[1])
                        return derivative(state,rack[k],thrust,grade[k],vertical[k],params)
                    q += model.DT*rates(q+.5*model.DT*rates(q))
                turn=np.degrees(course[j]-course[i])
                predicted=np.degrees(q[3]+q[1]-heading[i])-b[i]
                out_windows.append(dict(start_s=round(clock[i],2),duration_s=horizon*model.DT,
                    lap=rows[i]["lap"],gate=rows[i]["next_gate"],gear=gear[i]+1,beta_start=b[i],
                    course_unity=turn,course_calc=predicted,course_error=predicted-turn,
                    kph_unity=v[j]*3.6,kph_calc=q[0]*3.6,yaw_unity=yaw[j],yaw_calc=np.degrees(q[2]),
                    beta_unity=b[j],beta_calc=np.degrees(q[1]),x_unity=x[j],z_unity=z[j],
                    x_calc=q[4],z_calc=q[5],xz_error_m=np.hypot(q[4]-x[j],q[5]-z[j])))
                i=j+1
    write_table(out/"motion-windows.csv",out_windows)
    for horizon in (1,2):
        subset=[w for w in out_windows if w["duration_s"]==horizon]
        for group,predicate in (("all",lambda w:True),("sliding",lambda w:abs(w["beta_start"])>=8)):
            sample=[w for w in subset if predicate(w)]
            if sample:
                print(horizon,group,len(sample),"median abs course/xz/kph/yaw",
                      np.median([[abs(w["course_error"]),w["xz_error_m"],abs(w["kph_calc"]-w["kph_unity"]),
                         abs(w["yaw_calc"]-w["yaw_unity"])] for w in sample],axis=0))
                print("largest course miss",max(sample,key=lambda w:abs(w["course_error"])))
    plot(out, clock, rows, driving, x, z, b, hand, actual_wheels[:,0], off[:,1], longest, out_windows)


def plot(out, time, rows, driving, x, z, beta, hand, wheel, off, longest, windows):
    plt.rcParams["font.family"]=["Microsoft YaHei","DejaVu Sans"]
    fig,axes=plt.subplots(1,3,figsize=(15.8,4.8),layout="constrained")
    for lap,color in ((1,"#8995a6"),(2,"#187b79")):
        mask=driving & np.array([r["lap"]==str(lap) for r in rows])
        line_x=x.copy(); line_z=z.copy()
        jump=np.r_[False,[a["reset_generation"]!=b["reset_generation"] for a,b in zip(rows,rows[1:])]]
        line_x[~mask|jump]=np.nan;line_z[~mask|jump]=np.nan
        axes[0].plot(line_x,line_z,c=color,lw=1.2,label=f"第 {lap} 圈")
    a,j=longest
    axes[0].plot(x[a:j],z[a:j],lw=3,c="#d2742a",label="最长连续侧滑段")
    axes[0].set(aspect="equal",title="两圈实际行驶轨迹",xlabel="世界 X（米）",ylabel="世界 Z（米）")
    sel=(time>=time[a]-1.2)&(time<=time[j]+2)
    axes[1].plot(time[sel],beta[sel],label="车身侧滑角",c="#d2742a")
    axes[1].plot(time[sel],wheel[sel],label="实测左前轮角",c="#187b79",lw=2)
    axes[1].plot(time[sel],off[sel],"--",label="同状态关闭辅助的轮角",c="#596280")
    ax=axes[1]
    for start,stop in runs(sel&(hand>0)):
        ax.axvspan(time[start],time[stop-1]+.02,alpha=.09,color="#222222")
    barrier=np.array(["Barrier" in row["prev_contact_object"] for row in rows])
    hits=np.flatnonzero(sel & barrier)
    if len(hits):
        ax.axvline(time[hits[0]],ls=":",c="#9e3e31",label="首次触栏（图内）")
    ax.set(xlabel="发车后（秒）",ylabel="角度（°），阴影为手刹",title="同一段侧滑中的前轮与车身")
    for duration,marker in [(1,"o"),(2,"^")]:
        cases=[w for w in windows if w["duration_s"]==duration]
        axes[2].scatter([w["course_unity"] for w in cases],[w["course_calc"] for w in cases],
                        s=35,alpha=.78,marker=marker,label=f"{duration} 秒窗口")
    limits=axes[2].get_xlim()+axes[2].get_ylim()
    bounds=[min(limits),max(limits)]
    axes[2].plot(bounds,bounds,"--",color="#929292",linewidth=1)
    axes[2].set(xlim=bounds,ylim=bounds,aspect="equal",title="旧轨迹公式与 Unity 遥测",xlabel="实际行进方向改变量（°）",ylabel="预测改变量（°）")
    for ax in axes:
        ax.legend(frameon=False,fontsize=9);ax.grid(alpha=.2)
    fig.savefig(out/"comparison.png",dpi=160)
    plt.close(fig)


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("telemetry",type=Path)
    review(parser.parse_args().telemetry)
