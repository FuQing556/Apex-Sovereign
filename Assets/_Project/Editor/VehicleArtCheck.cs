using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
    public static class VehicleArtCheck {
        sealed class Input : IDriver {
            public DriveCommand command;
            public DriveCommand Read()=>command;
        }
        [MenuItem("Apex/Art/Verify 31 wheel visuals in Play Mode")]
        public static void CheckMenu()=>Debug.Log(Run());
        public static string Run(){
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode.");
            var scene=SceneManager.CreateScene("31 visual bench "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try{
                var sim=scene.GetPhysicsScene();
                var road=new GameObject("Dry skid pad");SceneManager.MoveGameObjectToScene(road,scene);
                road.transform.position=Vector3.down*.5f;
                road.AddComponent<BoxCollider>().size=new Vector3(800,1,800);
                var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
                var car=UnityEngine.Object.Instantiate(prefab,Vector3.up*.35f,Quaternion.identity);
                SceneManager.MoveGameObjectToScene(car.gameObject,scene);
                var fx=car.GetComponent<TyreSlipPlume>();
                ((Action)Delegate.CreateDelegate(typeof(Action),fx,"Start"))();
                var input=new Input();car.Driver=input;
                var rig=car.GetComponent<SuspensionVisualRig>();
                var fxStep=(Action)Delegate.CreateDelegate(typeof(Action),fx,"FixedUpdate");
                var drive=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                var wheels=(Action)Delegate.CreateDelegate(typeof(Action),car,"LateUpdate");
                var links=(Action)Delegate.CreateDelegate(typeof(Action),rig,"LateUpdate");
                var smoke=car.GetComponentInChildren<ParticleSystem>();
                var emitted=new int[car.wheels.Length];
                var particles=new ParticleSystem.Particle[smoke.main.maxParticles];
                int peak=0;float error=0,compression=0,steer=0;
                float dt=Time.fixedDeltaTime;
                void Step(){
                    drive();sim.Simulate(dt);wheels();links();fxStep();
                    int count=smoke.GetParticles(particles);
                    for(int i=0;i<car.wheels.Length;i++){
                        if(!car.wheels[i].GetGroundHit(out var hit))continue;
                        for(int j=0;j<count;j++){
                            var p=particles[j];
                            if(Mathf.Abs(p.remainingLifetime-p.startLifetime)<.0001f &&
                                (p.position-hit.point).sqrMagnitude<.2f*.2f)emitted[i]++;
                        }
                    }
                    smoke.Simulate(dt,false,false,false);
                    peak=Mathf.Max(peak,smoke.particleCount);
                    steer=Mathf.Max(steer,Mathf.Abs(car.wheels[0].steerAngle));
                    foreach(var j in rig.joints){
                        if(j.attachment!=SuspensionVisualRig.Attachment.Stretch)continue;
                        var end=j.pivot.TransformPoint(Vector3.forward*j.bindLength);
                        var hub=j.carrier.position+car.transform.TransformVector(j.wheelOffset);
                        error=Mathf.Max(error,Vector3.Distance(end,hub));
                        compression=Mathf.Max(compression,Mathf.Abs(j.pivot.localScale.z-1)*j.bindLength);
                    }
                }
                for(int i=0;i<75;i++)Step();
                int idle=smoke.particleCount;car.EnabledDrive=true;input.command=new DriveCommand{throttle=1};
                for(int i=0;i<168;i++)Step();
                input.command=new DriveCommand{steer=.70f,hand=1,throttle=1};
                for(int i=0;i<36;i++)Step();
                input.command=new DriveCommand{steer=-.68f,throttle=.8f};
                for(int i=0;i<45;i++)Step();
                bool rubber=car.tires.All(t=>t.GetComponentsInChildren<MeshFilter>().Count(f=>f.name.StartsWith("Tyre_") && f.sharedMesh.isReadable)==1);
                string perWheel=string.Join("/",emitted);
                if(rig.joints.Length!=28 || !rubber || idle!=0 || emitted.Any(n=>n==0) || steer<1 || error>.001f || compression<.001f)
                    throw new InvalidOperationException($"rig {rig.joints.Length}, tyre {rubber}, idle {idle}, particles FL/FR/RL/RR {perWheel}, error {error}, travel {compression}, steer {steer}");
                return $"28 visual joints, 4 deformable tyres; rest plume {idle}, peak particles {peak}, emitted FL/FR/RL/RR {perWheel}; peak |FL steer| {steer:F2} deg, max change in joint length {compression*1000:F1} mm; stretching joint endpoint error {error*1000:F3} mm.";
            }finally{SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
