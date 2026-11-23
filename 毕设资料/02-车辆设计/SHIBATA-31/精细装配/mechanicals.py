engine=group("11_VR38_long_block"); hot=group("12_Turbo_and_exhaust")
cold=group("13_Charge_air_and_cooling"); trans=group("14_Gearbox_and_final_drive")
plumbing=group("15_Fuel_brakes_electrical"); inside=group("16_Competition_cockpit"); cage=group("17_Cage_and_belts")
# The floor bridges the sills and clears the gearbox and prop tunnel.
strip("Footwell and transmission tunnel", [[(x,y,z) for x,z in [(-.78,.246),(-.22,.226),(-.143,.408),(.143,.408),(.22,.226),(.78,.246)]] for y in [-.91,.67]],dark,frame,reverse=True)
box("Bulkhead",(0,.676,.496),(1.45,.045,.52),dark,frame)
box("Rear firewall",(0,-.887,.49),(1.46,.045,.48),dark,frame)
# Build the crankcase, two angled cylinder banks, service covers and individual exhaust primaries.
box("Oil pan",(0,1.11,.295),(.322,.50,.095),dark,engine)
box("Crankcase",(0,1.115,.413),(.355,.545,.194),metal,engine,.034)
for s in [-1,1]:
    rot=(0,s*.44,0)
    box("Cylinder head",(s*.142,1.11,.529),(.176,.515,.179),metal,engine,.022,rot)
    box("Cam cover",(s*.188,1.11,.609),(.155,.503,.073),red,engine,.023,rot)
    for y in [.931,1.11,1.285]:
        box("Coil pack",(s*.173,y,.651),(.059,.059,.031),black,engine)
        tube("Coil loom",[(s*.175,y,.658),(s*.23,y,.633),(s*.245,1.34,.602)],.006,black,engine,7)
        tube("Exhaust primary",[(s*.196,y,.506),(s*.26,y,.488),(s*.31,y+.041,.451),(s*.311,1.39,.462),(s*.267,1.465,.49)],.023,heat,hot)
        tube("Intake runner",[(s*.122,y,.573),(s*.087,y,.632),(s*.049,y,.653)],.026,metal,cold)
    for y in seq(.902,1.326,5):
        for x in [.131,.243]:
            lathe("Cover fastener",[(0,.006),(.007,.006)],'Z',metal,engine,6,(s*x,y,.638 if x<.2 else .609))
box("Intake plenum",(0,1.118,.664),(.158,.469,.085),dark,cold,.032)
lathe("Crank pulley",[(0,.075),(.018,.078),(.025,.074)],'Y',dark,engine,40,(0,1.40,.403))
lathe("Alternator pulley",[(0,.035),(.021,.035)],'Y',metal,engine,28,(.208,1.40,.468))
outline("Accessory belt",[(x,1.416,z) for x,z in [(-.062,.369),(-.077,.409),(-.036,.468),(.20,.501),(.237,.475),(.227,.447),(.027,.338)]],.007,black,engine)
tube("Cross bank collector",[(-.267,1.465,.49),(-.20,1.52,.46),(.19,1.52,.46),(.284,1.484,.53)],.036,heat,hot)
tc=(.311,1.46,.572)
lathe("Turbine volute",[(-.091,.046),(-.077,.080),(-.048,.092),(-.013,.067)],'Y',heat,hot,48,tc)
lathe("Compressor cover",[(-.017,.052),(0,.086),(.033,.094),(.061,.082),(.079,.050),(.098,.050),(.100,.040),(.055,.036)],'Y',metal,cold,54,tc)
for a in seq(0,2*pi*10/11,11):
    mesh("Compressor inducer vane",[(tc[0]+r*cos(a+t),tc[1]+y,tc[2]+r*sin(a+t)) for r,t,y in [(.007,0,.082),(.038,.42,.075),(.034,.58,.068),(.007,.18,.077)]],[(0,1,2,3)],dark,cold)
