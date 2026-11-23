using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
public static class CompetitionPerformanceCheck {
    sealed class Driver : IDriver {
        public DriveCommand command;
        public DriveCommand Read() => command;
    }

    [MenuItem("Apex/Measure competition cornering and tyre temperatures in Play Mode")]
    static void Menu() => Debug.Log(Run());

    public static string Run() {
        if (!Application.isPlaying) throw new InvalidOperationException("Requires Play Mode");
        var scene = SceneManager.CreateScene("Grip measurement " + Guid.NewGuid(), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var report = new StringBuilder();
        var trace = new StringBuilder("target_kph,rack_input,time_s,speed_kph,x,z,turn_deg,grounded,FL_deg,FR_deg,RL_tread_c,RL_carcass_c,RL_gas_c,RL_bar,RL_slip_w\n");
        try {
            var road = new GameObject("Flat dry road");
            SceneManager.MoveGameObjectToScene(road,scene);
            road.transform.position = Vector3.down * .5f;
            road.AddComponent<BoxCollider>().size = new Vector3(1500,1,1500);
            var prefab = AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
            CheckHeat(prefab.spec.competition);
            foreach (var sample in new[]{new Vector2(30,.10f),new Vector2(30,-.10f),new Vector2(60,.10f),new Vector2(60,.14f),new Vector2(60,.20f),new Vector2(60,.30f)}) {
                var car = UnityEngine.Object.Instantiate(prefab,Vector3.up*.35f,Quaternion.identity);
                SceneManager.MoveGameObjectToScene(car.gameObject,scene);
                car.Chassis.interpolation = RigidbodyInterpolation.None;
                var driver = new Driver();
                car.Driver = driver;
                var advance = (Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                var physics = scene.GetPhysicsScene();
                float dt = Time.fixedDeltaTime;
                void Step() { advance(); physics.Simulate(dt); }
                for (int i=0;i<60;i++) Step();
                car.EnabledDrive = true;
                driver.command.throttle = 1;
                float target = sample.x/3.6f;
                for (int i=0;i<1000 && car.Speed<target;i++) Step();
                float turn=0, distance=0, elapsed=0, lateralImpulse=0, integral=0;
                float minSpeed=float.MaxValue, maxSpeed=0;
                int groundedTicks=0;
                for(int i=0;i*dt<12;i++){
                    var before = Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up);
                    float error = target - before.magnitude;
                    integral = Mathf.Clamp(integral + error*dt,-2,2);
                    float pedal = .24f*error+.14f*integral;
                    driver.command = new DriveCommand {steer=sample.y,throttle=Mathf.Clamp01(pedal),brake=Mathf.Clamp01(-pedal)};
                    Step();
                    var after = Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up);
                    float angle = Vector3.SignedAngle(before,after,Vector3.up);
                    float speed = .5f*(before.magnitude+after.magnitude);
                    var h = car.CompetitionChassis;
                    trace.AppendLine(FormattableString.Invariant($"{sample.x},{sample.y},{(i+1)*dt:F3},{speed*3.6f:F3},{car.Chassis.worldCenterOfMass.x:F4},{car.Chassis.worldCenterOfMass.z:F4},{angle:F5},{car.GroundedCount},{car.wheels[0].steerAngle:F2},{car.wheels[1].steerAngle:F2},{h.TyreC(2):F3},{h.CarcassC(2):F3},{h.GasC(2):F3},{h.TyreBar(2):F4},{h.SlipPowerW(2):F1}"));
                    if(i*dt<8) continue;
                    float radians=angle*Mathf.Deg2Rad;
                    turn+=radians; distance+=speed*dt; elapsed+=dt; lateralImpulse+=speed*radians;
                    minSpeed=Mathf.Min(minSpeed,speed); maxSpeed=Mathf.Max(maxSpeed,speed);
                    if(car.GroundedCount==4) groundedTicks++;
                }
                bool held = Mathf.Abs(turn)>.05f && groundedTicks*dt>=elapsed-.05f &&
                    Mathf.Abs(minSpeed-target)<1 && Mathf.Abs(maxSpeed-target)<1;
                if (sample.x==30 && !held) throw new InvalidOperationException("Low-speed reference failed at "+sample);
                string corner = held
                    ? FormattableString.Invariant($"path radius {distance/Mathf.Abs(turn):F2} m, mean lateral {Mathf.Abs(lateralImpulse)/elapsed/Physics.gravity.magnitude:F3} g")
                    : "did not hold the target speed and turn for the measurement window";
                report.AppendLine(FormattableString.Invariant($"{sample.x} km/h input {sample.y:F2}, FL/FR {car.wheels[0].steerAngle:F2}/{car.wheels[1].steerAngle:F2} deg, speed {minSpeed*3.6f:F2}-{maxSpeed*3.6f:F2} km/h, {corner}, RL tread/carcass/gas {car.CompetitionChassis.TyreC(2):F1}/{car.CompetitionChassis.CarcassC(2):F1}/{car.CompetitionChassis.GasC(2):F1} C."));
                car.gameObject.SetActive(false);
            }
            string folder=Path.Combine(Application.dataPath,"../Logs/HandlingChecks");
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"grip-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+".csv");
            File.WriteAllText(path,trace.ToString());
            return report.Append(path).ToString();
        } finally { SceneManager.UnloadSceneAsync(scene); }
    }

    static void CheckHeat(CompetitionSetup s) {
        var curve=new WheelFrictionCurve {extremumSlip=.2f,extremumValue=1,asymptoteSlip=.5f,asymptoteValue=.8f,stiffness=1};
        if(TyreThermalState.SlipPower(-1000,curve,curve,1,1,5,3)!=0 ||
            TyreThermalState.SlipPower(4000,curve,curve,1,1,0,0)!=0)
            throw new InvalidOperationException("Heat without slip work");
        float forward=TyreThermalState.SlipPower(3000,curve,curve,.2f,0,5,0);
        curve.stiffness=.78f;
        float reverse=TyreThermalState.SlipPower(3000,curve,curve,-.2f,0,-5,0);
        if(Mathf.Abs(reverse-forward*.78f)>1) throw new InvalidOperationException("Hot/reverse slip power");
        var state=new TyreThermalState(s.ambientC);
        for(int i=0;i<1500;i++) state.Step(s,50000,0,true,15,.02f);
        if(!(state.TreadC>state.CarcassC && state.CarcassC>state.AirC && state.AirC>s.ambientC))
            throw new InvalidOperationException("Thermal layers failed to separate during heating");
        var fast=state;
        for(int i=0;i<500;i++){state.Step(s,0,0,true,0,.02f);fast.Step(s,0,0,true,25,.02f);}
        if(!(fast.TreadC<state.TreadC && state.AirC>s.ambientC))
            throw new InvalidOperationException("Airflow or delayed pressure response failed");
    }
}
}
