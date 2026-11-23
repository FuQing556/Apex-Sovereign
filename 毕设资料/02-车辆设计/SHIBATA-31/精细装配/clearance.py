"""Check actual mesh intersections in the representative steering/travel envelope."""
from pathlib import Path
import json,bpy,math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
folder=Path(__file__).parent
dg=bpy.context.evaluated_depsgraph_get()
fixed=("01_","02_","03_","04_","05_","06_","07_","09_","10_","11_","12_","13_","14_","15_","16_","17_")
objs=[o for o in bpy.context.scene.objects if o.type=="MESH" and o.parent and o.parent.name.startswith(fixed)]
def bvh(o,mat):
    m=o.evaluated_get(dg).data
    return BVHTree.FromPolygons([mat @ v.co for v in m.vertices],[p.vertices[:] for p in m.polygons])
body={o.name:bvh(o,o.matrix_world) for o in objs}
hood=body["Bonnet"]
bay=[o for o in objs if o.parent.name.startswith(("09_","10_","11_","12_","13_","14_","15_"))]
lid_hits=[o.name for o in bay if hood.overlap(body[o.name])]
(folder/"bonnet-clearance.json").write_text(json.dumps(dict(checked=len(bay),intersections=lid_hits),indent=2),encoding="utf8")
print("Closed bonnet against",len(bay),"parts",lid_hits)
results=[]
for tag in ["FL","FR","RL","RR"]:
    o=bpy.data.objects["Tyre_"+tag];origin=o.matrix_world.translation
    for mm in [-120,-60,0,60,120]:
        for deg in ([-62.5,-45,-25,0,25,45,62.5] if tag[0]=="F" else [0]):
            xf=Matrix.Translation(origin+Vector((0,0,mm/1000))) @ Matrix.Rotation(math.radians(deg),4,'Z')
            tyre=bvh(o,xf)
            hit=[name for name,tree in body.items() if tree.overlap(tyre)]
            results.append(dict(wheel=tag,bump_mm=mm,steer_deg=deg,intersections=hit))
(folder/"clearance.json").write_text(json.dumps(results,indent=2),encoding="utf8")
brakes=[]
for tag in ["FL","FR","RL","RR"]:
    wheel=bpy.data.objects["Wheel_"+tag];carrier=bpy.data.objects["Caliper_"+tag]
    stationary={o.name:bvh(o,o.matrix_world) for o in carrier.children}
    spinning=[o for o in wheel.children if o.name.startswith(("Forged spoke","Rolled wheel barrel","Machined rim lip","Tyre_"))]
    for phase in range(0,36,3):
        m=Matrix.Translation(wheel.location) @ Matrix.Rotation(math.radians(phase),4,'X') @ Matrix.Translation(-wheel.location)
        hits=set()
        for o in spinning:
            spin=bvh(o,m @ o.matrix_world)
            hits.update(s for s,v in stationary.items() if v.overlap(spin))
        brakes.append(dict(wheel=tag,phase_deg=phase,intersections=sorted(hits)))
(folder/"brake-clearance.json").write_text(json.dumps(brakes,indent=2),encoding="utf8")
print("Rim to fixed brake checks",len(brakes),"clear",sum(not r["intersections"] for r in brakes))
for row in results:
    if row["intersections"]:print(row)
print("Cases",len(results),"clear",sum(not r["intersections"] for r in results))
