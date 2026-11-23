# Tie the curved end faces into the outer wing stations. Align edges at every height.
for o in list(skin.children)+list(hood.children)+list(deck.children):
    if not o.name.startswith(("Front wing","Nose return","Rear valance side","Bonnet","Trunk","Rear quarter")):continue
    if o.type!='MESH' or o.location.length>.001:continue
    for p in o.data.vertices:
        x,y,z=p.co;dist=abs(y)
        if dist<1.76:continue
        fn=front if y>0 else rear
        end=2.13 if y>0 else -2.13
        weight=min(1,(dist-1.76)/.37)**2
        p.co.y+=weight*(fn(x/.886)-end)
for s in [-1,1]:
    for fn,end,mat in [(front,2.13,red),(rear,-2.13,carbon)]:
        rows=[]
        for z in seq(.19,shoulder(end)-.032,24):
            x=sideX(end,z)
            rows.append([(s*(x*(1-t)+.886*t),fn((x*(1-t)+.886*t)/.886),z+.004*sin(t*pi)) for t in seq(0,1,9)])
        strip("Bumper corner wrap",rows,mat,skin,reverse=s<0)
        # Canted outer intake cheeks and dark inner returns surround the radiator.
        rows=[]
        for v in seq(0,1,16):
            z=.168+(.49+.037-v*.0-.168)*v if fn==front else .252+.306*v
            u0=(.669+.063*v) if fn==front else .80
            rows.append([(s*.886*u,fn(u)+(.007 if fn==front else -.007)*sin(pi*v),z+.018*(u-u0)) for u in seq(u0,1,16)])
        strip("Formed bumper cheek",rows,mat,skin,reverse=(s<0)==(fn==front))
        cap=[]
        for u in seq(0,1,41):
            x=u*w(end);cap.append([(s*x,fn(x/.886),crown(end,u)),(s*x,fn(x/.886),crown(fn(x/.886),x/.886))])
        strip("Rolled bonnet shutline",cap,mat,skin)
    for k in range(3):
        pts=[(s*(.622+.022*k+.072*t),front(.75)+.013,.267+.039*k+.008*t) for t in seq(0,1,5)]
        tube("Corner louver",pts,.006,carbon,trim)
    for k in range(2):
        tube("Splitter stay",[(s*(.53+k*.19),front(.7)+.018,.151),(s*(.55+k*.19),front(.7)-.014,.258)],.006,dark,aero)
    for k,(yy,zz) in enumerate([(-.54,.67),(-.60,.64),(.50,.665)]):
        rows=[]
        for y in seq(yy-.045,yy+.045,7):
            rows.append([(s*(sideX(y,z)+.010),y,z) for z in [zz-.008,zz+.008]])
        strip("Door scrutineering strip",rows,white,graphics,reverse=s<0)
    tube("Rear tow eye neck",[(s*.52,-2.024,.323),(s*.52,-2.102,.313)],.008,red,trim)
    # Box the suspension towers and create a continuous bay floor for the service view.
    strip("Front shock tower",[[(s*(cx+.064*cos(t)),1.251+.078*sin(t),z) for t in seq(0,2*pi*31/32,32)] for cx,z in [(.407,.326),(.409,.49),(.445,.595),(.517,.691)]],dark,frame,True,True,False,True)
    lathe("Top mount",[(0,.067),(.009,.067),(.016,.047)],'Z',metal,frame,32,(s*.517,1.251,.691))
    for a in seq(0,2*pi*2/3,3):
        lathe("Top mount nut",[(0,.009),(.012,.009)],'Z',gold,frame,6,(s*.517+.051*cos(a),1.251+.051*sin(a),.703))
    inner_rows=[]
    for y in seq(.70,1.78,32):
        row=[]
        for x,z in [(.461,.265),(.465,.52),(.502,.65),(.57,.718)]:
            x+=.16*min(1,max(0,(abs(y-1.2875)-.25)/.19))
            row.append((s*x,y,min(z,crown(y,x/w(y))-.023)))
        inner_rows.append(row)
    strip("Engine bay inner wing",inner_rows,carbon,frame,reverse=s>0)
