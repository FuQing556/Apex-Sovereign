using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Apex.Editor {
internal static class RoadsideDetailBuilder {
    public static void Build(Circuit course,string id,Vector3[] road,Vector3[][] bed,RoadsideSettings spec){
        var vertices=new List<Vector3>();var blades=new List<int>();var paint=new List<int>();
        var panels=new List<int>();var arrows=new List<int>();
        int n=bed.Length;var centre=new Vector3[n];var station=new float[n+1];
        for(int i=0;i<n;i++)centre[i]=(road[4*i]+road[4*i+3])*.5f;
        for(int i=0;i<n;i++)station[i+1]=station[i]+Vector3.Distance(centre[i],centre[(i+1)%n]);
        var random=new System.Random(31);
        float Random01()=>(float)random.NextDouble();
        for(int i=0;i<n;i++){
            int j=(i+1)%n;
            for(int k=0;k<2;k++){
                var a=bed[i][k==0?2:3];var b=bed[j][k==0?2:3];
                var rearA=bed[i][k==0?1:4];var rearB=bed[j][k==0?1:4];
                int clumps=Mathf.RoundToInt(Vector3.Distance(a,b)/spec.grassClusterSpacing);
                for(int g=0;g<clumps;g++){
                    float t=(g+.2f+.6f*Random01())/clumps;
                    var edge=Vector3.Lerp(a,b,t);var across=Vector3.Lerp(rearA,rearB,t)-edge;
                    float room=across.magnitude-spec.barrierWidth-.20f;
                    if(room<.35f)continue;
                    var root=edge+across.normalized*Mathf.Lerp(.20f,room,Random01());
                    for(int blade=0;blade<5;blade++){
                        float angle=Random01()*Mathf.PI*2;
                        var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        var basePoint=root+axis*(.10f*Random01())-Vector3.up*.015f;
                        var tip=basePoint+axis*.065f+Vector3.up*(spec.grassHeight*Mathf.Lerp(.45f,1,Random01()));
                        var half=Vector3.Cross(Vector3.up,axis)*.027f;
                        int v=vertices.Count;vertices.Add(basePoint-half);vertices.Add(basePoint+half);vertices.Add(tip);
                        blades.AddRange(new[]{v,v+2,v+1,v,v+1,v+2});
                    }
                }
            }
        }
        int grassVertices=vertices.Count;
        // Rank bends by turning angle per metre. Keep one approach per physical bend,
        // including bends that straddle the start/finish line.
        float length=station[n];var turns=new float[n];
        for(int i=0;i<n;i++){
            int before=(i+n-1)%n,after=(i+1)%n;
            turns[i]=Vector3.SignedAngle(Vector3.ProjectOnPlane(centre[i]-centre[before],Vector3.up),
                Vector3.ProjectOnPlane(centre[after]-centre[i],Vector3.up),Vector3.up)/
                Mathf.Max(.1f,(Vector3.Distance(centre[i],centre[before])+Vector3.Distance(centre[i],centre[after]))*.5f);
        }
        var bends=new List<float>();
        foreach(int apex in Enumerable.Range(0,n).OrderByDescending(i=>Mathf.Abs(turns[i]))){
            if(Mathf.Abs(turns[apex])<1.35f)break;
            float distance=station[apex];
            if(bends.Exists(s=>Mathf.Min(Mathf.Abs(s-distance),length-Mathf.Abs(s-distance))<4*spec.approachMarkInterval))continue;
            bends.Add(distance);int outer=turns[apex]>0?1:2;
            var verge=bed[apex];
            var inside=verge[outer==1?2:3];var back=verge[outer==1?1:4];
            var mount=back+(inside-back).normalized*spec.barrierWidth*.5f+Vector3.up*spec.barrierHeight;
            var face=Vector3.ProjectOnPlane(centre[(apex+n-2)%n]-mount,Vector3.up).normalized;
            var across=Vector3.Cross(face,Vector3.up);
            Vector3 P(float x,float y,float depth=0)=>mount+x*across+y*Vector3.up+depth*face;
            void Quad(List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d){
                int v=vertices.Count;vertices.AddRange(new[]{a,b,c,d,d,c,b,a});
                triangles.AddRange(new[]{v,v+1,v+2,v,v+2,v+3,v+4,v+5,v+6,v+4,v+6,v+7});
            }
            Quad(panels,P(-.06f,-.05f),P(.06f,-.05f),P(.06f,.56f),P(-.06f,.56f));
            Quad(panels,P(-1.08f,.48f),P(1.08f,.48f),P(1.08f,1.08f),P(-1.08f,1.08f));
            float dir=Mathf.Sign(turns[apex]);
            foreach(float x in new[]{-.66f,0,.66f}){
                Vector3 Stripe(float u,float y)=>P(x+dir*u,y,.016f);
                Quad(arrows,Stripe(-.23f,.53f),Stripe(-.08f,.53f),Stripe(.22f,.78f),Stripe(.07f,.78f));
                Quad(arrows,Stripe(.07f,.78f),Stripe(.22f,.78f),Stripe(-.08f,1.03f),Stripe(-.23f,1.03f));
            }
            for(int group=1;group<=3;group++)for(int bar=0;bar<group;bar++){
                float start=distance-group*spec.approachMarkInterval-bar*.85f;
                int v=vertices.Count;
                for(int end=0;end<2;end++){
                    float s=Mathf.Repeat(start+end*.30f,length);
                    int nearest=System.Array.BinarySearch(station,s);
                    int i=nearest>=0?nearest:~nearest-1;i=Mathf.Min(i,n-1);int j=(i+1)%n;
                    float t=Mathf.InverseLerp(station[i],station[i+1],s);
                    var paintEdge=Vector3.Lerp(road[4*i+outer],road[4*j+outer],t);
                    var inward=(Vector3.Lerp(centre[i],centre[j],t)-paintEdge).normalized;
                    paintEdge+=Vector3.up*.018f;
                    vertices.Add(paintEdge+inward*.30f);vertices.Add(paintEdge+inward*1.20f);
                }
                if(outer==1)paint.AddRange(new[]{v,v+2,v+1,v+1,v+2,v+3});
                else paint.AddRange(new[]{v,v+1,v+2,v+1,v+3,v+2});
            }
        }
        var mesh=RoadsideBuilder.AssetMesh(id+"VergeDetails");
        mesh.indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);mesh.subMeshCount=4;mesh.SetTriangles(blades,0);mesh.SetTriangles(paint,1);
        mesh.SetTriangles(panels,2);mesh.SetTriangles(arrows,3);
        mesh.RecalculateNormals();
        var normals=mesh.normals;
        for(int i=0;i<grassVertices;i++)normals[i]=Vector3.up;
        mesh.normals=normals;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        const string name="Verge grass and approach markings";
        var detail=course.transform.Find(name);
        if(!detail){detail=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;detail.SetParent(course.transform,false);}
        detail.GetComponent<MeshFilter>().sharedMesh=mesh;
        const string palette="Assets/_Project/Tracks/RidgeArt/Materials/";
        var renderer=detail.GetComponent<MeshRenderer>();renderer.sharedMaterials=new[]{spec.grass,spec.marking,
            AssetDatabase.LoadAssetAtPath<Material>(palette+"Sign ink.mat"),AssetDatabase.LoadAssetAtPath<Material>(palette+"Safety orange.mat")};
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        HideOldBoards(course.transform);
    }
    internal static void HideOldBoards(Transform root){
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if(filter.name=="Corner boards")filter.gameObject.SetActive(false);
    }
}
}
