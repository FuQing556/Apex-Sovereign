using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Apex.Editor {
    public static class RoadsideBuilder {
        const string Root = "Assets/_Project/";

        static RoadsideSettings Settings() {
            string path = Root + "Tracks/Roadside.asset";
            var spec = AssetDatabase.LoadAssetAtPath<RoadsideSettings>(path);
            if (spec) return spec;
            spec = ScriptableObject.CreateInstance<RoadsideSettings>();
            spec.concrete = AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Asphalt.mat");
            spec.marking = AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Porcelain.mat");
            spec.earth = AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Graphite.mat");
            spec.grass = AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/DryGrass.mat");
            spec.barrierContact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root+"Tracks/Guardrail.physicMaterial");
            AssetDatabase.CreateAsset(spec,path);
            return spec;
        }

        // Rings follow the road in driving order. Adjacent strips share section positions,
        // but have independent normals so the vertical barrier face stays flat.
        static Mesh Sweep(string path, Vector3[][] rings, int[] faceMaterials, int materials, bool flip) {
            var verts = new List<Vector3>();
            var indices = Enumerable.Range(0,materials).Select(i=>new List<int>()).ToArray();
            for (int face=0;face<faceMaterials.Length;face++) {
                int start=verts.Count;
                foreach (var ring in rings) { verts.Add(ring[face]); verts.Add(ring[face+1]); }
                for (int i=0;i<rings.Length;i++) {
                    int a=start+2*i,b=start+2*((i+1)%rings.Length);
                    indices[faceMaterials[face]].AddRange(flip ? new[]{a,a+1,b,a+1,b+1,b} : new[]{a,b,a+1,a+1,b,b+1});
                }
            }
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.SetVertices(verts);mesh.subMeshCount=materials;
            for(int i=0;i<materials;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static MeshCollider Surface(Transform parent,string id,string name,Vector3[][] rings,int[] faces,Material[] materials,bool flip=false){
            var t=parent.Find(name);
            if(!t){t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)).transform;t.SetParent(parent,false);}
            var mesh=Sweep(Root+"Tracks/"+id+name+".asset",rings,faces,materials.Length,flip);
            t.GetComponent<MeshFilter>().sharedMesh=mesh;t.GetComponent<MeshRenderer>().sharedMaterials=materials;
            var collider=t.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
            return collider;
        }

        static void MarkCover(GameObject obj,RoadCover cover,RoadsideSettings settings){
            if(!obj.TryGetComponent<TyreSurface>(out var surface))surface=obj.AddComponent<TyreSurface>();
            surface.cover=cover;
            surface.settings=settings;
            EditorUtility.SetDirty(surface);
        }

        static Mesh ContactMesh(string id,Mesh source,params int[] groups){
            string path=Root+"Tracks/"+id+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();
            mesh.vertices=source.vertices;
            mesh.triangles=groups.SelectMany(group=>source.GetTriangles(group)).ToArray();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void SplitRoad(Transform road,string id,RoadsideSettings settings){
            var source=road.GetComponent<MeshFilter>().sharedMesh;
            var asphalt=road.GetComponent<MeshCollider>();
            asphalt.sharedMesh=null;
            asphalt.sharedMesh=ContactMesh(id+"AsphaltContact",source,0);
            var kerb=road.Find("Kerb contact");
            if(!kerb){
                kerb=new GameObject("Kerb contact",typeof(MeshCollider)).transform;
                kerb.SetParent(road,false);
            }
            var kerbCollider=kerb.GetComponent<MeshCollider>();
            kerbCollider.sharedMesh=null;
            kerbCollider.sharedMesh=ContactMesh(id+"KerbContact",source,1,2);
            MarkCover(kerb.gameObject,RoadCover.Kerb,settings);
        }

        public static void Build(Circuit course,string id,Vector3[] left,Vector3[] right,float floor){
            var spec=Settings();
            var road=course.transform.Find("Road and kerbs");
            if(road)SplitRoad(road,id,spec);
            else foreach(Transform t in course.transform){
                if(t.name!="Edge")continue;
                if(!t.TryGetComponent<BoxCollider>(out _))t.gameObject.AddComponent<BoxCollider>();
                MarkCover(t.gameObject,RoadCover.Kerb,spec);
            }
            var field=GameObject.Find(road?"Runoff":"Grass");
            field.GetComponent<Renderer>().sharedMaterial=spec.grass;
            MarkCover(field,RoadCover.Grass,spec);
            var bed=new Vector3[left.Length][];
            foreach(int sign in new[]{-1,1}){
                var rings=new Vector3[left.Length][];
                for(int i=0;i<rings.Length;i++){
                    var outward=(right[i]-left[i]).normalized*sign;
                    var edge=sign<0?left[i]:right[i];
                    var foot=edge+outward*spec.shoulderWidth;
                    var back=foot+outward*spec.barrierWidth;
                    var top=Vector3.up*spec.barrierHeight;
                    rings[i]=new[]{foot,foot+top*.72f,foot+top*.85f,foot+top,back+top,back,foot};
                    var toe=back+outward*(Mathf.Max(0,back.y-floor)*spec.embankmentRunPerRise);toe.y=floor;
                    if(sign<0)bed[i]=new Vector3[6];
                    if(sign<0){bed[i][0]=toe;bed[i][1]=back;bed[i][2]=edge;}
                    else{bed[i][3]=edge;bed[i][4]=back;bed[i][5]=toe;}
                }
                var barrier=Surface(course.transform,id,sign<0?"LeftBarrier":"RightBarrier",rings,new[]{0,1,0,0,0,0},new[]{spec.concrete,spec.marking},sign<0);
                barrier.sharedMaterial=spec.barrierContact;
            }
            // The two grass verges start exactly at the outer kerb edges.
            var baseRings=bed.Select(r=>new[]{r[2],r[1],r[0],r[5],r[4],r[3]}).ToArray();
            var bedCollider=Surface(course.transform,id,"Roadbed",baseRings,new[]{0,1,1,1,0},new[]{spec.grass,spec.earth},true);
            MarkCover(bedCollider.gameObject,RoadCover.Grass,spec);
            FitTimingGates(course);
        }

        public static void FitTimingGates(Circuit course){
            var left=course.transform.Find("LeftBarrier").GetComponent<MeshCollider>();
            var right=course.transform.Find("RightBarrier").GetComponent<MeshCollider>();
            var limits=new Vector2[course.gates.Length];
            float probeHeight=Settings().barrierHeight*.5f;
            Physics.SyncTransforms();
            for(int i=0;i<limits.Length;i++){
                var gate=course.gates[i];
                var origin=gate.position+gate.up*probeHeight;
                if(!left.Raycast(new Ray(origin,-gate.right),out var l,Mathf.Infinity) ||
                   !right.Raycast(new Ray(origin,gate.right),out var r,Mathf.Infinity))
                    throw new System.InvalidOperationException($"Cannot locate both barriers at {course.name}, {gate.name}.");
                limits[i]=new Vector2(gate.InverseTransformPoint(l.point).x,gate.InverseTransformPoint(r.point).x);
            }
            course.gateLateralLimits=limits;
            for(int i=0;i<limits.Length;i++){
                foreach(Transform post in course.gates[i]){
                    if(post.name!="Guide post" && post.name!="Marker")continue;
                    var p=post.localPosition;
                    float halfPost=post.localScale.x*.5f;
                    p.x=p.x<0?limits[i].x+halfPost:limits[i].y-halfPost;
                    post.localPosition=p;
                }
            }
            EditorUtility.SetDirty(course);
        }

        static void PadStrip(Transform parent,string name,float start,float end,float width,Material material,RoadCover cover,RoadsideSettings settings){
            var t=parent.Find(name);
            if(!t){
                t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                t.SetParent(parent,false);
                t.name=name;
            }
            // Both strips start under the right wheels of a car driving straight off the grid.
            t.localPosition=new Vector3(.22f+width*.5f,.006f,(start+end)*.5f);
            t.localScale=new Vector3(width,.012f,end-start);
            t.GetComponent<Renderer>().sharedMaterial=material;
            MarkCover(t.gameObject,cover,settings);
        }

        [MenuItem("Apex/Update roadbeds and barriers")]
        public static void Apply(){
            if(EditorApplication.isPlaying || Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new System.InvalidOperationException("Save edits and exit Play Mode before updating tracks.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try{
                foreach(string id in new[]{"Ridge","Mesa","Circuit"}){
                    var scene=EditorSceneManager.OpenScene(Root+"Scenes/"+id+".unity");
                    var course=Object.FindAnyObjectByType<Circuit>();
                    Vector3[] left,right;float floor;
                    if(id=="Circuit"){
                        var sections=course.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Asphalt "))
                            .OrderBy(t=>int.Parse(t.name.Substring(8))).ToArray();
                        var points=sections.Select(t=>t.position+Vector3.up*.1f-t.forward*(t.localScale.z-1.2f)*.5f).ToArray();
                        left=new Vector3[points.Length];right=new Vector3[points.Length];
                        for(int i=0;i<points.Length;i++){
                            var tangent=points[(i+1)%points.Length]-points[(i+points.Length-1)%points.Length];
                            var side=Vector3.Cross(Vector3.up,tangent).normalized*6;
                            left[i]=points[i]-side;right[i]=points[i]+side;
                        }
                        floor=GameObject.Find("Grass").GetComponent<Collider>().bounds.max.y;
                    }else{
                        var v=course.transform.Find("Road and kerbs").GetComponent<MeshFilter>().sharedMesh.vertices;
                        left=Enumerable.Range(0,v.Length/4).Select(i=>v[i*4]).ToArray();
                        right=Enumerable.Range(0,v.Length/4).Select(i=>v[i*4+3]).ToArray();
                        floor=GameObject.Find("Runoff").GetComponent<Collider>().bounds.max.y;
                    }
                    Build(course,id,left,right,floor);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                var pad=EditorSceneManager.OpenScene(Root+"Scenes/TestPad.unity");
                var padCourse=Object.FindAnyObjectByType<Circuit>().transform;
                var settings=Settings();
                PadStrip(padCourse,"Dry kerb strip",30,78,1.25f,settings.marking,RoadCover.Kerb,settings);
                PadStrip(padCourse,"Dry grass strip",90,215,8,settings.grass,RoadCover.Grass,settings);
                EditorSceneManager.MarkSceneDirty(pad);EditorSceneManager.SaveScene(pad);
            }finally{EditorSceneManager.RestoreSceneManagerSetup(previous);}
            AssetDatabase.SaveAssets();
        }
    }
}