tube("Tower brace",[(-.517,1.251,.713),(-.45,1.04,.727),(-.35,.912,.714),(.35,.912,.714),(.45,1.04,.727),(.517,1.251,.713)],.015,metal,frame,16)
box("Front undertray",(0,1.438,.176),(.918,1.15,.02),carbon,frame)
for o in aero.children:
    if o.name.startswith(("Rear wing","Wing end fence")):
        o.location.z+=.153
    if o.name.startswith("Wing pylon"):
        for v in o.data.vertices:
            if v.co.z>.90:v.co.z+=.153
for name in ["Forged spoke","Hub face","Machined rim lip"]:
    for obj in bpy.data.objects:
        if obj.name.startswith(name):obj.data.materials[0]=dark if name!="Machined rim lip" else metal
# A solid frit and upper sun strip keep the bonded panes legible in bright reflections.
strip("Windscreen header", [[(x,y+.001,z+.003) for x,y,z in [roof(yy,u) for u in seq(-1,1,31)]] for yy in seq(.226,.277,6)],cherry,trim,reverse=True)
for s in [-1,1]:
    strip("Quarter light ceramic border", [[(s*(wcab(y)+.002),y,zcab(y)),(s*(wcab(y)+.016),y,zcab(y)-.029)] for y in seq(-1.28,-.53,24)],carbon,trim)
# Serviceable seat runners, lateral pads, lap belts and cockpit switchgear.
for s in [-1,1]:
    box("Seat runner",(driver+s*.149,-.17,.275),(.039,.46,.027),metal,inside,.006)
    box("Seat side bracket",(driver+s*.169,-.19,.321),(.014,.264,.089),dark,inside,.006)
    for y in [-.283,-.077]:
        lathe("Seat mount bolt",[(0,.009),(s*.005,.009)],'X',metal,inside,6,(driver+s*.177,y,.329))
    for label,coords in [("Thigh pad",[(-.28,.185,.373),(-.14,.20,.39),(.018,.18,.408)]),
                         ("Torso bolster",[(-.33,.168,.46),(-.365,.183,.55),(-.39,.168,.65)])]:
        rows=[]
        for y,xx,z in coords:
            rows.append([(driver+s*(xx+.023*cos(t)),y+.022*sin(t),z+.041*sin(t)) for t in seq(0,2*pi*15/16,16)])
        strip(label,rows,cloth,inside,True,True,False,True)
    ribbon("Lap webbing",[(driver+s*x,y-.019,z) for x,y,z in [(.197,-.243,.34),(.14,-.19,.386),(.019,-.15,.383)]],
           [(driver+s*x,y+.019,z) for x,y,z in [(.197,-.243,.34),(.14,-.19,.386),(.019,-.15,.383)]],red,cage)
box("Switch panel",(-.014,.405,.714),(.197,.025,.119),dark,inside,.009)
for k in range(4):
    x=-.078+k*.043
    tube("Toggle",[(x,.39,.726),(x,.373,.741)],.0035,metal,inside,8)
    lathe("Switch collar",[(0,.009),(.003,.009)],'Y',orange if k==0 else black,inside,16,(x,.389,.726))
lathe("Fire bottle",[(-.20,.049),(-.186,.069),(.157,.069),(.192,.047),(.198,.025)],'Y',red,inside,40,(-.354,-.04,.323))
for yy in [-.162,.069]:
    lathe("Fire bottle cradle",[(0,.07),(.018,.07),(.018,.068),(0,.068),(0,.07)],'Y',metal,inside,32,(-.354,yy,.323))
tube("Discharge valve",[(-.354,.158,.323),(-.354,.196,.323),(-.32,.196,.337)],.011,gold,inside)
