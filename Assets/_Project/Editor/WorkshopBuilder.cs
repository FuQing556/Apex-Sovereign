using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Apex.Editor {
public static class WorkshopBuilder {
    const string Dir="Assets/_Project/Lobby/Parts/";
    const string Vehicles="Assets/_Project/Vehicles/";
    const string Lobby="Assets/_Project/Scenes/Lobby.unity";
    static Material carbon,steel,gold,red,black,white,glow;
    static T Load<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path);
    static Material Finish(string name)=>Load<Material>(Vehicles+"SHIBATA31/Materials/"+name+".mat");

    // Bake by material so the detailed bench and stamped wheel covers use few draw calls.
    sealed class Assembly {
        readonly Dictionary<Material,List<CombineInstance>> batches=new();
        readonly List<Mesh> generated=new();
        static readonly Dictionary<PrimitiveType,Mesh> primitives=new();
        public void Add(Mesh mesh,Material mat,Vector3 p,Quaternion q,Vector3 size){
            if(!batches.TryGetValue(mat,out var list))batches.Add(mat,list=new List<CombineInstance>());
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,q,size)});
        }
        void Primitive(PrimitiveType type,Material mat,Vector3 p,Quaternion q,Vector3 size){
            if(!primitives.TryGetValue(type,out var mesh)){
                var temp=GameObject.CreatePrimitive(type);mesh=temp.GetComponent<MeshFilter>().sharedMesh;
                primitives[type]=mesh;Object.DestroyImmediate(temp);
            }
            Add(mesh,mat,p,q,size);
        }
        public void Box(Material mat,Vector3 p,Vector3 size,float tilt=0)=>
            Primitive(PrimitiveType.Cube,mat,p,Quaternion.Euler(tilt,0,0),size);
        public void Shaft(Material mat,Vector3 p,float radius,float length)=>
            Primitive(PrimitiveType.Cylinder,mat,p,Quaternion.Euler(0,0,90),new Vector3(radius*2,length*.5f,radius*2));
        public void BevelGear(Vector3 centre,Vector3 axis,float radius,int teeth){
            var rotation=Quaternion.FromToRotation(Vector3.right,axis);
            var v=new List<Vector3>();var indices=new List<int>();
            Vector3 Point(int i,int layer){
                float a=i*2*Mathf.PI/(teeth*4);
                float r=radius*(i%4==1 || i%4==2?1:.84f)*(layer==0?.59f:1);
                return new Vector3((layer==0?-.17f:.17f)*radius,r*Mathf.Cos(a),r*Mathf.Sin(a));
            }
            for(int i=0;i<teeth*4;i++){
                int n=v.Count;v.AddRange(new[]{Point(i,0),Point(i,1),Point(i+1,1),Point(i+1,0),new Vector3(.17f*radius,0,0)});
                indices.AddRange(new[]{n,n+3,n+2,n,n+2,n+1,n+1,n+2,n+4});
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
            generated.Add(mesh);Add(mesh,gold,centre,rotation,Vector3.one);
        }
        // Annular sector around local X, with solid back and machined side walls.
        public void Ring(Material mat,Vector3 center,float inner,float outer,float depth,float start=0,float span=360,int segments=64){
            var v=new List<Vector3>();var indices=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){
                int n=v.Count;v.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            Vector3 P(int n,float radius,float face){
                float angle=(start+span*n/segments)*Mathf.Deg2Rad;
                return new Vector3(face*depth*.5f,radius*Mathf.Cos(angle),radius*Mathf.Sin(angle));
            }
            for(int i=0;i<segments;i++){
                int j=i+1;
                Quad(P(i,inner,1),P(i,outer,1),P(j,outer,1),P(j,inner,1));
                Quad(P(j,inner,-1),P(j,outer,-1),P(i,outer,-1),P(i,inner,-1));
                Quad(P(i,outer,1),P(i,outer,-1),P(j,outer,-1),P(j,outer,1));
                Quad(P(j,inner,1),P(j,inner,-1),P(i,inner,-1),P(i,inner,1));
            }
            if(span<360){
                Quad(P(0,inner,-1),P(0,outer,-1),P(0,outer,1),P(0,inner,1));
                Quad(P(segments,inner,1),P(segments,outer,1),P(segments,outer,-1),P(segments,inner,-1));
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
            generated.Add(mesh);Add(mesh,mat,center,Quaternion.identity,Vector3.one);
        }
        public GameObject Save(string name){
            var root=new GameObject(name);
            try{
                foreach(var group in batches){
                    var mesh=new Mesh{name=name+" "+group.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                    mesh.CombineMeshes(group.Value.ToArray());mesh.RecalculateBounds();
                    string path=Dir+mesh.name+".asset";
                    var saved=Load<Mesh>(path);
                    if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                    else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
                    var child=new GameObject(group.Key.name);child.transform.SetParent(root.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=saved;child.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Dir+name+".prefab");
            }finally{Object.DestroyImmediate(root);foreach(var mesh in generated)Object.DestroyImmediate(mesh);}
        }
    }
    static GameObject WheelDisc(){
        var a=new Assembly();
        a.Ring(carbon,Vector3.zero,.31f,.74f,.035f);
        a.Ring(steel,Vector3.zero,.94f,1,.032f);
        a.Ring(steel,new Vector3(.018f,0,0),.30f,.335f,.023f);
        for(int i=0;i<12;i++){
            a.Ring(carbon,Vector3.zero,.72f,.955f,.04f,i*30+4,21,6);
            float rad=i*30*Mathf.Deg2Rad;
            a.Shaft(steel,new Vector3(.025f,.85f*Mathf.Cos(rad+.20f),.85f*Mathf.Sin(rad+.20f)),.022f,.025f);
        }
        a.Ring(red,new Vector3(.025f,0,0),.51f,.71f,.01f,5,48,12);
        a.Ring(white,new Vector3(.025f,0,0),.51f,.71f,.01f,57,7,3);
        return a.Save("后轮通风盘");
    }
    static GameObject Differential(string name,int plates){
        var a=new Assembly();var axis=new Vector3(0,.158f,0);
        a.Box(black,new Vector3(0,.012f,0),new Vector3(.42f,.024f,.25f));
        foreach(int side in new[]{-1,1})
            a.Box(steel,new Vector3(side*.14f,.055f,0),new Vector3(.027f,.095f,.12f));
        a.Shaft(steel,axis,.036f,.39f);
        a.Ring(black,axis+Vector3.left*.09f,.071f,.135f,.038f);
        for(int i=0;i<38;i++)a.Ring(steel,axis+Vector3.left*.09f,.13f,.147f,.039f,i*360f/38,5,2);
        foreach(int side in new[]{-1,1}){
            a.Ring(steel,axis+Vector3.right*(side*.068f),.038f,.089f,.024f);
            for(int i=0;i<plates;i++)a.Ring(i%2==0?gold:steel,axis+Vector3.right*(side*(.094f+.004f*i)),.042f,.073f,.003f);
        }
        for(int i=0;i<4;i++){
            float ang=i*Mathf.PI/2;
            a.Shaft(black,axis+new Vector3(0,.074f*Mathf.Cos(ang),.074f*Mathf.Sin(ang)),.011f,.14f);
        }
        foreach(int side in new[]{-1,1}){
            a.BevelGear(axis+Vector3.right*(side*.045f),Vector3.right*side,.046f,16);
            a.BevelGear(axis+Vector3.up*(side*.036f),Vector3.up*side,.032f,12);
        }
        return a.Save(name);
    }
    static GameObject ServiceBay(){
        var a=new Assembly();
        a.Box(black,new Vector3(0,.002f,0),new Vector3(5,.012f,6.7f));
        for(int s=-1;s<=1;s+=2){
            a.Box(gold,new Vector3(s*2.43f,.014f,0),new Vector3(.045f,.008f,6.5f));
            for(int i=0;i<4;i++)a.Box(white,new Vector3(s*2.26f,.015f,-2.6f+i*1.7f),new Vector3(.27f,.008f,.04f));
        }
        for(int i=-4;i<=4;i++){
            a.Box(black,new Vector3(i*1.33f,1.77f,-4.95f),new Vector3(1.30f,3.55f,.12f));
            a.Box(steel,new Vector3(i*1.33f,1.22f,-4.86f),new Vector3(1.29f,.045f,.06f));
        }
        a.Box(red,new Vector3(0,2.55f,-4.84f),new Vector3(11.8f,.19f,.045f));
        a.Box(glow,new Vector3(0,3.32f,-4.79f),new Vector3(11,.06f,.09f));
        // The roller door and high windows close the right edge of the overview.
        a.Box(black,new Vector3(-5.92f,1.8f,1.4f),new Vector3(.16f,3.65f,12.9f));
        for(int i=0;i<22;i++)
            a.Box(i%4==0?black:carbon,new Vector3(-5.79f,.10f+i*.12f,1.85f),new Vector3(.06f,.114f,4.75f));
        for(int i=0;i<6;i++){
            float z=-3.88f+2.10f*i;
            a.Box(steel,new Vector3(-5.77f,1.8f,z),new Vector3(.12f,3.6f,.10f));
            a.Box(carbon,new Vector3(-5.78f,2.99f,z+.88f),new Vector3(.07f,.57f,1.48f));
            a.Box(glow,new Vector3(-5.72f,3.13f,z+.88f),new Vector3(.035f,.10f,1.38f));
        }
        for(int i=0;i<3;i++)
            a.Box(black,new Vector3(0,3.73f,-4.75f+i*4.05f),new Vector3(12,.18f,.15f));
        a.Box(carbon,new Vector3(0,3.95f,1.3f),new Vector3(12.4f,.12f,12.8f));
        for(int i=0;i<4;i++){
            float x=-4.8f+i*.87f;
            a.Box(black,new Vector3(x,.48f,-4.05f),new Vector3(.84f,.94f,.73f));
            for(int j=0;j<5;j++){
                a.Box(i==0?red:carbon,new Vector3(x,.18f+j*.16f,-3.672f),new Vector3(.77f,.14f,.038f));
                a.Box(steel,new Vector3(x,.21f+j*.16f,-3.635f),new Vector3(.49f,.017f,.034f));
            }
        }
        a.Box(steel,new Vector3(-3.5f,.99f,-4.05f),new Vector3(3.6f,.07f,.9f));
        for(int i=0;i<6;i++)a.Ring(black,new Vector3(1.08f+i*.30f,.60f,-4.26f),.218f,.342f,.245f);
        a.Box(steel,new Vector3(1.86f,.30f,-4.3f),new Vector3(2.2f,.085f,.56f));
        for(int i=0;i<2;i++)a.Box(steel,new Vector3(.80f+i*2.12f,.37f,-4.3f),new Vector3(.05f,.74f,.62f));
        for(int i=0;i<4;i++){
            float x=-5.5f+i*3.5f;
            a.Box(black,new Vector3(x,3.58f,-1.8f),new Vector3(.16f,.16f,6.4f));
            a.Box(glow,new Vector3(x,3.48f,-1.8f),new Vector3(.072f,.04f,5.5f));
        }
        return a.Save("维修工位");
    }
    public static void Build(){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请在编辑模式生成车库。");
        if(!AssetDatabase.IsValidFolder(Dir.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/_Project/Lobby","Parts");
        carbon=Finish("Graphite composite");steel=Finish("Machined aluminium");gold=Finish("Satin champagne");
        red=Finish("Candy scarlet");black=Finish("Gunmetal anodised");white=Finish("Ivory vinyl");glow=Finish("Projector LED");
        var disc=WheelDisc();var bay=ServiceBay();
        var stock=Differential("多片式后桥",7);var soft=Differential("低耦合后桥",3);var open=Differential("开放式后桥",0);
        var catalog=Load<WorkshopCatalog>(Dir+"Workshop.asset");
        if(!catalog){
            catalog=ScriptableObject.CreateInstance<WorkshopCatalog>();
            catalog.differentials=new[]{
                new WorkshopCatalog.Part{id="original",title="基准限滑",transferFraction=1,displayModel=stock,description="沿用本车的限滑设定。驱动轮有转速差时，差速器把部分扭矩传给较慢的一侧。"},
                new WorkshopCatalog.Part{id="club-lsd",title="柔性限滑",transferFraction=.5f,displayModel=soft,description="项目试制件。左右轮扭矩转移量为基准的 50%，允许更大的轮速差。"},
                new WorkshopCatalog.Part{id="open",title="开放式差速器",transferFraction=0,displayModel=open,description="左右驱动轮各分一半扭矩，可不同速转动。单轮压草时，低抓地侧容易空转。"}};
            catalog.wheels=new[]{
                new WorkshopCatalog.Part{id="original",title="原车轮面",description="保留原有轮辐与制动盘外观。"},
                new WorkshopCatalog.Part{id="vent-disc",title="通风气动盘",rearWheelCover=disc,description="项目原创后轮盘面，十二组径向开口、金属外圈和红色定位标记。此件仅改变外观。"}};
            AssetDatabase.CreateAsset(catalog,Dir+"Workshop.asset");
        }
        foreach(var type in new[]{"Car","Visual"})FitSockets(Vehicles+"SHIBATA31/Okuibuki"+type+".prefab",true);
        const string ui="Assets/_Project/UI/SessionPanel.prefab";
        var panelRoot=PrefabUtility.LoadPrefabContents(ui);
        try{SessionPanelBuilder.AddWorkshop(panelRoot.GetComponent<SessionPanel>());PrefabUtility.SaveAsPrefabAsset(panelRoot,ui);}
        finally{PrefabUtility.UnloadPrefabContents(panelRoot);}
        var scene=SceneManager.GetSceneByPath(Lobby);bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(Lobby,OpenSceneMode.Additive);
        try{
            var room=scene.GetRootGameObjects().Select(o=>o.GetComponent<Showroom>()).First(o=>o);
            if(!room.bench){
                var furniture=(GameObject)PrefabUtility.InstantiatePrefab(bay,room.transform);
                var bench=new GameObject("差速器展示位").transform;bench.SetParent(furniture.transform,false);
                bench.localPosition=new Vector3(-2.65f,1.027f,-4.03f);room.bench=bench;
            }
            room.overviewEye=new Vector3(3.25f,1.45f,3.65f);
            foreach(var o in scene.GetRootGameObjects().Where(o=>o.name=="Bay marking"))
                o.GetComponent<Renderer>().enabled=false;
            if(!room.transform.Find("工位照明")){
                var light=new GameObject("工位照明",typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(room.transform,false);light.transform.localPosition=new Vector3(1.4f,3.2f,1.7f);
                light.type=LightType.Point;light.range=7;light.intensity=3;light.color=new Color(1,.88f,.74f);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        AssetDatabase.SaveAssets();
    }
    static void FitSockets(string path,bool competition){
        var root=PrefabUtility.LoadPrefabContents(path);
        try{
            if(root.GetComponent<WorkshopMounts>())return;
            var mount=root.AddComponent<WorkshopMounts>();
            if(root.TryGetComponent<RaceCar>(out var car)){mount.rearLeft=car.tires[2];mount.rearRight=car.tires[3];}
            else if(competition){mount.rearLeft=root.transform.Find("RL wheel pose");mount.rearRight=root.transform.Find("RR wheel pose");}
            else{
                var rear=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Wheel pivot" && t.localPosition.z<0).OrderBy(t=>t.localPosition.x).ToArray();
                mount.rearLeft=rear[0];mount.rearRight=rear[1];
            }
            if(!mount.rearLeft || !mount.rearRight)throw new InvalidDataException(path);
            mount.coverRadius=competition?.254f:.205f;mount.faceOffset=competition?.148f:.125f;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
}
