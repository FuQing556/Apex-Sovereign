using System.Collections.Generic;
using UnityEngine;

namespace Apex.Editor {
// Project the built scene onto a metre grid before allocating any infill parcels.
internal sealed class MarinaCitySites {
    public const int Existing=1,Street=2,Building=4,All=7;
    const float cell=4;
    readonly Dictionary<Vector2Int,byte> cells=new();
    readonly Dictionary<int,Vector2> shores=new();
    readonly MarinaRiver river;
    public MarinaCitySites(Transform course,MarinaRiver river){
        this.river=river;
        foreach(var filter in course.GetComponentsInChildren<MeshFilter>()){
            var mesh=filter.sharedMesh;string n=filter.name;
            if(!mesh || n=="City ground" || n=="Marina reservoir and river" || n=="Quays and embankments" ||
               n.StartsWith("Harbour -") || filter.GetComponent<MarinaBroadcastDrone>())continue;
            var v=mesh.vertices;var ix=mesh.triangles;
            var mat=course.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            for(int j=0;j<v.Length;j++)v[j]=mat.MultiplyPoint3x4(v[j]);
            for(int j=0;j<ix.Length;j+=3)Triangle(v[ix[j]],v[ix[j+1]],v[ix[j+2]]);
        }
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static Vector2 Grid(Vector3 p)=>new(p.x/cell,p.z/cell);
    void Triangle(Vector3 va,Vector3 vb,Vector3 vc){
        var a=Grid(va);var b=Grid(vb);var c=Grid(vc);
        var ab=b-a;var bc=c-b;var ca=a-c;
        if(Cross(ab,c-a)<0){(b,c)=(c,b);ab=b-a;bc=c-b;ca=a-c;}
        float Slack(Vector2 d)=>-.5f*(Mathf.Abs(d.x)+Mathf.Abs(d.y))-.002f;
        float e0=Slack(ab),e1=Slack(bc),e2=Slack(ca);
        for(int x=Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x)));x<=Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x)));x++)
        for(int z=Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y)));z<=Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y)));z++){
            var p=new Vector2(x+.5f,z+.5f);
            if(Cross(ab,p-a)>=e0 && Cross(bc,p-b)>=e1 && Cross(ca,p-c)>=e2)cells[new Vector2Int(x,z)]=Existing;
        }
    }
    Vector2 WaterRange(int x){
        if(shores.TryGetValue(x,out var range))return range;
        float left=x*cell,right=left+cell;
        range=new Vector2(Mathf.Min(river.Bank(left,true,out _).z,river.Bank(right,true,out _).z)-1,
                          Mathf.Max(river.Bank(left,false,out _).z,river.Bank(right,false,out _).z)+1);
        shores[x]=range;return range;
    }
    bool Rect(Vector3 p,Vector3 f,float w,float d,int exclude,int mark){
        var r=Vector3.Cross(Vector3.up,f);
        float expand=(Mathf.Abs(r.x)+Mathf.Abs(r.z))*cell*.5f;
        var dx=Mathf.Abs(r.x)*w*.5f+Mathf.Abs(f.x)*d*.5f;
        var dz=Mathf.Abs(r.z)*w*.5f+Mathf.Abs(f.z)*d*.5f;
        for(int x=Mathf.FloorToInt((p.x-dx)/cell);x<=Mathf.FloorToInt((p.x+dx)/cell);x++){
            var water=WaterRange(x);
            for(int z=Mathf.FloorToInt((p.z-dz)/cell);z<=Mathf.FloorToInt((p.z+dz)/cell);z++){
                float ox=(x+.5f)*cell-p.x,oz=(z+.5f)*cell-p.z;
                if(Mathf.Abs(ox*r.x+oz*r.z)>w*.5f+expand || Mathf.Abs(ox*f.x+oz*f.z)>d*.5f+expand)continue;
                var key=new Vector2Int(x,z);cells.TryGetValue(key,out byte used);
                if(exclude!=0 && ((used&exclude)!=0 || (z+1)*cell>=water.x && z*cell<=water.y))return false;
                if(mark!=0)cells[key]=(byte)(used|mark);
            }
        }
        return true;
    }
    public bool Take(Vector3 p,Vector3 f,float w,float d,int exclude,int mark){
        if(!Rect(p,f,w,d,exclude,0))return false;
        Rect(p,f,w,d,0,mark);return true;
    }
}
}
