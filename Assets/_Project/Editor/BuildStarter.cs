using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace Apex.Editor {
public static class BuildStarter {
    const string root="Assets/_Project/";
    static Material dark,road,line,glass,cyan;
    static Material Mat(string name,Color c){
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=c;
        AssetDatabase.CreateAsset(m,root+"Art/"+name+".mat");return m;
    }
    static GameObject Obj(string name,Transform parent=null){
        var g=new GameObject(name);if(parent)g.transform.SetParent(parent,false);return g;
    }
    static GameObject Box(string n,Transform p,Vector3 pos,Vector3 size,Material m,bool solid=false){
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);
        g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
        if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static Camera View(Vector3 pos,Vector3 at){
        var c=Obj("Camera").AddComponent<Camera>();c.tag="MainCamera";c.transform.position=pos;c.transform.LookAt(at);
        c.AddListener();c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.10f,.16f,.21f);return c;
    }
    static void AddListener(this Camera c){c.gameObject.AddComponent<AudioListener>();}
    static void Light(){
        var l=Obj("Sun").AddComponent<Light>();l.type=LightType.Directional;l.intensity=2;l.transform.rotation=Quaternion.Euler(48,-32,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.64f,.7f);
    }
    static void New(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
    static string PathFor(string n)=>root+"Scenes/"+n+".unity";
    static void Save(string n){EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),PathFor(n));}
    static GameObject Tire(Transform p, Vector3 pos){
        var pivot=Obj("Wheel pivot",p);pivot.transform.localPosition=pos;
        var mesh=GameObject.CreatePrimitive(PrimitiveType.Cylinder);mesh.transform.SetParent(pivot.transform,false);
        mesh.transform.localRotation=Quaternion.Euler(0,0,90);mesh.transform.localScale=new Vector3(.64f,.115f,.64f);
        mesh.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(mesh.GetComponent<Collider>());
        Box("Hub",pivot.transform,Vector3.zero,new Vector3(.24f,.16f,.16f),line);return pivot;
    }
    static RaceCar Entry(string id,Material paint){
        var spec=ScriptableObject.CreateInstance<CarSpec>();spec.displayName=id=="Lagoon"?"R01 · 湖蓝":"R01 · 瓷白";
        var skin=Obj("R01 "+id);Box("Body",skin.transform,new Vector3(0,.52f,0),new Vector3(1.75f,.48f,4.12f),paint);
        Box("Glasshouse",skin.transform,new Vector3(0,.90f,-.18f),new Vector3(1.42f,.37f,1.85f),glass);
        Box("Rear wing",skin.transform,new Vector3(0,.86f,-1.72f),new Vector3(1.77f,.09f,.35f),dark);
        for(int i=0;i<4;i++)Tire(skin.transform,new Vector3((i%2==0?-1:1)*spec.track/2,spec.radius,(i<2?1:-1)*spec.wheelbase/2));
        spec.body=PrefabUtility.SaveAsPrefabAsset(skin,root+"Vehicles/"+id+"Visual.prefab");
        var car=skin.AddComponent<RaceCar>();car.spec=spec;car.wheels=new WheelCollider[4];car.tires=new Transform[4];
        VehicleArtBuilder.AddTyrePlume(car);
        skin.AddComponent<BoxCollider>().center=new Vector3(0,.55f,0);skin.GetComponent<BoxCollider>().size=new Vector3(1.72f,.55f,4);
        skin.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.Interpolate;
        for(int i=0;i<4;i++){
            car.tires[i]=skin.transform.GetChild(3+i);var w=Obj("Suspension "+i,skin.transform);
            w.transform.localPosition=car.tires[i].localPosition+Vector3.up*spec.travel*.5f;car.wheels[i]=w.AddComponent<WheelCollider>();
        }
        AssetDatabase.CreateAsset(spec,root+"Vehicles/"+id+".asset");
        var prefab=PrefabUtility.SaveAsPrefabAsset(skin,root+"Vehicles/"+id+"Car.prefab");Object.DestroyImmediate(skin);return prefab.GetComponent<RaceCar>();
    }
    static void Bootstrap(RaceCar[] entries){
        New();var session=Obj("Race Session").AddComponent<RaceSession>();session.entries=entries;
        session.sounds=AssetDatabase.LoadAssetAtPath<DrivingSounds>(root+"Audio/DrivingSounds.asset");
        session.motionBlurProfile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(root+"Race/HighSpeedBlur.asset");
        session.tyreMarkPrefab=AssetDatabase.LoadAssetAtPath<TyreMarkField>(root+"Race/TyreMarks.prefab");
        var ev=Obj("Event System");ev.AddComponent<EventSystem>();ev.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var ui=new GameObject("Session UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        ui.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scale=ui.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);scale.matchWidthOrHeight=.5f;
        session.panel=SessionPanelBuilder.Place(ui.transform);Save("Bootstrap");
    }
    static void Lobby(){
        New();Light();var room=Obj("Showroom").AddComponent<Showroom>();room.display=Obj("Display slot",room.transform).transform;
        Box("Floor",null,new Vector3(0,-.25f,0),new Vector3(40,.5f,40),road);
        for(int i=-4;i<=4;i++)Box("Bay marking",null,new Vector3(i*3,.001f,0),new Vector3(.035f,.01f,36),cyan);
        View(new Vector3(4,2.2f,5),new Vector3(0,.45f,0));Save("Lobby");
    }
    static void Track(){
        New();Light();View(new Vector3(5,4,0),Vector3.zero);
        var c=Obj("Cobalt Circuit").AddComponent<Circuit>();const int count=48;var points=new Vector3[count];
        for(int i=0;i<count;i++){float a=i*2*Mathf.PI/count;points[i]=new Vector3(35*Mathf.Cos(a),0,55*Mathf.Sin(a));}
        Box("Grass",null,new Vector3(0,-.4f,0),new Vector3(280,.65f,320),dark,true);
        c.mapCenterline=points;
        c.gates=new Transform[8];
        for(int i=0;i<count;i++){
            var a=points[i];var b=points[(i+1)%count];var rot=Quaternion.LookRotation(b-a);var mid=(a+b)/2;
            var seg=Box("Asphalt "+i,c.transform,mid-Vector3.up*.1f,new Vector3(12,.2f,(b-a).magnitude+1.2f),road,true);seg.transform.rotation=rot;
            foreach(int sign in new[]{-1,1}){
                var kerb=Box("Edge",c.transform,mid+rot*Vector3.right*5.8f*sign+Vector3.up*.02f,new Vector3(.4f,.04f,(b-a).magnitude),i%2==0?line:cyan);kerb.transform.rotation=rot;
            }
            if(i%6==0){
                var gate=Obj("Sector "+i,c.transform).transform;gate.position=a;gate.rotation=rot;c.gates[i/6]=gate;
                foreach(int side in new[]{-1,1})Box("Marker",gate,new Vector3(side*6.4f,1.2f,0),new Vector3(.35f,2.4f,.35f),cyan);
            }
        }
        c.grid=Obj("Grid",c.transform).transform;c.grid.SetPositionAndRotation(points[0]+Vector3.up*.15f,c.gates[0].rotation);
        Box("Timing line",c.gates[0],Vector3.up*.025f,new Vector3(11,.04f,.55f),line);
        Save("Circuit");
    }
    [MenuItem("Apex/Create initial scenes")]
    public static void Build(){
        if(File.Exists(PathFor("Bootstrap")))throw new System.InvalidOperationException("Starter scenes already exist.");
        Directory.CreateDirectory(root+"Art");Directory.CreateDirectory(root+"Scenes");AssetDatabase.Refresh();
        dark=Mat("Graphite",new Color(.075f,.095f,.08f));road=Mat("Asphalt",new Color(.19f,.21f,.24f));
        glass=Mat("Smoke glass",new Color(.07f,.13f,.16f));line=Mat("Porcelain",Color.white);cyan=Mat("Lagoon",new Color(.08f,.65f,.58f));
        New();var entries=new[]{Entry("Lagoon",cyan),Entry("Porcelain",line)};
        Lobby();Track();Bootstrap(entries);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(PathFor("Bootstrap"),true),new EditorBuildSettingsScene(PathFor("Lobby"),true),new EditorBuildSettingsScene(PathFor("Circuit"),true)};
        AssetDatabase.SaveAssets();
    }
}
}
