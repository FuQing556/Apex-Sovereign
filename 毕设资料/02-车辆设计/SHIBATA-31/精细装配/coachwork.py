"""Panel stations, in the vehicle coordinate system (+Y nose)."""
skin=group("01_Pressed_panels"); hood=group("02_Bonnet_lid"); deck=group("03_Deck_lid")
trim=group("04_Seals_and_hardware"); windows=group("05_Glazing")
lamps=group("06_Lamps_and_grilles"); aero=group("07_Aero"); graphics=group("08_Livery")
width=[(-2.1325,.76),(-2.02,.87),(-1.65,.94),(-1.28,.966),(-.74,.888),(-.25,.867),(.66,.875),(1.28,.977),(1.70,.938),(1.97,.875),(2.1325,.79)]
height=[(-2.1325,.68),(-1.91,.758),(-1.35,.80),(-.75,.813),(.48,.825),(.78,.817),(1.26,.771),(1.70,.682),(1.99,.59),(2.1325,.54)]
def w(y):return interp(y,width)
def h(y):return interp(y,height)
def cut(y):
    base=.19
    for axle,rad,opening in [(1.2875,.3215,.385),(-1.2875,.354,.417)]:
        rad+=.084
        d=abs(y-axle)
        if d<=opening:base=max(base,rad+sqrt(max(0,opening**2-d*d)))
        elif d<opening+.025:base=max(base,rad-(rad-.19)*(d-opening)/.025)
    return base
def shoulder(y):return max(h(y),cut(y)+.057)
def sideX(y,z):
    n=max(0,min(1,(z-cut(y))/max(.03,shoulder(y)-cut(y))))
    return w(y)-.022*(1-n)-.017*sin(pi*n)**2+.007*sin(2*pi*n)
def crown(y,u):
    ramp=max(0,min(1,(u-.45)/(.26 if y<0 else .46)))
    blend=ramp*ramp*(3-2*ramp)
    return h(y)+(shoulder(y)-h(y))*blend+.025*(1-u*u)-.032*u**5
