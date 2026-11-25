using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Apex.Editor {
public static class VehicleComparisonCheck {
    sealed class Trial : IDriver, IDisposable {
        public readonly RaceCar car;
        readonly Action advance;
        readonly PhysicsScene physics;
        public readonly float dt=Time.fixedDeltaTime;
        public DriveCommand input;
        public DriveCommand Read()=>input;
        public Vector3 Velocity=>Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up);
        public float Kph=>Velocity.magnitude*3.6f;
        public float YawRate=>car.Chassis.angularVelocity.y*Mathf.Rad2Deg;

        public Trial(RaceCar prefab,Scene scene){
            physics=scene.GetPhysicsScene();
            car=Object.Instantiate(prefab,Vector3.up*.35f,Quaternion.identity,new InstantiateParameters{scene=scene});
            car.Chassis.interpolation=RigidbodyInterpolation.None;
            car.Driver=this;
            advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
            for(int i=0;i<Mathf.RoundToInt(1.2f/dt);i++)Step();
            car.EnabledDrive=true;input.throttle=1;
        }
        public void Step(){advance();physics.Simulate(dt);}
        public void AccelerateTo(float target){
            for(int i=0;i<Mathf.RoundToInt(90/dt);i++){
                Step();
                if(Kph>=target)return;
            }
            throw new InvalidOperationException(car.spec.name+" did not reach "+target+" km/h.");
        }
        public void Dispose()=>Object.DestroyImmediate(car.gameObject);
    }

    [MenuItem("Apex/Checks/Compare garage cars in Play Mode")]
    public static void Check()=>Debug.Log(Run());
    public static string Run(){
        if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
        var session=Object.FindAnyObjectByType<RaceSession>();
        var scene=SceneManager.CreateScene("Car comparison "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var report=new StringBuilder();
        try{
            var floor=new GameObject("Flat dry road");
            SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=Vector3.down*.5f;
            floor.AddComponent<BoxCollider>().size=new Vector3(30000,1,30000);
            report.AppendLine(FormattableString.Invariant($"Fixed step {Time.fixedDeltaTime:F3} s; stock differential, fresh tyres, dry flat road. Speeds are horizontal ground speeds."));
            foreach(var prefab in session.entries){
                report.AppendLine(prefab.spec.displayName);
                void Measure(Func<Trial,string> test){using(var t=new Trial(prefab,scene))report.AppendLine(test(t));}
                Measure(Acceleration);
                Measure(ManualShifts);
                Measure(t=>Brake(t,100));Measure(t=>Brake(t,160));
                foreach(int side in new[]{1,-1}){
                    Measure(t=>Corner(t,side,.35f,false));
                    Measure(t=>Corner(t,side,1,false));
                    Measure(t=>Corner(t,side,1,true));
                }
                if(prefab.spec.slipSteerGain!=0)Measure(WithoutSlipSteer);
            }
        }finally{SceneManager.UnloadSceneAsync(scene);}
        string folder=Path.Combine(Application.dataPath,"../毕设资料/02-车辆设计/领克155");
        Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"两车性能对照实测.txt"),report.ToString());
        return report.ToString();
    }

    static string Acceleration(Trial t){
        float z=t.car.Chassis.position.z,time400=0,speed400=0,peak=0,lastMin=float.PositiveInfinity,lastMax=0;
        var thresholds=new[]{60,80,100,120,160,200};
        var times=new float[thresholds.Length];
        for(int tick=1;tick<=Mathf.RoundToInt(90/t.dt);tick++){
            t.Step();float time=tick*t.dt,v=t.Kph;
            for(int j=0;j<thresholds.Length;j++)if(times[j]==0 && v>=thresholds[j])times[j]=time;
            if(time400==0 && t.car.Chassis.position.z-z>=400){time400=time;speed400=v;}
            peak=Mathf.Max(peak,v);
            if(time>=80){lastMin=Mathf.Min(lastMin,v);lastMax=Mathf.Max(lastMax,v);}
        }
        var seconds=Array.ConvertAll(times,v=>v==0?"unreached":v.ToString("F2",CultureInfo.InvariantCulture));
        return FormattableString.Invariant($"straight: 0 to 60/80/100/120/160/200 km/h [s] {string.Join("/",seconds)}; 400 m {time400:F2} s @ {speed400:F2} km/h; peak {peak:F2}, last 10 s {lastMin:F2}..{lastMax:F2} km/h; final G{t.car.Gear+1} at {t.car.EngineRpm:F0} rpm");
    }

    static string ManualShifts(Trial t){
        var log=new StringBuilder("requested upshifts at 30 and 60 s:");
        int interval=Mathf.RoundToInt(30/t.dt);
        for(int i=0;i<3*interval;i++){
            t.input.gearChange=i==interval || i==2*interval?1:0;
            t.Step();
            if((i+1)%interval==0)log.Append(FormattableString.Invariant($" {(i+1)*t.dt:F0}s G{t.car.Gear+1}, {t.Kph:F2} km/h, {t.car.EngineRpm:F0} rpm;"));
        }
        return log.ToString();
    }

    static string Brake(Trial t,float entry){
        t.AccelerateTo(entry);
        float initial=t.Kph,distance=0;var pos=t.car.Chassis.position;
        t.input=new DriveCommand{brake=1};
        for(int tick=1;tick<=Mathf.RoundToInt(20/t.dt);tick++){
            t.Step();var next=t.car.Chassis.position;distance+=Vector3.ProjectOnPlane(next-pos,Vector3.up).magnitude;pos=next;
            if(t.Kph<=1)return FormattableString.Invariant($"brake{entry}: {initial:F2} to {t.Kph:F2} km/h; {tick*t.dt:F2} s, {distance:F2} m");
        }
        throw new InvalidOperationException("Incomplete brake test at "+entry+" km/h.");
    }

    static string Corner(Trial t,int side,float throttle,bool useHandbrake){
        t.AccelerateTo(80);var entry=t.Velocity;
        float peakBeta=0,heading=0;
        t.input.throttle=throttle;
        int hold=Mathf.RoundToInt(2/t.dt);
        for(int tick=0;tick<hold;tick++){
            float time=tick*t.dt;
            float degrees=4*Mathf.Min(1,(tick+1)*t.dt/.25f);
            t.input.steer=side*degrees/t.car.spec.steeringBySpeed.Evaluate(t.car.Speed);
            t.input.hand=useHandbrake && time>=.5f && time<.74f?1:0;
            t.Step();
            peakBeta=Mathf.Max(peakBeta,t.car.DriftAngle);heading+=side*t.YawRate*t.dt;
        }
        float pathTurn=side*Vector3.SignedAngle(entry,t.Velocity,Vector3.up);
        var report=FormattableString.Invariant($"corner {(side>0?"R":"L")} gas {throttle:P0} hand {useHandbrake}: entry {entry.magnitude*3.6f:F2}, 2s speed {t.Kph:F2} km/h; heading {heading:F2}, course {pathTurn:F2}, peak sideslip {peakBeta:F2}, end sideslip {t.car.DriftAngle:F2} deg; wheel FL/FR {t.car.wheels[0].steerAngle:F2}/{t.car.wheels[1].steerAngle:F2} deg; yaw {side*t.YawRate:F2} deg/s");
        t.input.steer=0;t.input.hand=0;
        for(int i=0;i<Mathf.RoundToInt(1/t.dt);i++)t.Step();
        return report+FormattableString.Invariant($", 1s after release {side*t.YawRate:F2} deg/s");
    }
    static string WithoutSlipSteer(Trial t){
        var original=t.car.spec;
        var variant=Object.Instantiate(original);
        try{
            variant.slipSteerGain=0;t.car.spec=variant;
            return "isolated copy, slipSteerGain=0: "+Corner(t,1,1,false);
        }finally{t.car.spec=original;Object.DestroyImmediate(variant);}
    }
}
}
