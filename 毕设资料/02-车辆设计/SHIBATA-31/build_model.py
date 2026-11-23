"""Run with Blender --background --factory-startup --python build_model.py."""
import math
from pathlib import Path
import bpy
from mathutils import Vector

here = Path(__file__).resolve().parent
root = here.parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
bpy.context.preferences.filepaths.save_version = 0

def material(name, color, metal=0, rough=.36):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color,1)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*color,1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    return m

red = material("Vermilion lacquer",(.62,.019,.028),.38)
black = material("Carbon graphite",(.025,.033,.042),.34)
glass = material("Smoked glazing",(.062,.125,.155),.55,.16)
rubber = material("Tyre sidewall",(.022,.025,.029),0,.78)
rim = material("Satin alloy",(.32,.36,.39),.82)
steel = material("Iron rotor",(.17,.19,.21),.78)
white = material("Ice white",(.85,.91,.94),.35)
tail = material("Ruby lens",(.76,.025,.012),.3)
def group(name):
    o=bpy.data.objects.new(name,None)
    scene.collection.objects.link(o)
    o.empty_display_size=.12
    return o
body=group("Shell"); cabin=group("Cockpit"); aero=group("Aero"); power=group("Driveline"); brake=group("Uprights")
def mesh(name, verts, faces, mat, parent):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
    o=bpy.data.objects.new(name,m);scene.collection.objects.link(o);o.parent=parent
    m.materials.append(mat)
    # Closed mesh normals and open exterior panels retain consistent winding.
    import bmesh
    bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(m);bm.free()
    return o
def box(name,pos,sz,mat,parent,bevel=.018):
    bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;o.name=name;o.dimensions=sz
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.parent=parent;o.location=pos;o.data.materials.append(mat)
    if bevel:
        b=o.modifiers.new("Edge radius","BEVEL");b.width=bevel;b.segments=3
        o.modifiers.new("Face normals","WEIGHTED_NORMAL")
    return o
def rod(name,a,b,r,mat,parent):
    v=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=r,depth=v.length)
    o=bpy.context.object;o.name=name;o.parent=parent;o.location=(Vector(a)+Vector(b))/2
    o.rotation_euler=v.to_track_quat('Z','Y').to_euler();o.data.materials.append(mat)
    return o
def loft(name,sections,mat,parent):
    n=len(sections[0]); faces=[(i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j) for i in range(len(sections)-1) for j in range(n)]
    faces += [tuple(reversed(range(n))),tuple(range((len(sections)-1)*n,len(sections)*n))]
    return mesh(name,[v for s in sections for v in s],faces,mat,parent)
wb=2.575
# SHIBATIRE R23 catalogue outer radii, in metres. Both are unloaded.
front_radius, rear_radius = .3215, .354
wheels=[("FL",-1,1,.265,18,.025),("FR",1,1,.265,18,.025),
        ("RL",-1,-1,.285,20,.012),("RR",1,-1,.285,20,.012)]
def interp(y, table):
    for (a,v),(b,w) in zip(table,table[1:]):
        if y<=b:return v+(w-v)*(y-a)/(b-a)
    return table[-1][1]
width=[(-2.1325,.77),(-1.93,.90),(-1.33,.964),(-.65,.90),(.58,.895),(1.28,.978),(1.83,.91),(2.1325,.80)]
deck=[(-2.1325,.66),(-1.85,.79),(-1.30,.84),(-.70,.80),(.63,.835),(1.25,.79),(1.81,.68),(2.1325,.53)]
sections=[]
for i in range(123):
    y=-2.1325+4.265*i/122;w=interp(y,width);h=interp(y,deck)
    sill=.16
    for _,_,ax,t,inch,_et in (wheels[0],wheels[2]):
        rad=front_radius if ax>0 else rear_radius
        dy=abs(y-ax*wb/2); opening=rad+.055
        if dy<opening:sill=max(sill,rad+math.sqrt(opening**2-dy**2))
    shoulder=max(h-.04,sill+.035)
    sec=[(.63,.16),(.70,sill),(w-.015,sill),(w,shoulder),(w*.90,max(shoulder,h)),(.63,h+.025)]
    sections.append([(x,y,z) for x,z in sec]+[(-x,y,z) for x,z in reversed(sec)])
shell=loft("Widebody with four wheel wells",sections,red,body)
for p in shell.data.polygons:p.use_smooth=True
# Greenhouse from sill to narrower roof with separate panes and pillars.
cab=[(-1.49,.79,.81),(-1.29,.73,.92),(-.77,.66,1.18),(-.52,.63,1.25),(.10,.62,1.26),(.30,.65,1.19),(.83,.79,.845)]
rings=[]
for y,w,z in cab:
    rings.append([(-.83,y,.80),(-w,y,z),(0,y,z+.022),(w,y,z),(.83,y,.80)])
