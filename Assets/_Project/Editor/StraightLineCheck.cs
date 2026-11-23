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
    // An isolated physics scene makes the two runs use the same level road,
    // controller, physics step and starting conditions.
    public static string Run()=>Run(false);
    [MenuItem("Apex/Verify Okuibuki acceleration and braking in Play Mode")]
    static void CheckOkuibukiMenu()=>Debug.Log(Run(true));
    public static string Run(bool okuibuki){
        if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
        var scene=SceneManager.CreateScene("Straight line check "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try {
            var physics=scene.GetPhysicsScene();
            var floor=new GameObject("Test road");SceneManager.MoveGameObjectToScene(floor,scene);
            floor.transform.position=new Vector3(0,-.5f,3500);
            floor.AddComponent<BoxCollider>().size=new Vector3(100,1,8000);
            var path=okuibuki?"SHIBATA31/OkuibukiCar":"LagoonCar";
            var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/"+path+".prefab");
            var car=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.35f,0),Quaternion.identity);
            SceneManager.MoveGameObjectToScene(car.gameObject,scene);
            var driver=new FullThrottle();car.Driver=driver;
            var advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
            float dt=Time.fixedDeltaTime,sixty=0,hundred=0,at400=0,at1000=0;
            for(int i=0;i<(okuibuki?60:50);i++){advance();physics.Simulate(dt);}
            float firstShiftKph=0;
            car.EnabledDrive=true;
            for(int i=1;i<=Mathf.RoundToInt(60/dt);i++){
                advance();physics.Simulate(dt);
                float kph=car.Speed*3.6f;
                if(okuibuki && car.Gear>0 && firstShiftKph==0){
                    firstShiftKph=kph;
                    float firstGearLimit=car.spec.upshift*2*Mathf.PI*car.wheels[2].radius*3.6f/(60*car.spec.gears[0]*car.spec.finalDrive);
                    if(kph<firstGearLimit-1)throw new InvalidOperationException("Upshift during wheelspin at "+kph+" km/h.");
                }
                if(sixty==0 && kph>=60)sixty=i*dt;
                if(hundred==0 && kph>=100)hundred=i*dt;
                if(okuibuki && car.ForwardSpeed*3.6f>=100){
                    float brakeStart=car.Chassis.position.z, initialKph=car.ForwardSpeed*3.6f;
                    if(firstShiftKph==0 && car.spec.upshift*2*Mathf.PI*car.wheels[2].radius/(60*car.spec.gears[0]*car.spec.finalDrive)<car.ForwardSpeed)
                        throw new InvalidOperationException("First gear held above its scheduled shift speed.");
                    for(int j=1;j*dt<20;j++){
                        driver.command=DriveCommand.Pedals(0,1,car.ForwardSpeed,car.spec.directionThreshold);
                        advance();physics.Simulate(dt);
                        if(car.ForwardSpeed<=car.spec.directionThreshold)
                            return $"0-60 {sixty:F2}s, 0-100 {i*dt:F2}s, first upshift {firstShiftKph:F1} km/h; braking {initialKph:F2} to {car.ForwardSpeed*3.6f:F2} km/h in {j*dt:F2}s / {car.Chassis.position.z-brakeStart:F2}m";
                    }
                    throw new InvalidOperationException("Braking did not reach the reverse threshold.");
                }
                if(at400==0 && car.Chassis.position.z>=400)at400=kph;
                if(at1000==0 && car.Chassis.position.z>=1000)at1000=kph;
            }
            if(okuibuki)throw new InvalidOperationException("Did not reach 100 km/h in 60 s.");
            return $"60 km/h: {sixty:F2}s; 100 km/h: {hundred:F2}s; at 400m: {at400:F1}; at 1000m: {at1000:F1}; after 60s: {car.Speed*3.6f:F1} km/h; z={car.Chassis.position.z:F0}";
        }finally{SceneManager.UnloadSceneAsync(scene);}
    }
}
}
