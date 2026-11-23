using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Apex.Editor {
public static class DrivingSchoolBuilder {
    const string Root="Assets/_Project/";
    const string Folder=Root+"Vehicles/SHIBATA31/";
    static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path);
    [MenuItem("Apex/Build driving school and reference car")]
    public static void Build(){
        if(EditorApplication.isPlaying || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save open scenes before building.");
        var old=EditorSceneManager.GetSceneManagerSetup();
        try {
            var path=Root+"Scenes/DrivingSchool.unity";
            if(!Load<SceneAsset>(path) && !AssetDatabase.CopyAsset(Root+"Scenes/TestPad.unity",path))
                throw new InvalidOperationException("Cannot create school scene");
            var track=EditorSceneManager.OpenScene(path);
            var course=Object.FindAnyObjectByType<Circuit>();
            course.name="Driving School";
            course.grid.position=new Vector3(0,.35f,-12);
            var lines=GameObject.Find("50 m grid and turn circles");
            if(lines)lines.SetActive(false);
            var school=course.GetComponent<DrivingSchool>()??course.gameObject.AddComponent<DrivingSchool>();
            school.lanePaint=Load<Material>(Root+"Art/Porcelain.mat");
            school.targetPaint=Load<Material>(Root+"Art/Lagoon.mat");
            var car=BuildCar();
            EditorSceneManager.MarkSceneDirty(track);EditorSceneManager.SaveScene(track);
            var boot=EditorSceneManager.OpenScene(Root+"Scenes/Bootstrap.unity");
            var session=Object.FindAnyObjectByType<RaceSession>();
            if(!session.tracks.Any(t=>t.scene=="DrivingSchool"))
                session.tracks=session.tracks.Append(new RaceSession.TrackChoice{title="驾驶训练场",scene="DrivingSchool",freeDrive=true}).ToArray();
            if(!session.entries.Contains(car))session.entries=session.entries.Append(car).ToArray();
            EditorSceneManager.MarkSceneDirty(boot);EditorSceneManager.SaveScene(boot);
            if(!EditorBuildSettings.scenes.Any(s=>s.path==path))
                EditorBuildSettings.scenes=EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path,true)).ToArray();
            AssetDatabase.SaveAssets();
        } finally { EditorSceneManager.RestoreSceneManagerSetup(old); }
    }
    static RaceCar BuildCar(){
        var existing=Load<GameObject>(Folder+"OkuibukiCar.prefab");
        if(existing && Load<GameObject>(Folder+"SHIBATA31.Detail.fbx")){
            VehicleArtBuilder.ApplyCar();
            return Load<GameObject>(Folder+"OkuibukiCar.prefab").GetComponent<RaceCar>();
        }
        var model=Load<GameObject>(Folder+"SHIBATA31.fbx");
        if(!model)throw new InvalidOperationException("Import SHIBATA31.fbx first.");
        var spec=Load<CarSpec>(Folder+"Okuibuki.asset");
        if(!spec)throw new InvalidOperationException("Restore the authored Okuibuki.asset before building.");
        var root=new GameObject("Okuibuki 31");
        try{
            var visual=Object.Instantiate(model,root.transform);visual.name="Assembly";
            visual.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
            var wheels=new Transform[4];var carriers=new Transform[4];
            var tags=new[]{"FL","FR","RL","RR"};
            for(int i=0;i<4;i++){
                var wheel=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Wheel_"+tags[i]);
                var pivot=new GameObject(tags[i]+" wheel pose").transform;
                pivot.SetParent(root.transform,false);pivot.position=wheel.position;
                wheel.SetParent(pivot,true); wheels[i]=pivot;
                var mount=new GameObject(tags[i]+" caliper pose").transform;
                mount.SetParent(root.transform,false);mount.position=pivot.position;
                var caliper=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Caliper_"+tags[i]);
                caliper.SetParent(mount,true); carriers[i]=mount;
            }
            spec.body=PrefabUtility.SaveAsPrefabAsset(root,Folder+"OkuibukiVisual.prefab");
            EditorUtility.SetDirty(spec);
            var car=root.AddComponent<RaceCar>();car.spec=spec;car.tires=wheels;car.carriers=carriers;
            car.wheels=new WheelCollider[4];
            for(int i=0;i<4;i++){
                var axle=new GameObject(tags[i]+" suspension").transform;axle.SetParent(root.transform,false);
                axle.localPosition=wheels[i].localPosition+Vector3.up*spec.travel*.5f;
                car.wheels[i]=axle.gameObject.AddComponent<WheelCollider>();
            }
            var hull=root.AddComponent<BoxCollider>();hull.center=new Vector3(0,.49f,0);hull.size=new Vector3(1.78f,.65f,4.18f);
            root.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.Interpolate;
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+"OkuibukiCar.prefab").GetComponent<RaceCar>();
        }finally{Object.DestroyImmediate(root);}
    }
    static Material ConvertMaterial(Material original){
        var path=Folder+original.name+".mat";var mat=Load<Material>(path);
        if(mat)return mat;
        mat=new Material(Load<Material>(Root+"Art/Porcelain.mat").shader){name=original.name,color=original.color};
        AssetDatabase.CreateAsset(mat,path);return mat;
    }
}
}
