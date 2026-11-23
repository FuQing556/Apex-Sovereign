using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Apex.Editor {
public static class DrivingSchoolCheck {
    sealed class EntryDriver : IDriver {
        public DriveCommand command;
        public DriveCommand Read()=>command;
    }
    // A deterministic reference driver for grip-line verification. The physics solver moves both cars.
    sealed class LineDriver : IDriver {
        readonly RaceCar car;
        readonly List<Vector3> line = new List<Vector3>();
        readonly float targetSpeed;
        int nearest;
        public LineDriver(RaceCar car, DrivingSchool.Lesson lesson, float radius,float speed){
            this.car=car; targetSpeed=speed;
            void AddCircle(bool right){
                float sign=right?-1:1;
                for(int i=0;i<120;i++){
                    float angle=2*Mathf.PI*i/120;
                    line.Add(new Vector3(sign*radius*(Mathf.Cos(angle)-1),0,radius*Mathf.Sin(angle)));
                }
            }
            AddCircle(lesson==DrivingSchool.Lesson.RightCircle);
            if(lesson==DrivingSchool.Lesson.FigureEight)AddCircle(true);
        }
        public DriveCommand Read(){
            for(int i=0;i<10;i++){
                int next=(nearest+1)%line.Count;
                if((line[next]-car.Chassis.position).sqrMagnitude>=(line[nearest]-car.Chassis.position).sqrMagnitude)break;
                nearest=next;
            }
            var local=car.transform.InverseTransformPoint(line[(nearest+6)%line.Count]);
            float angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float error=targetSpeed-car.ForwardSpeed;
            return new DriveCommand{steer=Mathf.Clamp(angle/car.spec.steeringBySpeed.Evaluate(car.Speed),-1,1),
                throttle=Mathf.Clamp01(error/3),brake=Mathf.Clamp01(-error/3)};
        }
    }
    [MenuItem("Apex/Verify driving school in Play Mode")]
    public static void RunMenu(){ Debug.Log(Run()); }
    public static string Run(){
        var live=Object.FindAnyObjectByType<DrivingSchool>();
        var session=Object.FindAnyObjectByType<RaceSession>();
        if(!Application.isPlaying || !live || !session.Car)
            throw new InvalidOperationException("Enter the driving school from the garage first.");
        var report=new StringBuilder();
        foreach(DrivingSchool.Lesson lesson in Enum.GetValues(typeof(DrivingSchool.Lesson))){
            var scene=SceneManager.CreateScene("School check "+lesson,new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try{
                var sim=scene.GetPhysicsScene();
                var root=new GameObject("Private test pad");
                SceneManager.MoveGameObjectToScene(root,scene);
                var surface=new GameObject("Asphalt");surface.transform.SetParent(root.transform,false);
                surface.transform.position=Vector3.down*.5f;surface.AddComponent<BoxCollider>().size=new Vector3(400,1,400);
                var course=root.AddComponent<Circuit>();
                course.grid=new GameObject("Grid").transform;course.grid.SetParent(root.transform,false);
                course.grid.position=new Vector3(0,.35f,-12);
                var school=root.AddComponent<DrivingSchool>();EditorUtility.CopySerialized(live,school);
                var prefab=Array.Find(session.entries,e=>e.spec==session.Car.spec);
                var car=Object.Instantiate(prefab,course.grid.position,Quaternion.identity,root.transform);
                car.Chassis.interpolation=RigidbodyInterpolation.None;
                school.Begin(car,session.entries[0]);
                if(lesson!=school.Current)school.Select(lesson);
                var tick=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                Action aiTick=null;
                if(school.Rival){
                    school.Rival.Chassis.interpolation=RigidbodyInterpolation.None;
                    aiTick=(Action)Delegate.CreateDelegate(typeof(Action),school.Rival,"FixedUpdate");
                }
                var driver=new LineDriver(car,lesson,school.radius,school.targetSpeed*.85f);
                car.Driver=driver;
                float dt=Time.fixedDeltaTime, partnerTravel=0;
                var prev=car.Chassis.position;
                var prevAI=school.Rival?school.Rival.Chassis.position:Vector3.zero;
                int steps=0, target=lesson==DrivingSchool.Lesson.FigureEight?1:2;
                for(;steps*dt<125 && school.Completed<target;steps++){
                    car.EnabledDrive=steps*dt>=1;
                    if(aiTick!=null && (steps==Mathf.RoundToInt(8/dt) || steps==Mathf.RoundToInt(20/dt)))school.NextStyle();
                    tick();aiTick?.Invoke();sim.Simulate(dt);
                    var now=car.Chassis.position;school.Sample(prev,now,dt);prev=now;
                    if(school.Rival){var pos=school.Rival.Chassis.position;partnerTravel+=Vector3.Distance(pos,prevAI);prevAI=pos;}
                }
                report.AppendLine($"{lesson}: runs={school.Completed}/{target}, time={steps*dt:F1}s, grounded={car.GroundedCount}, partner travel={partnerTravel:F0}m, {school.Status().Replace('\n',' ')}");
                if(school.Completed<target || car.GroundedCount!=4 || (school.Rival && partnerTravel<2*Mathf.PI*school.radius))
                    throw new InvalidOperationException(report.ToString());
                if(school.Rival){
                    school.RestartAttempt();
                    var lead=school.Rival;
                    float height=car.Chassis.position.y;
                    car.Chassis.position=new Vector3(0,height,-.5f);
                    lead.Chassis.position=new Vector3(0,height,9);
                    car.Chassis.rotation=lead.Chassis.rotation=Quaternion.identity;
                    car.Chassis.angularVelocity=lead.Chassis.angularVelocity=Vector3.zero;
                    car.Chassis.linearVelocity=Vector3.forward*9;
                    lead.Chassis.linearVelocity=Vector3.zero;
                    lead.EnabledDrive=false;
                    car.Driver=new EntryDriver{command=new DriveCommand{throttle=.3f}};
                    int contacts=car.VehicleContacts;
                    for(int i=0;i*dt<3 && car.VehicleContacts==contacts;i++){
                        var before=car.Chassis.position;
                        tick();aiTick();sim.Simulate(dt);school.Sample(before,car.Chassis.position,dt);
                    }
                    int hits=car.VehicleContacts-contacts;
                    report.AppendLine($"Closing on stopped lead: in chase window {school.FollowingSeconds:F2}s, physical contacts {hits}.");
                    if(hits==0 || school.FollowingSeconds<=0)throw new InvalidOperationException(report.ToString());
                }
                if(lesson==DrivingSchool.Lesson.LeftCircle){
                    school.RestartAttempt();
                    school.Sample(Vector3.forward,Vector3.back,dt);
                    school.Sample(new Vector3(2*school.halfWidth,0,-1),new Vector3(2*school.halfWidth,0,1),dt);
                    if(school.NextGate!=0)throw new InvalidOperationException("Reverse or off-lane crossing advanced the gate.");
                    car.gameObject.SetActive(false);
                    var entry=Object.Instantiate(prefab,new Vector3(0,.35f,-50),Quaternion.identity,root.transform);
                    entry.Chassis.interpolation=RigidbodyInterpolation.None;
                    school.Begin(entry,session.entries[0]);
                    var pedals=new EntryDriver();entry.Driver=pedals;
                    var drive=(Action)Delegate.CreateDelegate(typeof(Action),entry,"FixedUpdate");
                    float entryTime=-1, peak=0;
                    for(int i=0;i*dt<12;i++){
                        entry.EnabledDrive=i*dt>1;
                        if(entryTime<0 && school.NextGate!=0)entryTime=i*dt;
                        float since=i*dt-entryTime;
                        pedals.command=entryTime<0?new DriveCommand{throttle=1}:new DriveCommand{
                            steer=since<1?-.78f:.25f,throttle=.85f,hand=since>.12f && since<.85f?1:0};
                        var before=entry.Chassis.position;drive();sim.Simulate(dt);school.Sample(before,entry.Chassis.position,dt);
                        if(entryTime>=0)peak=Mathf.Max(peak,entry.SignedDriftAngle);
                        if(entryTime>=0 && since>2)break;
                    }
                    report.AppendLine($"Left entry: peak outward slip {peak:F1} deg, scored {school.SlidingSeconds:F2}s; reverse and missed gate rejected.");
                    if(school.SlidingSeconds<=0)throw new InvalidOperationException(report.ToString());
                }
            }finally{SceneManager.UnloadSceneAsync(scene);}
        }
        return report.ToString();
    }
}
}
