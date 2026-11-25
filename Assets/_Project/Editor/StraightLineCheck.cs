using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
public static class StraightLineCheck {
    sealed class FullThrottle : IDriver {
        public DriveCommand command=new DriveCommand{throttle=1};
        public DriveCommand Read()=>command;
    }
    [MenuItem("Apex/Verify Okuibuki acceleration and braking in Play Mode")]
    static void CheckOkuibukiMenu()=>Debug.Log(Run());
    [MenuItem("Apex/Verify Zhuzhou acceleration and braking in Play Mode")]
    static void CheckZhuzhouMenu()=>Debug.Log(Run(ZhuzhouCarBuilder.CarPath));
    public static string Run()=>Run("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
    public static string Run(string prefabPath){
        if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
        var scene=SceneManager.CreateScene("Straight line check "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try {
            var physics=scene.GetPhysicsScene();
            var floor=new GameObject("Test road");SceneManager.MoveGameObjectToScene(floor,scene);
            floor.transform.position=new Vector3(0,-.5f,3500);
            floor.AddComponent<BoxCollider>().size=new Vector3(100,1,8000);
            var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>(prefabPath);
            // Awake configures wheel substeps for the physics scene in which the vehicle is born.
            var car=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.35f,0),Quaternion.identity,new InstantiateParameters{scene=scene});
            var driver=new FullThrottle();car.Driver=driver;
            int driven=car.spec.frontWheelDrive?0:2,passive=2-driven;
            var advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
            float dt=Time.fixedDeltaTime,sixty=0;
            for(int i=0;i<60;i++){advance();physics.Simulate(dt);}
            float firstShiftKph=0;
            car.EnabledDrive=true;
            for(int i=1;i<=Mathf.RoundToInt(60/dt);i++){
                advance();physics.Simulate(dt);
                if(car.wheels[passive].motorTorque!=0 || car.wheels[passive+1].motorTorque!=0)
                    throw new InvalidOperationException("Motor torque applied to the free axle.");
                float kph=car.Speed*3.6f;
                if(car.Gear>0 && firstShiftKph==0){
                    firstShiftKph=kph;
                    float firstGearLimit=car.spec.upshift*2*Mathf.PI*car.wheels[driven].radius*3.6f/(60*car.spec.gears[0]*car.spec.finalDrive);
                    if(kph<firstGearLimit-1)throw new InvalidOperationException("Upshift during wheelspin at "+kph+" km/h.");
                }
                if(sixty==0 && kph>=60)sixty=i*dt;
                if(car.ForwardSpeed*3.6f>=100){
                    float brakeStart=car.Chassis.position.z, initialKph=car.ForwardSpeed*3.6f;
                    if(firstShiftKph==0 && car.spec.upshift*2*Mathf.PI*car.wheels[driven].radius/(60*car.spec.gears[0]*car.spec.finalDrive)<car.ForwardSpeed)
                        throw new InvalidOperationException("First gear held above its scheduled shift speed.");
                    for(int j=1;j*dt<20;j++){
                        driver.command=DriveCommand.Pedals(0,1,car.ForwardSpeed,car.spec.directionThreshold);
                        advance();physics.Simulate(dt);
                        if(car.ForwardSpeed<=car.spec.directionThreshold)
                            return $"0-60 {sixty:F2}s, 0-100 {i*dt:F2}s, first upshift {firstShiftKph:F1} km/h; braking {initialKph:F2} to {car.ForwardSpeed*3.6f:F2} km/h in {j*dt:F2}s / {car.Chassis.position.z-brakeStart:F2}m";
                    }
                    throw new InvalidOperationException("Braking did not reach the reverse threshold.");
                }
            }
            throw new InvalidOperationException("Did not reach 100 km/h in 60 s.");
        }finally{SceneManager.UnloadSceneAsync(scene);}
    }
}
}
