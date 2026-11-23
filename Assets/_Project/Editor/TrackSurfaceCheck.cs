using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
// All cases start with the same cold car, wheel speeds and clutch position.
public static class TrackSurfaceCheck {
    sealed class Driver : IDriver {
        public DriveCommand command;
        public DriveCommand Read()=>command;
    }

    static TyreSurface Half(Scene scene,float x,RoadsideSettings settings){
        var ground=new GameObject("Test surface");
        SceneManager.MoveGameObjectToScene(ground,scene);
        ground.transform.position=new Vector3(x,-.5f,0);
        ground.AddComponent<BoxCollider>().size=new Vector3(100,1,400);
        var surface=ground.AddComponent<TyreSurface>();
        surface.settings=settings;
        return surface;
    }

    [MenuItem("Apex/Measure dry track surfaces in Play Mode")]
    static void Menu()=>Debug.Log(Run());

    public static string Run(){
        if(!Application.isPlaying)throw new InvalidOperationException("Requires Play Mode");
        var settings=AssetDatabase.LoadAssetAtPath<RoadsideSettings>("Assets/_Project/Tracks/Roadside.asset");
        var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
        var scene=SceneManager.CreateScene("Dry surface comparison "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var csv=new StringBuilder("left,right,braking,start_kph,distance_to_1kph_m,effective_decel_g,yaw_rate_at_0p4s_deg_s,max_rim_speed_locked_window_mps,matched_0p4s_ticks,total_0p4s_ticks\n");
        var lateral=new StringBuilder("surface,start_kph,initial_slip_deg,window_s,mean_path_lateral_g\n");
        try{
            var left=Half(scene,-50,settings);
            var right=Half(scene,50,settings);
            var physics=scene.GetPhysicsScene();
            foreach(var cover in new[]{RoadCover.Asphalt,RoadCover.Kerb,RoadCover.Grass}){
                Measure(cover,cover,true);
                MeasureLateral(cover);
                if(cover==RoadCover.Asphalt)Measure(cover,cover,false);
                else{
                    Measure(RoadCover.Asphalt,cover,false);
                    Measure(cover,RoadCover.Asphalt,false);
                }
            }
            // Observe the direction of velocity as an initially sideslipping car recovers.
            // No steering, brake, throttle or closed-loop driver is involved.
            void MeasureLateral(RoadCover cover){
                left.cover=right.cover=cover;
                var car=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.35f,-70),Quaternion.identity);
                SceneManager.MoveGameObjectToScene(car.gameObject,scene);
                car.Chassis.interpolation=RigidbodyInterpolation.None;
                var advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                var driver=new Driver();
                car.Driver=driver;
                float dt=Time.fixedDeltaTime;
                for(int i=0;i<60;i++){advance();physics.Simulate(dt);}
                car.EnabledDrive=true;
                driver.command=new DriveCommand{clutch=1};
                float start=40/3.6f;
                car.Chassis.linearVelocity=Quaternion.Euler(0,15,0)*Vector3.forward*start;
                foreach(var w in car.wheels)w.rotationSpeed=start*Mathf.Cos(15*Mathf.Deg2Rad)/w.radius*Mathf.Rad2Deg;
                float impulse=0;
                int steps=Mathf.RoundToInt(.2f/dt);
                for(int i=0;i<steps;i++){
                    var before=Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up);
                    advance();physics.Simulate(dt);
                    for(int j=0;j<4;j++)if(car.WheelCover(j)!=cover)throw new InvalidOperationException("Lost test surface.");
                    var after=Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up);
                    impulse+=.5f*(before.magnitude+after.magnitude)*Vector3.SignedAngle(before,after,Vector3.up)*Mathf.Deg2Rad;
                }
                lateral.AppendLine(FormattableString.Invariant($"{cover},40,15,{steps*dt:F3},{Mathf.Abs(impulse)/(steps*dt*Physics.gravity.magnitude):F4}"));
                car.gameObject.SetActive(false);
            }
            void Measure(RoadCover l,RoadCover r,bool lockWheels){
                left.cover=l;
                right.cover=r;
                var car=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.35f,-70),Quaternion.identity);
                SceneManager.MoveGameObjectToScene(car.gameObject,scene);
                car.Chassis.interpolation=RigidbodyInterpolation.None;
                var driver=new Driver();
                car.Driver=driver;
                var advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                float dt=Time.fixedDeltaTime;
                for(int i=0;i<60;i++){advance();physics.Simulate(dt);}
                car.EnabledDrive=true;
                driver.command=new DriveCommand{brake=1,clutch=1};
                float start=40/3.6f;
                car.Chassis.linearVelocity=Vector3.forward*start;
                foreach(var wheel in car.wheels)wheel.rotationSpeed=start/wheel.radius*Mathf.Rad2Deg;
                float distance=0,yaw=0,rimMax=0,speed=start;
                int matched=0,samples=0,lockTicks=0;
                for(int step=1;step<=500 && speed>1/3.6f;step++){
                    var before=car.Chassis.worldCenterOfMass;
                    advance();
                    if(lockWheels)foreach(var wheel in car.wheels){wheel.motorTorque=0;wheel.brakeTorque=100000;}
                    if((step-1)*dt<.4f-.0001f){
                        samples++;
                        if(car.WheelCover(0)==l && car.WheelCover(1)==r &&
                           car.WheelCover(2)==l && car.WheelCover(3)==r)matched++;
                    }
                    physics.Simulate(dt);
                    var displacement=car.Chassis.worldCenterOfMass-before;
                    distance+=Vector3.ProjectOnPlane(displacement,Vector3.up).magnitude;
                    speed=Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up).magnitude;
                    if(step==Mathf.RoundToInt(.4f/dt))yaw=car.Chassis.angularVelocity.y*Mathf.Rad2Deg;
                    if(lockWheels && speed*3.6f<32 && speed*3.6f>8){
                        lockTicks++;
                        foreach(var wheel in car.wheels)rimMax=Mathf.Max(rimMax,Mathf.Abs(wheel.rpm)*Mathf.PI/30*wheel.radius);
                    }
                }
                if(matched!=samples || speed>1/3.6f || (lockWheels && (lockTicks==0 || rimMax>.2f)))
                    throw new InvalidOperationException($"Surface test {l}/{r}: {matched}/{samples} mixed-contact steps, final {speed:F2} m/s, rim {rimMax:F2} m/s.");
                float decel=(start*start-speed*speed)/(2*distance*Physics.gravity.magnitude);
                csv.AppendLine(FormattableString.Invariant($"{l},{r},{(lockWheels?"locked":"service")},40,{distance:F3},{decel:F4},{yaw:F3},{(lockWheels?rimMax.ToString("F3",CultureInfo.InvariantCulture):"")},{matched},{samples}"));
                car.gameObject.SetActive(false);
            }
            string dir=Path.Combine(Application.dataPath,"../Logs/HandlingChecks");
            Directory.CreateDirectory(dir);
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture);
            string path=Path.Combine(dir,"surfaces-"+stamp+".csv");
            File.WriteAllText(path,csv.ToString());
            File.WriteAllText(Path.Combine(dir,"lateral-surfaces-"+stamp+".csv"),lateral.ToString());
            return csv.Append(lateral).Append(path).ToString();
        }finally{SceneManager.UnloadSceneAsync(scene);}
    }
}
}
