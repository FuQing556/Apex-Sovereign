"""Build in metres. Source meshes stay individually selectable in the Blender master."""
import math
from math import sin, cos, pi, sqrt
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector, Matrix
here = Path(__file__).resolve().parent
project = here.parents[3]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = 'NONE'
palette = {}
def paint(name, rgb, metal=0, rough=.38, alpha=1, glow=0):
    m = bpy.data.materials.new(name)
    palette[name] = m
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    m.diffuse_color = (*rgb,alpha)
    for k,v in {"Base Color":(*rgb,1), "Metallic":metal, "Roughness":rough,
                "Alpha":alpha, "Emission Color":(*rgb,1), "Emission Strength":glow}.items():
        p.inputs[k].default_value = v
    if alpha<1: m.surface_render_method = 'DITHERED'
    return m
red=paint("Candy scarlet",(.56,.009,.023),.53,.23)
cherry=paint("Oxblood pearl",(.18,.006,.014),.47,.27)
carbon=paint("Graphite composite",(.022,.026,.032),.38,.32)
black=paint("EPDM black",(.019,.021,.024),0,.65)
rubber=paint("Tread compound",(.025,.026,.029),0,.8)
metal=paint("Machined aluminium",(.49,.53,.56),.84,.27)
dark=paint("Gunmetal anodised",(.088,.096,.106),.73,.33)
gold=paint("Satin champagne",(.48,.32,.145),.78,.29)
heat=paint("Heat tinted stainless",(.24,.146,.09),.82,.3)
white=paint("Ivory vinyl",(.91,.90,.81),.16,.29)
orange=paint("Signal amber",(1,.24,.025),.32,.35)
glass=paint("Laminated smoke glass",(.13,.23,.245),.32,.13,.32)
lamp=paint("Projector LED",(.78,.94,1),.24,.2,1,1.2)
ruby=paint("Tail light guides",(.72,.004,.012),.2,.22,1,.65)
cloth=paint("Aramid upholstery",(.037,.041,.046),0,.92)
blue=paint("Caliper enamel",(.025,.11,.37),.47,.29)
groups={}
def group(name,center=(0,0,0)):
    o=bpy.data.objects.new(name,None)
    scene.collection.objects.link(o)
    o.location=center
    o.empty_display_size=.06
    groups[name]=o
    return o
def mesh(name,v,f,mat,g,smooth=False,closed=False):
    m=bpy.data.meshes.new(name); m.from_pydata(v,[],f);m.update()
    if closed:
        bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(m);bm.free()
    o=bpy.data.objects.new(name,m);scene.collection.objects.link(o);o.parent=g;m.materials.append(mat)
    if smooth:
        for p in m.polygons:p.use_smooth=True
    return o
def strip(name,rows,mat,g,wrap=False,smooth=True,reverse=False,caps=False):
    n=len(rows[0]);f=[]
    for i in range(len(rows)-1):
        for j in range(n if wrap else n-1):
            k=(j+1)%n;ids=(i*n+j,i*n+k,(i+1)*n+k,(i+1)*n+j);f.append(ids[::-1] if reverse else ids)
    if caps:f.extend([tuple(reversed(range(n))),tuple((len(rows)-1)*n+j for j in range(n))])
    return mesh(name,[tuple(v) for row in rows for v in row],f,mat,g,smooth,caps)
def tube(name,pts,r,mat,g,n=10,cap=True):
    pts=[Vector(p) for p in pts]; rows=[]; last=None
    for i,p in enumerate(pts):
        d=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
        if last is None:
            ref=Vector((0,0,1)) if abs(d.z)<.88 else Vector((0,1,0))
            u=d.cross(ref).normalized()
        else:u=(last-d*last.dot(d)).normalized()
        v=d.cross(u);last=u
        rows.append([p+r*(cos(2*pi*j/n)*u+sin(2*pi*j/n)*v) for j in range(n)])
    return strip(name,rows,mat,g,True,True,False,cap)
def box(name,c,span,mat,g,b=.012,rotation=None):
    dx,dy,dz=[v/2 for v in span]
    vs=[(x,y,z) for z in (-dz,dz) for y in (-dy,dy) for x in (-dx,dx)]
    fs=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
    o=mesh(name,vs,fs,mat,g);o.location=c
    if rotation:o.rotation_euler=rotation
    if b:
        mod=o.modifiers.new("Formed edge","BEVEL");mod.width=min(b,dx*.9,dy*.9,dz*.9);mod.segments=3
        mod=o.modifiers.new("Planar highlights","WEIGHTED_NORMAL")
    return o
def lathe(name,profile,axis,mat,g,n=56,center=(0,0,0)):
    rows=[]
    for h,r in profile:
        row=[]
        for i in range(n):
            a=2*pi*i/n;c=r*cos(a);s=r*sin(a)
            p=(h,c,s) if axis=='X' else ((c,h,s) if axis=='Y' else (c,s,h))
            row.append(tuple(p[j]+center[j] for j in range(3)))
        rows.append(row)
    hollow=profile[0]==profile[-1]
    return strip(name,rows,mat,g,True,True,False,not hollow)
def ribbon(name,out,back,mat,g):
    return strip(name,[out,back],mat,g)
def outline(name,pts,r,mat,g,n=10):
    return tube(name,pts+[pts[0]],r,mat,g,n)
def interp(t,keys):
    if t<=keys[0][0]:return keys[0][1]
    for i,((a,p),(b,q)) in enumerate(zip(keys,keys[1:])):
        if t<=b:
            x=(t-a)/(b-a)
            m=(q-keys[max(i-1,0)][1])/(b-keys[max(i-1,0)][0])
            nn=keys[min(i+2,len(keys)-1)]
            n=(nn[1]-p)/(nn[0]-a)
            return (2*x**3-3*x*x+1)*p+(x**3-2*x*x+x)*(b-a)*m+(-2*x**3+3*x*x)*q+(x**3-x*x)*(b-a)*n
    return keys[-1][1]
def seq(a,b,n):return [a+(b-a)*k/(n-1) for k in range(n)]
def text_mesh(name,letters,pos,angle,size,mat,g):
    data=bpy.data.curves.new(name,'FONT');data.body=letters;data.align_x='CENTER';data.align_y='CENTER';data.size=size;data.shear=.14
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.parent=g;obj.location=pos;obj.rotation_euler=angle;data.materials.append(mat)
    return obj
