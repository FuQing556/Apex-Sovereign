"""Summarise Unity's physical-step results. No vehicle dynamics are simulated here."""
import csv
import math
import sys
from collections import defaultdict
from pathlib import Path
from statistics import mean

ROOT = Path(__file__).resolve().parents[2] / "Logs/HandlingChecks/handling-v9"


def summarise(revision):
    folder = ROOT / revision
    cases = defaultdict(list)
    with (folder / "frames.csv").open(encoding="utf-8-sig") as stream:
        for row in csv.DictReader(stream):
            key = row.pop("case"), row.pop("variant")
            row.pop("prev_contact_object")
            cases[key].append({k:float(v) if v else math.nan for k,v in row.items()})
    output = []
    for (case, mode), frames in cases.items():
        data = [r for r in frames if r["t"] >= 0]
        first, end = data[0], data[-1]
        stable = lambda r: abs(r["beta"]) <= 5 and abs(r["yaw"]) <= 10 and r["ground"] == 4
        recovery = all(stable(r) for r in data if r["t"]>=end["t"]-.50001) and end["contacts"] == first["contacts"] and end["resets"] == first["resets"]
        step = round(data[1]["t"]-first["t"],5)
        count = 0
        best = 0
        for r in data:
            qualifying = 12<=abs(r["beta"])<=45 and abs(r["yaw"])>8 and r["kph"]>30 and r["ground"]==4
            count = count + 1 if qualifying else 0
            best = max(count,best)
        def angle(name):
            return sum((b[name]-a[name]+180)%360-180 for a,b in zip(data,data[1:]))
        d = dict(case=case,variant=mode,entry_xz_error=first["position_error"],end_xz_error=end["position_error"],
                 entry_kph=first["kph"],end_kph=end["kph"],peak_beta=max(abs(r["beta"]) for r in data),
                 end_beta=end["beta"],end_yaw=end["yaw"],body_turn=angle("heading"),path_turn=angle("path"),
                 x=end["x"]-first["x"],z=end["z"]-first["z"],contacts=end["contacts"]-first["contacts"],
                 resets=end["resets"]-first["resets"],min_ground=min(r["ground"] for r in data),
                 drift_s=max(0,best-1)*step,recovered=int(recovery),drift_exit=int((best-1)*step>=.5 and recovery))
        start=2 if case=="long" else 0
        for k in ("prev_net_M_y","prev_residual_M_y","next_rolling_M_y","next_bar_M_y","next_aero_M_y"):
            # Forces are issued at the preceding boundary; interval endpoints describe the next boundary.
            seq=[a[k] if k.startswith("next") else b[k] for a,b in zip(data,data[1:]) if start < b["t"] <= start+1 and b["prev_balance_valid"]]
            d[k]=mean(seq) if seq else math.nan
        output.append(d)
    with (folder/"summary.csv").open("w",encoding="utf-8-sig",newline="") as f:
        writer=csv.DictWriter(f,fieldnames=output[0]);writer.writeheader();writer.writerows(output)
    by_kind=defaultdict(list)
    for r in output:
        key=r["case"].split("_")
        if len(key)==5:
            by_kind[key[0],key[1]].append(r)
        elif r["variant"] in ("continue","counter","recorded","equal","lift","clutch"):
            print(r["case"],r["variant"],*(round(r[k],2) for k in ("end_kph","end_beta","end_yaw","path_turn","end_xz_error","contacts","prev_net_M_y","prev_residual_M_y")))
    for key,rows in by_kind.items():
        print(*key,"recovered",sum(r["recovered"] for r in rows),"/",len(rows),"drift+exit",sum(r["drift_exit"] for r in rows),
              "drift secs range",min(r["drift_s"] for r in rows),max(r["drift_s"] for r in rows))


if __name__=="__main__":
    summarise(sys.argv[1])