loft("Glazing",rings,glass,cabin)
loft("Roof skin",[[(-w,y,z),(0,y,z+.021),(w,y,z),(w,y,z+.009),(0,y,z+.036),(-w,y,z+.009)] for y,w,z in cab[2:6]],black,body)
for sign in [-1,1]:
    for name,a,b in [("A pillar",(.81,.82,.84),(.65,.23,1.22)),("B pillar",(.83,-.46,.81),(.63,-.46,1.25)),("C pillar",(.80,-1.44,.82),(.65,-.69,1.21))]:
        rod(name,(sign*a[0],*a[1:]),(sign*b[0],*b[1:]),.028,red,body)
    rod("Mirror stalk",(sign*.82,.48,.86),(sign*.94,.52,.91),.023,black,body)
    box("Mirror",(sign*.97,.53,.92),(.125,.17,.082),black,body)
    box("Rocker extension",(sign*.91,0,.17),(.095,1.79,.085),black,aero)
    for y in [-wb/2,wb/2]:
        rad=(rear_radius if y<0 else front_radius)+.052
        strips=[]
        for k in range(33):
            a=math.pi*k/32;yy=y+rad*math.cos(a);zz=rad-.052+rad*math.sin(a);ww=interp(yy,width)
            strips.append([(sign*(ww+d),yy,zz+h) for d,h in [(-.03,0),(.016,0),(.008,.032),(-.03,.055)]])
        loft("Overfender lip",strips,red,body)
    mesh("Beltline graphic",[(sign*x,y,z) for x,y,z in [(.912,-.70,.73),(.901,.61,.66),(.902,.52,.52),(.904,-.44,.60)]],[(0,1,2,3)],black,body)
    bpy.ops.object.text_add()
    number=bpy.context.object
    number.name="Door number"
    number.data.body="31";number.data.size=.265;number.data.shear=.17
    number.data.align_x='CENTER';number.data.align_y='CENTER'
    number.rotation_euler=(math.pi/2,0,sign*math.pi/2)
    number.location=(sign*.914,-.02,.56);number.parent=body
    number.data.materials.append(white)
    bpy.ops.object.convert(target='MESH')
    def lamp_strip(name, outline, mat, lift=0):
        pts = [(sign*x,y,interp(y,deck)+.036+z+lift) for x,y,z in outline]
        lens=mesh(name,pts,[(0,1,2),(0,2,3)],mat,body)
        thick=lens.modifiers.new("Lens depth","SOLIDIFY");thick.thickness=.008
    lamp_strip("Headlamp housing",[(.50,2.126,0),(.78,2.12,0),(.84,1.85,0),(.67,1.94,0)],black)
    lamp_strip("Front LED signature",[(.52,2.119,0),(.76,2.116,0),(.80,2.045,0),(.61,2.08,0)],white,.009)
    lamp_strip("Tail lamp housing",[(.39,-2.128,0),(.76,-2.128,0),(.84,-1.916,0),(.65,-2.01,0)],black)
    lamp_strip("Tail light guide",[(.41,-2.12,0),(.75,-2.12,0),(.79,-2.02,0),(.66,-2.07,0)],tail,.011)
    box("Wing pylon",(sign*.53,-1.90,.92),(.043,.14,.38),black,aero)
    box("Wing endplate",(sign*.985,-1.96,1.115),(.028,.41,.17),black,aero)
    rod("Cage main hoop",(sign*.63,-.58,.31),(sign*.57,-.58,1.16),.022,rim,cabin)
    rod("Cage diagonal",(sign*.57,-.58,1.16),(-sign*.56,-1.20,.36),.022,rim,cabin)
    rod("Rear outlet",(sign*.65,-1.82,.26),(sign*.65,-2.18,.26),.065,steel,aero)
rod("Cage crossmember",(-.57,-.58,1.16),(.57,-.58,1.16),.022,rim,cabin)
box("Front splitter",(0,1.86,.14),(1.86,.59,.047),black,aero)
box("Rear diffuser",(0,-1.93,.19),(1.65,.43,.09),black,aero)
box("Wing foil",(0,-1.96,1.11),(1.97,.33,.055),black,aero)
box("Intake opening",(0,2.133,.365),(1.18,.028,.249),black,body)
for i in range(13):box("Intake vane",((i-6)*.085,2.153,.365),(.015,.022,.194),steel,body,.003)
for sign in [-1,1]:
    for j in range(4):
        y=1.0+j*.085
        mesh("Hood extraction louver",[(sign*x,yy,interp(yy,deck)+.033) for x,yy in [(.20,y),(.45,y+.035),(.46,y+.071),(.21,y+.03)]],[(0,1,2,3)],black,body)