def top(y,u):return (w(y)*u,y,crown(y,abs(u)))
for sign in [-1,1]:
    sg="L" if sign<0 else "R"
    for label,a,b in [("Rear valance side",-2.13,-1.795),("Rear quarter",-1.792,-.665),("Door",-.660,.642),("Front wing",.647,1.838),("Nose return",1.841,2.13)]:
        rows=[]
        for y in seq(a,b,max(13,int((b-a)*130))):
            low=cut(y); high=shoulder(y)
            rows.append([(sign*sideX(y,z),y,z) for z in seq(low,high-.031,9)]+[top(y,sign*(1-t*.25)) for t in seq(0,1,8)])
        strip(label+" "+sg,rows,carbon if b<0 else red,skin,reverse=sign<0)
    for ax,rr,z in [(1.2875,.385,.3215),(-1.2875,.417,.354)]:
        z+=.084
        rows=[];liner=[]
        pts=[(ax+rr,.19)]+[(ax+rr*cos(t),z+rr*sin(t)) for t in seq(0,pi,81)]+[(ax-rr,.19)]
        for y,v in pts:
            rows.append([(sign*(w(y)+off),y,v+up) for off,up in [(-.028,.005),(.005,0),(.013,.009),(.006,.023),(-.004,.035)]])
            liner.append([(sign*(w(y)-k),y,v+.004) for k in [0,.10,.18]])
        strip("Rolled arch "+sg,rows,red,skin,reverse=sign<0)
        strip("Inner wheel tub "+sg,liner,black,trim,reverse=sign>0)
    rows=[]
    for y in seq(-.83,.86,22):
        wing=.025+.028*(abs(y)/.86)**2
        rows.append([(sign*x,y,z) for x,z in [(w(y)-.04,.22),(w(y)+.008,.205),(w(y)+wing,.145),(w(y)+wing,.125),(w(y)-.035,.137)]])
    strip("Rocker blade "+sg,rows,carbon,aero,True,True,False,True)
    # Door graphics follow the local stamped surface in depth.
    for title,y1,y2,z1,z2,band,mat in [("Swage",-1.82,.78,.72,.34,.061,cherry),("Pinstripe",-1.72,.76,.726,.364,.010,white),("Sill flash",-.71,.79,.43,.23,.042,carbon)]:
        rows=[]
        for u in seq(0,1,68):
            y=y1+(y2-y1)*u;v=z1+(z2-z1)*u;lo=max(v,cut(y)+.009);hi=max(lo+.0001,v+band)
            rows.append([(sign*(sideX(y,z)+.004),y,z) for z in (lo,hi)])
        strip(title+" "+sg,rows,mat,graphics,reverse=sign<0)
    # Number panel lies on the comparatively flat centre of each door.
    yy=.04;xx=max(sideX(k,.568) for k in seq(-.22,.3,12))+.008
    box("Team door field "+sg,(sign*xx,yy,.589),(.004,.74,.154),cherry,graphics,.002)
    text_mesh("Team logotype "+sg,"SHIBATIRE",(sign*(xx+.006),yy,.60),(pi/2,0,sign*pi/2),.134,white,graphics)
    text_mesh("Team lettering "+sg,"RACING TEAM",(sign*(xx+.006),yy,.540),(pi/2,0,sign*pi/2),.040,white,graphics)
    text_mesh("Quarter glass number "+sg,"31",(sign*.765,-.76,.904),(pi/2,0,sign*pi/2),.117,white,graphics)
    for title,y,z in [("Latch",-.48,.731),("Key barrel",-.58,.737)]:
        box(title+" recess "+sg,(sign*sideX(y,z),y,z),(.013,.114 if title=="Latch" else .017,.024),black,trim,.009)
    box("Pull handle "+sg,(sign*(sideX(-.48,.735)+.008),-.48,.736),(.021,.092,.014),red,trim)
    tube("Mirror stem "+sg,[(sign*.816,.555,.837),(sign*.89,.54,.874),(sign*.976,.515,.903)],.015,carbon,trim)
    rows=[[(sign*(x+rx*cos(t)),y,z+rz*sin(t)) for t in seq(0,2*pi*(1-1/32),32)] for y,x,z,rx,rz in [(.604,.976,.927,.019,.013),(.563,.99,.936,.088,.046),(.491,.995,.934,.092,.042),(.472,.994,.936,.079,.034)]]
    strip("Mirror fairing "+sg,rows,red,trim,True,True,False,True)
    lathe("Mirror silver "+sg,[(-.004,.034),(0,.035)],"Y",metal,trim,32,(sign*.995,.467,.937)).scale.x=1
for name,a,b,g in [("Bonnet",.802,2.129,hood),("Trunk",-2.13,-1.344,deck),("Cowl",.69,.798,skin),("Parcel shelf",-1.34,-1.23,skin)]:
    strip(name,[[top(y,u) for u in seq(-.748,.748,35)] for y in seq(a,b,34)],carbon if name in ("Trunk","Cowl") else red,g,reverse=True)
# Subtle raised bonnet power bulge, vent housings and separate louvers.
for s in [-1,1]:
    rows=[]
    for y in seq(1.02,1.79,28):
        k=(y-1.02)/.77;u=.40-.07*k;radius=.075*max(.04,sin(pi*k)**.52)
        rows.append([(s*w(y)*(u+radius*t),y,crown(y,u+radius*t)+.004+.017*sin(pi*k)*(1-t*t)) for t in seq(-1,1,9)])
    strip("Bonnet extraction recess",rows,carbon,hood,reverse=s>0)
    for y in seq(1.13,1.66,7):
        u=.40-.07*(y-1.02)/.77
        tube("Extraction louver",[(s*w(y)*(u+t),y+.015*(1-(t/.056)**2),crown(y,u+t)+.018) for t in seq(-.056,.056,8)],.005,dark,hood,6)
    for y in [1.90]:
        x=s*.476;z=crown(y,abs(x)/w(y))
        lathe("Bonnet flush catch",[(0,.020),(.005,.021)],'Z',dark,hood,24,(x,y,z))
