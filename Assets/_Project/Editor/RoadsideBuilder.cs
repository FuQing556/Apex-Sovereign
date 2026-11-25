using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
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
            var collider=t.GetComponent<MeshCollider>();
            if(!collider)collider=t.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=null;collider.sharedMesh=mesh;
            return collider;
        }

        static void MarkCover(GameObject obj,RoadCover cover,RoadsideSettings settings){
            if(!obj.TryGetComponent<TyreSurface>(out var surface))surface=obj.AddComponent<TyreSurface>();
            surface.cover=cover;
            surface.settings=settings;
            EditorUtility.SetDirty(surface);
        }

        internal static Mesh AssetMesh(string id){
            var path=Root+"Tracks/"+id+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.name=id;return mesh;
        }

        // Limit change per metre on a closed ring, including the last-to-first join.
        // Two visits in each direction propagate a bound across the start line.
        static void LimitRise(float[] values,float[] allowance){
            int n=values.Length;
            for(int k=0;k<2*n;k++){
                int i=k%n,j=(i+1)%n;values[j]=Mathf.Min(values[j],values[i]+allowance[i]);
            }
            for(int k=2*n-1;k>=0;k--){
                int i=k%n,j=(i+1)%n;values[i]=Mathf.Min(values[i],values[j]+allowance[i]);
            }
        }

        static float FlatDistance(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;

        // Carry a narrow apex width through the bend before tapering out again.
        // Erode before averaging so smoothing cannot overshoot the curvature limit.
        static void SmoothSetback(float[] radius,float[] stations,float length,float blend){
            int n=radius.Length;var minimum=(float[])radius.Clone();
            float Distance(int i,int j){
                float d=Mathf.Abs(stations[i]-stations[j]);return Mathf.Min(d,length-d);
            }
            for(int i=0;i<n;i++)for(int j=0;j<n;j++)
                if(Distance(i,j)<=blend)minimum[i]=Mathf.Min(minimum[i],radius[j]);
            for(int i=0;i<n;i++){
                float sum=0,weight=0;
                for(int j=0;j<n;j++){
                    float w=Mathf.Max(0,1-Distance(i,j)/blend);
                    sum+=minimum[j]*w;weight+=w;
                }
                radius[i]=sum/weight;
            }
        }

        public static void Build(Circuit course,string id,Vector3[] left,Vector3[] right,float floor){
            var spec=Settings();
            var road=course.transform.Find("Road and kerbs");
            // Keep the original spline stations as the source on every rebake.
            var layout=AssetDatabase.LoadAssetAtPath<RoadLayout>(Root+"Tracks/"+id+".asset");
            int n=left.Length;
            var centre=new Vector3[n];var side=new Vector3[n];var width=new float[n];
            for(int i=0;i<n;i++){
                centre[i]=(left[i]+right[i])*.5f;
                if(layout)centre[i].y=layout.Sample(i/layout.samplesPerSpan,(float)(i%layout.samplesPerSpan)/layout.samplesPerSpan).y;
                side[i]=(right[i]-left[i]).normalized;width[i]=FlatDistance(right[i],left[i])*.5f;
            }
            var run=new float[n];var bend=new float[n];
            for(int i=0;i<n;i++){
                int j=(i+1)%n;var d=Vector3.ProjectOnPlane(centre[j]-centre[i],Vector3.up);
                run[i]=d.magnitude;bend[i]=Vector3.Dot(side[j]-side[i],d.normalized);
            }
            var back=new float[2][];var toe=new float[2][];
            var stations=new float[n];for(int i=1;i<n;i++)stations[i]=stations[i-1]+run[i-1];
            float loopLength=stations[n-1]+run[n-1];
            var taper=run.Select(x=>x*spec.shoulderTaper).ToArray();
            for(int k=0;k<2;k++){
                int sign=2*k-1;back[k]=new float[n];toe[k]=new float[n];
                for(int i=0;i<n;i++){
                    int prev=(i+n-1)%n;
                    float Cap(int e)=>sign*bend[e]<0?(1-spec.minimumInsideAdvance)*run[e]/(-sign*bend[e]):float.PositiveInfinity;
                    // An inward offset past the local radius reverses the mesh winding.
                    float limit=Mathf.Min(Cap(i),Cap(prev));
                    back[k][i]=Mathf.Min(width[i]+spec.shoulderWidth+spec.barrierWidth,limit);
                    if(back[k][i]<=width[i]+spec.barrierWidth)
                        throw new System.InvalidOperationException($"{id} section {i}: paint and barrier exceed inside bend radius.");
                    toe[k][i]=Mathf.Min(limit,back[k][i]+Mathf.Max(0,centre[i].y-floor)*spec.embankmentRunPerRise);
                }
                LimitRise(back[k],taper);LimitRise(toe[k],taper);
                SmoothSetback(back[k],stations,loopLength,spec.shoulderBlendMetres);
                SmoothSetback(toe[k],stations,loopLength,spec.shoulderBlendMetres);
            }
            Vector3 At(int i,int k,float distance)=>centre[i]+side[i]*((2*k-1)*distance);
            var heights=centre.Select(p=>p.y).ToArray();var allowed=new float[n];
            for(int i=0;i<n;i++){
                int j=(i+1)%n;
                float inner=Mathf.Min(FlatDistance(At(i,0,back[0][i]),At(j,0,back[0][j])),FlatDistance(At(i,1,back[1][i]),At(j,1,back[1][j])));
                float grade=Mathf.Max(spec.insideGradeLimit,Mathf.Abs(heights[j]-heights[i])/run[i]);
                allowed[i]=Mathf.Min(run[i],inner)*grade;
            }
            LimitRise(heights,allowed);
            var bed=new Vector3[n][];var roadVerts=new Vector3[n*4];
            float kerbWidth=.6f;
            for(int i=0;i<n;i++){
                centre[i].y=heights[i];bed[i]=new Vector3[6];
                roadVerts[i*4]=At(i,0,width[i]);roadVerts[i*4+1]=At(i,0,width[i]-kerbWidth);
                roadVerts[i*4+2]=At(i,1,width[i]-kerbWidth);roadVerts[i*4+3]=At(i,1,width[i]);
            }
            for(int k=0;k<2;k++){
                var rings=new Vector3[n][];
                for(int i=0;i<n;i++){
                    var foot=At(i,k,back[k][i]-spec.barrierWidth);var rear=At(i,k,back[k][i]);
                    var bottom=At(i,k,toe[k][i]);bottom.y=floor;
                    var top=Vector3.up*spec.barrierHeight;
                    rings[i]=new[]{foot,foot+top*.72f,foot+top*.85f,foot+top,rear+top,rear,foot};
                    if(k==0){bed[i][0]=bottom;bed[i][1]=rear;bed[i][2]=roadVerts[i*4];}
                    else{bed[i][3]=roadVerts[i*4+3];bed[i][4]=rear;bed[i][5]=bottom;}
                }
                var barrier=Surface(course.transform,id,k==0?"LeftBarrier":"RightBarrier",rings,new[]{0,1,0,0,0,0},new[]{spec.concrete,spec.marking},k==0);
                barrier.sharedMaterial=spec.barrierContact;
            }
            var baseRings=bed.Select(r=>new[]{r[2],r[1],r[0],r[5],r[4],r[3]}).ToArray();
            var meadow=AssetDatabase.LoadAssetAtPath<Material>(Root+"Tracks/RidgeArt/Materials/Meadow.mat");
            var bedCollider=Surface(course.transform,id,"Roadbed",baseRings,new[]{0,1,1,1,0},new[]{meadow,spec.earth},true);
            var bedMesh=bedCollider.GetComponent<MeshFilter>().sharedMesh;
            bedMesh.uv=bedMesh.vertices.Select(p=>new Vector2(p.x,p.z)*.30f).ToArray();
            Object.DestroyImmediate(bedCollider);
            if(!road){
                road=new GameObject("Road and kerbs",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)).transform;
                road.SetParent(course.transform,false);
                road.GetComponent<MeshRenderer>().sharedMaterials=new[]{spec.concrete,spec.marking,AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Lagoon.mat")};
                foreach(var t in course.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Asphalt ") || t.name=="Edge").ToArray())Object.DestroyImmediate(t.gameObject);
            }
            UpdateRoad(road,id,roadVerts,bed,spec);
            var field=course.gameObject.scene.GetRootGameObjects().Single(obj=>obj.name=="Runoff");
            MarkCover(field,RoadCover.Grass,spec);
            for(int g=0;g<course.gates.Length;g++){
                int i=g*n/course.gates.Length;
                var gate=course.gates[g];float dy=centre[i].y-gate.position.y;
                gate.position+=Vector3.up*dy;
                if(g==0)course.grid.position+=Vector3.up*dy;
            }
            FitTimingGates(course);
            RoadsideDetailBuilder.Build(course,id,roadVerts,bed,spec);
        }

        static void UpdateRoad(Transform road,string id,Vector3[] verts,Vector3[][] bed,RoadsideSettings spec){
            int n=bed.Length;
            var src=AssetMesh(id+"Surface");
            var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            var contactVerts=new List<Vector3>(8*n);var contactTriangles=new List<int>(42*n);
            for(int i=0;i<n;i++){
                int j=(i+1)%n;
                for(int lane=0;lane<3;lane++){
                    int a=i*4+lane,b=j*4+lane;
                    faces[lane==1?0:1+(i/2)%2].AddRange(new[]{a,b,a+1,a+1,b,b+1});
                }
                contactVerts.AddRange(new[]{bed[i][0],bed[i][1],verts[i*4],verts[i*4+1],verts[i*4+2],verts[i*4+3],bed[i][4],bed[i][5]});
                for(int lane=0;lane<7;lane++){
                    int a=i*8+lane,b=j*8+lane;
                    contactTriangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
                }
            }
            src.vertices=verts;src.subMeshCount=3;
            for(int i=0;i<3;i++)src.SetTriangles(faces[i],i);
            src.uv=verts.Select(v=>new Vector2(v.x,v.z)*.64f).ToArray();
            src.RecalculateNormals();src.RecalculateTangents();src.RecalculateBounds();EditorUtility.SetDirty(src);
            var filter=road.GetComponent<MeshFilter>();
            if(id=="Ridge" && filter.sharedMesh && filter.sharedMesh!=src){
                EditorUtility.CopySerialized(src,filter.sharedMesh);filter.sharedMesh.name="Ridge surface UV";EditorUtility.SetDirty(filter.sharedMesh);
            }else filter.sharedMesh=src;
            var old=road.Find("Kerb contact");if(old)Object.DestroyImmediate(old.gameObject);
            var ribbon=AssetMesh(id+"DrivingContact");
            ribbon.SetVertices(contactVerts);ribbon.SetTriangles(contactTriangles,0);ribbon.RecalculateBounds();EditorUtility.SetDirty(ribbon);
            var collider=road.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=ribbon;
            MarkCover(road.gameObject,RoadCover.Asphalt,spec);road.GetComponent<TyreSurface>().trackRibbon=true;
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
                    p.x=p.x<0?limits[i].x-halfPost:limits[i].y+halfPost;
                    post.localPosition=p;
                    if(post.TryGetComponent<Collider>(out var visualOnly))Object.DestroyImmediate(visualOnly);
                }
            }
            EditorUtility.SetDirty(course);
        }

        [MenuItem("Apex/Update roadbeds and barriers")]
        public static void Apply(){
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before baking tracks.");
            const string id="Ridge",path=Root+"Scenes/Ridge.unity";
            var scene=SceneManager.GetSceneByPath(path);
            bool alreadyOpen=scene.isLoaded;
            if(!alreadyOpen)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{
                if(scene.isDirty)throw new System.InvalidOperationException("Save pending edits to "+path+" before baking it.");
                var course=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Circuit>()).Single();
                var v=course.transform.Find("Road and kerbs").GetComponent<MeshFilter>().sharedMesh.vertices;
                var left=Enumerable.Range(0,v.Length/4).Select(i=>v[i*4]).ToArray();
                var right=Enumerable.Range(0,v.Length/4).Select(i=>v[i*4+3]).ToArray();
                var field=scene.GetRootGameObjects().Single(o=>o.name=="Runoff");
                Build(course,id,left,right,field.GetComponent<Collider>().bounds.max.y);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new System.IO.IOException(path);
            }finally{if(!alreadyOpen)EditorSceneManager.CloseScene(scene,true);}
            AssetDatabase.SaveAssets();
        }
    }
}
