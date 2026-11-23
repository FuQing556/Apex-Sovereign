using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Apex.Editor {
public static class RoadCourseBuilder {
    const string Root="Assets/_Project/", Dir=Root+"Tracks/";
    static Material Art(string id)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/"+id+".mat");
    static RoadLayout Layout(string id,string title,Vector3[] knots){
        var path=Dir+id+".asset";
        var profile=AssetDatabase.LoadAssetAtPath<RoadLayout>(path);
        if(profile)return profile;
        profile=ScriptableObject.CreateInstance<RoadLayout>();profile.title=title;profile.knots=knots;
        AssetDatabase.CreateAsset(profile,path);return profile;
    }
    static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
    static Transform Node(string name, Transform parent){
        var t=new GameObject(name).transform;t.SetParent(parent,false);return t;
    }
    static void Mark(Transform parent,string name,Vector3 pos,Vector3 size,Material material){
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
        g.transform.localPosition=pos;g.transform.localScale=size;
        Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;
    }
    static void Bake(string id,RoadLayout layout){
        var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        var course=Node(layout.title,null).gameObject.AddComponent<Circuit>();
        int stride=layout.samplesPerSpan,n=layout.knots.Length*stride;
        var path=new Vector3[n];
        for(int i=0;i<n;i++)path[i]=layout.Sample(i/stride,(float)(i%stride)/stride);
        var vertices=new Vector3[n*4];var triangles=new System.Collections.Generic.List<int>[] {
            new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>()};
        course.laps=layout.laps;course.startDelay=layout.countdownSeconds;course.gateHalfWidth=layout.halfWidth+.5f;
        course.gates=new Transform[layout.knots.Length*2];var width=layout.halfWidth;
        float distance=0;
        for(int i=0;i<n;i++){
            int next=(i+1)%n;distance+=Vector3.Distance(path[i],path[next]);
            var tangent=(path[next]-path[(i+n-1)%n]).normalized;var side=Vector3.Cross(Vector3.up,tangent).normalized;
            var up=Vector3.Cross(tangent,side);float[] offsets={-width-.6f,-width,width,width+.6f};
            for(int j=0;j<4;j++)vertices[i*4+j]=path[i]+side*offsets[j];
            for(int j=0;j<3;j++){
                int a=i*4+j,b=next*4+j;
                triangles[j==1?0:1+(i/2)%2].AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
            if(i%stride==0 || i%stride==stride/2){
                int index=2*(i/stride)+(i%stride==0?0:1);
                var g=Node("Gate "+(index+1),course.transform);g.SetPositionAndRotation(path[i],Quaternion.LookRotation(tangent,up));course.gates[index]=g;
                if(i%stride==0)foreach(int sign in new[]{-1,1})Mark(g,"Guide post",P(sign*(width+1),1,0),P(.3f,2,.3f),Art("Porcelain"));
            }
        }
        var mesh=new Mesh{name=id+" road surface"};mesh.vertices=vertices;mesh.subMeshCount=3;
        for(int j=0;j<3;j++)mesh.SetTriangles(triangles[j],j);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var mp=Dir+id+"Surface.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(mp);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,mp);
        var road=Node("Road and kerbs",course.transform).gameObject;road.AddComponent<MeshFilter>().sharedMesh=mesh;
        road.AddComponent<MeshRenderer>().sharedMaterials=new[]{Art("Asphalt"),Art("Porcelain"),Art("Lagoon")};road.AddComponent<MeshCollider>().sharedMesh=mesh;
        var grid=Node("Start grid",course.transform);var gate=course.gates[0];grid.SetPositionAndRotation(gate.position+gate.up*.35f,gate.rotation);course.grid=grid;
        for(int col=0;col<16;col++)for(int row=0;row<2;row++)
            Mark(gate,"Finish tile",P(-width+(col+.5f)*width/8,.025f,row*.55f),P(width/8,.035f,.55f),Art((col+row)%2==0?"Porcelain":"Graphite"));
        float floor=path.Min(p=>p.y)-.9f;course.resetHeight=floor-3;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Runoff";ground.transform.position=P(0,floor-.5f,0);ground.transform.localScale=P(900,1,900);
        ground.GetComponent<Renderer>().sharedMaterial=Art("Graphite");
        RoadsideBuilder.Build(course,id,Enumerable.Range(0,n).Select(i=>vertices[4*i]).ToArray(),
            Enumerable.Range(0,n).Select(i=>vertices[4*i+3]).ToArray(),floor);
        if(id=="Ridge")VehicleArtBuilder.DressRidge(course);
        EditorSceneManager.SaveScene(scene,Root+"Scenes/"+id+".unity");Debug.Log($"{layout.title}: {distance:F0} m, {path.Max(p=>p.y)-path.Min(p=>p.y):F1} m relief, {course.gates.Length} gates.");
    }
    [MenuItem("Apex/Bake road courses")]
    public static void Build(){
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save scene edits before baking.");
        Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
        var ridge=Layout("Ridge","山脊大奖赛道",new[]{P(0,2,0),P(0,2,75),P(30,6,120),P(85,12,125),P(110,16,90),P(80,13,57),P(97,8,27),P(151,5,23),P(178,4,-10),P(151,4,-40),P(89,6,-43),P(51,10,-79),P(76,14,-120),P(38,7,-158),P(-30,2,-150),P(-57,2,-99),P(-18,2,-62)});
        var mesa=Layout("Mesa","台地回头弯",new[]{P(0,2,0),P(0,2,65),P(0,7,95),P(0,10,110),P(0,7,130),P(-30,4,167),P(-94,3,154),P(-110,6,108),P(-65,9,86),P(-53,12,54),P(-96,14,30),P(-155,10,39),P(-185,7,-4),P(-150,4,-48),P(-98,3,-41),P(-65,3,-78),P(-83,4,-125),P(-48,4,-151),P(-8,2,-111)});
        Bake("Ridge",ridge);Bake("Mesa",mesa);
        var scene=EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity");
        var s=Object.FindAnyObjectByType<RaceSession>();s.tracks=new[]{
            new RaceSession.TrackChoice{title=ridge.title,scene="Ridge",layout=ridge},new RaceSession.TrackChoice{title=mesa.title,scene="Mesa",layout=mesa},
            new RaceSession.TrackChoice{title="钴蓝椭圆赛道",scene="Circuit"},
            new RaceSession.TrackChoice{title="开放试车场",scene="TestPad",freeDrive=true},
            new RaceSession.TrackChoice{title="驾驶训练场",scene="DrivingSchool",freeDrive=true}};
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var builds=EditorBuildSettings.scenes.ToList();
        foreach(string id in new[]{"Ridge","Mesa"}){string file=Root+"Scenes/"+id+".unity";if(!builds.Any(x=>x.path==file))builds.Add(new EditorBuildSettingsScene(file,true));}
        EditorBuildSettings.scenes=builds.ToArray();AssetDatabase.SaveAssets();
    }
}
}
