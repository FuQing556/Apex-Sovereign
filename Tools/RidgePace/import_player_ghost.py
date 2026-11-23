"""Package a finished 50 Hz keyboard telemetry file into the Unity player archive."""
import argparse
import csv
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
from scipy.spatial.transform import Rotation
import inverse_drift as model


def convert(path, title):
    with path.open(encoding="utf-8-sig") as stream:
        all_rows = list(csv.DictReader(stream))
    rows = all_rows[next(i for i,r in enumerate(all_rows) if r["phase"]=="Driving"):]
    assert rows[-1]["phase"]=="Results" and all(r["phase"]=="Driving" for r in rows[:-1])
    assert all(r["driver"]=="LocalDriver" and r["vehicle"]=="Okuibuki" and r["setup_revision"]=="9"
               and float(r["pad_x"])==0 and r["advanced_controls"]=="0" for r in rows)
    def col(k):
        return np.array([float(row[k]) for row in rows])
    dt=.02
    np.testing.assert_allclose(np.diff(col("sim_s")),dt,atol=.00021)
    # These keyboard sessions explicitly contain digital pedals; reject analogue histories.
    assert np.isin(col("throttle"),[0,1]).all() and np.isin(col("brake"),[0,1]).all()
    n=len(rows)
    header=dict(version=1,id=path.stem,title=title,track="Ridge",car="Okuibuki",count=n,revision=9,
                interval=dt,firstLap=next(i*dt for i,r in enumerate(rows) if r["lap"]=="2"),
                practice=bool(col("reset_generation").max()>col("reset_generation")[0]),recovery=True,
                advanced=False,differential=rows[0]["differential_part"],rear=rows[0]["rear_wheel_part"],
                sourceHash=hashlib.sha256(path.read_bytes()).hexdigest())
    assert np.all(col("keyboard_recovery_enabled")[:-1]==1)
    body=np.column_stack([col(k) for k in ("x","y","z","body_qx","body_qy","body_qz","body_qw")])
    inverse=Rotation.from_quat(body[:,3:]).inv()
    columns=[body]
    for i,w in enumerate(("FL","FR","RL","RR")):
        front=i<2
        radius=model.car["radius" if front else "rearRadius"]
        cold=model.setup["frontTyre" if front else "rearTyre"]["coldGaugeBar"]
        stiffness=model.car["tireStiffness"]*col(w+"_tyre_bar")/cold
        flatten=np.clip(col(w+"_load_n")/np.maximum(1,stiffness),0,model.car["maxFlatten"])
        hit=np.column_stack([col(w+"_hit_"+axis) for axis in "xyz"])
        normal=np.column_stack([col(w+"_normal_"+axis) for axis in "xyz"])
        center=inverse.apply(hit+(radius-flatten[:,None])*normal-body[:,:3])
        center[col(w+"_ground")==0]=[(-1 if i%2==0 else 1)*(.835 if front else .805),
                                     radius-model.car["travel"]*.5, (1 if front else -1)*model.wheelbase/2]
        # Old telemetry has no rendered wheel pose. Spin phase is reconstructed from rpm.
        spin=np.cumsum(np.r_[0,col(w+"_rpm")[:-1]]*6*dt)
        rotation=Rotation.from_euler("y",col(w+"_steer_deg"),degrees=True)*Rotation.from_euler("x",spin,degrees=True)
        columns.extend([center,rotation.as_quat()])
    columns.extend([col(w+"_steer_deg") for w in ("FL","FR")])
    columns.extend([col(w+"_tyre_c") for w in ("FL","FR","RL","RR")])
    columns.extend([col("speed_mps")*3.6,col("signed_drift_deg"),col("engine_rpm"),np.zeros((n,3))])
    floats=np.column_stack(columns).astype("<f4")
    mask=sum((1<<i)*col(k).astype(int) for i,k in enumerate(("W","A","D","S","space","shift")))
    mask+=64*np.r_[0,np.diff(col("reset_generation"))!=0].astype(int)
    mask+=128*(col("clutch_pedal")>0)+256*col("auto_request").astype(int)
    integers=np.column_stack([col("gear"),mask,col("next_gate"),col("gear_request")]).astype("<i4")
    payload=np.concatenate([floats.view("u1").reshape(n,-1),integers.view("u1").reshape(n,-1)],axis=1).tobytes()
    assert len(payload)==204*n
    meta=json.dumps(header,ensure_ascii=False).encode()
    folder=model.ROOT/"Assets/_Project/Resources/GhostSeeds"
    folder.mkdir(parents=True,exist_ok=True)
    dest=folder/(path.stem+".bytes")
    dest.write_bytes(struct.pack("<i",len(meta))+meta+payload)
    print(title,n,header["firstLap"],(n-1)*dt,header["sourceHash"])


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source",type=Path)
    parser.add_argument("title")
    args=parser.parse_args()
    convert(args.source,args.title)
