"""Run blender -b --factory-startup --python-exit-code 1 -P build.py."""
from pathlib import Path
import json
from collections import defaultdict
folder=Path(__file__).resolve().parent
for module in ["geometry","coachwork","running_gear","mechanicals","finishing"]:
    exec(compile((folder/(module+".py")).read_text(encoding="utf8"),str(folder/(module+".py")),"exec"))
# Organize the native file into named collections without touching the mounting hierarchy.
for g in [skin,hood,deck,windows,graphics]:
    for obj in g.children:
        if obj.type!='MESH' or obj.location.length>.001 or obj.name.startswith(("Inner","Extraction","Rolled arch")):
            continue
        for face in obj.data.polygons:
            x,y,z=face.center
            normal=Vector((x*.83,max(0,abs(y)-1.74)*(1 if y>0 else -1)*3.6,(z-.42)*1.65))
            if face.normal.dot(normal)<0:face.flip()
        obj.data.update()
for name,g in groups.items():
    col=bpy.data.collections.new(name);scene.collection.children.link(col)
    for obj in [g]+list(g.children):
        col.objects.link(obj);scene.collection.objects.unlink(obj)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.region_3d.view_distance=6.8;sp.region_3d.view_location=(0,0,.62)
            sp.region_3d.view_rotation=(Vector((0,0,.55))-Vector((4,6,2.45))).to_track_quat('-Z','Y')
            sp.overlay.show_overlays=False;sp.shading.color_type='MATERIAL';sp.shading.light='STUDIO'
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(folder/"SHIBATA31-装配.blend"),compress=True)
# Bake evaluated meshes. Rigid metal and deforming rubber must use different render meshes.
dg=bpy.context.evaluated_depsgraph_get(); inventory={}; packets=[]
for name,g in list(groups.items()):
    inventory[name]=[o.name for o in g.children]
    buckets=defaultdict(lambda:[[],[],[]]);origin=g.matrix_world.inverted()
    for o in g.children:
        ev=o.evaluated_get(dg);m=ev.to_mesh()
        if m is None:continue
        xf=origin @ ev.matrix_world;mat=m.materials[0] if len(m.materials) else o.data.materials[0]
        mat=bpy.data.materials[mat.name]
        key=o.name if o.name.startswith("Tyre_") else "Surface "+mat.name
        v,f,smooth=buckets[(key,mat)]
        offset=len(v);v.extend([tuple(xf @ p.co) for p in m.vertices])
        f.extend([tuple(offset+i for i in p.vertices) for p in m.polygons]);smooth.extend([p.use_smooth for p in m.polygons])
        ev.to_mesh_clear()
    packets.append((name,tuple(g.location),buckets))
print("Baked source meshes",flush=True)
bpy.data.batch_remove(list(scene.objects))
triangles=0;meshes=0
for name,center,buckets in packets:
    anchor=group(name,center)
    for (key,mat),(verts,faces,smooth) in buckets.items():
        o=mesh(key,verts,faces,mat,anchor)
        o.data.polygons.foreach_set("use_smooth",smooth)
        triangles+=sum(len(face)-2 for face in faces);meshes+=1
out=project/"Assets/_Project/Vehicles/SHIBATA31"
def surface(m):
    p=m.node_tree.nodes.get("Principled BSDF").inputs;r,g,b,a=m.diffuse_color
    return dict(name=m.name,r=r,g=g,b=b,a=a,metal=p["Metallic"].default_value,
                gloss=1-p["Roughness"].default_value,emission=p["Emission Strength"].default_value)
(folder/"surfaces.json").write_text(json.dumps(dict(surfaces=[surface(m) for m in palette.values()]),indent=2),encoding="utf8")
(folder/"joints.json").write_text(json.dumps(dict(links=rig_links),indent=2),encoding="utf8")
bpy.ops.export_scene.fbx(filepath=str(out/"SHIBATA31.Detail.fbx"),axis_forward='-Z',axis_up='Y',object_types={'EMPTY','MESH'},add_leaf_bones=False,bake_anim=False,apply_unit_scale=True)
(folder/"parts.json").write_text(json.dumps({"groups":inventory,"wheelCentersBlenderM":wheel_origins,"sourceObjects":sum(map(len,inventory.values())),"exportMeshes":meshes,"triangles":triangles},indent=2),encoding="utf8")
print("Finished",triangles,"triangles",meshes,"meshes",flush=True)
