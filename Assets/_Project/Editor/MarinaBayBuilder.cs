using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static Apex.Editor.MarinaStreets;

namespace Apex.Editor {
public static class MarinaBayBuilder {
    const string Folder="Assets/_Project/Tracks/MarinaBay",ScenePath="Assets/_Project/Scenes/MarinaBay.unity";
    internal const int RouteRevision=2;
    internal const string Title="滨海湾 · 霓虹夜赛";
    const int White=0xfff5da,Cyan=0x68e7ff,Pink=0xff559a,Steel=0x586675,Concrete=0x9696a1;
    static readonly Vector3 Up=Vector3.up;
    static Transform Node(string name,Transform parent=null){
        var t=new GameObject(name).transform;t.SetParent(parent,false);return t;
    }
    static Material MaterialAt(string name,string shader,float surface=0){
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        var target=Shader.Find(shader);
        if(!m){m=new Material(target){name=name};AssetDatabase.CreateAsset(m,path);}
        else if(m.shader!=target)m.shader=target;
        if(m.HasProperty("_Surface"))m.SetFloat("_Surface",surface);
        EditorUtility.SetDirty(m);return m;
    }
    static void Panel(MarinaGeometry art,Vector3 p,Vector3 f,string label,float width,float height,int accent){
        var right=Vector3.Cross(Up,f);
        art.Box(p,width,height,.4f,0x17253b,f);
        art.Box(p+Up*(height*.45f)-f*.23f,width*.96f,.15f,.11f,accent,f);
        art.Box(p-Up*(height*.45f)-f*.23f,width*.96f,.12f,.11f,accent,f);
        float px=Mathf.Min(width*.74f/(label.Length*6),height*.60f/7);
        art.Lettering(label,p+right*width*.04f-f*.24f,f,px,White);
        for(int i=0;i<3;i++){
            var a=p-right*width*.45f+right*(i*.43f)-f*.26f;
            art.Beam(a-Up*height*.25f,a+right*.65f+Up*height*.25f,.15f,accent);
        }
    }
    static void MainRoad(MarinaStreets map,MarinaRiver river,Circuit course,Material city,Material pavement){
        var surface=new MarinaGeometry();var walls=new MarinaGeometry();var details=new MarinaGeometry();
        var verge=new MarinaGeometry();
        var meshFence=new MarinaGeometry();var lamps=new List<Vector3>();var line=map.main;int n=line.Length;
        float ds=LapMetres/n;int lastLight=-1;var gates=Node("Timing lines",course.transform);
        for(int i=0;i<n;i++){
            int j=(i+1)%n;var a=line[i];var b=line[j];var f=Forward(line,i);
            var side=Vector3.Cross(Up,f);var nextSide=Vector3.Cross(Up,Forward(line,j));
            float w=map.Width(a)*.5f,v=map.Width(b)*.5f;
            surface.Quad(a-side*w,b-nextSide*v,b+nextSide*v,a+side*w,0x747882,i*ds,(i+1)*ds);
            bool street=!river.BridgeReserve(i,MarinaBridgeRoads.Approach);
            if(street)verge.Quad(a-side*w-Up*.04f,b-nextSide*v-Up*.04f,b+nextSide*v-Up*.04f,a+side*w-Up*.04f,0x555a5e);
            for(int sign=-1;sign<=1;sign+=2){
                var left=side*sign;var right=nextSide*sign;
                var p=a+left*w;var q=b+right*v;
                var outside=p+left*.45f;var far=q+right*.45f;
                if(street){
                    var kerb=p+left*3.55f;var endKerb=q+right*3.55f;
                    if(sign==1){
                        verge.Quad(p-Up*.04f,q-Up*.04f,endKerb-Up*.04f,kerb-Up*.04f,0x8e8c85);
                        verge.Quad(kerb-Up*.04f,endKerb-Up*.04f,endKerb-Up*.27f,kerb-Up*.27f,0x76797a);
                    }else{
                        verge.Quad(kerb-Up*.04f,endKerb-Up*.04f,q-Up*.04f,p-Up*.04f,0x8e8c85);
                        verge.Quad(kerb-Up*.27f,endKerb-Up*.27f,endKerb-Up*.04f,kerb-Up*.04f,0x76797a);
                    }
                    if(i%2==0)details.Beam(outside-Up*.027f,kerb-Up*.027f,.014f,0x686c6e,4);
                }
                if(sign==1)walls.Quad(p,q,q+Up,p+Up,Concrete);
                else walls.Quad(q,p,p+Up,q+Up,Concrete);
                if(sign==1)walls.Quad(p+Up,q+Up,far+Up,outside+Up,0xc9c4ba);
                else walls.Quad(outside+Up,far+Up,q+Up,p+Up,0xc9c4ba);
                if(sign==1)walls.Quad(far-Up*.10f,outside-Up*.10f,outside+Up,far+Up,0x85858d);
                else walls.Quad(outside-Up*.10f,far-Up*.10f,far+Up,outside+Up,0x85858d);
                var fwd=(q-p).normalized;
                details.Box((p+q)*.5f-left*.31f+Up*.012f,.15f,.014f,Vector3.Distance(p,q)+.02f,0xf0ecdb,fwd);
                float turn=Vector3.SignedAngle(Forward(line,(i+n-2)%n),Forward(line,(i+2)%n),Up);
                if(turn*sign>5){
                    details.Box((p+q)*.5f-left*.67f+Up*.025f,.55f,.028f,Vector3.Distance(p,q),i%2==0?0xf3ded2:0xc75364,fwd);
                }
                if(i%3==0){
                    details.Beam(outside+Up,outside+Up*2.9f,.045f,Steel);
                }
                if(sign==1)meshFence.Quad(outside+Up,far+Up,far+Up*2.85f,outside+Up*2.85f,0x8998ac,i*ds,(i+1)*ds);
                else meshFence.Quad(far+Up,outside+Up,outside+Up*2.85f,far+Up*2.85f,0x8998ac,(i+1)*ds,i*ds);
                foreach(float y in new[]{1.55f,2.85f})details.Beam(outside+Up*y,far+Up*y,.026f,0x8495a4,3);
                if(i%6<3){
                    var mid=(p+q)*.5f-left*.025f+Up*.48f;
                    details.Box(mid,.055f,.32f,Vector3.Distance(p,q)*.91f,
                        i/96%3==0?0x38576a:i/96%3==1?0x9c517b:0x386475,fwd);
                }
            }
            int span=Mathf.FloorToInt(i*ds/32);
            if(span!=lastLight){
                lastLight=span;int sign=span%2==0?-1:1;
                var basePos=a+side*sign*(w+1);
                details.Box(basePos+Up*.025f,.68f,.27f,.68f,0x83878b,f);
                var top=basePos+Up*10.4f;var end=a+Up*10+side*sign*1.8f;
                details.Beam(basePos,top,.13f,Steel);details.Beam(top,end,.14f,Steel);
                details.Beam(basePos+Up*8.7f,Vector3.Lerp(top,end,.38f),.075f,Steel);
                for(int k=0;k<3;k++){
                    var head=end+side*sign*(k*1.50f);
                    details.Box(head,1.1f,.36f,1.2f,0x667482,f);
                    details.Box(head-Up*.20f,1,.09f,1.1f,0xfff9ec,f);
                }
                lamps.Add(end-Up*.45f);
            }
        }
        var road=surface.Save(Folder,"Grand Prix asphalt",course.transform,pavement,true);
        road.AddComponent<TyreSurface>().cover=RoadCover.Asphalt;
        walls.Save(Folder,"Concrete race barriers",course.transform,city,true);
        verge.Save(Folder,"Road foundations and public footpaths",course.transform,city,true);
        course.mapCenterline=line;course.gates=new Transform[map.gateIndices.Length];course.gateLateralLimits=new Vector2[course.gates.Length];
        for(int g=0;g<course.gates.Length;g++){
            int i=map.gateIndices[g];var t=Node(g==0?"Start finish":"Sector line "+g,gates);
            t.SetPositionAndRotation(line[i],Quaternion.LookRotation(Forward(line,i)));course.gates[g]=t;
            float w=map.Width(line[i])*.5f;course.gateLateralLimits[g]=new Vector2(-w,w);
        }
        course.grid=Node("Starting position",course.transform);
        course.grid.SetPositionAndRotation(line[0]+Up*.35f,course.gates[0].rotation);
        course.laps=1;course.gateHalfWidth=map.Width(line[0])*.5f;course.resetHeight=-6;
        var start=course.gates[0];
        for(int g=1;g<course.gates.Length;g++){
            int corner=map.gateIndices[g],count=line.Length;
            int look=Mathf.RoundToInt(38/ds);
            float bend=Vector3.SignedAngle(Forward(line,(corner+count-look)%count),Forward(line,(corner+look)%count),Up);
            if(Mathf.Abs(bend)<24)continue;
            foreach(int metres in new[]{100,50}){
                int i=(corner+count-Mathf.RoundToInt(metres/ds))%count;var dir=Forward(line,i);
                var side=Vector3.Cross(Up,dir)*-Mathf.Sign(bend);
                var p=line[i]+side*(map.Width(line[i])*.5f+1.5f)+Up*2.35f;
                details.Beam(p-Up*2.43f,p-Up*.49f,.065f,Steel);
                details.Box(p,1.85f,1.05f,.12f,0xdfe3dc,dir);
                details.Lettering(metres.ToString(),p-dir*.075f,dir,.088f,0x262c36);
            }
        }
        float startWidth=map.Width(start.position);int squares=Mathf.RoundToInt(startWidth/.53f);
        float cell=startWidth/squares;
        for(int x=0;x<squares;x++)for(int row=0;row<2;row++)
            details.Box(start.TransformPoint(new Vector3((x+.5f-squares*.5f)*cell,.022f,row*cell)),cell,.02f,cell,
                (x+row)%2==0?White:0x333945,start.forward);
        for(int i=1;i<=16;i++){
            var pos=start.position-start.forward*(i*6)+start.right*(i%2==0?3:-3);
            details.Box(pos+Up*.02f,2.4f,.012f,.12f,White,start.forward);
            foreach(int s in new[]{-1,1})details.Box(pos+start.right*s*1.14f-start.forward*.6f+Up*.02f,.12f,.012f,1.2f,White,start.forward);
        }
        var floods=course.gameObject.AddComponent<MarinaFloodlights>();floods.fixtures=lamps.ToArray();
        details.Save(Folder,"Floodlight gantries fencing and paint",course.transform,city);
        meshFence.Save(Folder,"Catch fencing",course.transform,MaterialAt("Catch fence","Apex/Marina Catch Fence"));
    }
    static void Trackside(MarinaStreets map,MarinaRiver river,Circuit course,Material city){
        var art=new MarinaGeometry();var gate=course.gates[0];var f=gate.forward;var r=gate.right;
        var pit=gate.position-r*34;art.Box(pit+Up*5.5f,22,11,238,0x39475c,f);
        art.Box(pit+Up*12,28,1.8f,246,0xb7bac8,f);
        for(int i=-9;i<=9;i++){
            var front=pit+r*11.4f+f*(i*12);
            art.Box(front+Up*3,1,5.4f,10,0x637b89,f);art.Box(front+Up*8.9f,1.2f,3,10.8f,0x9fb2bc,f);
            art.Box(front+Up*6.35f,1.25f,.44f,10.8f,i%3==0?Pink:Cyan,f);
            for(int j=0;j<5;j++)art.Box(front+r*.54f+Up*(.95f+j*.87f),.085f,.09f,9.6f,Steel,f);
            for(int j=-2;j<=2;j++)art.Box(front+r*.65f+Up*8.9f+f*(j*2.12f),.11f,3,.12f,Steel,f);
        }
        for(int i=-3;i<=3;i++)MarinaWaterfront.Stand(art,gate.position+r*(map.Width(gate.position)*.5f+5)+f*(i*34),r,33,13);
        var arch=gate.position+f*22;
        float postOffset=map.Width(gate.position)*.5f+2.8f;
        foreach(int s in new[]{-1,1})art.Beam(arch+r*s*postOffset,arch+r*s*postOffset+Up*9.8f,.44f,Steel);
        Panel(art,arch+Up*8.8f,f,"MARINA BAY",postOffset*2+.8f,3.3f,Cyan);
        for(int i=0;i<5;i++)art.Box(arch+Up*6.75f+r*((i-2)*.64f),.41f,.36f,.58f,0x694452,f);
        art.Save(Folder,"Pit lane grandstands and start gantry",course.transform,city);
        art=new MarinaGeometry();
        string[] signs={"PIT","ION","AFTERGLOW","NIGHT SHIFT","ARCADE","PADANG","RIVER","FULLERTON","TIDAL","BAYFRONT","MARINA"};
        for(int j=0;j<signs.Length;j++){
            int index=map.gateIndices[new[]{1,2,3,4,5,6,7,8,10,12,14}[j]];
            int i=(index+map.main.Length-12)%map.main.Length;
            f=Forward(map.main,i);r=Vector3.Cross(Up,f);int s=j%2==0?1:-1;
            var foot=map.main[i]+r*s*(map.Width(map.main[i])*.5f+13);
            if(!river.Dry(foot,1) || river.BridgeReserve(i,38))continue;
            art.Beam(foot,foot+Up*9,.45f,Steel);
            Panel(art,foot+Up*9,f,signs[j],15,6,j%2==0?Pink:Cyan);
        }
        int muralStation=map.gateIndices[5]-28; f=Forward(map.main,muralStation);r=Vector3.Cross(Up,f);
        var mural=map.main[muralStation]+r*14+Up*2.4f;art.Box(mural,1,4.8f,22,0x394052,f);
        for(int j=0;j<14;j++){
            var a=mural-r*.56f+f*(j*1.45f-10)-Up*(.5f+Mathf.Sin(j)*.72f);
            art.Beam(a,a+f*1.4f+Up*(1.4f+Mathf.Cos(j)*.7f),.32f,j%3==0?White:j%3==1?Pink:Cyan);
        }
        art.Save(Folder,"Original neon advertisements and market mural",course.transform,city);
        art=new MarinaGeometry();var screens=new MarinaGeometry();
        int[] monitorGates={2,4,6,10,13};
        foreach(int g in monitorGates){
            int i=(map.gateIndices[g]+map.main.Length-17)%map.main.Length;f=Forward(map.main,i);r=Vector3.Cross(Up,f);
            var p=map.main[i]+r*(g%2==0?22:-22)+Up*13;
            if(!river.Dry(p,2) || river.BridgeReserve(i,38))continue;
            art.Box(p,19,9,.7f,0x1f3f60,f);var display=p-f*.40f;
            screens.Quad(display-r*9-Up*4,display-r*9+Up*4,display+r*9+Up*4,display+r*9-Up*4,White,0,1);
            art.Beam(p-Up*4,p-Up*13,.7f,Steel);art.Box(p+Up*4.6f,18,.18f,.8f,Cyan,f);
        }
        screens.Save(Folder,"Animated blue advertising screens",course.transform,MaterialAt("Blue hologram","Apex/Marina Night",3));
        art.Save(Folder,"Display structures",course.transform,city);
    }
    static void Night(Circuit course,Material sky){
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.39f,.43f,.54f);RenderSettings.fog=false;
        var fill=Node("Cool night fill",course.transform).gameObject.AddComponent<Light>();
        fill.type=LightType.Directional;fill.color=new Color(.77f,.85f,1);fill.intensity=.95f;fill.transform.rotation=Quaternion.Euler(52,-36,0);
        var camera=Node("Main Camera",course.transform).gameObject.AddComponent<Camera>();camera.tag="MainCamera";
        camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=4100;camera.nearClipPlane=.15f;
        camera.transform.SetPositionAndRotation(course.grid.position-course.grid.forward*8+Up*3,course.grid.rotation);
        camera.gameObject.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        string path=Folder+"/Floodlight bloom.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile){
            profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);
            var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(.84f);bloom.intensity.Override(.32f);
            bloom.scatter.Override(.62f);AssetDatabase.AddObjectToAsset(bloom,profile);EditorUtility.SetDirty(profile);
        }
        var volume=Node("Night exposure",course.transform).gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
    }
    static void Register(){
        const string bootPath="Assets/_Project/Scenes/Bootstrap.unity";
        var boot=SceneManager.GetSceneByPath(bootPath);bool opened=!boot.isLoaded;
        if(opened)boot=EditorSceneManager.OpenScene(bootPath,OpenSceneMode.Additive);
        try{
            var session=boot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RaceSession>()).Single();
            var entry=session.tracks.FirstOrDefault(t=>t.scene=="MarinaBay");
            if(entry==null || entry.routeRevision!=RouteRevision){
                if(entry==null){
                    entry=new RaceSession.TrackChoice{title=Title,scene="MarinaBay"};
                    var tracks=session.tracks.ToList();tracks.Insert(1,entry);session.tracks=tracks.ToArray();
                }
                entry.routeRevision=RouteRevision;EditorSceneManager.MarkSceneDirty(boot);
                if(!EditorSceneManager.SaveScene(boot))throw new InvalidOperationException("Could not register Marina Bay in Bootstrap.");
            }
        }finally{if(opened)EditorSceneManager.CloseScene(boot,true);}
        var builds=EditorBuildSettings.scenes.ToList();
        if(!builds.Any(b=>b.path==ScenePath)){builds.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=builds.ToArray();}
    }
    [MenuItem("Apex/Maps/Bake Marina Bay night circuit")]
    public static void Build(){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Bake in Edit Mode.");
        var original=SceneManager.GetActiveScene();var previous=SceneManager.GetSceneByPath(ScenePath);
        var boot=SceneManager.GetSceneByName("Bootstrap");
        if(previous.isLoaded || (boot.isLoaded && boot.isDirty))throw new InvalidOperationException("Close the target scene and save pending garage edits before baking.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Project/Tracks","MarinaBay");
        var city=MaterialAt("City night","Apex/Marina Night");var asphalt=MaterialAt("Track at night","Apex/Marina Night",1);
        var water=MaterialAt("Bay reflections","Apex/Marina Night",2);var sky=MaterialAt("Midnight sky","Apex/Marina Sky");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try{
            SceneManager.SetActiveScene(scene);var course=Node(Title).gameObject.AddComponent<Circuit>();var map=new MarinaStreets();
            var river=new MarinaRiver(map);
            river.Build(Folder,course.transform,MaterialAt("Paved city ground","Apex/Marina Night",5),city,water);
            MainRoad(map,river,course,city,asphalt);Trackside(map,river,course,city);
            MarinaBridgeRoads.Build(map,river,Folder,course.transform,city);
            new MarinaWaterfront(map,river,course.transform,Folder,city,water).Build();Night(course,sky);
            new MarinaRaceNight(map,river,course.transform,Folder,city).Build();
            new MarinaCityDistricts(map,river,course.transform,Folder,MaterialAt("City district architecture","Apex/Marina District")).Build();
            BakeRoadPaint(course);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Could not save Marina Bay.");
        }finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
        Register();AssetDatabase.SaveAssets();
    }

    public static void BakeRoadPaint(Circuit course){
        var material=MaterialAt("Road marking paint","Apex/Marina Road Paint");
        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Road lettering.png");
        if(!material.mainTexture)throw new InvalidOperationException("Missing Marina road lettering atlas.");
        new MarinaRoadPaint(course).Bake(Folder,material);
    }
}
}
