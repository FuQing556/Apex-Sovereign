"""Build a bounded racing line from the actual MarinaBay scene centreline."""
from pathlib import Path
import re
import numpy as np
from scipy.optimize import minimize
from scipy.interpolate import CubicSpline

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
scene = (root / "Assets/_Project/Scenes/MarinaBay.unity").read_text(encoding="utf-8")
ring = scene.split("  mapCenterline:\n", 1)[1].split("\n  padMarkings:", 1)[0]
points = np.array([(float(x), float(z)) for x, z in re.findall(
    r"^  - \{x: ([-.\dEe+]+), y: [-.\dEe+]+, z: ([-.\dEe+]+)\}", ring, re.M)])
if len(points) != 1600:
    raise ValueError(f"Unexpected centreline length: {len(points)} samples")

# Optimize the squared second differences on a periodic grid. Limit the
# displacement to 7.5 m even on the 24 m-wide parts of the course.
coarse = points[::4]
tangent = np.roll(coarse, -1, 0) - np.roll(coarse, 1, 0)
normal = tangent[:, [1, 0]] * [1, -1] / np.linalg.norm(tangent, axis=1)[:, None]
def laplacian(p):
    return np.roll(p, 1, 0) + np.roll(p, -1, 0) - 2*p
def objective(offsets):
    # A small regularizer avoids changing lanes unnecessarily on straights.
    curvature = laplacian(coarse + offsets[:, None]*normal)
    return (np.sum(curvature**2) + .0003*(offsets@offsets),
            2*np.sum(laplacian(curvature)*normal, axis=1) + .0006*offsets)
result = minimize(objective, np.zeros(len(coarse)), jac=True, method="L-BFGS-B",
                  bounds=[(-7.5, 7.5)]*len(coarse), options={"maxiter":1500,"ftol":1e-11})
tangent = np.roll(points,-1,0)-np.roll(points,1,0)
normal = tangent[:,[1,0]]*[1,-1]/np.linalg.norm(tangent,axis=1)[:,None]
offset = CubicSpline(np.arange(0,len(points)+1,4),np.r_[result.x,result.x[0]],
                     bc_type="periodic")(np.arange(len(points))).clip(-7.5,7.5)
line = points+offset[:,None]*normal
np.savetxt(out/"line.csv", line, fmt="%.6f", delimiter=",", header="x,z",comments="")
print("samples",len(line),"length_m",np.linalg.norm(np.roll(line,-1,0)-line,axis=1).sum())