tube("Downpipe",[(.33,1.40,.562),(.38,1.32,.482),(.38,1.08,.334),(.25,.68,.25),(.207,.28,.225),(.216,-.53,.222),(.294,-1.18,.225),(.518,-1.72,.268),(.55,-2.105,.299)],.040,heat,hot,16)
lathe("Exhaust tail rolled rim",[(0,.043),(-.07,.047),(-.07,.038),(-.005,.035)],'Y',metal,hot,36,(.55,-2.07,.299))
for name,y,lo,hi,widthM in [("Intercooler",1.95,.242,.448,.997),("Radiator",1.797,.288,.533,.822)]:
    box(name+" core",(0,y,(lo+hi)/2),(widthM,.056,hi-lo),dark,cold)
    for z in seq(lo+.009,hi-.009,18):
        box(name+" cooling fin",(0,y+.032,z),(widthM-.060,.008,.0024),metal,cold,.0006)
    for s in [-1,1]:box(name+" end tank",(s*widthM*.49,y,(lo+hi)/2),(.058,.073,hi-lo),metal,cold)
for name,points,mat,rad in [
 ("Hot charge pipe",[(.38,1.48,.58),(.50,1.49,.61),(.539,1.73,.49),(.54,1.91,.35),(.48,1.95,.34)],metal,.034),
 ("Cold charge pipe",[(-.48,1.95,.35),(-.525,1.88,.36),(-.49,1.69,.57),(-.406,1.54,.648),(0,1.50,.657),(0,1.30,.66)],metal,.038),
 ("Coolant hose",[(-.37,1.76,.50),(-.36,1.58,.57),(-.25,1.42,.543)],black,.022)]:
    tube(name,points,rad,mat,cold,16)
    for i in range(1,len(points)-1):
        p=Vector(points[i]);q=Vector(points[i+1]);direction=(q-p).normalized()
        tube("Silicone coupling",[p,p+direction*.035],rad+.002,black,cold,16)
        tube("Hose clamp",[p+direction*.007,p+direction*.013],rad+.004,metal,cold,16)
lathe("Clutch bell and gearbox",[(-.52,.060),(-.49,.085),(-.23,.098),(-.19,.125),(.08,.127),(.13,.168),(.205,.17)],'Y',metal,trans,24,(0,.71,.379))
for y in seq(.49,.79,6):
    lathe("Case stiffener",[(0,.125),(.008,.132),(.014,.125)],'Y',dark,trans,20,(0,y,.379))
tube("Propeller shaft",[(0,.205,.371),(0,-1.155,.32)],.033,dark,trans,24)
for y,z in [(.19,.371),(-1.14,.32)]:
    tube("Universal joint", [(-.045,y,z),(.045,y,z)],.020,metal,trans)
box("Rear differential housing",(0,-1.285,.326),(.227,.249,.199),dark,trans,.048)
for x in seq(-.082,.082,8):box("Diff cover rib",(x,-1.414,.322),(.007,.015,.124),metal,trans)
box("Fuel cell",(0,-1.654,.447),(.64,.36,.21),dark,plumbing,.019)
for x in [-.207,.207]:
    box("Tank retention strap",(x,-1.654,.556),(.031,.374,.009),metal,plumbing)
for i in range(3):
    x=-.37+i*.024
    tube("Chassis hard line",[(x,-1.66,.49),(x,-1.40,.29),(x,-.86,.26),(x,.68,.26),(x,.83,.50)],.004,metal,plumbing,7)
for y in [.80,.915]:
    lathe("Fluid reservoir",[(0,.039),(.092,.04),(.103,.025)],'Z',white,plumbing,24,(-.389,y,.48))
    lathe("Reservoir cap",[(0,.03),(.011,.03)],'Z',black,plumbing,20,(-.389,y,.583))
box("ECU enclosure",(-.51,.58,.582),(.17,.07,.114),metal,plumbing)
box("Battery and isolator",(-.40,-.77,.345),(.191,.163,.128),black,plumbing)
for s in [-1,1]:
    x=s*.699
    tube("Cage front leg",[(x,.53,.261),(x,.50,.796),(s*.582,.10,1.178),(s*.593,-.47,1.181)],.023,metal,cage)
    tube("Cage rear stay",[(s*.593,-.47,1.181),(s*.593,-1.17,.503)],.021,metal,cage)
    for a,b in [(.30,.72),(.70,.30)]:
        tube("Door cross bar",[(x,.49,a),(x,-.48,b)],.019,metal,cage)
