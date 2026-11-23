"""Bake metre-scale scenery around the actual sampled Ridge centreline. Unity (x,y,z) -> Blender (x,z,y)."""
from pathlib import Path
from math import sin,cos,pi,sqrt,atan2
import re, math, random, collections, json, bpy
import numpy as np
from mathutils import Vector
root=Path(__file__).resolve().parents[3]
out=root/"Assets/_Project/Tracks/RidgeArt";out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;rng=random.Random(31)
src=(root/"Assets/_Project/Tracks/Ridge.asset").read_text(encoding="utf8")
knots=np.array([[float(k) for k in p] for p in re.findall(r'- \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}',src)])
n=len(knots);path=[]
for i,b in enumerate(knots):
    a=knots[i-1];c=knots[(i+1)%n];d=knots[(i+2)%n]
    for t in np.arange(26)/26:
        path.append(.5*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t**3))
path=np.array(path);xx=path[:,0];yy=path[:,2]
colors={"Meadow":(.155,.205,.098),"Heather":(.21,.229,.126),"Shale":(.205,.211,.18),"Distant ridge":(.12,.18,.188),"Fir shade":(.036,.083,.055),"Spruce":(.065,.132,.078),"Fir tips":(.089,.159,.087),"Autumn leaf":(.261,.243,.11),"Bark":(.131,.087,.052),"Creek water":(.084,.257,.261),"Ripple":(.39,.57,.53),"Galvanized":(.388,.415,.409),"Pit charcoal":(.079,.092,.098),"Pit ivory":(.712,.685,.57),"Safety orange":(.83,.186,.039),"Timing glass":(.10,.20,.23),"Sign ink":(.028,.044,.047),"Asphalt apron":(.12,.13,.134)}
mats={}
colors.update({"Cedar boughs":(.84,.89,.83),"Birch canopy":(.90,.89,.83)})
(Path(__file__).parent/"surfaces.json").write_text(json.dumps(dict(surfaces=[dict(name=n,r=c[0],g=c[1],b=c[2],a=1,metal=.55 if n=="Galvanized" else .08,gloss=.75 if n in ("Creek water","Timing glass") else .24,emission=0) for n,c in colors.items()]),indent=2),encoding="utf8")
for name,col in colors.items():
    m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);mats[name]=m
buckets=collections.defaultdict(lambda:[[],[]])
def add(name,mat,vs,fs):
    key=(name,mat);v,f=buckets[key];off=len(v);v.extend(tuple(p) for p in vs);f.extend(tuple(off+k for k in face) for face in fs)
def quad(n,m,a,b,c,d):add(n,m,[a,b,c,d],[(0,1,2,3)])
def block(n,m,pos,size):
    x,y,z=pos;dx,dy,dz=[v*.5 for v in size]
    vs=[(x+i*dx,y+j*dy,z+k*dz) for k in (-1,1) for j in (-1,1) for i in (-1,1)]
    add(n,m,vs,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)])
