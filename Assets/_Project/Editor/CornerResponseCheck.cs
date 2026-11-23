using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
public static class CornerResponseCheck {
    sealed class Driver:IDriver {
        public DriveCommand input;
        public DriveCommand Read()=>input;
    }
    // Isolate the handbrake on a level pad. Each car gets its own spec,
    // contact history, and lane; both directions receive mirrored inputs.
    public static string Run(float brakeNm=-1,float diffLimit=-1,bool poweredDrift=true,bool countersteer=false,bool handbrake=true,float entryKph=100,float throttleDelay=0,bool record=false){
        if(!Application.isPlaying)throw new InvalidOperationException("Requires Play Mode");
        var scene=SceneManager.CreateScene("Corner test "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var result=new StringBuilder();
        var trace=record?new StringBuilder("side,time_s,front_x,front_z,rear_x,rear_z,speed_kph,beta_deg,hand_applied,rear_brake_nm,rear_drive_nm\n"):null;
        var spec=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CarSpec>("Assets/_Project/Vehicles/Lagoon.asset"));
        try{
            var physics=scene.GetPhysicsScene();
            var ground=new GameObject("Level pad");SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.position=Vector3.down*.5f;ground.AddComponent<BoxCollider>().size=new Vector3(1000,1,1000);
            if(brakeNm>=0)spec.handbrakeNm=brakeNm;
            if(diffLimit>=0)spec.differentialLimit=diffLimit;
            spec.poweredDrift=poweredDrift;
            var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/LagoonCar.prefab");
            foreach(int side in new[]{-1,1}){
                var car=UnityEngine.Object.Instantiate(prefab,new Vector3(side*150,.35f,0),Quaternion.identity);
                SceneManager.MoveGameObjectToScene(car.gameObject,scene);car.spec=spec;
                var driver=new Driver();car.Driver=driver;
                var tick=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                float dt=Time.fixedDeltaTime;
                for(int i=0;i<60;i++){tick();physics.Simulate(dt);}
                car.EnabledDrive=true;driver.input.throttle=1;
                for(int i=0;i<600 && car.Speed*3.6f<entryKph;i++){tick();physics.Simulate(dt);}
                driver.input=new DriveCommand{steer=side};
                for(int i=0;i<10;i++){tick();physics.Simulate(dt);}
                var initial=car.Chassis.position;
                var initialVelocity=car.Chassis.linearVelocity;
                driver.input.hand=handbrake?1:0;
                int both=0;float maxAngle=0,lateBrake=0;
                Vector3 frontLocal=(car.transform.InverseTransformPoint(car.wheels[0].transform.position)+
                    car.transform.InverseTransformPoint(car.wheels[1].transform.position))*.5f;
                Vector3 rearLocal=(car.transform.InverseTransformPoint(car.wheels[2].transform.position)+
                    car.transform.InverseTransformPoint(car.wheels[3].transform.position))*.5f;
                for(int i=0;i<75;i++){
                    driver.input.throttle=i*dt>=throttleDelay?1:0;
                    if(countersteer && i==50)driver.input.steer=-side;
                    tick();physics.Simulate(dt);maxAngle=Mathf.Max(maxAngle,car.DriftAngle);
                    if(record){
                        var front=car.Chassis.position+car.Chassis.rotation*frontLocal-initial;
                        var rear=car.Chassis.position+car.Chassis.rotation*rearLocal-initial;
                        trace.AppendFormat(CultureInfo.InvariantCulture,
                            "{0},{1:F3},{2:F4},{3:F4},{4:F4},{5:F4},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2}\n",
                            side,(i+1)*dt,front.x,front.z,rear.x,rear.z,car.Speed*3.6f,
                            car.SignedDriftAngle,car.HandbrakeApplied,car.wheels[2].brakeTorque,
                            car.wheels[2].motorTorque+car.wheels[3].motorTorque);
                    }
                    if(i*dt>=.5f)lateBrake=Mathf.Max(lateBrake,car.wheels[2].brakeTorque,car.wheels[3].brakeTorque);
                    if(Mathf.Abs(car.wheels[2].rpm)<1 && Mathf.Abs(car.wheels[3].rpm)<1)both++;
                }
                var path=car.Chassis.position-initial;
                result.AppendFormat("max rear brake after 0.5s {0:F0} Nm; ",lateBrake);
                float headingChange=Vector3.SignedAngle(Vector3.ProjectOnPlane(initialVelocity,Vector3.up),
                    Vector3.ProjectOnPlane(car.Chassis.linearVelocity,Vector3.up),Vector3.up)*side;
                result.AppendFormat("travel direction change {0:F1} deg; ",headingChange);
                result.AppendFormat("side {0}: lateral {1:F2} m, forward {2:F2} m, peak slip {3:F1} deg, both locked {4:F2}s, exit {5:F1} km/h, end slip {6:F1} deg, rear drive {7:F0} Nm; ",side,side*path.x,path.z,maxAngle,both*dt,car.Speed*3.6f,car.DriftAngle,car.wheels[2].motorTorque+car.wheels[3].motorTorque);
                driver.input.hand=0;
                bool boosted=false;
                for(int i=0;i<75;i++){
                    // Unwind countersteer as the signed lateral velocity approaches zero.
                    float across=Vector3.Dot(car.Chassis.linearVelocity,car.transform.right);
                    driver.input.steer=Mathf.Clamp(across/5f,-1,1);
                    tick();physics.Simulate(dt);boosted|=car.BoostRemaining>0;
                }
                result.AppendFormat("recovered slip {0:F1} deg, boost {1}; ",car.DriftAngle,boosted);
                car.gameObject.SetActive(false);
            }
            if(record){
                string directory=Path.Combine(Application.dataPath,"../Logs/HandlingChecks");
                Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"axles-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".csv");
                File.WriteAllText(path,trace.ToString());
                result.Append("Trace: "+path);
            }
            return result.ToString();
        }finally{UnityEngine.Object.Destroy(spec);SceneManager.UnloadSceneAsync(scene);}
    }
}
}
