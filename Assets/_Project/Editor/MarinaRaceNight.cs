using System.Collections.Generic;
using UnityEngine;
using static Apex.Editor.MarinaStreets;
using static Apex.Editor.MarinaStreetProps;

namespace Apex.Editor {
internal sealed class MarinaRaceNight {
    readonly MarinaStreets map;
    readonly MarinaRiver river;
    readonly Transform root;
    readonly Material city,display;
    readonly string folder;
    readonly System.Random random=new(31966);
    readonly HashSet<Vector2Int> occupied=new();
    static readonly Vector3 up=Vector3.up;
    const float cell=2;
    static readonly string[] districts={"Pit paddock","Promoter village","Night market","Civic lawn","River walk","Bay grandstand"};
    static readonly int[] turf={0x526f51,0x597958,0x45654e,0x68825b,0x4c7258};
    public MarinaRaceNight(MarinaStreets map,MarinaRiver river,Transform course,string folder,Material city){
        this.map=map;this.river=river;this.folder=folder;this.city=city;
        display=course.Find("Animated blue advertising screens").GetComponent<MeshRenderer>().sharedMaterial;
        IndexScenery(course);
        root=new GameObject("Race night - streets and harbour").transform;root.SetParent(course,false);
    }
    float Range(float low,float high)=>Mathf.Lerp(low,high,(float)random.NextDouble());
    static Vector2Int Cell(Vector3 p)=>new(Mathf.FloorToInt(p.x/cell),Mathf.FloorToInt(p.z/cell));
    // Index the actual baked props, including those without colliders. Empty pavements stay available.
    void IndexScenery(Transform course){
        foreach(var mf in course.GetComponentsInChildren<MeshFilter>()){
            var mesh=mf.sharedMesh;
            if(!mesh || mesh.bounds.max.y<.14f || mf.name=="Quays and embankments" ||
                mf.name=="Catch fencing" || mf.name=="Concrete race barriers" || mf.name=="Floodlight gantries fencing and paint")continue;
            var verts=mesh.vertices;
            // The existing city builder emits four consecutive vertices per facade, box face or beam face.
            for(int i=0;i+3<verts.Length;i+=4){
                var lo=Vector3.Min(Vector3.Min(verts[i],verts[i+1]),Vector3.Min(verts[i+2],verts[i+3]));
                var hi=Vector3.Max(Vector3.Max(verts[i],verts[i+1]),Vector3.Max(verts[i+2],verts[i+3]));
                if(hi.y<.14f)continue;
                var a=Cell(lo);var b=Cell(hi);
                for(int x=a.x;x<=b.x;x++)for(int z=a.y;z<=b.y;z++)occupied.Add(new Vector2Int(x,z));
            }
        }
    }
    bool Lot(Vector3 p,Vector3 f,float w,float d,bool reserve){
        var r=Vector3.Cross(up,f);
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++){
            var edge=p+r*(x*w/2)+f*(z*d/2);
            if(!river.Dry(edge,.6f) || map.Clearance(edge)<2.7f)return false;
        }
        int nx=Mathf.CeilToInt(w/cell),nz=Mathf.CeilToInt(d/cell);
        for(int i=0;i<=nx;i++)for(int j=0;j<=nz;j++){
            var at=Cell(p+r*(w*((float)i/nx-.5f))+f*(d*((float)j/nz-.5f)));
            if(reserve)occupied.Add(at);else if(occupied.Contains(at))return false;
        }
        return true;
    }
    // Try both banks of the road. A reserved parcel can never receive a second cluster.
    bool Site(int i,float width,float depth,float setback,out Vector3 p,out Vector3 facing){
        i%=map.main.Length;
        p=default;facing=default;
        if(river.BridgeReserve(i,MarinaBridgeRoads.Approach+5))return false;
        var road=map.main[i];var along=Forward(map.main,i);var side=Vector3.Cross(up,along);
        int preferred=i%2==0?-1:1;
        foreach(float extra in new[]{0f,5f,12f})foreach(int s in new[]{preferred,-preferred}){
            facing=-side*s;
            p=road-facing*(map.Width(road)*.5f+depth*.5f+setback+extra);
            p.y=MarinaRiver.LandY;
            if(!Lot(p,facing,width,depth,false))continue;
            Lot(p,facing,width,depth,true);
            return true;
        }
        return false;
    }
    void Meadow(MarinaGeometry g,Vector3 p,Vector3 f,float w,float d,int count){
        var r=Vector3.Cross(up,f);
        for(int i=0;i<count;i++){
            var basePos=p+r*Range(-w*.49f,w*.49f)+f*Range(-d*.49f,d*.49f);
            float blade=Range(.105f,.33f);
            for(int k=0;k<3;k++){
                var dir=Quaternion.Euler(0,Range(0,360),0)*Vector3.forward;
                var foot=basePos+dir*.07f;var cross=Vector3.Cross(up,dir)*Range(.035f,.074f);
                g.Triangle(foot-cross,foot+up*(blade*Range(.65f,1.2f))+dir*.14f,foot+cross,turf[random.Next(turf.Length)],true);
            }
        }
    }
    void CivicGrass(){
        var f=(map.At(360,392)-map.At(220,651)).normalized;var r=Vector3.Cross(up,f);
        var centre=map.At(303,526)+r*68;var g=new MarinaGeometry();
        for(int x=-5;x<5;x++)for(int z=-10;z<10;z++){
            var at=centre+r*((x+.5f)*11)+f*((z+.5f)*13);
            if(map.Clearance(at)<12 || !river.Dry(at,9))continue;
            var p=at+up*.014f;var side=r*5.5f;var end=f*6.5f;
            g.Quad(p-side-end,p-side+end,p+side+end,p+side-end,z%2==0?0x49694f:0x43624d);
            Meadow(g,p,f,11,13,25);
        }
        g.Save(folder,"Padang - Mown turf and grass tufts",root,city);
    }
    void Garden(MarinaGeometry g,Vector3 p,Vector3 f){
        g.Box(p+up*.13f,15,.26f,5.8f,0x909384,f);
        g.Box(p+up*.257f,14.65f,.075f,5.45f,0x47644d,f);
        Meadow(g,p+up*.295f,f,14.6f,5.4f,108);
    }
    void StreetGroup(MarinaGeometry g,Vector3 p,Vector3 f,int theme,int id){
        var r=Vector3.Cross(up,f);
        Vector3 P(float x,float y,float z)=>p+r*x+up*y+f*z;
        g.Box(P(0,.08f,0),18,.16f,6.8f,0x74797b,f);
        var floor=p+up*.16f;
        if(theme==0){
            Van(g,P(-4,.16f,-.65f),r,id%2==0?0xa44e55:0x879da3,true);
            for(int k=0;k<4;k++)Case(g,P(.3f+k*1.35f,.16f,-1.65f),f,1.18f,.91f,1.15f,k%2==0?0x596978:0xa78460);
            Case(g,P(.3f,1.07f,-1.65f),f,1.04f,.68f,.92f,0x4d6472);
        }else if(theme==1){
            Kiosk(g,P(-3,.16f,-1),f,id%2==0?0xb26575:0x548a91);
            Case(g,P(-7.1f,.16f,-1.6f),f,1.3f,.92f,1.1f,0x9c8461);
        }else if(theme==2){
            Van(g,P(-4.4f,.16f,-1.1f),-r,0x9aabaf,false);
            Bunting(g,P(3.4f,.16f,-1.0f),f,7);
        }else Bunting(g,P(0,.16f,-1.2f),f,13);
        int count=theme==0?4:random.Next(7,13);
        for(int j=0;j<count;j++){
            float x=Mathf.Lerp(-7.9f,7.9f,(j+Range(.18f,.82f))/count);
            var pos=floor+r*x+f*Range(1.35f,2.85f);
            var turn=Quaternion.Euler(0,Range(-26,26),0)*f;
            Person(g,pos,turn,random.Next(4096),theme==0);
        }
        PavementMural(g,P(5,.172f,-.6f),f,.92f,0xba6581);
    }
    void KerbPaint(MarinaGeometry g,int i,int accent){
        var origin=map.main[i];var f=Forward(map.main,i);var r=Vector3.Cross(up,f)*(i%2==0?1:-1);
        var p=origin+r*(map.Width(origin)*.5f-1.85f)+up*.036f;
        for(int j=0;j<4;j++){
            var a=p+f*(j*1.45f-2.2f);
            var b=a+r*.74f+f*.45f;
            if(i%2==0)g.Quad(a,a+f*.34f,b+f*.34f,b,j==0?accent:0xc1c0ad);
            else g.Quad(b,b+f*.34f,a+f*.34f,a,j==0?accent:0xc1c0ad);
        }
    }
    void Screen(MarinaGeometry g,MarinaGeometry pixels,int gate,string title,int accent){
        int i=map.gateIndices[gate];var along=Forward(map.main,i);
        for(int offset=0;offset<36;offset+=3){
            int station=(i+offset)%map.main.Length;
            if(!Site(station,27,25,3,out var foot,out var towardTrack))continue;
            var f=(along-towardTrack*.55f).normalized;var r=Vector3.Cross(up,f);
            var centre=foot+up*10.1f;const float w=23,h=8.4f;
            foreach(int sign in new[]{-1,1}){
                var basePos=foot+r*(sign*7.5f);
                g.Box(basePos+up*.28f,2.15f,.56f,2.2f,0x7d8182,f);
                g.Beam(basePos,basePos+up*6.5f,.29f,0x697887,6);
            }
            g.Box(centre,w,h,.82f,0x233347,f);var face=centre-f*.44f;
            pixels.Quad(face-r*10.7f-up*3.58f,face-r*10.7f+up*1.12f,face+r*10.7f+up*1.12f,face+r*10.7f-up*3.58f,0xffffff,0,1);
            g.Lettering(title,face+up*2.63f,f,.235f,0xfff5da);
            g.Box(centre+up*(h/2),w,.24f,.90f,accent,f);
            g.Box(centre-r*11.35f,.28f,h,.91f,accent,f);
            return;
        }
        throw new System.InvalidOperationException("No space for race screen at "+title);
    }
    void Drones(){
        var g=new MarinaGeometry();var f=Vector3.forward;var r=Vector3.right;
        g.Box(Vector3.zero,.6f,.28f,.88f,0xc2c8c4);
        g.Box(Vector3.down*.27f,.32f,.28f,.34f,0x354556);g.Box(f*.18f-up*.27f,.18f,.15f,.06f,0x77a9bc);
        foreach(int s in new[]{-1,1})foreach(int z in new[]{-1,1}){
            var end=r*(s*.88f)+f*(z*.68f);
            g.Beam(Vector3.zero,end,.065f,0x6b7883);g.Beam(end,end+up*.16f,.105f,0x606773,7);
            g.Box(end+up*.19f,.85f,.035f,.08f,0xabb6bb,Quaternion.Euler(0,32*z,0)*f);
            g.Box(end-up*.025f,.18f,.08f,.18f,s>0?0x77e9fc:0xff559e);
        }
        var first=g.Save(folder,"Broadcast quadcopter",root,city);var second=Object.Instantiate(first,root);
        void Flight(GameObject drone,Vector3 centre,Vector3 sweep,float period,float phase){
            drone.transform.localPosition=centre;
            var flight=drone.AddComponent<MarinaBroadcastDrone>();flight.sweep=sweep;flight.period=period;flight.phase=phase;
        }
        Flight(first,map.main[0]+up*18,Forward(map.main,0)*39,36,.4f);
        Flight(second,map.At(962,810)+up*19,Vector3.right*55,47,2.2f);
    }
    public void Build(){
        CivicGrass();
        var frames=new MarinaGeometry();var pixels=new MarinaGeometry();
        Screen(frames,pixels,2,"PIT RADIO",0xff559e);
        Screen(frames,pixels,7,"NIGHT MARKET",0x77e9fc);
        Screen(frames,pixels,14,"BAY SIGNAL",0xff559e);
        frames.Save(folder,"Race broadcast - Gantries and lettering",root,city);
        pixels.Save(folder,"Race broadcast - LED walls",root,display);
        int groups=0,gardens=0,hubs=0;
        for(int sector=0;sector<districts.Length;sector++){
            int start=sector*Count/districts.Length,end=(sector+1)*Count/districts.Length;
            var g=new MarinaGeometry();
            for(int i=start+30;i<end-10;i+=9){
                if(!Site(i,40,21,14,out var yard,out var front))continue;
                MotorPool(g,yard,front,sector%3!=0);hubs++;break;
            }
            for(int i=start+13;i<end;i+=21){
                int id=i/21;
                if(Site(i,19,7.2f,2.9f,out var lot,out var face)){
                    int theme=sector==0 && id%3!=0?0:sector==2 && id%3!=0?1:(sector+id)%4;
                    StreetGroup(g,lot,face,theme,id);groups++;
                }
                if(!river.BridgeReserve(i,54) && id%3==0)KerbPaint(g,i,sector%2==0?0xb0637e:0x638d94);
                if(!river.BridgeReserve(i,54) && id%8==3){
                    var outward=Vector3.Cross(up,Forward(map.main,i))*(sector%2==0?1:-1);
                    var p=map.main[i]+outward*(map.Width(map.main[i])*.5f-2.6f)+up*.034f;
                    PavementMural(g,p,outward,1.02f,sector%2==0?0xc37492:0x75a5ad);
                }
                if(id%3==1 && Site(i+7,15.8f,6.2f,3.5f,out var bed,out var direction)){
                    Garden(g,bed,direction);gardens++;
                }
            }
            g.Save(folder,"Night streets - "+districts[sector],root,city);
        }
        if(groups<25 || gardens<6)throw new System.InvalidOperationException($"Street dressing too sparse: {groups} lots / {gardens} gardens");
        new MarinaHarbour(map,river,root,folder,city).Build();Drones();
        Debug.Log($"Marina Bay race night: {groups} staffed spectator and service areas, {hubs} paddock and market compounds, {gardens} grass beds, three feature screens.");
    }
}
}
