using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
// Search at the project's fixed physics step, then validate through the garage's
// actual race session. All driving commands are representable by W/A/S/D/Space.
public static class MarinaPace {
    static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
    public static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/MarinaPace"));
    public sealed class Line {
        public readonly Vector3[] points;
        public readonly float[] curvature,pace;
        public readonly float spacing;
        public int Wrap(int i)=>(i%points.Length+points.Length)%points.Length;
        public Line(float lat,float decel,float top,float width){
            string path=Path.Combine(Application.dataPath,"../Tools/MarinaPace/line.csv");
            points=File.ReadLines(path).Skip(1).Select(row=>{
                var a=row.Split(',');return new Vector3(float.Parse(a[0],Culture),0,float.Parse(a[1],Culture));
            }).ToArray();
            var center=UnityEngine.Object.FindAnyObjectByType<Circuit>().mapCenterline;
            if(center.Length!=points.Length)throw new InvalidDataException("Racing line belongs to a different route.");
            for(int i=0;i<points.Length;i++)points[i]=Vector3.Lerp(center[i],points[i],width);
            int n=points.Length;pace=new float[n];curvature=new float[n];
            float[] edges=new float[n];
            for(int i=0;i<n;i++){
                edges[i]=Vector3.Distance(points[i],points[Wrap(i+1)]);
                var a=points[i]-points[Wrap(i-4)];var b=points[Wrap(i+4)]-points[i];
                curvature[i]=2*Vector3.Cross(a,b).y/Mathf.Max(.001f,a.magnitude*b.magnitude*(a+b).magnitude);
                pace[i]=Mathf.Min(top,Mathf.Sqrt(lat/Mathf.Max(.0001f,Mathf.Abs(curvature[i]))));
            }
            spacing=edges.Sum()/n;
            for(int j=2*n-1;j>=0;j--){
                int i=j%n;float v=pace[Wrap(i+1)];pace[i]=Mathf.Min(pace[i],Mathf.Sqrt(v*v+2*decel*edges[i]));
            }
        }
    }
    public sealed class Pilot:IDriver {
        readonly RaceCar car;
        readonly Line line;
        readonly float slip,look,handSeconds;
        readonly bool recovery;
        int station,slide;
        float age,cooldown;
        public int Keys {get;private set;}
        public Pilot(RaceCar car,Line line,float slip,float look,float handSeconds,bool recovery){
            this.car=car;this.line=line;this.slip=slip;this.look=look;this.handSeconds=handSeconds;this.recovery=recovery;
        }
        public DriveCommand Read(){
            var pos=car.Chassis.position;int closest=station;float best=float.PositiveInfinity;
            for(int j=-3;j<=9;j++){int i=line.Wrap(station+j);float d=(pos-line.points[i]).sqrMagnitude;
                if(d<best){closest=i;best=d;}}
            station=closest;
            float v=car.Speed,beta=car.SignedDriftAngle;
            float yaw=Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg;
            float lookSamples=(6+look*v)/line.spacing;
            int idx=line.Wrap(station+Mathf.FloorToInt(lookSamples));
            var aim=Vector3.Lerp(line.points[idx],line.points[line.Wrap(idx+1)],lookSamples-Mathf.Floor(lookSamples));
            var local=car.transform.InverseTransformPoint(aim);
            float dist=Mathf.Max(2,new Vector2(local.x,local.z).magnitude);
            float k=2*local.x/(dist*dist);
            float angle=Mathf.Atan(car.spec.wheelbase*k)*Mathf.Rad2Deg*(1+.022f*v)
                +.45f*beta+.09f*(k*v*Mathf.Rad2Deg-yaw);
            float ahead=line.curvature[idx],at=line.curvature[station],dt=Time.fixedDeltaTime;
            cooldown=Mathf.Max(0,cooldown-dt);
            if(slip>0 && slide==0 && cooldown<=0 && v>17 && Mathf.Abs(beta)<12 &&
               Mathf.Abs(ahead)>.005f && Mathf.Abs(ahead)>Mathf.Abs(at)+.001f){
                slide=(int)Mathf.Sign(ahead);age=0;
            }
            bool hand=false;
            if(slide!=0){
                age+=dt;
                if((age>.55f && slide*ahead<.0025f) || Mathf.Abs(beta)>45 || age>5){
                    slide=0;cooldown=1;
                }else if(age<=handSeconds && -slide*beta<slip*.8f){
                    angle=slide*22;hand=true;
                }else{
                    float pathK=2*Mathf.Sin(Mathf.Atan2(local.x,local.z)-beta*Mathf.Deg2Rad)/dist;
                    float wantedYaw=v*pathK*Mathf.Rad2Deg+2.5f*(beta+slide*slip);
                    angle=Mathf.Atan(car.spec.wheelbase*pathK)*Mathf.Rad2Deg+beta+.23f*(wantedYaw-yaw);
                }
            }
            float rack=(car.wheels[0].steerAngle+car.wheels[1].steerAngle)*.5f;
            float steer=angle>.25f && rack<angle-.12f?1:angle<-.25f && rack>angle+.12f?-1:0;
            float target=line.pace[station];
            bool brake=v>target+1.1f && !hand,gas=!brake && v<target+.2f;
            Keys=(gas?1:0)|(steer<0?2:steer>0?4:0)|(brake?8:0)|(hand?16:0);
            return new DriveCommand{digitalSteering=true,keyboardRecovery=recovery,steer=steer,
                throttle=gas?1:0,brake=brake?1:0,hand=hand?1:0};
        }
    }
    // Use the scene's actual road meshes, positions, and tyre surface setup. Keep
    // this local simulation independent of the active car and main physics clock.
    public static string Trial(float lat,float decel,float top,float slip,float look,float pulse,float width=1){
        var session=UnityEngine.Object.FindAnyObjectByType<RaceSession>();
        var course=UnityEngine.Object.FindAnyObjectByType<Circuit>();
        if(!session || !session.Car || !course || course.gameObject.scene.name!="MarinaBay")
            throw new InvalidOperationException("Start the MarinaBay time trial first.");
        var line=new Line(lat,decel,top,width);var scene=SceneManager.CreateScene("Marina pace "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try {
            foreach(var src in course.GetComponentsInChildren<MeshCollider>()){
                var obj=new GameObject(src.name=="Concrete race barriers"?"Marina Barrier":src.name);
                SceneManager.MoveGameObjectToScene(obj,scene);obj.layer=src.gameObject.layer;
                obj.transform.SetPositionAndRotation(src.transform.position,src.transform.rotation);obj.transform.localScale=src.transform.lossyScale;
                var collider=obj.AddComponent<MeshCollider>();EditorUtility.CopySerialized(src,collider);
                var surface=src.GetComponentInParent<TyreSurface>();if(surface)EditorUtility.CopySerialized(surface,obj.AddComponent<TyreSurface>());
            }
            var prefab=session.entries.Single(e=>e.spec==session.Car.spec);
            var car=UnityEngine.Object.Instantiate(prefab,course.grid.position,course.grid.rotation);
            SceneManager.MoveGameObjectToScene(car.gameObject,scene);car.FitParts(session.Car.Differential,session.Car.RearWheels);
            car.Chassis.interpolation=RigidbodyInterpolation.None;
            var contact=car.gameObject.AddComponent<RidgeSearchContact>();
            var pilot=new Pilot(car,line,slip,look,pulse,session.panel.KeyboardRecovery);car.Driver=pilot;
            var step=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");var physics=scene.GetPhysicsScene();
            float dt=Time.fixedDeltaTime;
            for(int i=0;i<Mathf.CeilToInt(course.startDelay/dt);i++){step();physics.Simulate(dt);}
            car.EnabledDrive=true;int passed=0,sliding=0;float peak=0;int ticks=0;
            for(;ticks<10000 && passed<course.gates.Length;ticks++){
                Vector3 last=car.Chassis.position;step();physics.Simulate(dt);
                if(course.Crossed((passed+1)%course.gates.Length,last,car.Chassis.position))passed++;
                if(car.GroundedCount>=3 && car.ForwardSpeed>8 && Mathf.Abs(car.SignedDriftAngle)>=12 && Mathf.Abs(car.SignedDriftAngle)<=50)sliding++;
                peak=Mathf.Max(peak,car.Speed);
                if(contact.contacts>0 || car.Chassis.position.y<course.resetHeight || (ticks>200 && car.Speed<1))break;
            }
            return FormattableString.Invariant($"{lat},{decel},{top},{slip},{look},{pulse},{passed},{ticks*dt:F2},{contact.contacts},{sliding*dt:F2},{peak*3.6f:F1},{width}");
        }finally{SceneManager.UnloadSceneAsync(scene);}
    }
    [MenuItem("Apex/滨海湾/演示最快验证走线")]
    public static void Demonstrate()=>Start(12,6,69,19,.30f,.38f,.50f);

    public static void Start(float lat,float decel,float top,float slip,float look,float pulse,float width){
        var session=UnityEngine.Object.FindAnyObjectByType<RaceSession>();
        if(!Application.isPlaying || !session || session.State!=RaceSession.Phase.Lobby)
            throw new InvalidOperationException("在 Play 模式的车库启动滨海湾演示。");
        for(int i=0;i<session.tracks.Length && !session.panel.location.text.Contains("滨海湾");i++)session.NextTrack();
        if(!session.panel.location.text.Contains("滨海湾"))throw new InvalidOperationException("没有找到滨海湾赛道。");
        if(session.RacingGrid)session.ToggleGrid();
        session.Primary();
        void Bind(){
            if(!Application.isPlaying || !session || session.State==RaceSession.Phase.Lobby){EditorApplication.update-=Bind;return;}
            if(!session.Car || session.State==RaceSession.Phase.Loading)return;
            EditorApplication.update-=Bind;
            var driver=new Pilot(session.Car,new Line(lat,decel,top,width),slip,look,pulse,session.panel.KeyboardRecovery);
            session.Car.gameObject.AddComponent<MarinaPaceKeyboard>().Bind(session,()=>{
                driver.Read();return driver.Keys;
            });
        }
        EditorApplication.update+=Bind;
    }
}
}