cabY=[-1.36,-1.19,-.84,-.61,-.34,-.01,.20,.36,.54,.766]
cabZ=[.910,.984,1.133,1.226,1.271,1.278,1.249,1.182,1.062,.847]
cabW=[.733,.713,.655,.622,.612,.607,.617,.647,.70,.774]
def belt(y):return max(.803+.012*(y+.5),cut(y)+.046)
def zcab(y):return max(interp(y,list(zip(cabY,cabZ))),belt(y)+.036)
def wcab(y):return interp(y,list(zip(cabY,cabW)))
def roof(y,u):return (wcab(y)*u,y+.037*(1-u*u),zcab(y)+.042*(1-u*u))
for name,a,b,mat,g in [("Backlight",-1.34,-.621,glass,windows),("Roof panel",-.62,.224,carbon,skin),("Windscreen",.226,.762,glass,windows)]:
    strip(name,[[roof(y,u) for u in seq(-1,1,33)] for y in seq(a,b,21)],mat,g,reverse=True)
    for y in (a,b):tube("Bonding strip", [roof(y,u) for u in seq(-1,1,31)],.009,black,trim)
for s in [-1,1]:
    for name,a,b,mat,g in [("Quarter light",-1.333,-.553,glass,windows),("B pillar",-.551,-.478,carbon,skin),("Door glass",-.475,.758,glass,windows)]:
        rows=[]
        for y in seq(a,b,37):
            high=zcab(y);low=belt(y)
            rows.append([(s*((1-t)*(.798-.015*(y+.1)**2)+t*wcab(y)+.014*sin(pi*t)),y,low+(high-low)*t) for t in seq(0,1,9)])
        strip(name,rows,mat,g,reverse=s<0)
        tube("Upper window surround",[(s*wcab(y),y,zcab(y)) for y in seq(a,b,30)],.020,red,trim)
    tube("Waist weatherstrip",[(s*(.798-.015*(y+.1)**2),y,belt(y)) for y in seq(-1.34,.765,35)],.013,black,trim)
    # C pillar blends the shoulder into the backlight.
    strip("Flying C pillar", [[roof(y,s*(1-u*.10)) for u in seq(0,1,5)] for y in seq(-1.34,-.61,25)],red,skin,reverse=s<0)
    tube("Wiper blade",[(s*.09,.671,.936),(s*.37,.652,.947),(s*.64,.604,.959)],.009,black,trim)
    tube("Wiper linkage",[(s*.49,.758,.856),(s*.37,.652,.947)],.007,dark,trim)
# Fascia with a real intake opening. Splitter and grilles follow the bowed bumper.
def front(u):return 2.128-.202*abs(u)**2.7
def rear(u):return -2.129+.160*abs(u)**2.4
for name,fn,high,low,mat,g in [
    ("Nose brow",front,lambda u:crown(front(u),abs(u))+.002,lambda u:.444+.102*abs(u)**1.6,red,skin),
    ("Intake lower return",front,lambda u:.213+.018*u*u,lambda u:.157+.012*u*u,red,skin),
    ("Rear upper panel",rear,lambda u:crown(rear(u),abs(u)),lambda u:.53+.025*u*u,red,skin),
    ("Rear crash cover",rear,lambda u:.548+.018*u*u,lambda u:.274+.072*u*u,red,skin)]:
    strip(name,[[(u*.886,fn(u)+(.012*sin(pi*v) if fn==front else -.024*sin(pi*v)),low(u)+(high(u)-low(u))*v) for v in seq(0,1,6)] for u in seq(-1,1,73)],mat,g,reverse=fn==rear)
