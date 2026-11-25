using System.Collections.Generic;
using UnityEngine;
using static Apex.Editor.MarinaStreets;

namespace Apex.Editor {
// Bank lines are in the same plan coordinates as the circuit. Both shores, their
// collision surfaces and placement queries use the same piecewise-linear boundary.
internal sealed class MarinaRiver {
    public const float LandY=-.085f, WaterY=-4.1f;
    readonly List<Vector3> north=new(),south=new();
    public const float BridgeLength=152;
    public readonly int bridgeStation;
    public readonly Vector3 bridgeForward;
    readonly MarinaStreets streets;
    public MarinaRiver(MarinaStreets map){
        streets=map;bridgeStation=Nearest(map.main,map.At(469,806));
        bridgeForward=Forward(map.main,bridgeStation);
        // The boulevard crosses one inlet. The rest of the circuit follows the mainland.
        Add(-4400,925,1020);Add(-360,919,1007);Add(160,901,1000);Add(315,910,1025);
        float x=map.main[bridgeStation].x/map.scale+900;
        Add(x-83/map.scale,864,1070);Add(x-56/map.scale,748,1080);
        Add(x-25/map.scale,699,1100);Add(x+29/map.scale,702,1130);
        Add(x+56/map.scale,753,1160);Add(x+83/map.scale,865,1190);
        Add(599,835,1270);
        Add(671,775,1370);Add(784,751,1431);Add(864,775,1450);Add(937,784,1460);
        Add(1141,796,1416);Add(1302,805,1310);Add(1545,823,1164);Add(1715,832,1060);
        Add(2080,849,970);Add(6000,849,970);
    }
    void Add(float x,float ny,float sy){north.Add(streets.At(x,ny));south.Add(streets.At(x,sy));}
    public Vector3 Bank(float worldX,bool farBank,out Vector3 east){
        var bank=farBank?south:north;
        int i=0;
        while(i<bank.Count-2 && worldX>bank[i+1].x)i++;
        var a=bank[i];var b=bank[i+1];east=(b-a).normalized;
        return Vector3.LerpUnclamped(a,b,(worldX-a.x)/(b.x-a.x));
    }
    public bool Dry(Vector3 p,float setback=0){
        for(int i=0;i<north.Count-1;i++){
            var a=north[i];var b=north[i+1];var c=south[i];var d=south[i+1];
            if(p.x>=a.x && p.x<=b.x){
                float t=(p.x-a.x)/(b.x-a.x);
                if(p.z<=Mathf.Lerp(a.z,b.z,t) && p.z>=Mathf.Lerp(c.z,d.z,t))return false;
            }
            if(setback>0 && p.x>=a.x-setback && p.x<=b.x+setback &&
                (DistanceXZ(p,a,b)<setback || DistanceXZ(p,c,d)<setback))return false;
        }
        return true;
    }
    static float DistanceXZ(Vector3 p,Vector3 a,Vector3 b){
        p.y=0;var delta=b-a;return Vector3.Distance(p,a+delta*Mathf.Clamp01(Vector3.Dot(p-a,delta)/delta.sqrMagnitude));
    }
    public bool BridgeReserve(int station,float extra=0){
        float ds=LapMetres/MarinaStreets.Count;
        return Mathf.Abs(station-bridgeStation)*ds < BridgeLength*.5f+extra;
    }
    bool UnderDeck(Vector3 p){
        var delta=p-streets.main[bridgeStation];
        return Mathf.Abs(Vector3.Dot(delta,bridgeForward))<BridgeLength*.5f+2 &&
            Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,bridgeForward)))<streets.Width(p)*.5f+3.8f;
    }
    public IEnumerable<(Vector3 edge,Vector3 direction,Vector3 inland)> Promenade(){
        foreach(var bank in new[]{north,south}){
            for(int i=0;i<bank.Count-1;i++){
                var a=bank[i];var b=bank[i+1];var f=(b-a).normalized;
                if(bank==south)f=-f;
                var inland=Vector3.Cross(f,Vector3.up);
                int count=Mathf.FloorToInt(Vector3.Distance(a,b)/23);
                for(int k=0;k<count;k++){
                    var edge=Vector3.Lerp(a,b,(k+.5f)/count);
                    if(edge.x<streets.At(198,0).x || edge.x>streets.At(1290,0).x ||
                        UnderDeck(edge+inland*4) || streets.Clearance(edge+inland*4)<12)continue;
                    yield return (edge,f,inland);
                }
            }
        }
    }
    // Continuous land behind the quay instead of an invisible floor below the reservoir.
    public void Build(string folder,Transform parent,Material groundMat,Material city,Material waterMat){
        var land=new MarinaGeometry();var water=new MarinaGeometry();var quay=new MarinaGeometry();
        var up=Vector3.up;const int landRows=32;
        for(int i=0;i<north.Count-1;i++){
            int columns=Mathf.CeilToInt((north[i+1].x-north[i].x)/125);
            for(int j=0;j<columns;j++){
                float t=(float)j/columns,v=(j+1f)/columns;
                var a=Vector3.Lerp(north[i],north[i+1],t);var b=Vector3.Lerp(north[i],north[i+1],v);
                var c=Vector3.Lerp(south[i],south[i+1],v);var d=Vector3.Lerp(south[i],south[i+1],t);
                water.Quad(a+up*WaterY,b+up*WaterY,c+up*WaterY,d+up*WaterY,0x1c3849);
                void BankStrip(Vector3 start,Vector3 end,int sign){
                    var step=Vector3.forward*sign;float extent=4800-sign*(start.z+end.z)*.5f;
                    for(int k=0;k<landRows;k++){
                        var near=step*(extent*k/landRows)+up*LandY;
                        var far=step*(extent*(k+1)/landRows)+up*LandY;
                        land.Quad(start+near,start+far,end+far,end+near,0x4b5156);
                    }
                    quay.Quad(start+up*LandY,end+up*LandY,end+up*(WaterY-1),start+up*(WaterY-1),0x737678);
                    var tangent=(end-start).normalized;var inward=Vector3.Cross(tangent,up);
                    int blocks=Mathf.CeilToInt(Vector3.Distance(start,end)/3);
                    for(int k=0;k<blocks;k++){
                        var curb=Vector3.Lerp(start,end,(k+.5f)/blocks)+inward*.23f;
                        if(!UnderDeck(curb))quay.Box(curb,.52f,.27f,Vector3.Distance(start,end)/blocks-.025f,0xa5a599,tangent);
                    }
                    // Constant Z offsets meet exactly at the next cross-section.
                    // Keep this shoreline paving below the racing surface at abutments.
                    var inner=step*4.8f;
                    quay.Quad(start+up*(-.06f),start+inner+up*(-.06f),end+inner+up*(-.06f),end+up*(-.06f),0x8d8d86);
                    int joints=Mathf.Max(1,Mathf.RoundToInt((end-start).magnitude/6));
                    for(int tIndex=0;tIndex<joints;tIndex++){
                        var joint=Vector3.Lerp(start,end,(float)tIndex/joints)-up*.049f;
                        if(Mathf.Abs(joint.x)>1300)continue;
                        quay.Beam(joint+inward*.5f,joint+inner,.016f,0x61666a,4);
                    }
                    for(int k=0;k<3;k++){
                        float height=-.8f-k*1.05f;
                        quay.Beam(start+up*height,end+up*height,.052f,0x595e62,4);
                    }
                }
                BankStrip(a,b,1);BankStrip(c,d,-1);
            }
        }
        land.Save(folder,"City ground",parent,groundMat,true);
        water.Save(folder,"Marina reservoir and river",parent,waterMat);
        quay.Save(folder,"Quays and embankments",parent,city);
    }
}
}
