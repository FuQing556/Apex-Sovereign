"""Render the saved assembly for scale and silhouette review, without editing the blend."""
from pathlib import Path
import bpy
from mathutils import Vector
folder=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder/'SHIBATA31.blend'))
scene=bpy.context.scene
bpy.ops.object.camera_add(location=(5.9,6.6,3.25))
cam=bpy.context.object
cam.rotation_euler=(Vector((0,0,.59))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=5.8;scene.camera=cam
scene.render.engine='BLENDER_WORKBENCH'
sh=scene.display.shading;sh.light='STUDIO';sh.color_type='MATERIAL';sh.show_shadows=True
sh.show_cavity=True;sh.cavity_type='WORLD';sh.show_specular_highlight=True
sh.background_type='WORLD';scene.world=bpy.data.worlds.new('Review');scene.world.color=(.17,.19,.22)
scene.render.resolution_x=1000;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.filepath=str(folder/'装配预览.png')
bpy.ops.render.render(write_still=True)

# An assembly study separates the wheels and brakes from the drivetrain.
# Hide the outer shell so the normally occluded parts can be identified.
for parent in ('Shell', 'Cockpit', 'Aero'):
    for part in bpy.data.objects[parent].children_recursive:
        part.hide_render=True
for tag, side in [('FL',-1),('FR',1),('RL',-1),('RR',1)]:
    bpy.data.objects['Wheel_'+tag].location.x+=side*.52
    bpy.data.objects['Caliper_'+tag].location.x+=side*.15
cam.location=(4.4,6.4,6.5)
cam.rotation_euler=(Vector((0,0,.42))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=5.2
scene.render.filepath=str(folder/'动力与轮端拆解.png')
bpy.ops.render.render(write_still=True)
