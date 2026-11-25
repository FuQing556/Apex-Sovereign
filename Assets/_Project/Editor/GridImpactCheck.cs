using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
public static class GridImpactCheck {
    sealed class Coast:IDriver {public DriveCommand Read()=>default;}
    [MenuItem("Apex/Checks/Four car race/Measure car impacts in Play")]
    public static void Run(){
        if(!Application.isPlaying)throw new InvalidOperationException("Requires Play Mode.");
        var log=new StringBuilder("fixture,control,t,contacts,j_Ns,closing_mps,b_x,b_z,b_vx,b_vz,b_vy,b_yaw_dps,b_height,b_ground\n");
        var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
        void Trial(string name,Vector3 from,float angle,float speed,bool control){
            var scene=SceneManager.CreateScene("Contact "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(220,1,240);
            var a=UnityEngine.Object.Instantiate(prefab,from+Vector3.up*.35f+(control?Vector3.left*40:Vector3.zero),Quaternion.Euler(0,angle,0));
            var b=UnityEngine.Object.Instantiate(prefab,Vector3.up*.35f,Quaternion.identity);
            try{
                SceneManager.MoveGameObjectToScene(a.gameObject,scene);SceneManager.MoveGameObjectToScene(b.gameObject,scene);
                var stepA=(Action)Delegate.CreateDelegate(typeof(Action),a,"FixedUpdate");
                var stepB=(Action)Delegate.CreateDelegate(typeof(Action),b,"FixedUpdate");
                var physics=scene.GetPhysicsScene();float dt=Time.fixedDeltaTime;
                void Step(){stepA();stepB();physics.Simulate(dt);}
                for(int i=0;i<100;i++)Step();
                void Roll(RaceCar car,float v){
                    car.Driver=new Coast();car.EnabledDrive=true;
                    car.Chassis.linearVelocity=car.transform.forward*v;
                    foreach(var w in car.wheels)w.rotationSpeed=v/w.radius*Mathf.Rad2Deg;
                }
                Roll(a,speed);Roll(b,20);
                for(int i=0;i<125;i++){
                    int seq=b.CarImpulseSample;Step();var p=b.Chassis.position;var v=b.Chassis.linearVelocity;
                    float j=b.CarImpulseSample==seq?0:b.LastCarImpulse.magnitude;
                    log.AppendLine(FormattableString.Invariant($"{name},{(control?1:0)},{dt*(i+1):F3},{b.VehicleContacts},{j:F2},{(j>0?b.LastCarClosingSpeed:0):F2},{p.x:F3},{p.z:F3},{v.x:F3},{v.z:F3},{v.y:F3},{Vector3.Dot(b.Chassis.angularVelocity,Vector3.up)*Mathf.Rad2Deg:F3},{p.y:F3},{b.GroundedCount}"));
                }
            }finally{
                UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(b.gameObject);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
        foreach(bool control in new[]{false,true}){
            Trial("graze",new Vector3(-2.65f,0,-.5f),4,20,control);
            Trial("rear",new Vector3(0,0,-7),0,28,control);
            Trial("offset",new Vector3(-4.6f,0,-6),28,30,control);
        }
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/GridRace"));Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,"impact-fixtures.csv"),log.ToString());
    }
}
}
