"""Render the actual editable assembly in a softbox studio, including component studies."""
from pathlib import Path
import bpy, math
from mathutils import Vector
folder=Path(__file__).resolve().parent
scene=bpy.context.scene
scene.render.engine="CYCLES";scene.cycles.samples=16;scene.cycles.use_denoising=True
scene.render.resolution_percentage=100;scene.render.resolution_x=1056;scene.render.resolution_y=660
scene.render.image_settings.file_format='PNG'
world=bpy.data.worlds.new("Neutral sweep");scene.world=world;world.use_nodes=True
world.node_tree.nodes["Background"].inputs["Color"].default_value=(.35,.40,.47,1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value=.40
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003))
floor=bpy.context.object;floor.name="Studio floor"
m=bpy.data.materials.new("Studio basalt");m.use_nodes=True
m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(.073,.086,.105,1);floor.data.materials.append(m)
def light(name,p,target,watts,x,y,color):
    l=bpy.data.lights.new(name,'AREA');o=bpy.data.objects.new(name,l);scene.collection.objects.link(o)
    l.energy=watts;l.shape='RECTANGLE';l.size=x;l.size_y=y;l.color=color;o.location=p;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
light("Long reflection",(-3,2,5),(0,0,.6),540,5,1.6,(1,.90,.79))
light("Overhead strip",(2,-.5,4),(0,0,0),540,4,.8,(.81,.89,1))
light("Nose softbox",(1,5,2.5),(0,0,.55),310,3,2.4,(1,.96,.86))
light("Tail separation",(-.5,-4,2.8),(0,-1,.5),380,3,1,(.81,.90,1))
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam
def shot(name,pos,target,lens):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.lens=lens;cam.data.clip_start=.02
    scene.render.filepath=str(folder/(name+".png"));bpy.ops.render.render(write_still=True)
shot("01-整车前视",(5.6,6.15,2.42),(0,.03,.57),58)
shot("02-整车后视",(-5,-6.4,2.02),(0,0,.54),57)
for colname in ["02_Bonnet_lid"]:bpy.data.collections[colname].hide_render=True
shot("03-揭盖动力舱",(1.95,3.39,3.05),(0,1.18,.52),69)
for collection in bpy.data.collections:
    if collection.name not in ("Wheel_FR","Caliper_FR","09_Suspension_links","10_Unibody_and_cradles") and not collection.name.startswith("Link_FR_"):collection.hide_render=True
shot("04-右前轮与悬架",(2.58,2.03,1.22),(.73,1.27,.37),56)
for c in bpy.data.collections:
    c.hide_render=c.name not in ("16_Competition_cockpit","17_Cage_and_belts","18_Steering_wheel","10_Unibody_and_cradles")
shot("05-座舱与防滚架",(-1.79,-1.28,1.79),(.05,.02,.59),47)
for c in bpy.data.collections:
    c.hide_render=c.name.startswith(("01_","02_","03_","04_","05_","06_","07_","08_","16_","17_"))
floor.hide_render=True
shot("06-底盘传动",(-2.42,-3.48,-3.25),(0,-.03,.34),43)
