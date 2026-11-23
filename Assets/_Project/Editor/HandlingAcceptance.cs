using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Apex.Editor {
// In play mode the local PhysicsScene is explicitly stepped. No scene assets or saved parts are edited.
public static class HandlingAcceptance {
    const string Root="Assets/_Project/";
    const float Dt=.02f;
    static readonly CultureInfo Ci=CultureInfo.InvariantCulture;
    static readonly string Output=Path.GetFullPath("Logs/HandlingChecks/handling-v9");
    static string current;
    static Scene ridge;
    static RaceCar prefab;
    static WorkshopCatalog catalog;
    static bool assistance;
    static StreamWriter trace;
    static readonly StringBuilder line=new StringBuilder(2300);
    sealed class Buttons:IDriver { public DriveCommand command; public DriveCommand Read()=>command; }
    struct Frame {
        public Vector3 p,v,w;
        public Quaternion q;
        public DriveCommand c;
        public float time,beta,yaw,kph;
        public bool enabled;
        public float[] rpm,tread,core,gas,drive;
    }
    sealed class Rig:IDisposable {
        public RaceCar car;
        public Buttons buttons=new Buttons();
        public Action command;
        public Action<float> stepDrive;
        public PhysicsScene physics;
        public HandlingContactProbe hits;
        public Rig(Scene scene,Vector3 p,Quaternion q){
            car=Object.Instantiate(prefab,p,q);SceneManager.MoveGameObjectToScene(car.gameObject,scene);
            car.FitParts(catalog.differentials.Single(v=>v.id=="club-lsd"),catalog.wheels.Single(v=>v.id=="original"));
            car.Chassis.interpolation=RigidbodyInterpolation.None;
            car.Driver=buttons;physics=scene.GetPhysicsScene();
            hits=car.gameObject.AddComponent<HandlingContactProbe>();
            command=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
            stepDrive=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),car,"StepDrive");
        }
        public void Step(){command();physics.Simulate(Dt);}
        public void Dispose(){Object.DestroyImmediate(car.gameObject);}
    }
    static float Read(string s)=>float.Parse(s,Ci);
    static Frame[] Load(string suffix){
        string path=Directory.GetFiles("Logs/DriveTelemetry","*"+suffix+".csv").Single();
        using var file=File.OpenText(path);
        var ix=file.ReadLine().TrimStart('\uFEFF').Split(',').Select((k,i)=>(k,i)).ToDictionary(a=>a.k,a=>a.i);
        var list=new List<Frame>();
        string text;
        while((text=file.ReadLine())!=null){
            var a=text.Split(',');
            float N(string key)=>Read(a[ix[key]]);
            Vector3 V(string key)=>new Vector3(N(key+"x"),N(key+"y"),N(key+"z"));
            float[] Wheels(string key)=>new[]{"FL","FR","RL","RR"}.Select(k=>N(k+"_"+key)).ToArray();
            var p=V("");var f=V("front_axle_");var r=V("rear_axle_");var forward=(f-r).normalized;
            var q=Quaternion.LookRotation(forward,Vector3.ProjectOnPlane((f+r)*.5f-p,forward).normalized);
            var v=new Frame{time=N("sim_s"),p=p,q=q,enabled=N("drive_enabled")>0,
                c=new DriveCommand{digitalSteering=true,steer=N("steer_cmd"),throttle=N("throttle"),brake=N("brake"),
                    reverse=N("reverse")>0,hand=N("hand"),clutch=N("clutch_pedal"),gearChange=(int)N("gear_request"),resumeAutomatic=N("auto_request")>0},
                rpm=Wheels("rpm"),tread=Wheels("tyre_c"),core=Wheels("carcass_c"),gas=Wheels("gas_c"),drive=Wheels("drive_nm"),
                beta=N("signed_drift_deg"),yaw=N("yaw_deg_s"),kph=N("speed_mps")*3.6f};
            if(list.Count>0){
                var prev=list[list.Count-1];(q*Quaternion.Inverse(prev.q)).ToAngleAxis(out var deg,out var axis);
                v.w=axis*Mathf.DeltaAngle(0,deg)*Mathf.Deg2Rad/Dt;
                var raw=q*new Vector3(N("body_lateral_mps"),0,N("body_forward_mps"));var up=q*Vector3.up;
                // Position is the chassis origin, velocity in the log is the COM velocity.
                var com=new Vector3(0,prefab.spec.centerHeight,(prefab.spec.frontWeight-.5f)*prefab.spec.wheelbase);
                var diff=(p+q*com-prev.p-prev.q*com)/Dt;
                v.v=raw+up*Vector3.Dot(diff,up);
                v.w+=up*(v.yaw*Mathf.Deg2Rad-Vector3.Dot(v.w,up));
            }
            list.Add(v);
        }
        return list.ToArray();
    }
    static void Match(Rig rig,Frame f){
        var b=rig.car.Chassis;b.position=f.p;b.rotation=f.q;b.linearVelocity=f.v;b.angularVelocity=f.w;
        for(int j=0;j<4;j++)rig.car.wheels[j].rotationSpeed=f.rpm[j]*6;
    }
    // Old records contain all three thermal nodes. Diagnostic seeding only, outside scored intervals.
    static void Temperatures(RaceCar car,Frame f){
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var nodes=(TyreThermalState[])typeof(CompetitionChassis).GetField("tyres",flags).GetValue(car.CompetitionChassis);
        for(int j=0;j<4;j++){
            object node=nodes[j];
            typeof(TyreThermalState).GetField("<TreadC>k__BackingField",flags).SetValue(node,f.tread[j]);
            typeof(TyreThermalState).GetField("<CarcassC>k__BackingField",flags).SetValue(node,f.core[j]);
            typeof(TyreThermalState).GetField("<AirC>k__BackingField",flags).SetValue(node,f.gas[j]);
            nodes[j]=(TyreThermalState)node;
        }
    }
    static float Heading(Vector3 v)=>Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg;
    static void Value(float f)=>line.Append(',').Append(f.ToString("G8",Ci));
    static void Record(string test,string variant,int n,Rig rig,VehicleStepTrace audit,Frame? observed,float sampleDt=Dt){
        var car=rig.car;var b=car.Chassis;var v=Quaternion.Inverse(b.rotation)*b.linearVelocity;
        line.Clear();line.Append(test).Append(',').Append(variant);Value(n*sampleDt);
        Value(b.position.x);Value(b.position.y);Value(b.position.z);Value(car.Speed*3.6f);
        Value(Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg);Value(Vector3.Dot(b.angularVelocity,b.rotation*Vector3.up)*Mathf.Rad2Deg);
        Value(Heading(b.rotation*Vector3.forward));Value(Heading(b.linearVelocity));Value(car.GroundedCount);
        Value(rig.hits.events);Value(car.ResetGeneration);Value(car.Gear+1);Value(car.AppliedCommand.steer);Value(car.AppliedCommand.throttle);Value(car.AppliedCommand.hand);
        for(int j=0;j<4;j++){
            var w=car.wheels[j];w.GetGroundHit(out var hit);Value(w.steerAngle);Value(w.motorTorque);Value(w.brakeTorque);Value(w.rpm);
            Value(hit.force);Value(hit.forwardSlip);Value(hit.sidewaysSlip);Value((int)car.WheelCover(j));
            Value(car.CompetitionChassis.TyreC(j));Value(car.CompetitionChassis.CarcassC(j));Value(car.CompetitionChassis.GasC(j));
        }
        if(observed.HasValue){
            var f=observed.Value;Value(Vector2.Distance(new Vector2(b.position.x,b.position.z),new Vector2(f.p.x,f.p.z)));
            Value(f.kph);Value(f.beta);Value(f.yaw);
        }else line.Append(",,,,");
        audit.Append(line,car,sampleDt);trace.WriteLine(line.ToString());
    }
    static void Event(string name,Frame[] data,float at,int length,string mode){
        int a=Array.FindIndex(data,f=>Mathf.Abs(f.time-at)<.005f);
        bool grid=mode=="grid";int start=grid?0:a-250,free=grid?0:a-66;
        var f=data[start];var course=ridge.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Circuit>()).Single();
        using var rig=new Rig(ridge,grid?course.grid.position:f.p,grid?course.grid.rotation:f.q);
        var car=rig.car;var audit=rig.hits.trace;
        if(!grid)Temperatures(car,f);
        for(int i=start;i<=a+length;i++){
            f=data[i];int n=i-a;
            if(!grid && i<=free){Match(rig,f);if(i==free)Temperatures(car,f);}
            car.EnabledDrive=f.enabled;
            var c=f.c;
            c.keyboardRecovery=n>=0 && assistance;
            if(n==0 && mode=="six-substeps")car.wheels[0].ConfigureVehicleSubsteps(10,5,6);
            bool half=n>=0 && mode=="half-step";
            if(n>=0){
                if(mode=="lift")c.throttle=0;
                if(mode=="clutch")c.clutch=1;
                if(mode=="counter" && n>=10 && n<30)c.steer=Mathf.Sign(data[a].beta);
            }
            rig.buttons.command=c;
            if(half)rig.stepDrive(Dt*.5f);else rig.command();
            if(n>=0 && (mode=="recorded" || mode=="equal")){
                float total=f.drive[2]+f.drive[3];
                for(int j=2;j<4;j++)car.wheels[j].motorTorque=mode=="equal"?total*.5f:f.drive[j];
            }
            float subDt=half?Dt*.5f:Dt;
            if(n>=-66)Record(name,mode,half?2*n:n,rig,audit,f,subDt);
            if(i==a+length)break;
            rig.physics.Simulate(subDt);
            if(half){
                rig.stepDrive(subDt);Record(name,mode,2*n+1,rig,audit,null,subDt);rig.physics.Simulate(subDt);
            }
        }
    }
    static void Pad(Scene scene,int kph,int side,int hold,int delay,string kind){
        using var rig=new Rig(scene,new Vector3(0,.35f,0),Quaternion.identity);
        for(int i=0;i<150;i++)rig.Step();
        rig.car.EnabledDrive=true;
        var cmd=new DriveCommand{digitalSteering=true,throttle=1,keyboardRecovery=assistance};
        rig.buttons.command=cmd;
        int warm=0;
        while(rig.car.Speed*3.6f<kph && warm++<2200)rig.Step();
        if(warm>=2200)throw new InvalidOperationException("Could not reach entry speed");
        string key=$"{kind}_{kph}_{side}_{hold}_{delay}";
        for(int i=0;i<=220;i++){
            // Fixed inputs. 10 Hz key decisions with 0.16/0.20/0.24 s latency and 0.10 s hold perturbations.
            cmd.steer=i<hold?side:0;cmd.hand=0;cmd.throttle=1;
            if((kind=="normal" || kind=="longpress") && i>=hold+delay && i<hold+delay+10)cmd.steer=-side;
            if(kind=="drift" || kind=="handentry"){
                cmd.hand=i>=10 && i<20?1:0;
                if(kind=="handentry")cmd.hand=i>=hold-15 && i<hold?1:0;
                if(i>=hold+delay && i<hold+delay+20)cmd.steer=-side;
                if(i>=85)cmd.throttle=0;
            }
            if(kind=="sustain"){
                cmd.hand=i>=hold-15 && i<hold?1:0;
                cmd.steer=i<hold?side:i>=115+delay && i<135+delay?-side:0;
                if(i>=115)cmd.throttle=0;
            }
            rig.buttons.command=cmd;rig.command();Record(key,"fixed",i,rig,rig.hits.trace,null);rig.physics.Simulate(Dt);
        }
    }
    static void Tests(){
        prefab=AssetDatabase.LoadAssetAtPath<RaceCar>(Root+"Vehicles/SHIBATA31/OkuibukiCar.prefab");
        catalog=AssetDatabase.LoadAssetAtPath<WorkshopCatalog>(Root+"Lobby/Parts/Workshop.asset");
        string revision="r"+prefab.spec.competition.revision+(assistance?"":"-off");
        current=Path.Combine(Output,revision);
        Directory.CreateDirectory(current);
        string temp=Path.Combine(current,"frames.tmp");
        using(trace=new StreamWriter(temp,false,new UTF8Encoding(false),65536)){
            line.Clear();line.Append("case,variant,t,x,y,z,kph,beta,yaw,heading,path,ground,contacts,resets,gear,key,gas,hand");
            foreach(var w in new[]{"FL","FR","RL","RR"})
                foreach(var k in new[]{"steer","drive","brake","rpm","load","long_slip","side_slip","cover","tread","core","gas"})line.Append(',').Append(w).Append('_').Append(k);
            line.Append(",position_error,observed_kph,observed_beta,observed_yaw");VehicleStepTrace.Headers(line);trace.WriteLine(line.ToString());
            var main=Load("01c464b3");var other=Load("73a0ea1d");
            foreach(var mode in new[]{"grid","continue","lift","clutch","recorded","equal","counter"})Event("long",main,146.5599f,188,mode);
            foreach(var mode in new[]{"continue","lift","clutch","recorded","equal","six-substeps","half-step"})Event("rise",main,148.5599f,50,mode);
            foreach(var mode in new[]{"grid","continue","counter"}){
                Event("independent_L",other,305.1799f,110,mode);Event("independent_R",other,318.6599f,120,mode);
            }
            var scene=SceneManager.CreateScene("Handling pad",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try{
                var pad=new GameObject("Flat dry asphalt");SceneManager.MoveGameObjectToScene(pad,scene);
                pad.transform.position=Vector3.down*.5f;pad.AddComponent<BoxCollider>().size=new Vector3(3000,1,3000);
                foreach(int side in new[]{-1,1}){
                    foreach(int delay in new[]{8,10,12})foreach(int hold in new[]{25,30,35}){
                        Pad(scene,90,side,hold,delay,"normal");Pad(scene,105,side,hold,delay,"normal");
                    }
                    foreach(int hold in new[]{30,35,40})Pad(scene,85,side,hold,10,"drift");
                    foreach(int delay in new[]{8,10,12})foreach(int hold in new[]{45,50,55}){
                        Pad(scene,90,side,hold,delay,"longpress");Pad(scene,105,side,hold,delay,"longpress");
                    }
                    foreach(int hold in new[]{45,50,55})Pad(scene,85,side,hold,10,"handentry");
                    foreach(int hold in new[]{45,50,55})Pad(scene,85,side,hold,10,"sustain");
                }
            }finally{SceneManager.UnloadSceneAsync(scene);}
        }
        File.Copy(temp,Path.Combine(current,"frames.csv"),true);File.Delete(temp);
        File.WriteAllText(Path.Combine(current,"complete.txt"),Application.unityVersion+" "+revision+" club-lsd/original, dt="+Time.fixedDeltaTime);
    }
    [MenuItem("Apex/Tests/Handling acceptance")]
    public static void Run()=>Start(true);
    [MenuItem("Apex/Tests/Handling acceptance without assistance")]
    public static void RunBaseline()=>Start(false);
    static void Start(bool recovery){
        if(!Application.isPlaying)throw new InvalidOperationException("Start Bootstrap Play Mode first");
        assistance=recovery;
        ridge=SceneManager.GetSceneByName("Ridge");
        if(!ridge.isLoaded){
            var op=SceneManager.LoadSceneAsync("Ridge",new LoadSceneParameters(LoadSceneMode.Additive,LocalPhysicsMode.Physics3D));
            op.completed+=_=>{ridge=SceneManager.GetSceneByName("Ridge");Execute();};
        }else Execute();
    }
    static void Execute(){
        if(ridge.GetPhysicsScene()==Physics.defaultPhysicsScene)throw new InvalidOperationException("Run from garage, not racing");
        try{Tests();Debug.Log("Handling acceptance complete: "+current);}
        catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());Debug.LogException(e);}
        finally{SceneManager.UnloadSceneAsync(ridge);}
    }
}
public sealed class HandlingContactProbe:MonoBehaviour {
    public int events;
    public readonly VehicleStepTrace trace=new VehicleStepTrace();
    void OnCollisionEnter(Collision c){events++;trace.Contact(c,GetComponent<Rigidbody>());}
    void OnCollisionStay(Collision c){trace.Contact(c,GetComponent<Rigidbody>());}
}
}
