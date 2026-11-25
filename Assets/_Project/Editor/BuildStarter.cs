using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace Apex.Editor {
public static class BuildStarter {
    const string root="Assets/_Project/";
    static Material road,cyan;
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
    static void Bootstrap(RaceCar[] entries){
        New();var session=Obj("Race Session").AddComponent<RaceSession>();session.entries=entries;
        session.tracks=new[]{
            new RaceSession.TrackChoice{title="山脊大奖赛道",scene="Ridge",layout=AssetDatabase.LoadAssetAtPath<RoadLayout>(root+"Tracks/Ridge.asset")},
            new RaceSession.TrackChoice{title=MarinaBayBuilder.Title,scene="MarinaBay",routeRevision=MarinaBayBuilder.RouteRevision}
        };
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
    [MenuItem("Apex/Create initial scenes")]
    public static void Build(){
        if(File.Exists(PathFor("Bootstrap")))throw new System.InvalidOperationException("Starter scenes already exist.");
        var car=AssetDatabase.LoadAssetAtPath<RaceCar>(root+"Vehicles/SHIBATA31/OkuibukiCar.prefab");
        if(!car)throw new System.InvalidOperationException("Restore the 31 GR86 prefab before creating the garage.");
        if(!File.Exists(PathFor("Lobby"))){
            road=AssetDatabase.LoadAssetAtPath<Material>(root+"Art/Asphalt.mat");
            cyan=AssetDatabase.LoadAssetAtPath<Material>(root+"Art/Lagoon.mat");
            Lobby();
        }
        var lynk=AssetDatabase.LoadAssetAtPath<RaceCar>(ZhuzhouCarBuilder.CarPath);
        if(!lynk)throw new System.InvalidOperationException("Build the Zhuzhou 155 prefab before creating the garage.");
        Bootstrap(new[]{car,lynk});
        EditorBuildSettings.scenes=System.Array.ConvertAll(new[]{"Bootstrap","Lobby","Ridge","MarinaBay"},s=>new EditorBuildSettingsScene(PathFor(s),true));
        AssetDatabase.SaveAssets();
    }
}
}
