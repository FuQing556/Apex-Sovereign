using System.Collections.Generic;
using UnityEngine;
using static Apex.Editor.MarinaCityForms;

namespace Apex.Editor {
internal sealed class MarinaCityDistricts {
    readonly MarinaStreets map;
    readonly MarinaRiver river;
    readonly MarinaCitySites sites;
    readonly Transform root;
    readonly Material material;
    readonly string folder;
    readonly Dictionary<Vector2Int,MarinaGeometry> tiles=new();
    readonly System.Random random=new(204350);
    readonly int[] uses=new int[7];
    int frontage,quays;
    static readonly Vector3 up=Vector3.up;
    const int west=-19,east=20,south=-16,north=15;
    public MarinaCityDistricts(MarinaStreets map,MarinaRiver river,Transform course,string folder,Material material){
        this.map=map;this.river=river;this.folder=folder;this.material=material;
        sites=new MarinaCitySites(course,river);
        root=new GameObject("Marina city districts").transform;root.SetParent(course,false);
    }
    MarinaGeometry Tile(Vector3 p){
        var k=new Vector2Int(Mathf.FloorToInt(p.x/450),Mathf.FloorToInt(p.z/450));
        if(!tiles.TryGetValue(k,out var g)){g=new MarinaGeometry();tiles.Add(k,g);}
        return g;
    }
    float R(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)random.NextDouble());
    // Adjacent parcels share street intersections. Long boulevards bend towards the bay.
    static Vector3 Node(int col,int row){
        float x=col*124+16*Mathf.Sin(col*1.19f)+43*Mathf.Sin(row*.39f+col*.18f)+row*4.6f;
        float z=row*100-205+12*Mathf.Sin(row*1.31f)+37*Mathf.Sin(col*.54f-row*.23f)-col*4.6f;
        return new Vector3(x,0,z);
    }
    void Road(Vector3 a,Vector3 b,float width,bool boulevard,bool crossingStart=false,bool crossingEnd=false){
        var f=(b-a).normalized;var right=Vector3.Cross(up,f);int n=Mathf.CeilToInt(Vector3.Distance(a,b)/12);
        for(int i=0;i<n;i++){
            var start=Vector3.Lerp(a,b,(float)i/n);var end=Vector3.Lerp(a,b,(i+1f)/n);var p=(start+end)*.5f;
            float len=Vector3.Distance(start,end);
            if(!sites.Take(p,f,width,len,MarinaCitySites.Existing|MarinaCitySites.Building,MarinaCitySites.Street))continue;
            var g=Tile(p);
            void Strip(float x1,float x2,float y,int color){
                var h=up*y;
                g.Quad(start+right*x1+h,end+right*x1+h,end+right*x2+h,start+right*x2+h,color);
            }
            // Base terrain is -0.085 m. Give the pavement and paint independent elevations.
            Strip(-width*.5f,width*.5f,-.052f,boulevard?0x3f4953:0x4c555c);
            if(i==0 && crossingStart || i==n-1 && crossingEnd)continue;
            float walk=width*(boulevard?.18f:.15f);
            Strip(-width*.5f,-width*.5f+walk,-.021f,0x8f918a);
            Strip(width*.5f-walk,width*.5f,-.021f,0x8f918a);
            if(boulevard){
                Strip(-.60f,.60f,-.018f,0x556b58);
                if(i%3==1){
                    var line=(start+end)*.5f+right*(width*.18f);
                    g.Box(line-up*.024f,.14f,.012f,len*.46f,0xbcbaa3,f);
                }
            }
        }
    }
    void Network(){
        for(int x=west;x<=east;x++)for(int z=south;z<=north;z++){
            var p=Node(x,z);
            if(x<east)Road(p,Node(x+1,z),z%4==0?20:12,z%4==0,true,true);
            if(z<north)Road(p,Node(x,z+1),x%5==0?18:11,x%5==0,true,true);
        }
    }
    bool Place(Vector3 p,Vector3 f,float w,float d,float h,Use use){
        if(!sites.Take(p,f,w+1.5f,d+1.5f,MarinaCitySites.All,MarinaCitySites.Building))return false;
        p.y=MarinaRiver.LandY+.012f;
        if(use==Use.Hotel || use==Use.Offices){
            float slenderness=w<27 || d<20?3.7f:6.4f;
            h=Mathf.Min(h,Mathf.Min(w*.69f,d*.54f)*slenderness);
        }
        MarinaCityForms.Draw(Tile(p),use,p,f,w,d,h,random.Next(32768));uses[(int)use]++;
        return true;
    }
    Vector3 Inland(Vector3 eastward,bool far)=>Vector3.Cross(eastward,up)*(far?-1:1);
    void Waterfront(bool far){
        const int step=20;
        Vector3 Promenade(int x){
            var p=river.Bank(x,far,out var eastward);
            p.z+=(far?-1:1)*22/Mathf.Max(.55f,eastward.x);
            return p;
        }
        // Both ends of every strip share a shoreline sample, including at the inlet.
        for(int x=-2240;x<2300;x+=step)Road(Promenade(x),Promenade(x+step),14,true);
    }
    void ShoreFronts(bool far){
        for(int x=-2190;x<2270;x+=46){
            var bank=river.Bank(x,far,out var tangent);var inland=Inland(tangent,far);
            for(int row=0;row<2;row++){
                float offset=row==0?45:96;
                var p=bank+inland*offset;
                Use use;
                if(row==0)use=x>650 && far?Use.Depot:random.Next(5)==0?Use.Park:Use.Terraces;
                else use=x>650 && far?Use.Hall:random.Next(3)==0?Use.Offices:Use.Hotel;
                float h=row==0?R(10,18):use==Use.Hall?R(13,22):R(42,92);
                for(int attempt=0;attempt<4;attempt++){
                    var q=p+inland*(attempt/2*10)+tangent*(attempt%2*13-6);
                    if(!Place(q,inland,attempt<2?39:27,row==0?16:25,h,use))continue;
                    quays++;break;
                }
            }
        }
    }
    void CircuitFronts(){
        // Give the race a readable street wall behind the people, marshals and stalls.
        for(int i=0;i<map.main.Length;i+=13){
            if(river.BridgeReserve(i,18))continue;
            var centre=map.main[i];var f=MarinaStreets.Forward(map.main,i);var r=Vector3.Cross(up,f);
            float x=centre.x/map.scale+900;
            foreach(int side in new[]{-1,1}){
                var away=r*side;Use use;
                if(x<590)use=i%4==0?Use.Court:Use.Terraces;
                else if(x>1390)use=i%3==0?Use.Hall:i%3==1?Use.Hotel:Use.Depot;
                else use=i%4==0?Use.Hotel:i%4==1?Use.Court:Use.Terraces;
                float h=use==Use.Hotel?R(26,48):use==Use.Terraces?R(9,16):R(12,22);
                for(int k=0;k<6;k++){
                    float d=k%2==0?19:14,w=k%2==0?31:20;
                    var p=centre+away*(map.Width(centre)*.5f+9+d*.5f+10*k)+f*(k%2==0?-3:7);
                    if(Place(p,away,w,d,h,use)){frontage++;break;}
                }
            }
        }
    }
    Use LandUse(Vector3 p,int draw){
        float x=p.x/map.scale+900,y=510-p.z/map.scale;
        bool waterfront=p.z<river.Bank(p.x,true,out _).z;
        if(draw<11)return Use.Park;
        if(waterfront && x>1380)return draw<61?Use.Depot:draw<82?Use.Hall:Use.Court;
        if(waterfront){
            float ridge=Mathf.Exp(-Mathf.Pow((x-910)/820,2)-Mathf.Pow((y-1620)/390,2));
            if(draw>66-29*ridge)return Use.Offices;
            return draw<31?Use.Terraces:draw<47?Use.Court:Use.Hotel;
        }
        if(x>1380 && y>-340 && y<960)return draw<43?Use.Hall:draw<65?Use.Depot:draw<85?Use.Hotel:Use.Court;
        if(x<610 && y>290 && y<990)return draw<54?Use.Terraces:draw<86?Use.Court:Use.Hall;
        return draw<29?Use.Terraces:draw<49?Use.Court:draw<68?Use.Hotel:Use.Offices;
    }
    float Height(Vector3 p,Use use){
        switch(use){
        case Use.Hotel:return R(29,64);
        case Use.Offices:
            var centre=map.At(970,1660);var second=map.At(1090,110);
            float distance=Mathf.Min(Vector3.Distance(p,centre),Vector3.Distance(p,second)*1.24f);
            return R(39,76)+Mathf.Exp(-distance/355)*R(48,128);
        case Use.Terraces:return R(8.4f,15);
        case Use.Court:return R(15,29);
        default:return R(11,19);
        }
    }
    void Blocks(){
        for(int x=west;x<east;x++)for(int z=south;z<north;z++){
            var a=Node(x,z);var b=Node(x+1,z);var c=Node(x,z+1);var d=Node(x+1,z+1);
            var along=((b-a)+(d-c)).normalized;var f=Vector3.Cross(along,up);
            var mid=(a+b+c+d)*.25f;
            int style=random.Next(100);var large=LandUse(mid,style);
            if((x-west<2 || east-x<=2 || z-south<2 || north-z<=2) && style%3!=1)large=Use.Park;
            if((large==Use.Depot || large==Use.Hall || large==Use.Park || style%7==0) &&
                Place(mid,f,Mathf.Min(85,(b-a).magnitude*.65f),Mathf.Min(63,(c-a).magnitude*.61f),Height(mid,large),large))continue;
            // Subdivide constrained blocks. Narrow plots finish the gaps beside the race and quays.
            for(int row=0;row<2;row++)for(int col=0;col<2;col++){
                float u=.275f+.45f*col,v=.28f+.44f*row;
                var p=Vector3.Lerp(Vector3.Lerp(a,b,u),Vector3.Lerp(c,d,u),v);
                var use=LandUse(p,random.Next(100));
                float w=Mathf.Clamp((b-a).magnitude*.34f,26,47),depth=Mathf.Clamp((c-a).magnitude*.32f,22,36);
                if(Place(p,row==0?f:-f,w,depth,Height(p,use),use))continue;
                Place(p,row==0?f:-f,w*.68f,depth*.65f,Height(p,Use.Terraces),use==Use.Park?Use.Park:Use.Terraces);
            }
        }
    }
    void Landmark(string name,Vector3 centre,Vector3 f,float w,float d,System.Action<MarinaGeometry,Vector3,Vector3> draw){
        for(int radius=0;radius<=156;radius+=12){
            int samples=radius==0?1:Mathf.CeilToInt(radius*.40f);
            for(int j=0;j<samples;j++){
                float a=j*2*Mathf.PI/samples;
                var p=centre+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
                if(!sites.Take(p,f,w+4,d+4,MarinaCitySites.All,MarinaCitySites.Building))continue;
                var g=new MarinaGeometry();p.y=MarinaRiver.LandY+.024f;
                draw(g,p,f);g.Save(folder,name,root,material);return;
            }
        }
        throw new System.InvalidOperationException("No dry parcel for "+name);
    }
    List<Vector3> Perimeter(){
        var rim=new List<Vector3>();
        for(int x=west;x<east;x++)rim.Add(Node(x,south));
        for(int z=south;z<north;z++)rim.Add(Node(east,z));
        for(int x=east;x>west;x--)rim.Add(Node(x,north));
        for(int z=north;z>south;z--)rim.Add(Node(west,z));
        return rim;
    }
    public void Build(){
        var civicAxis=(map.At(360,392)-map.At(220,651)).normalized;
        Landmark("West civic gallery and quadrangle",map.At(137,536),civicAxis,85,54,MarinaDistrictLandmarks.Gallery);
        Landmark("East circuit exhibition halls",map.At(1566,348),Vector3.forward,91,59,MarinaDistrictLandmarks.Expo);
        var southBank=river.Bank(map.At(902,0).x,true,out var tang);
        var inland=Inland(tang,true);
        Landmark("South quay terrace hotel",southBank+inland*115,inland,107,52,MarinaDistrictLandmarks.BayHotel);
        var port=river.Bank(map.At(1550,0).x,true,out tang);inland=Inland(tang,true);
        Landmark("East port transfer yard",port+inland*65,inland,116,64,MarinaDistrictLandmarks.Port);
        Waterfront(true);Waterfront(false);Network();
        ShoreFronts(true);ShoreFronts(false);CircuitFronts();Blocks();
        foreach(var pair in tiles){
            pair.Value.Save(folder,$"City districts {pair.Key.x} {pair.Key.y}",root,material);
        }
        MarinaOutskirts.Build(Perimeter(),river,root,folder,material);
        Debug.Log($"Marina districts: {frontage} trackside parcels, {quays} quayside parcels; terraces, courts, hotels, offices, halls, depots, parks: {string.Join(", ",uses)}");
    }
}
}
