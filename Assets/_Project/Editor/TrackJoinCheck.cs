using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Apex.Editor {
public static class TrackJoinCheck {
    sealed class Driver : IDriver {
        public DriveCommand Read() => new DriveCommand { steer=-1, hand=1, digitalSteering=true, keyboardRecovery=true };
    }

    [MenuItem("Apex/Tests/Track joins in Play Mode")]
    public static void Run(){
        var session=Object.FindAnyObjectByType<RaceSession>();
        if(!Application.isPlaying || !session || session.State!=RaceSession.Phase.Lobby)
            throw new InvalidOperationException("Start in the garage before testing tracks.");
        session.StartCoroutine(Check());
    }

    static IEnumerator Check(){
        var result=new StringBuilder();
        const string id="Ridge";
        yield return SceneManager.LoadSceneAsync(id,new LoadSceneParameters(LoadSceneMode.Additive,LocalPhysicsMode.Physics3D));
        var scene=SceneManager.GetSceneByName(id);
        try {
            var course=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Circuit>()).Single();
            var physics=scene.GetPhysicsScene();
            var left=course.transform.Find("LeftBarrier").GetComponent<MeshFilter>().sharedMesh.vertices;
            var right=course.transform.Find("RightBarrier").GetComponent<MeshFilter>().sharedMesh.vertices;
            var road=course.transform.Find("Road and kerbs");
            var surface=road?road.GetComponent<TyreSurface>():null;
            var ribbon=surface?surface.GetComponent<MeshCollider>():null;
            var roadVertices=surface?road.GetComponent<MeshFilter>().sharedMesh.vertices:null;
            int rings=left.Length/12,missing=0,folds=0,coverErrors=0;
            var bed=course.transform.Find("Roadbed").GetComponent<MeshFilter>().sharedMesh.vertices;
            float minUp=1, maxError=0;
            for(int i=0;i<rings;i++){
                int j=(i+1)%rings;
                var middle=(left[i*2]+right[i*2])/2;
                var forward=Vector3.ProjectOnPlane((left[j*2]+right[j*2])/2-middle,Vector3.up).normalized;
                for(int face=0;face<5;face++) for(int edge=0;edge<2;edge++){
                    var ix=2*rings*face+edge;
                    if(Vector3.Dot(bed[ix+2*j]-bed[ix+2*i],forward)<-.0001f)folds++;
                }
                foreach(float u in new[]{.02f,.12f,.22f,.5f,.78f,.88f,.98f}){
                    var a=Vector3.Lerp(left[i*2],right[i*2],u);
                    var b=Vector3.Lerp(left[j*2],right[j*2],u);
                    var p=Vector3.Lerp(a,b,.5f);
                    if(!physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,4)){missing++;continue;}
                    maxError=Mathf.Max(maxError,Mathf.Abs(hit.point.y-p.y));minUp=Mathf.Min(minUp,hit.normal.y);
                }
                if(ribbon && surface.trackRibbon){
                    // The four render vertices give the painted boundaries independently of the collision bands.
                    var e=roadVertices;
                    var row=new[]{left[i*2],e[4*i],e[4*i+1],e[4*i+2],e[4*i+3],right[i*2]};
                    var next=new[]{left[j*2],e[4*j],e[4*j+1],e[4*j+2],e[4*j+3],right[j*2]};
                    for(int lane=0;lane<5;lane++){
                        var p=(row[lane]+row[lane+1]+next[lane]+next[lane+1])*.25f;
                        if(!physics.Raycast(p+Vector3.up,Vector3.down,out var hit,2) ||
                           ribbon!=hit.collider)coverErrors++;
                        else if(surface.CoverAt(hit.point,hit.normal)!=expected[lane])coverErrors++;
                    }
                }
            }
            result.AppendLine($"{id} sections={rings} backwardsProfileEdges={folds} roadRayMisses={missing} coverErrors={coverErrors} minNormalY={minUp:F3} maxHeightResidual={maxError:F3}m");
            Drive(scene,result);
            if(roadVertices!=null)
                foreach(int side in new[]{-1,1})CrossKerb(scene,roadVertices,side,result);
        } finally { SceneManager.UnloadSceneAsync(scene); }
        var dir="Logs/TrackJoins";Directory.CreateDirectory(dir);
        var path=Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".txt");
        File.WriteAllText(path,result.ToString());Debug.Log(result.ToString());
    }
    static readonly RoadCover[] expected={RoadCover.Grass,RoadCover.Kerb,RoadCover.Asphalt,RoadCover.Kerb,RoadCover.Grass};
    sealed class Coast : IDriver {public DriveCommand Read()=>new DriveCommand{clutch=1};}
    static void CrossKerb(Scene scene,Vector3[] road,int side,StringBuilder result){
        int n=road.Length/4;
        Vector3 Centre(int i)=>(road[4*i+1]+road[4*i+2])*.5f;
        // Coast across each kerb on the opening straight.
        const int i=1;
        var tangent=(Centre((i+1)%n)-Centre(i)).normalized;
        var outward=(road[4*i+3]-road[4*i]).normalized*side;
        var direction=(tangent+outward*.21f).normalized;
        var rotation=Quaternion.LookRotation(direction,Vector3.up);
        var physics=scene.GetPhysicsScene();
        var edge=road[4*i+(side>0?2:1)];
        var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
        var car=Object.Instantiate(prefab,edge-outward*1.7f+Vector3.up*.40f,rotation);
        SceneManager.MoveGameObjectToScene(car.gameObject,scene);
        try{
            car.Driver=new Coast();car.Chassis.interpolation=RigidbodyInterpolation.None;
            var step=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
            for(int t=0;t<45;t++){step();physics.Simulate(.02f);}
            car.EnabledDrive=true;
            car.Chassis.linearVelocity=direction*(65/3.6f);
            foreach(var wheel in car.wheels)wheel.rotationSpeed=65/3.6f/wheel.radius*Mathf.Rad2Deg;
            var probe=car.gameObject.AddComponent<HandlingContactProbe>();
            int coverMask=0,split=0,air=0;float worst=0;
            for(int t=0;t<32;t++){
                float speed=car.Speed;step();physics.Simulate(.02f);
                for(int w=0;w<4;w++)coverMask|=1<<(int)car.WheelCover(w);
                if(car.WheelCover(0)!=car.WheelCover(1))split++;
                if(car.GroundedCount==0)air++;
                worst=Mathf.Max(worst,(speed-car.Speed)*3.6f);
            }
            bool allCovers=(coverMask&14)==14;
            result.AppendLine($"{scene.name} {(side<0?"left":"right")} 65km/h crossing: threeCovers={allCovers} splitFrontSteps={split} worstStepLoss={worst:F2}km/h noGroundSteps={air} bodyContacts={probe.events}");
        }finally{Object.DestroyImmediate(car.gameObject);}
    }
    [MenuItem("Apex/Tests/High speed airflow on Ridge")]
    static void CheckAirflow(){
        var s=Object.FindAnyObjectByType<RaceSession>();
        if(!Application.isPlaying || !s || s.State!=RaceSession.Phase.Driving ||
            SceneManager.GetActiveScene().name!="Ridge")throw new InvalidOperationException("Launch the Ridge time trial before checking airflow.");
        s.StartCoroutine(Airflow(s));
    }
    static IEnumerator Airflow(RaceSession session){
        var car=session.Car;var trails=car.GetComponentsInChildren<TrailRenderer>();
        if(trails.Length!=2)throw new InvalidOperationException("Missing body airflow trails.");
        var gate=Object.FindAnyObjectByType<Circuit>().gates[0];
        car.Chassis.position=gate.position+gate.up*.47f+gate.forward*12;
        car.Chassis.rotation=gate.rotation;car.Chassis.angularVelocity=Vector3.zero;
        car.Chassis.linearVelocity=gate.forward*38;
        foreach(var w in car.wheels)w.rotationSpeed=38/w.radius*Mathf.Rad2Deg;
        var report=new StringBuilder();bool captured=false;
        string dir=Path.GetFullPath("Logs/TrackJoins");Directory.CreateDirectory(dir);
        for(int frame=0;frame<24;frame++){
            yield return null;
            report.AppendLine($"{frame} speed={car.Speed*3.6f:F1} emitting={trails[0].emitting}/{trails[1].emitting} points={trails[0].positionCount}/{trails[1].positionCount} width={trails[0].widthMultiplier:F3}");
            if(!captured && trails.All(t=>t.emitting && t.positionCount>=3)){
                ScreenCapture.CaptureScreenshot(Path.Combine(dir,"High-speed.png"));captured=true;
            }
        }
        File.WriteAllText(Path.Combine(dir,"airflow.txt"),report.ToString());
        Debug.Log("High speed airflow captured="+captured);
    }
    // Measured pose, COM velocity, angular velocity and wheel speeds at 1891.4393 s in 1c889771.
    // Fixed left + handbrake approach, cold-started chassis. This is a repeatable contact test.
    static void Drive(Scene scene,StringBuilder result){
        var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
        var catalog=AssetDatabase.LoadAssetAtPath<WorkshopCatalog>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:WorkshopCatalog")[0]));
        var car=Object.Instantiate(prefab,new Vector3(92.9621f,9.3648f,-120.7735f),new Quaternion(-.013293f,.9992253f,.0271183f,-.0252402f));
        SceneManager.MoveGameObjectToScene(car.gameObject,scene);
        try {
            car.FitParts(catalog.differentials.Single(x=>x.id=="original"),catalog.wheels.Single(x=>x.id=="vent-disc"));
            car.Driver=new Driver();car.EnabledDrive=true;
            var rb=car.Chassis;rb.interpolation=RigidbodyInterpolation.None;
            rb.linearVelocity=new Vector3(-7.1412f,1.785715f,-24.94632f);
            rb.angularVelocity=new Vector3(.000006f,-.944237f,-.059885f);
            float[] rpm={703.6732f,746.214f,517.8276f,568.6848f};
            for(int i=0;i<4;i++)car.wheels[i].rotationSpeed=rpm[i]*6;
            var step=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
            var probe=car.gameObject.AddComponent<HandlingContactProbe>();
            float worst=0,peakUp=0;int airborne=0;
            for(int tick=0;tick<30;tick++){
                float before=rb.linearVelocity.magnitude;
                step();scene.GetPhysicsScene().Simulate(.02f);
                worst=Mathf.Max(worst,(before-rb.linearVelocity.magnitude)*3.6f);
                peakUp=Mathf.Max(peakUp,rb.linearVelocity.y);
                if(car.GroundedCount==0)airborne++;
            }
            result.AppendLine($"Gate24 0.60s: end={rb.linearVelocity.magnitude*3.6f:F2}km/h worstStepLoss={worst:F2}km/h contacts={probe.events} peakVy={peakUp:F2}m/s noGroundSteps={airborne} position={rb.position:F2}");
        } finally { Object.DestroyImmediate(car.gameObject); }
    }
}
}