for s in [-1,1]:
    for u in [.72,.95]:
        rows=[]
        for v in seq(0,1,7):
            k=u-.06*(1-v)
            rows.append([(s*(k+d)*.886,front(k+d),.191+v*(.27+.05*k)) for d in [-.038,.038]])
        strip("Fascia bridge",rows,red,skin,reverse=s>0)
    def headlight(u,t,lift=0):
        x=.464+.364*u
        y=2.113-.268*u**1.6-(.010+.137*sin(pi*u)**.72)*(1-t)
        weight=max(0,min(1,(y-1.76)/.37))**2
        return (s*x,y+weight*(front(x/.886)-2.13),crown(y,x/w(y))+.012+lift)
    strip("Black projector housing",[[headlight(u,t) for t in seq(0,1,8)] for u in seq(0,1,29)],carbon,lamps,reverse=s<0)
    tube("DRL eyebrow",[headlight(u,.87,.003) for u in seq(.04,.98,28)],.0065,lamp,lamps,12)
    tube("Lens perimeter",[headlight(u,.05,.001) for u in seq(.03,.98,24)],.004,dark,lamps)
    for u in [.49,.70]:
        x,y,z=headlight(u,.44,.006)
        lathe("Projector shroud",[(-.009,.028),(.004,.029),(.012,.020)],'Y',dark,lamps,32,(x,y,z))
        lathe("Projector optic",[(0,.016),(.006,.013),(.009,.007)],'Y',lamp,lamps,28,(x,y+.012,z))
    tube("Tail smoked bezel",[(s*x,rear(x/.886)-.01,z) for x,z in [(.02,.601),(.52,.62),(.71,.665),(.835,.651)]],.029,carbon,lamps)
    tube("Tail LED light pipe",[(s*x,rear(x/.886)-.034,z) for x,z in [(.02,.601),(.52,.62),(.71,.665),(.835,.651),(.746,.611)]],.011,ruby,lamps)
# Patterned grille batched on export. An opaque radiator sits behind the open cells.
for row in range(5):
    for col in range(-16,17):
        x=(col+.5*(row%2))*.039
        if abs(x)>.598:continue
        y=front(x/.886)-.019
        outline("Intake hex",[(x+.022*cos(a),y,.241+row*.035+.022*sin(a)) for a in seq(pi/6,pi/6+2*pi*5/6,6)],.0026,dark,lamps,5)
for fn,factor,z in [(front,1,.14),(rear,-1,.235)]:
    rows=[]
    for u in seq(-1,1,45):
        x=u*.931;y=fn(u)
        rows.append([(x,y+factor*d,z+rise) for d,rise in [(-.12,.012),(.047,.012),(.047,-.006),(-.12,-.006)]])
    strip("Splitter" if factor>0 else "Diffuser lip",rows,carbon,aero,True,True,False,True)
for x in seq(-.71,.71,7):
    mesh("Diffuser strake",[(x,y,z) for y,z in [(-1.75,.15),(-1.78,.18),(-2.135,.252),(-2.14,.162)]],[(0,1,2,3)],carbon,aero)
for s in [-1,1]:
    for offset in [-.016,.016]:
        mesh("Wing pylon",[(s*.55+offset,y,z) for y,z in [(-1.77,.77),(-1.87,.78),(-2.02,.961),(-1.94,.97)]],[(0,1,2,3)],dark,aero)
    box("Wing foot",(s*.55,-1.82,.765),(.078,.162,.021),carbon,aero)
    box("Wing end fence",(s*.975,-1.987,.961),(.012,.257,.099),carbon,aero)
rows=[]
for x in seq(-.974,.974,25):
    rows.append([(x,-1.88-.22*t,.976+.014*cos(pi*t)+.021*sin(pi*t)+.014*t+.009*x*x) for t in seq(0,1,13)]+[(x,-1.88-.22*t,.968+.014*cos(pi*t)+.014*t+.009*x*x) for t in seq(.98,.02,11)])
strip("Rear wing airfoil",rows,carbon,aero,True,True,False,True)
