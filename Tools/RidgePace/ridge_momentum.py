"""World-axis angular momentum from the archived axle positions and current prefab inertia."""
import csv
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation

root=Path(__file__).resolve().parents[2]
output=root/"Logs/HandlingChecks/handling-v9"
with (output/"r8/frames.csv").open(encoding="utf-8-sig") as f:
    rig=next(csv.DictReader(f))
principal=np.array([float(rig["mass_axes_I_"+a]) for a in "xyz"])
axis=Rotation.from_quat([float(rig["inertia_local_q"+a]) for a in "xyzw"])
with next((root/"Logs/DriveTelemetry").glob("*01c464b3.csv")).open(encoding="utf-8-sig") as f:
    rows=[r for r in csv.DictReader(f) if 146<float(r["sim_s"])<151]
t=np.array([float(r["sim_s"]) for r in rows])
def vectors(prefix):
    return np.array([[float(r[prefix+a]) for a in "xyz"] for r in rows])
def unit(a):
    return a/np.linalg.norm(a,axis=1)[:,None]
front,rear=vectors("front_axle_"),vectors("rear_axle_")
z=unit(front-rear)
x=unit(np.cross(.5*(front+rear)-vectors(""),z))
y=np.cross(z,x)
q=Rotation.from_matrix(np.stack([x,y,z],axis=-1))
omega=np.zeros_like(z)
omega[1:]=(q[1:]*q[:-1].inv()).as_rotvec()/np.diff(t)[:,None]
om_y=np.radians([float(r["yaw_deg_s"]) for r in rows])
omega+=y*(om_y-np.sum(omega*y,axis=1))[:,None]
axes=q*axis
h=axes.apply(axes.inv().apply(omega)*principal)
dt=np.diff(t)
moment=np.diff(h,axis=0)/dt[:,None]
with (output/"original-world-momentum.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.writer(f);w.writerow(["sim_s","Hx","Hy","Hz","Mx","My","Mz","omega_x","omega_y","omega_z"])
    for i in range(2,len(t)):
        w.writerow([t[i],*h[i],*moment[i-1],*omega[i]])
for half in (0,.04,.10):
    # h is reconstructed from rounded legacy observations. Endpoint averaging checks noise.
    a=h[abs(t-148.5599)<=half+.001].mean(axis=0)
    b=h[abs(t-149.5599)<=half+.001].mean(axis=0)
    print("endpoint half-window",half,"mean net torque N m",np.round(b-a,1))