box("Seat",(0.34,-.23,.47),(.43,.47,.16),black,cabin)
box("Seat back",(0.34,-.44,.70),(.43,.14,.55),black,cabin)
box("V6 block envelope",(0,.89,.49),(.51,.55,.30),steel,power)
for s in [-1,1]:
    bank=box("Cylinder bank",(s*.205,.88,.65),(.19,.52,.16),black,power);bank.rotation_euler.y=s*.45
    for j in range(3):
        y=.70+j*.16
        box("Coil envelope",(s*.208,y,.744),(.067,.08,.035),steel,power,.008)
        rod("Intake runner",(s*.065,y,.77),(s*.16,y,.69),.028,rim,power)
box("Intake plenum envelope",(0,.89,.78),(.135,.50,.085),rim,power)
box("Gearbox envelope",(0,.25,.35),(.31,.55,.24),steel,power)
rod("Propshaft",(0,.04,.34),(0,-1.28,.355),.043,steel,power)
for y in [-1.18,-.03]:
    rod("Propshaft coupling",(0,y-.02,.35),(0,y+.02,.35),.075,rim,power)
box("Final drive",(0,-wb/2,.355),(.29,.27,.22),steel,power)
for side in [-1,1]:
    rod("Rear half shaft",(side*.11,-wb/2,.355),(side*.802,-wb/2,rear_radius),.027,steel,power)
    for end in [.17,.71]:
        for rib in range(4):
            x=side*(end+rib*.014)
            rod("CV boot rib",(x,-wb/2,.355),(x+side*.009,-wb/2,.355),.040+.003*rib,rubber,power)
for tag,sg,ax,t,inch,et in wheels:
    rad=front_radius if ax>0 else rear_radius
    bead=inch*.0254/2;y=ax*wb/2;x=sg*(.835 if ax>0 else .805)
    hub=group("Wheel_"+tag);hub.location=(x,y,rad)
    hub["catalogue_OD_m"]=rad*2;hub["ET_m"]=et
    def ring(n,profile,mat):
        segs=64;verts=[(u,r*math.cos(2*math.pi*j/segs),r*math.sin(2*math.pi*j/segs)) for u,r in profile for j in range(segs)]
        faces=[(i*segs+j,i*segs+(j+1)%segs,((i+1)%len(profile))*segs+(j+1)%segs,((i+1)%len(profile))*segs+j) for i in range(len(profile)) for j in range(segs)]
        o=mesh(n,verts,faces,mat,hub)
        for f in o.data.polygons:f.use_smooth=True
    ring("Tyre_"+tag,[(-t/2+.019,rad),(-t/2,rad-.023),(-t/2,bead),(-t/2+.018,bead-.006),(t/2-.018,bead-.006),(t/2,bead),(t/2,rad-.023),(t/2-.019,rad)],rubber)
    ring("Rim barrel",[(t*.48,bead+.008),(-t*.48,bead+.008),(-t*.48,bead-.012),(t*.48,bead-.012)],rim)
    face=sg*t*.46
    for j in range(10):
        a=2*math.pi*j/10;rod("Spoke",(face-sg*.026,.054*math.cos(a),.054*math.sin(a)),(face,(bead-.01)*math.cos(a+.09),(bead-.01)*math.sin(a+.09)),.014,rim,hub)
    rod("Hub",(face,0,0),(face-sg*.055,0,0),.064,steel,hub)
    for j in range(5):
        a=2*math.pi*j/5
        yy,zz=.047*math.cos(a),.047*math.sin(a)
        rod("Lug nut",(face,yy,zz),(face+sg*.009,yy,zz),.009,rim,hub)
    rod("Brake disc",(face-sg*.06,0,0),(face-sg*.085,0,0),bead*.80,steel,hub)
    box("Caliper_"+tag,(x+face-sg*.079,y-.138,rad),(.075,.072,.148),red,brake)
# Make the scientific source editable and the project asset portable.
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=7
        area.spaces.active.region_3d.view_location=(0,0,.62)
        area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(here/'SHIBATA31.blend'))
out=root/'Assets/_Project/Vehicles/SHIBATA31'
out.mkdir(exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(out/'SHIBATA31.fbx'),axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,object_types={'EMPTY','MESH'},apply_unit_scale=True)
print('WHEEL DATUM', [(o.name,tuple(o.location)) for o in scene.objects if o.name.startswith('Wheel_')])
