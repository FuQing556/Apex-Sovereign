using System.Collections.Generic;
using UnityEngine;

namespace Apex.Editor {
// The night circuit is designed around broad arcs connected by straights.
internal sealed class MarinaStreets {
    public const float LapMetres=4450;
    public const int Count=1600;
    // Plan units are scaled uniformly with the scenery. The third coordinate is the corner radius.
    static readonly (Vector2 point,float radius)[] Plan={
        (new(1638,428),0), (new(1602,140),88), (new(1453,105),72),
        (new(1440,280),90), (new(1492,465),78), (new(1070,465),135),
        (new(733,275),88), (new(582,437),76), (new(427,336),80),
        (new(220,674),85), (new(276,765),83), (new(361,806),100),
        (new(601,806),94), (new(698,660),92), (new(860,657),110),
        (new(954,726),100), (new(1310,720),120), (new(1590,754),86),
        (new(1668,668),75)
    };
    static readonly int[] GateAnchors={0,1,2,4,5,6,7,8,9,11,12,13,15,17,18};
    public Vector3[] main;
    public int[] gateIndices;
    public float scale;
    public Vector3 At(float x,float y)=>new Vector3((x-900)*scale,0,(510-y)*scale);

    static List<Vector2> RoundedLoop(){
        var path=new List<Vector2>();
        for(int i=0;i<Plan.Length;i++){
            var (corner,radius)=Plan[i];
            if(radius==0){path.Add(corner);continue;}
            var incoming=corner-Plan[(i+Plan.Length-1)%Plan.Length].point;
            var outgoing=Plan[(i+1)%Plan.Length].point-corner;
            var direction=incoming.normalized;
            float turn=Vector2.SignedAngle(direction,outgoing.normalized)*Mathf.Deg2Rad;
            float tangent=Mathf.Tan(Mathf.Abs(turn)*.5f);
            float trim=Mathf.Min(radius*tangent,.46f*Mathf.Min(incoming.magnitude,outgoing.magnitude));
            var entry=corner-direction*trim;
            var normal=new Vector2(-direction.y,direction.x)*Mathf.Sign(turn);
            var centre=entry+normal*(trim/tangent);
            var spoke=entry-centre;
            int samples=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(turn)*spoke.magnitude/1.4f));
            for(int k=0;k<=samples;k++){
                float angle=turn*k/samples,cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
                path.Add(centre+new Vector2(spoke.x*cos-spoke.y*sin,spoke.x*sin+spoke.y*cos));
            }
        }
        path.Add(path[0]);
        return path;
    }
    public MarinaStreets(){
        var raw=RoundedLoop();
        var arc=new float[raw.Count];for(int i=1;i<raw.Count;i++)arc[i]=arc[i-1]+Vector2.Distance(raw[i-1],raw[i]);
        float total=arc[arc.Length-1]; scale=LapMetres/total;
        main=new Vector3[Count];int segment=0;
        for(int i=0;i<Count;i++){
            float s=total*i/Count;while(arc[segment+1]<s)segment++;
            var p=Vector2.Lerp(raw[segment],raw[segment+1],Mathf.InverseLerp(arc[segment],arc[segment+1],s));main[i]=At(p.x,p.y);
        }
        float chord=0;for(int i=0;i<Count;i++)chord+=Vector3.Distance(main[i],main[(i+1)%Count]);
        float correction=LapMetres/chord;scale*=correction;for(int i=0;i<Count;i++)main[i]*=correction;
        gateIndices=new int[GateAnchors.Length];for(int i=0;i<gateIndices.Length;i++){
            var p=Plan[GateAnchors[i]].point;gateIndices[i]=Nearest(main,At(p.x,p.y));
        }
    }
    public static int Nearest(Vector3[] points,Vector3 p){
        float best=float.MaxValue;int nearest=0;
        for(int i=0;i<points.Length;i++){float sq=(p-points[i]).sqrMagnitude;if(sq<best){best=sq;nearest=i;}}
        return nearest;
    }
    public static Vector3 Forward(Vector3[] points,int i,bool loop=true){
        int n=points.Length;
        int a=loop?(i+n-1)%n:Mathf.Max(0,i-1),b=loop?(i+1)%n:Mathf.Min(n-1,i+1);
        return (points[b]-points[a]).normalized;
    }
    public float Width(Vector3 p){
        float east=p.x/scale+900;
        return Mathf.Lerp(20,24,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1050,1510,east)));
    }
    public float Clearance(Vector3 p){
        float nearest=Vector3.Distance(main[Nearest(main,p)],p)-Width(p)/2;
        return nearest;
    }
}
}