def pole(n,m,a,b,r,sides=7):
    a,b=Vector(a),Vector(b);d=(b-a).normalized();u=d.cross(Vector((0,1,0)) if abs(d.y)<.85 else Vector((0,0,1))).normalized();v=d.cross(u)
    pts=[p+r*(u*cos(i*2*pi/sides)+v*sin(i*2*pi/sides)) for p in [a,b] for i in range(sides)]
    add(n,m,pts,[(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]+[tuple(range(sides,2*sides))])
def river(y):return 332+15*sin(y/47)+6*sin(y/17)
def distance(x,y):
    i=np.argmin((xx-x)**2+(yy-y)**2);return math.hypot(xx[i]-x,yy[i]-y)
def smooth(lo,hi,t):
    v=max(0,min(1,(t-lo)/(hi-lo)));return v*v*(3-2*v)
def ground(x,y):
    r=distance(x,y);outer=math.hypot((x-85)*.96,(y+30)*.99)
    hills=(5+3*sin(x*.036+y*.011)+2*cos(y*.048)+3*sin(x*.017-y*.04))
    peaks=max(0,outer-270)*(.33+.13*sin(x*.008+y*.01)+.10*cos(y*.013)+.075*sin(x*.021-y*.012))
    h=.1+smooth(20,65,r)*(hills+peaks)
    d=abs(x-river(y))
    if -350<y<335:h=1.10+(h-1.10)*smooth(2.7,16,d)
    if -72<x<-14 and -44<y<75:h=.72+(h-.72)*smooth(0,10,max(-61-x,x+21,-32-y,y-58))
    return h
# Sections culled independently. Keep landscaping below the existing road and its runoff.
for ix in range(-8,9):
    for iy in range(-8,9):
        tile=f"Relief {ix} {iy}";x0=ix*76;y0=iy*76
        grid=[]
        for i in range(9):
            for j in range(9):
                x=x0+i*9.5;y=y0+j*9.5;grid.append((x,y,ground(x,y)))
        for i in range(8):
            for j in range(8):
                a=i*9+j;p=grid[a]
                mat="Meadow"
                quad(tile,mat,p,grid[a+9],grid[a+10],grid[a+1])
def tree(x,y,h):
    z=ground(x,y)-.20;sector=f"Forest {math.floor(x/90)} {math.floor(y/90)}"
    broad=rng.random()<.24;width=h*(.34 if broad else .238)*rng.uniform(.81,1.25)
    phase=rng.random()*pi
    for k in range(3):
        ang=phase+pi*k/3;dx=cos(ang)*width;dy=sin(ang)*width
        quad(sector,"Birch canopy" if broad else "Cedar boughs",(x-dx,y-dy,z),(x+dx,y+dy,z),(x+dx,y+dy,z+h),(x-dx,y-dy,z+h))
for _ in range(5800):
    x=rng.uniform(-440,555);y=rng.uniform(-475,460);d=distance(x,y)
    if d<28 or abs(x-river(y))<7 or (-78<x<19 and -56<y<100):continue
    if sin(x*.028)*cos(y*.021)<-.34 and d<140:continue
    tree(x,y,rng.uniform(5.2,17))
# Mountain brook, banks and broken highlights. Set water above the original runoff plane.
for j in range(-64,64):
    y=j*5;b=y+5
    quad("Stream","Creek water",(river(y)-2.7,y,1.29),(river(y)+2.7,y,1.29),(river(b)+2.7,b,1.29),(river(b)-2.7,b,1.29))
    if j%3==0:
        x=river(y)+rng.uniform(-1,1)
        quad("Stream","Ripple",(x-.6,y,1.303),(x+.5,y+.16,1.303),(x+.72,y+.26,1.303),(x-.42,y+.1,1.303))
# Timing tower, pit terrace and service bays to the left of the starting straight.
block("Paddock","Asphalt apron",(-40,13,.74),(45,88,.13))
for y in [-16,-4,8,20,32]:
    block("Service bays","Pit ivory",(-45,y,3.02),(11,11.5,4.45))
    block("Service bays","Pit charcoal",(-39.43,y,2.56),(.15,8.2,3.2))
    for k in range(9):block("Service bays","Galvanized",(-39.31,y,1.17+k*.33),(.052,7.8,.031))
    block("Service bays","Safety orange",(-39.23,y,4.54),(.13,10.7,.19))
block("Service bays","Pit charcoal",(-44,8,5.44),(14,61,.34))
block("Control tower","Pit ivory",(-25.6,47,4.46),(9.5,13.4,7.4))
block("Control tower","Timing glass",(-25.6,47,7.55),(10.7,14.2,2.6))
block("Control tower","Pit charcoal",(-25.6,47,9.05),(12,15.7,.44))
for y in range(41,54,2):block("Control tower","Galvanized",(-20.18,y,7.55),(.15,.13,2.65))
for z in [6.27,8.82]:block("Control tower","Pit ivory",(-25.6,47,z),(10.9,14.5,.15))
for j in range(12):block("Terrace stair","Galvanized",(-31.9,38+j*.31,.86+j*.2),(1.8,.34,.18))
for i in range(3):
    block("Viewing terrace","Pit ivory",(-47-i*1.3,60,1.16+i*.46),(1.33,17,.5+i*.45))
# Lattice gantry set clear of existing barriers, with 6.5 m clearance over the road.
for x in [-14,14]:
    block("Start bridge","Pit ivory",(x,3,1.96),(1.4,1.8,2.5))
    for y in [2.52,3.48]:pole("Start bridge","Galvanized",(x,y,2.65),(x,y,10.0),.122)
    for z in range(3,10):pole("Start bridge","Galvanized",(x,2.52,z),(x,3.48,z+.9),.07)
for y in [2.52,3.48]:
    for z in [8.9,10.08]:pole("Start bridge","Galvanized",(-14,y,z),(14,y,z),.125)
    for j in range(-14,14,2):
        pole("Start bridge","Galvanized",(j,y,8.92),(j+1,y,10.08),.058)
        pole("Start bridge","Galvanized",(j+1,y,10.08),(j+2,y,8.92),.058)
block("Start bridge","Pit charcoal",(0,2.40,9.52),(13.7,.16,1.22))
for side in [-1,1]:
    for i in range(4):
        for j in range(3):
            if (i+j)%2==0:block("Start bridge","Pit ivory",(side*(4.74+i*.34),2.302,9.20+.34*j),(.34,.012,.34))
# Trackside marshal shelters and reflective chevrons placed from the actual local tangent.
for i in [54,110,157,218,274,321,379]:
    pt=path[i];delta=path[(i+1)%len(path)]-path[i-1];side=np.array([delta[2],-delta[0]]);side/=np.linalg.norm(side)
    x,y=np.array([pt[0],pt[2]])+side*20;z=ground(x,y)
    if distance(x,y)<16:continue
    block("Marshal posts","Pit ivory",(x,y,z+.95),(3.0,2.2,1.9))
    for dx in [-1.34,1.34]:
        for dy in [-.9,.9]:pole("Marshal posts","Galvanized",(x+dx,y+dy,z+1.8),(x+dx,y+dy,z+3.25),.059)
    block("Marshal posts","Safety orange",(x,y,z+3.27),(3.45,2.75,.15))
# Spatial partition forests and terrain; each tile is one mesh with material slots.
for i in [38,87,140,194,239,295,350,418]:
    p=path[i];v=path[(i+1)%len(path)]-path[i-1];v=v/np.linalg.norm(v)
    q=np.array([v[2],-v[0]]); ahead=path[(i+12)%len(path)]-p
    turn=1 if np.dot([ahead[0],ahead[2]],q)>=0 else -1
    x,y=np.array([p[0],p[2]])-turn*q*13.8;q=q*turn;z=p[1]
    def coord(offset,height,forward=0):return (x+offset*q[0]+forward*v[0],y+offset*q[1]+forward*v[2],z+height)
    for k in [-1,1]:pole("Corner boards","Galvanized",coord(k,-.65),coord(k,1.85),.045)
    quad("Corner boards","Sign ink",coord(-1.5,1.14),coord(1.5,1.14),coord(1.5,1.99),coord(-1.5,1.99))
    for k in [-.87,0,.87]:
        quad("Corner boards","Safety orange",coord(k-.29,1.22,-.015),coord(k-.05,1.22,-.015),coord(k+.29,1.57,-.015),coord(k+.04,1.57,-.015))
        quad("Corner boards","Safety orange",coord(k+.04,1.57,-.015),coord(k+.29,1.57,-.015),coord(k-.05,1.92,-.015),coord(k-.29,1.92,-.015))
for j in range(12):
    y=-24+j*6.4
    block("Pit lane paint","Pit ivory",(-34,y,.816),(6.4,.10,.015))
for x,y in [(-38,-17),(-38,8),(-38,25)]:
    for z in [.97,1.24,1.51]:
        pole("Service tyre stacks","Pit charcoal",(x,y,z-.11),(x,y,z+.11),.37,20)
assembly=collections.defaultdict(list)
for (sector,mat),geom in buckets.items():assembly[sector].append((mat,geom))
for name,items in assembly.items():
    vs=[];fs=[];ids=[];slots=[]
    for mat,(verts,faces) in items:
        off=len(vs);vs.extend(verts);fs.extend(tuple(off+i for i in f) for f in faces);ids.extend([len(slots)]*len(faces));slots.append(mat)
    if name.startswith("Relief"):
        unique={v:i for i,v in enumerate(dict.fromkeys(vs))}
        fs=[tuple(unique[vs[i]] for i in face) for face in fs];vs=list(unique)
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
    obj=bpy.data.objects.new(name,me);scene.collection.objects.link(obj)
    for mat in slots:me.materials.append(mats[mat])
    me.polygons.foreach_set("material_index",ids)
    uv=me.uv_layers.new()
    if name.startswith(("Relief","Forest")):
        for face in me.polygons:
            for j,li in enumerate(face.loop_indices):
                co=me.vertices[me.loops[li].vertex_index].co
                uv.data[li].uv=([(0,0),(1,0),(1,1),(0,1)][j] if name.startswith("Forest") else (co.x*.19,co.y*.19))
    if name.startswith("Relief"):
        for f in me.polygons:f.use_smooth=True
# Readable start lettering oriented towards approaching drivers.
font=bpy.data.curves.new("Finish board",'FONT');font.body="RIDGELINE   /   GP";font.align_x='CENTER';font.align_y='CENTER';font.size=.77
obj=bpy.data.objects.new("Gantry legend",font);scene.collection.objects.link(obj);obj.location=(0,2.296,9.51);obj.rotation_euler=(pi/2,0,0);font.materials.append(mats["Pit ivory"])
bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH')
bpy.ops.export_scene.fbx(filepath=str(out/"Ridgeline valley.fbx"),axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,object_types={'MESH'})
print("Track scenery:",len(assembly),"sectors",flush=True)