tube("Bent main hoop",[(-.68,-.48,.25),(-.68,-.48,.85),(-.604,-.48,1.155),(-.542,-.48,1.198),(.542,-.48,1.198),(.604,-.48,1.155),(.68,-.48,.85),(.68,-.48,.25)],.023,metal,cage)
tube("Harness bar",[(-.68,-.51,.72),(.68,-.51,.72)],.020,metal,cage)
tube("Rear diagonal",[(-.66,-.48,.32),(.593,-.48,1.15)],.019,metal,cage)
# A single driver's bucket, right-hand side. Curved bolsters wrap the torso.
driver=.351
for name,coords in [("Seat base",[(-.39,.356,.199),(-.285,.325,.215),(-.14,.313,.206),(.025,.359,.195),(.075,.38,.182)]),("Seat back",[(-.34,.337,.192),(-.383,.492,.188),(-.43,.64,.171),(-.447,.755,.222),(-.476,.815,.164),(-.495,.933,.105)])]:
    rows=[]
    for y,z,ww in coords:
        rows.append([(driver+t*ww,y+.061*abs(t)**3,z+.059*abs(t)**3) for t in seq(-1,1,17)])
    strip(name,rows,cloth,inside)
for s in [-1,1]:
    x=driver+s*.075
    ribbon("Shoulder belt",[(x-.021,y,z) for y,z in [(-.51,.731),(-.46,.788),(-.399,.693),(-.276,.44),(-.15,.368)]],[(x+.021,y,z) for y,z in [(-.51,.731),(-.46,.788),(-.399,.693),(-.276,.44),(-.15,.368)]],red,cage)
lathe("Harness buckle",[(0,.025),(.016,.025)],'Z',metal,cage,18,(driver,-.15,.373))
box("Dashboard",(0,.52,.733),(1.42,.205,.119),carbon,inside,.051)
box("Display bezel",(driver,.408,.752),(.248,.024,.113),black,inside,.009)
box("Display screen",(driver,.393,.753),(.210,.004,.078),glass,inside)
for j in range(9):box("Shift LED",(driver-.082+.020*j,.39,.795),(.013,.008,.004),orange if j>5 else lamp,inside)
wc=(driver,.222,.713)
tube("Steering column",[(driver,.53,.55),wc],.018,dark,inside)
def steering_point(a,r):return (driver+r*cos(a),wc[1]+.34*r*sin(a),wc[2]+r*sin(a))
outline("Suede steering rim",[steering_point(a,.148) for a in seq(0,2*pi*63/64,64)],.014,cloth,inside,12)
for a in [0,pi,1.5*pi]:tube("Steering spoke",[wc,steering_point(a,.142)],.010,dark,inside)
tube("Centre index",[steering_point(pi/2-.06,.148),steering_point(pi/2+.06,.148)],.015,orange,inside)
helm=group("18_Steering_wheel",wc)
for piece in list(inside.children):
    if piece.name.startswith(("Suede steering rim","Steering spoke","Centre index")):
        piece.parent=helm;piece.location-=Vector(wc)
for x in [-.12,0,.095]:
    tube("Pedal arm",[(driver+x,.55,.426),(driver+x,.492,.314)],.009,metal,inside)
    box("Pedal pad",(driver+x,.48,.322),(.047,.013,.068),dark,inside)
for name,p,q in [("Sequential lever",(.09,.10,.395),(.08,.14,.586)),("Hydraulic handbrake",(.125,-.12,.389),(.13,-.03,.654))]:
    tube(name,[p,q],.010,metal,inside);tube(name+" grip",[Vector(p).lerp(Vector(q),.75),q],.018,black,inside)
for s in [-1,1]:
    strip("Upper body shoulder", [[top(y,s*.746),(s*(.798-.015*(y+.1)**2),y,max(.803+.012*(y+.5),cut(y)+.037))] for y in seq(-1.34,.767,58)],red,skin,reverse=s<0)
