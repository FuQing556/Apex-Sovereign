"""Compare aligned race-clock samples; never move the vehicle or align by nearest position."""
import argparse
import csv
import hashlib
import json
import struct
from pathlib import Path

import numpy as np


def race(path):
    with path.open(encoding="utf-8-sig") as stream:
        data = list(csv.DictReader(stream))
    start = next(i for i,row in enumerate(data) if row["phase"]=="Driving")
    return data[start:]


def column(rows, keys):
    return np.array([[float(row[k]) for k in keys] for row in rows])


def progress(rows):
    return [(i,row["phase"],row["lap"],row["next_gate"]) for i,row in enumerate(rows)
            if i==0 or any(row[k]!=rows[i-1][k] for k in ("phase","lap","next_gate"))]


def compare(source, replay, archive):
    original, driven = race(source), race(replay)
    assert driven[-1]["phase"] in ("Results","ReplayEnded")
    assert all(x["driver"]=="RecordedLocalDriver" for x in driven)
    data=archive.read_bytes()
    size,=struct.unpack_from("<i",data)
    header=json.loads(data[4:4+size])
    assert header["sourceHash"]==hashlib.sha256(source.read_bytes()).hexdigest()
    packed=np.frombuffer(data[4+size:],dtype="<f4").reshape(-1,51)
    dt=header["interval"]
    assert len(packed)==len(original)==header["count"]
    keys=("W","A","D","S","space","shift")
    masks=(column(original,keys).astype(int)*(1<<np.arange(6))).sum(axis=1)
    assert np.array_equal(packed.view("<i4")[:,48]&63,masks)
    for rows in (original,driven):
        elapsed=column(rows,["sim_s"]).ravel()
        np.testing.assert_allclose(elapsed-elapsed[0],np.arange(len(rows))*dt,atol=.00021)
    a=[r for r in original if r["phase"]=="Driving"]
    b=[r for r in driven if r["phase"]=="Driving"]
    common=min(len(a),len(b))
    def mismatches(fields):
        return sum(any(x[k]!=y[k] for k in fields) for x,y in zip(a,b))
    n=min(len(original),len(driven))
    axyz=column(original[:n],"xyz")
    xyz=np.linalg.norm(axyz-column(driven[:n],"xyz"),axis=1)
    def max_error(field):
        return float(np.max(abs(column(original[:n],[field])-column(driven[:n],[field]))))
    result=dict(source=source.name,source_sha256=header["sourceHash"],replay=replay.name,
        archive=archive.name,archive_samples=len(packed),archive_raw_key_errors=0,
        archive_max_xyz_error_m=float(np.max(np.linalg.norm(column(original,"xyz")-packed[:,:3],axis=1))),
        driving_steps_source=len(a),driving_steps_replay=len(b),compared_input_steps=common,
        raw_key_mismatch_steps=mismatches(keys),
        command_mismatch_steps=mismatches(("steer_cmd","throttle","brake","hand","reverse","gear_request","auto_request","clutch_pedal","keyboard_recovery_enabled")),
        configuration_mismatch_steps=mismatches(("setup_revision","vehicle","differential_part","rear_wheel_part","advanced_controls")),
        original_finish=original[-1]["phase"],replay_finish=driven[-1]["phase"],
        replay_seconds=(len(driven)-1)*dt,replay_lap=int(driven[-1]["lap"]),replay_gate=int(driven[-1]["next_gate"]),
        checkpoint_ticks_identical=progress(original)==progress(driven),
        reset_s=[round(i*dt,2) for i in range(1,len(driven)) if driven[i]["reset_generation"]!=driven[i-1]["reset_generation"]],
        contact_flag_mismatch_steps=mismatches(("contact_body","prev_contact_object")),
        peak_position_error_m=float(max(xyz)),end_position_error_m=float(xyz[-1]),
        peak_speed_error_kph=max_error("speed_mps")*3.6,
        peak_sideslip_error_deg=float(np.max(abs((column(original[:n],["signed_drift_deg"])-column(driven[:n],["signed_drift_deg"])+180)%360-180))),
        peak_yaw_rate_error_deg_s=max_error("yaw_deg_s"))
    return result


if __name__=="__main__":
    p=argparse.ArgumentParser(description=__doc__)
    for name in ("source","replay","archive"):
        p.add_argument(name,type=Path)
    p.add_argument("--output",type=Path)
    args=p.parse_args()
    result=json.dumps(compare(args.source,args.replay,args.archive),indent=2,ensure_ascii=False)
    print(result)
    if args.output:
        args.output.write_text(result+"\n",encoding="utf-8")
