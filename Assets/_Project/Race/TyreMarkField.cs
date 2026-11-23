using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex {
    [DefaultExecutionOrder(50), RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class TyreMarkField : MonoBehaviour {
        [Header("接地印迹 · 米")]
        public Vector2 spacingRollToSlide=new Vector2(.85f,.22f);
        [Min(.01f)] public float surfaceLift=.014f;
        [Min(.1f)] public float breakDistance=4;
        [Range(128,16380)] public int segmentBudget=16380;
        [Header("印迹深浅")]
        [Tooltip("开始加深与达到最深印迹的接地滑移速度（米/秒）。")]
        public Vector2 slipRange=new Vector2(1.2f,11);
        public Color rubberTint=new Color(.085f,.075f,.065f);
        public Color grassTint=new Color(.42f,.30f,.15f);
        public Vector2 pavementOpacity=new Vector2(.075f,.88f), grassOpacity=new Vector2(.28f,.82f);
        [Tooltip("大侧滑时，印宽按沿行驶轨迹扫过的接地斑估算。")]
        [Range(.2f,1)] public float patchLengthToWidth=.65f;

        struct Contact {
            public bool valid,hasEdge;
            public RoadCover cover;
            public Vector3 point,normal,halfSpan;
            public Color32 color;
            public float distance;
        }
        sealed class CarTrails {
            public RaceCar car;
            public Contact[] wheels;
            public int reset;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex {
            public Vector3 point;
            public Color32 color;
            public Vector2 uv;
            public Vertex(Vector3 p,Color32 c,float u,float v){point=p;color=c;uv=new Vector2(u,v);}
        }

        readonly List<CarTrails> cars=new List<CarTrails>();
        const MeshUpdateFlags Upload=MeshUpdateFlags.DontRecalculateBounds;
        Vertex[] vertices;
        Mesh mesh;
        Bounds bounds;
        int capacity,cursor,count,pending;

        void Awake(){
            capacity=Mathf.Clamp(segmentBudget,128,16380);
            vertices=new Vertex[4*capacity];
            mesh=new Mesh{name="赛道轮印"};mesh.MarkDynamic();
            mesh.SetVertexBufferParams(vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.UNorm8,4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
            var indices=new ushort[6*capacity];
            for(int i=0;i<capacity;i++){
                int j=6*i;ushort v=(ushort)(4*i);
                indices[j]=v;indices[j+1]=(ushort)(v+2);indices[j+2]=(ushort)(v+1);
                indices[j+3]=(ushort)(v+1);indices[j+4]=(ushort)(v+2);indices[j+5]=(ushort)(v+3);
            }
            mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices,0,0,indices.Length);
            mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,0),Upload);
            GetComponent<MeshFilter>().sharedMesh=mesh;
        }

        public void Track(RaceCar car){
            cars.Add(new CarTrails{car=car,wheels=new Contact[car.wheels.Length],reset=car.ResetGeneration});
        }
        void FixedUpdate(){
            for(int c=cars.Count-1;c>=0;c--){
                var traces=cars[c];var car=traces.car;
                if(!car){cars.RemoveAt(c);continue;}
                if(traces.reset!=car.ResetGeneration || !car.isActiveAndEnabled){
                    System.Array.Clear(traces.wheels,0,traces.wheels.Length);
                    traces.reset=car.ResetGeneration;
                    if(!car.isActiveAndEnabled)continue;
                }
                for(int i=0;i<traces.wheels.Length;i++)Sample(car,i,ref traces.wheels[i]);
            }
        }

        void Sample(RaceCar car,int i,ref Contact last){
            var wheel=car.wheels[i];
            if(!wheel.GetGroundHit(out var hit) || hit.force<=0 || hit.normal.y<.3f || hit.collider.attachedRigidbody){
                last=default;return;
            }
            var cover=car.WheelCover(i);
            var point=hit.point+hit.normal*surfaceLift;
            var velocity=car.Chassis.GetPointVelocity(hit.point);
            float slip=RaceCar.ContactSlipSpeed(wheel,hit,velocity);
            float strength=Mathf.InverseLerp(slipRange.x,slipRange.y,slip);
            bool grass=cover==RoadCover.Grass;
            var tint=grass?grassTint:rubberTint;
            var opacity=grass?grassOpacity:pavementOpacity;
            float axleShare=i<2?car.spec.frontWeight:1-car.spec.frontWeight;
            float staticLoad=.5f*car.Chassis.mass*Physics.gravity.magnitude*axleShare;
            tint.a=Mathf.Clamp01(Mathf.Lerp(opacity.x,opacity.y,strength)*Mathf.Sqrt(hit.force/Mathf.Max(1,staticLoad)));
            var delta=point-last.point;
            float length=delta.magnitude;
            if(!last.valid || last.cover!=cover || length>breakDistance){
                last=new Contact{valid=true,point=point,normal=hit.normal,cover=cover,color=tint};return;
            }
            if(length<Mathf.Max(.02f,Mathf.Lerp(spacingRollToSlide.x,spacingRollToSlide.y,strength)))return;
            var across=Vector3.Cross(hit.normal,delta).normalized;
            if(across.sqrMagnitude<.5f){last=default;return;}
            // Preserve left/right edge ordering when the car changes travel direction.
            if(Vector3.Dot(across,last.hasEdge?last.halfSpan:hit.sidewaysDir)<0)across=-across;
            float width=i<2?car.spec.visualTyreWidths.x:car.spec.visualTyreWidths.y;
            float lateral=Vector3.Dot(across,hit.sidewaysDir);
            float longitudinal=patchLengthToWidth*Vector3.Dot(across,hit.forwardDir);
            var span=across*(.5f*width*Mathf.Sqrt(lateral*lateral+longitudinal*longitudinal));
            var previousSpan=last.hasEdge?last.halfSpan:Vector3.ProjectOnPlane(span,last.normal);
            int first=cursor*4;float end=last.distance+length;
            vertices[first]=new Vertex(last.point-previousSpan,last.color,0,last.distance);
            vertices[first+1]=new Vertex(last.point+previousSpan,last.color,1,last.distance);
            vertices[first+2]=new Vertex(point-span,tint,0,end);
            vertices[first+3]=new Vertex(point+span,tint,1,end);
            if(count==0)bounds=new Bounds(point,Vector3.zero);
            for(int v=first;v<first+4;v++)bounds.Encapsulate(vertices[v].point);
            cursor=(cursor+1)%capacity;count=Mathf.Min(capacity,count+1);pending=Mathf.Min(capacity,pending+1);
            last.point=point;last.normal=hit.normal;last.color=tint;last.halfSpan=span;last.hasEdge=true;last.distance=end;
        }

        void LateUpdate(){
            if(pending==0)return;
            int first=(cursor-pending+capacity)%capacity;
            int head=Mathf.Min(pending,capacity-first);
            mesh.SetVertexBufferData(vertices,first*4,first*4,head*4,0,Upload);
            if(head<pending)mesh.SetVertexBufferData(vertices,0,0,(pending-head)*4,0,Upload);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,count*6){bounds=bounds,vertexCount=count*4},Upload);
            mesh.bounds=bounds;pending=0;
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
