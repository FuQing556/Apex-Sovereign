using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Apex.Editor {
    public static class VehicleArtBuilder {
        const string CarDir="Assets/_Project/Vehicles/SHIBATA31/";
        const string MapDir="Assets/_Project/Tracks/RidgeArt/";
        const string CarModel=CarDir+"SHIBATA31.Detail.fbx", MapModel=MapDir+"Ridgeline valley.fbx";
        const string Ridge="Assets/_Project/Scenes/Ridge.unity";
        static readonly string[] Axles={"FL","FR","RL","RR"};
        [Serializable] class Surface { public string name; public float r,g,b,a,metal,gloss,emission; }
        [Serializable] class Palette { public Surface[] surfaces; }
        [Serializable] class Link { public string name,axle,mode; public Vector3 start,end; public bool steer; }
        [Serializable] class Linkage { public Link[] links; }

        static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static Material MaterialAsset(string path,string shader){
            var mat=Load<Material>(path);
            if(mat)return mat;
            var program=Shader.Find(shader);
            if(!program)throw new InvalidOperationException(shader);
            mat=new Material(program);
            AssetDatabase.CreateAsset(mat,path);
            return mat;
        }
        static void Transparent(Material mat){
            mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
            mat.SetOverrideTag("RenderType","Transparent");mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue=(int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster",false);
        }
        static void ImportModel(string model,string manifest,bool readable){
            string dir=Path.GetDirectoryName(model).Replace('\\','/')+"/Materials";
            if(!AssetDatabase.IsValidFolder(dir))AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\','/'),"Materials");
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            importer.isReadable=readable;importer.addCollider=false;importer.importCameras=false;importer.importLights=false;
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText(manifest));
            foreach(var s in palette.surfaces){
                var mat=MaterialAsset(dir+"/"+s.name+".mat","Universal Render Pipeline/Lit");
                var color=new Color(s.r,s.g,s.b).gamma;color.a=s.a;
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",s.metal);
                mat.SetFloat("_Smoothness",s.gloss);mat.SetFloat("_Cull",(int)CullMode.Off);
                if(s.name=="Cedar boughs" || s.name=="Birch canopy"){
                    var tree=Load<Texture2D>(MapDir+(s.name=="Cedar boughs"?"Japanese cedar.png":"Mountain birch.png"));
                    mat.SetTexture("_BaseMap",tree);mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.34f);
                    mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");
                    mat.renderQueue=(int)RenderQueue.AlphaTest;
                    mat.EnableKeyword("_EMISSION");mat.SetTexture("_EmissionMap",tree);mat.SetColor("_EmissionColor",new Color(.21f,.22f,.19f));
                }else if(s.name=="Meadow"){
                    mat.SetTexture("_BaseMap",Load<Texture2D>(MapDir+"Meadow grain.png"));
                    mat.SetColor("_BaseColor",new Color(.78f,.86f,.76f));
                }
                if(s.a<1)Transparent(mat);
                if(s.emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*s.emission);}
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),s.name),mat);
            }
            importer.SaveAndReimport();
        }
        static void Clear(Transform t){
            for(int i=t.childCount-1;i>=0;i--)Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
        static Transform Named(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        public static void AddTyrePlume(RaceCar car){
            var mat=MaterialAsset(CarDir+"Materials/Tyre smoke.mat","Universal Render Pipeline/Particles/Unlit");
            mat.SetTexture("_BaseMap",Load<Texture2D>(CarDir+"TyrePlume.png"));
            mat.SetColor("_BaseColor",Color.white);Transparent(mat);EditorUtility.SetDirty(mat);
            if(!car.TryGetComponent<TyreSlipPlume>(out var fx))fx=car.gameObject.AddComponent<TyreSlipPlume>();
            fx.plume=mat;
        }
        static Transform Rig(GameObject root,Transform model){
            var previous=root.transform.Find("轮端连杆");
            if(previous)Object.DestroyImmediate(previous.gameObject);
            var mounts=new GameObject("轮端连杆").transform;mounts.SetParent(root.transform,false);
            if(!root.TryGetComponent<SuspensionVisualRig>(out var rig))rig=root.AddComponent<SuspensionVisualRig>();
            rig.steeringWheel=Named(model,"18_Steering_wheel");
            rig.frontLeft=root.transform.Find("FL caliper pose");rig.frontRight=root.transform.Find("FR caliper pose");
            rig.rearLightGuides=model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterial.name=="Tail light guides");
            var source=JsonUtility.FromJson<Linkage>(File.ReadAllText("毕设资料/02-车辆设计/SHIBATA-31/精细装配/joints.json"));
            rig.joints=source.links.Select(l=>{
                var piece=Named(model,l.name);
                var pivot=new GameObject(l.name+" pivot").transform;pivot.SetParent(mounts,false);
                pivot.localPosition=l.start;
                pivot.localRotation=Quaternion.LookRotation((l.end-l.start).normalized,Vector3.up);
                piece.SetParent(pivot,true);
                var carrier=root.transform.Find(l.axle+" caliper pose");
                return new SuspensionVisualRig.Joint{pivot=pivot,carrier=carrier,chassisPoint=l.start,wheelRest=l.end,
                    wheelOffset=l.end-carrier.localPosition,bindLength=Vector3.Distance(l.start,l.end),
                    bindRotation=pivot.localRotation,followsSteer=l.steer,
                    attachment=(SuspensionVisualRig.Attachment)Enum.Parse(typeof(SuspensionVisualRig.Attachment),l.mode)};
            }).ToArray();
            return mounts;
        }

        public static void ApplyCar(){
            ImportModel(CarModel,"毕设资料/02-车辆设计/SHIBATA-31/精细装配/surfaces.json",true);
            var model=Load<GameObject>(CarModel);
            foreach(string path in new[]{CarDir+"OkuibukiVisual.prefab",CarDir+"OkuibukiCar.prefab"}){
                var root=PrefabUtility.LoadPrefabContents(path);
                try{
                    var assembly=root.transform.Find("Assembly");
                    if(!assembly)throw new InvalidOperationException("Missing assembly in "+path);
                    var visual=Object.Instantiate(model,root.transform);visual.transform.localRotation=Quaternion.Euler(0,180,0);
                    foreach(string tag in Axles){
                        var wheel=Named(visual.transform,"Wheel_"+tag);
                        var carrier=Named(visual.transform,"Caliper_"+tag);
                        var pose=root.transform.Find(tag+" wheel pose");
                        var mount=root.transform.Find(tag+" caliper pose");
                        if(Vector3.Distance(wheel.position,pose.position)>.001f || Vector3.Distance(carrier.position,mount.position)>.001f)
                            throw new InvalidOperationException("Wheel datum mismatch: "+tag+" at "+wheel.position+" vs "+pose.position);
                        Clear(pose);Clear(mount);wheel.SetParent(pose,true);carrier.SetParent(mount,true);
                        var tyre=pose.GetComponentsInChildren<MeshFilter>().Single(f=>f.name.StartsWith("Tyre_",StringComparison.Ordinal));
                        if(!tyre.sharedMesh.isReadable)throw new InvalidOperationException("Unreadable rubber "+tag);
                    }
                    Object.DestroyImmediate(assembly.gameObject);visual.name="Assembly";
                    var mounts=Rig(root,visual.transform);
                    var renderers=root.GetComponentsInChildren<Renderer>();
                    var reduced=renderers.Where(r=>!r.transform.IsChildOf(mounts) && !(r.transform.IsChildOf(visual.transform) &&
                        Enumerable.Range(9,7).Any(i=>r.transform.parent.name.StartsWith(i.ToString("00")+"_")))).ToArray();
                    if(!root.TryGetComponent<LODGroup>(out var lod))lod=root.AddComponent<LODGroup>();
                    lod.SetLODs(new[]{new LOD(.22f,renderers),new LOD(.004f,reduced)});lod.RecalculateBounds();
                    if(root.TryGetComponent<RaceCar>(out var car))AddTyrePlume(car);
                    if(!PrefabUtility.SaveAsPrefabAsset(root,path))throw new IOException(path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
        }

        static Material Asphalt(){
            var tex=(TextureImporter)AssetImporter.GetAtPath(MapDir+"AggregateNormal.png");
            if(tex.textureType!=TextureImporterType.NormalMap){tex.textureType=TextureImporterType.NormalMap;tex.SaveAndReimport();}
            var mat=MaterialAsset(MapDir+"Materials/Ridge aggregate.mat","Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_BaseMap",Load<Texture2D>(MapDir+"Aggregate.png"));
            mat.SetTexture("_BumpMap",Load<Texture2D>(MapDir+"AggregateNormal.png"));mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(mat);return mat;
        }
        public static void DressRidge(Circuit course){
            if(!Load<GameObject>(MapModel))return;
            var scene=course.gameObject.scene;
            if(SceneManager.GetActiveScene()!=scene)throw new InvalidOperationException("Make Ridge active before dressing.");
            ImportModel(MapModel,"毕设资料/04-赛道设计/山脊美术/surfaces.json",false);
            var old=course.transform.Find("山谷围场");
            if(old)Object.DestroyImmediate(old.gameObject);
            var art=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(MapModel),scene);
            art.name="山谷围场";art.transform.SetParent(course.transform,false);
            art.transform.localRotation=Quaternion.Euler(0,180,0);
            var road=course.transform.Find("Road and kerbs");
            var src=Load<Mesh>("Assets/_Project/Tracks/RidgeSurface.asset");
            string path=MapDir+"Ridge surface UV.asset";var surf=Load<Mesh>(path);
            if(!surf){surf=Object.Instantiate(src);AssetDatabase.CreateAsset(surf,path);}
            else EditorUtility.CopySerialized(src,surf);
            surf.name="Ridge surface UV";surf.uv=src.vertices.Select(v=>new Vector2(v.x*.64f,v.z*.64f)).ToArray();
            surf.RecalculateTangents();EditorUtility.SetDirty(surf);
            road.GetComponent<MeshFilter>().sharedMesh=surf;
            var renderer=road.GetComponent<MeshRenderer>();var mats=renderer.sharedMaterials;mats[0]=Asphalt();renderer.sharedMaterials=mats;
            var runoff=scene.GetRootGameObjects().Single(g=>g.name=="Runoff").GetComponent<Renderer>();
            runoff.sharedMaterial=Load<Material>(MapDir+"Materials/Meadow.mat");
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogStartDistance=140;RenderSettings.fogEndDistance=690;
            RenderSettings.fogColor=new Color(.62f,.71f,.75f);
            var sky=MaterialAsset(MapDir+"Materials/Mountain daylight.mat","Skybox/Procedural");
            sky.SetFloat("_AtmosphereThickness",.94f);sky.SetFloat("_SunSize",.027f);
            sky.SetColor("_GroundColor",new Color(.52f,.60f,.62f));sky.SetFloat("_Exposure",1.05f);
            RenderSettings.skybox=sky;EditorUtility.SetDirty(sky);
            var sun=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);
            sun.transform.rotation=Quaternion.Euler(37,-32,0);sun.color=new Color(1,.935f,.82f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;
            EditorSceneManager.MarkSceneDirty(scene);
        }
        [MenuItem("Apex/Art/Install 31 and Ridgeline art")]
        public static void Install(){
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Requires Edit Mode.");
            ApplyCar();
            var previous=SceneManager.GetActiveScene();
            var s=SceneManager.GetSceneByPath(Ridge);bool open=s.isLoaded;
            if(!open)s=EditorSceneManager.OpenScene(Ridge,OpenSceneMode.Additive);
            try{
                if(s.isDirty)throw new InvalidOperationException("Save pending Ridge edits first.");
                SceneManager.SetActiveScene(s);
                DressRidge(s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Circuit>()).Single());
                if(!EditorSceneManager.SaveScene(s))throw new IOException(Ridge);
                AssetDatabase.SaveAssets();
            }finally{SceneManager.SetActiveScene(previous);if(!open)EditorSceneManager.CloseScene(s,true);}
        }
    }
}
