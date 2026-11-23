axles=group("09_Suspension_links");frame=group("10_Unibody_and_cradles")
for s in [-1,1]:
    tube("Box rail",[(s*.53,1.92,.31),(s*.424,1.54,.29),(s*.420,1.06,.28),(s*.57,.64,.235),(s*.60,-.71,.235),(s*.51,-1.32,.31),(s*.49,-1.91,.35)],.040,dark,frame,6)
for y in [-1.45,-1.07,.93,1.50]:
    tube("Crossmember", [(-.55,y,.28),(-.32,y+.055,.238),(.32,y+.055,.238),(.55,y,.28)],.034,dark,frame,8)
wheel_origins={}
rig_links=[]
def rig_link(tag,name,pieces,start,end,mode,steer=False):
    anchor=group("Link_"+tag+"_"+name)
    for piece in pieces:piece.parent=anchor
    def unity(p):return dict(x=p[0],y=p[2],z=p[1])
    rig_links.append(dict(name=anchor.name,axle=tag,start=unity(start),end=unity(end),mode=mode,steer=steer))
for tag,sg,front_axle in [("FL",-1,True),("FR",1,True),("RL",-1,False),("RR",1,False)]:
    preceding=set(axles.children)
    cy=1.2875 if front_axle else -1.2875
    cx=sg*(.835 if front_axle else .805)
    R=.3215 if front_axle else .354
    half=.1325 if front_axle else .1425
    bead=(18 if front_axle else 20)*.0254/2
    center=(cx,cy,R);wheel_origins[tag]=center
    wheel=group("Wheel_"+tag,center);carrier=group("Caliper_"+tag,center)
    prof=[(-half+.015,bead),(-half,bead+.021),(-half,R-.039),(-half+.006,R-.018),(-half+.019,R-.005)]
    # Grooves cut into the tread profile. No painted lines or grooved rim mirroring the contact patch.
    for x in seq(-half+.024,half-.024,41):
        d=min(abs(x-g) for g in [-.071,-.023,.023,.071])
        prof.append((x,R-.003*(x/half)**2-max(0,1-d/.0048)*.0052))
    prof+= [(half-.019,R-.005),(half-.006,R-.018),(half,R-.039),(half,bead+.021),(half-.015,bead),(-half+.015,bead)]
    rows=[]
    for x,radius in prof:
        row=[]
        for k in range(112):
            angle=2*pi*k/112
            phase=(k/112*14+abs(x)*7.8+.095*math.sin(x*24))%1
            groove=max(0,1-min(phase,1-phase)/.109)*min(1,max(0,(abs(x)-.033)/.020))
            rad=radius-(.0034*groove if radius>R-.014 else 0)
            row.append((x,rad*cos(angle),rad*sin(angle)))
        rows.append(row)
    tyre=strip("Tyre_"+tag,rows,rubber,wheel,True)
    flange=sg*(half-.012);base=sg*(half-.050)
    def at(x,r,theta):return (x,r*cos(theta),r*sin(theta))
    # The barrel is an annulus, leaving the disc visible between the spokes.
    lathe("Rolled wheel barrel",[(sg*x,r) for x,r in [(-half+.01,bead-.011),(-half+.013,bead+.002),(.02,bead+.001),(half-.009,bead+.007),(half-.005,bead+.002),(half-.015,bead-.010),(.02,bead-.009),(-half+.01,bead-.011)]],'X',dark,wheel,64)
    lathe("Machined rim lip",[(sg*x,r) for x,r in [(half-.014,bead-.005),(half-.002,bead-.005),(half-.003,bead+.005),(half-.014,bead+.003),(half-.014,bead-.005)]],'X',gold,wheel,72)
    for k in range(10):
        a=2*pi*k/10
        rows=[]
        for t in seq(0,1,9):
            r=.050+(bead-.058)*t;angle=a+.13*sin(t*pi*.72)
            hw=(.012+.004*sin(pi*t)+.001*t)/r;xx=base+(flange-base)*t-.013*sin(pi*t)
            rows.append([at(xx+sg*d,r,angle+h*hw) for d,h in [(-.012,-1),(0,-.85),(.003,.44),(-.001,1),(-.014,.9),(-.016,-.85)]])
        strip("Forged spoke",rows,gold,wheel,True,True,False,True)
    lathe("Hub face",[(base-.018*sg,.055),(base,.069),(base+.009*sg,.052),(base+.015*sg,.025)],'X',gold,wheel,48)
    for a in seq(0,2*pi*4/5,5):
        lug=at(base+sg*.005,.043,a)
        lathe("Hex wheel nut",[(0,.010),(.015*sg,.010)],'X',dark,wheel,6,lug)
    tube("Tubeless valve",[at(flange,bead-.015,.51),at(flange+sg*.012,bead-.028,.51)],.004,black,wheel,8)
    disc=(.166 if front_axle else .173);baseDisc=sg*.013
    for offset in [-.012,.012]:
        lathe("Floating disc friction plate",[(baseDisc+offset+v,r) for v,r in [(0,disc-.049),(0,disc),(.005,disc),(.005,disc-.049),(0,disc-.049)]],'X',metal,wheel,72)
    lathe("Rotor bell",[(baseDisc-.014,.044),(baseDisc-.014,disc-.04),(baseDisc+.008,disc-.044),(baseDisc+.014,.050)],'X',dark,wheel,48)
    for a in seq(0,2*pi*23/24,24):
        tube("Vent vane",[at(baseDisc,disc-.045,a),at(baseDisc,disc-.006,a+.094)],.008,dark,wheel,6)
    for a in seq(0,2*pi*9/10,10):
        p=at(baseDisc+sg*.022,disc-.062,a)
        lathe("Disc bobbin",[(0,.006),(sg*.004,.006)],'X',gold,wheel,8,p)
    for pos in ([-1] if front_axle else [-1,1]):
        # Independent rear service and handbrake caliper representations.
        y=pos*disc*.88
        caliper_height=.137 if pos<0 else .105
        for face in [-1,1]:
            box("Caliper piston housing",(baseDisc+face*.032,y,.003),(.026,.043,caliper_height),blue,carrier,.009)
            box("Friction pad backing",(baseDisc+face*.020,y,-.002),(.004,.038,caliper_height*.86),dark,carrier)
        for zz in [-.037,.037]:
            box("Caliper bridge",(baseDisc,pos*(disc+.011),zz),(.083,.018,.027),blue,carrier,.007)
        for z in [-.038,.038]:
            lathe("Caliper fluid crossover boss",[(0,.019),(sg*.009,.017)],'X',blue,carrier,20,(baseDisc+sg*.040,y,z))
        tube("Pad retainer",[(baseDisc+sg*.044,y-.022,-.026),(baseDisc+sg*.047,y+.022,-.026)],.003,metal,carrier)
    box("Upright",(-sg*.057,0,0),(.064,.074,.166),dark,carrier,.019)
    outer=(cx-sg*.065,cy,R-.088)
    for dy in [-.20,.18]:
        mount=(sg*.38,cy+dy,.274)
        arm=tube("Lower suspension link",[mount,outer],.018,metal,axles,12)
        rig_link(tag,"LeadingArm" if dy>0 else "TrailingArm",[arm],mount,outer,"Fixed")
    a=Vector((cx-sg*.089,cy,R+.031));b=Vector((sg*(.517 if front_axle else .580),cy-.036,R+.386))
    tube("Damper lower body",[a,a.lerp(b,.59)],.030,blue,axles)
    tube("Damper piston rod",[a.lerp(b,.50),b],.014,metal,axles)
    n=(b-a).normalized();u=n.cross(Vector((0,1,0))).normalized();v=n.cross(u)
    coil=[a.lerp(b,t)+.045*(cos(t*2*pi*8)*u+sin(t*2*pi*8)*v) for t in seq(.2,.91,113)]
    tube("Helical road spring",coil,.0062,orange,axles,7)
    for t in [.18,.92]:
        tube("Spring seat",[a.lerp(b,t),a.lerp(b,t+.025)],.053,dark,axles,24)
    if front_axle:
        start=(sg*.33,cy-.17,.348);end=(cx-sg*.060,cy-.102,R)
        tie=tube("Steering tie rod",[start,end],.012,metal,axles)
        rig_link(tag,"ToeRod",[tie],start,end,"Fixed",True)
    else:
        for dy in [-.135,.135]:
            start=(sg*.43,cy+dy,.471);end=(cx-sg*.073,cy,R+.075)
            arm=tube("Upper wishbone branch",[start,end],.014,dark,axles)
            rig_link(tag,"UpperFront" if dy>0 else "UpperRear",[arm],start,end,"Fixed")
        shaft=tube("Half shaft",[(sg*.12,cy,.322),(cx-sg*.04,cy,R)],.019,metal,axles)
        for xx in [.17,abs(cx)-.118]:
            for j in range(4):
                lathe("CV bellows",[(0,.024),(.005,.038),(.012,.025)],'X',black,axles,20,(sg*(xx+j*.010),cy,R))
        boots=[o for o in axles.children if o not in preceding and o.name.startswith("CV bellows")]
        rig_link(tag,"Driveshaft",[shaft]+boots,(sg*.12,cy,.322),(cx-sg*.04,cy,R),"Stretch")
    def parts(prefix):return [o for o in axles.children if o not in preceding and o.name.startswith(prefix)]
    rig_link(tag,"Coil",parts(("Helical road spring","Spring seat")),b,a,"Stretch")
    rig_link(tag,"Piston",parts("Damper piston rod"),b,a,"Fixed")
    rig_link(tag,"Cylinder",parts("Damper lower body"),b,a,"Wheel")
for cy in [-1.2875,1.2875]:
    span=.464 if cy>0 else .66
    tube("Anti roll bar",[(-span,cy-.09,.34),(-span+.025,cy-.22,.326),(-.38,cy-.22,.326),(.38,cy-.22,.326),(span-.025,cy-.22,.326),(span,cy-.09,.34)],.010,dark,axles)
