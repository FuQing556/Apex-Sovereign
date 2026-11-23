using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Editor {
public static class CompetitionVehicleCheck {
    sealed class Driver : IDriver {
        public DriveCommand command;
        public DriveCommand Read()=>command;
    }
    static void Require(bool ok,string message) {
        if(!ok) throw new InvalidOperationException(message);
    }
    [MenuItem("Apex/Verify competition vehicle in Play Mode")]
    public static void Menu()=>Debug.Log(Run());

    // Feed the original driven-wheel speeds to the crank/clutch solver. This isolates
    // the drivetrain from changes in the tyres, steering and the player's later path.
    static string ReplayRecordedAxle(CarSpec spec){
        string path=Path.Combine(Application.dataPath,"../毕设资料/02-车辆设计/SHIBATA-31/Ridge换向二挡遥测摘录.csv");
        var lines=File.ReadAllLines(path);
        var columns=Array.ConvertAll(lines[0].Split(','),s=>s.Trim());
        float Read(string[] row,string field)=>float.Parse(row[Array.IndexOf(columns,field)],CultureInfo.InvariantCulture);
        var drive=new CompetitionDriveline();
        drive.Reset(spec.idle);
        float ratio=spec.gears[1]*spec.finalDrive;
        float inertia=spec.wheelMass*spec.rearRadius*spec.rearRadius;
        float loadLimit=spec.mass*Physics.gravity.magnitude*(1-spec.frontWeight)*spec.competition.rearTyre.longitudinal*spec.rearRadius;
        int oldZeros=0,zeros=0,propelling=0;
        float maxStep=0;
        for(int i=1;i<lines.Length;i++){
            var row=lines[i].Split(',');
            float rpm=(Read(row,"RL_rpm")+Read(row,"RR_rpm"))*.5f;
            if(i==1)
                for(int warm=0;warm<100;warm++)drive.Step(spec,.02f,1,0,rpm,ratio,inertia,loadLimit,false,true);
            float last=drive.EngineRpm;
            float torque=drive.Step(spec,.02f,Read(row,"throttle"),0,rpm,ratio,inertia,loadLimit,false,true);
            maxStep=Mathf.Max(maxStep,Mathf.Abs(drive.EngineRpm-last));
            if(Mathf.Abs(Read(row,"RL_drive_nm")+Read(row,"RR_drive_nm"))<.01f)oldZeros++;
            if(Mathf.Abs(torque)<.01f)zeros++;
            if(torque>.01f)propelling++;
        }
        Require(lines.Length==42 && oldZeros==24 && zeros<oldZeros && maxStep<1000,"Ridge recorded-axle replay regression");
        return $"Ridge measured wheel-speed replay: original {oldZeros}/41 zero, now {zeros}/41 zero, {propelling}/41 positive, max crank step {maxStep:F0} rpm.";
    }
    public static string Run(){
        Require(Application.isPlaying,"Requires Play Mode");
        var scene=SceneManager.CreateScene("Competition vehicle checks "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var report=new StringBuilder();
        var trace=new StringBuilder("case,time_s,kph,gear,engine_rpm,input_rpm,engagement,clutch_nm,rear_nm,FL_deg,FR_deg,yaw_deg_s,beta_deg,front_bar,rear_bar,hand_bar,FL_disc_c,steer_cmd,gas_cmd,hand_cmd,front_x,front_z,rear_x,rear_z,grounded\n");
        try {
            var road=new GameObject("Level dry road");SceneManager.MoveGameObjectToScene(road,scene);
            road.transform.position=Vector3.down*.5f;road.AddComponent<BoxCollider>().size=new Vector3(2000,1,3000);
            var physics=scene.GetPhysicsScene();
            var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>("Assets/_Project/Vehicles/SHIBATA31/OkuibukiCar.prefab");
            report.AppendLine(ReplayRecordedAxle(prefab.spec));
            float dt=Time.fixedDeltaTime;
            foreach(string test in new[]{"keyboard","highway-left","highway-right","catch-left","catch-right","left","right","hand","clutch","surface","drift-left","drift-right"}){
                var car=UnityEngine.Object.Instantiate(prefab,new Vector3(0,.35f,-500),Quaternion.identity);
                SceneManager.MoveGameObjectToScene(car.gameObject,scene);car.Chassis.interpolation=RigidbodyInterpolation.None;
                var advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
                var driver=new Driver();car.Driver=driver;
                float time=0;
                var frontCenter=car.transform.InverseTransformPoint((car.wheels[0].transform.position+car.wheels[1].transform.position)*.5f);
                var rearCenter=car.transform.InverseTransformPoint((car.wheels[2].transform.position+car.wheels[3].transform.position)*.5f);
                void Step(){
                    advance();physics.Simulate(dt);time+=dt;
                    var d=car.Driveline;var h=car.CompetitionChassis;
                    var front=car.Chassis.position+car.Chassis.rotation*frontCenter;
                    var rear=car.Chassis.position+car.Chassis.rotation*rearCenter;
                    trace.AppendFormat(CultureInfo.InvariantCulture,
                        "{0},{1:F3},{2:F2},{3},{4:F1},{5:F1},{6:F3},{7:F1},{8:F1},{9:F2},{10:F2},{11:F2},{12:F2},{13:F2},{14:F2},{15:F2},{16:F2},{17:F3},{18:F3},{19:F3},{20:F4},{21:F4},{22:F4},{23:F4},{24}\n",
                        test,time,car.Speed*3.6f,car.Gear+1,d.EngineRpm,d.ShaftRpm,d.Engagement,d.ClutchNm,
                        car.wheels[2].motorTorque+car.wheels[3].motorTorque,car.wheels[0].steerAngle,car.wheels[1].steerAngle,
                        Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg,car.SignedDriftAngle,
                        h.FrontBar,h.RearBar,h.HandBar,h.DiscC(0),driver.command.steer,driver.command.throttle,driver.command.hand,
                        front.x,front.z,rear.x,rear.z,car.GroundedCount);
                }
                for(int i=0;i<60;i++)Step();
                car.EnabledDrive=true;
                if(test=="keyboard"){
                    foreach(int side in new[]{-1,1}){
                        int inner=side>0?1:0;
                        float Probe(float lateralSpeed,float duration,out float lockTime,float threshold,float forwardSpeed=0,float yawRate=0){
                            float elapsed=0;
                            lockTime=0;
                            int count=Mathf.RoundToInt(duration/dt);
                            driver.command=new DriveCommand{steer=side,digitalSteering=true,clutch=1};
                            for(int i=0;i<count;i++){
                                if(forwardSpeed!=0){
                                    car.Chassis.angularVelocity=car.transform.up*(yawRate*Mathf.Deg2Rad);
                                    car.Chassis.linearVelocity=car.transform.TransformDirection(new Vector3(lateralSpeed,0,forwardSpeed));
                                }
                                Step();
                                elapsed+=dt;
                                if(lockTime==0 && side*car.wheels[inner].steerAngle>=threshold)lockTime=elapsed;
                            }
                            return side*car.wheels[inner].steerAngle;
                        }
                        car.ResetDrift();car.Chassis.linearVelocity=Vector3.zero;car.Chassis.angularVelocity=Vector3.zero;
                        float at20=Probe(0,.20f,out _,float.PositiveInfinity);
                        float at32=Probe(0,.12f,out _,float.PositiveInfinity);
                        float full=Probe(0,2,out _,float.PositiveInfinity);
                        Require(at20>0 && at32>at20 && at32<full && full>50,"Keyboard steering did not progress to full lock");
                        driver.command=new DriveCommand{digitalSteering=true,clutch=1};
                        for(int i=0;i<100;i++)Step();
                        Require(Mathf.Abs(car.wheels[inner].steerAngle)<.01f,"Keyboard did not return to centre");
                        float fastSpeed=125/3.6f,sideSpeed=fastSpeed*Mathf.Tan(18*Mathf.Deg2Rad);
                        car.ResetDrift();
                        float straight=Probe(0,.20f,out _,float.PositiveInfinity,fastSpeed);
                        car.ResetDrift();
                        float sideways=Probe(side*sideSpeed,.20f,out _,float.PositiveInfinity,fastSpeed);
                        Require(straight>0 && straight<at20 && Mathf.Abs(straight-sideways)<.05f,"Same key press changed gain with sideslip");
                        const float comparisonSpeed=50/3.6f;
                        car.ResetDrift();
                        float fromCentre=Probe(0,.40f,out _,float.PositiveInfinity,comparisonSpeed);
                        car.ResetDrift();
                        driver.command=new DriveCommand{steer=-side*.4f,clutch=1};
                        for(int i=0;i<Mathf.CeilToInt(.4f/dt);i++)Step();
                        Require(side*car.wheels[inner].steerAngle<-15,"Opposite steering was not established");
                        float afterReversal=Probe(0,.40f,out _,float.PositiveInfinity,comparisonSpeed);
                        Require(afterReversal>0 && afterReversal<=fromCentre+.2f,"Unwind speed carried through centre into the opposite turn");
                        car.ResetDrift();
                        float across=side*comparisonSpeed*Mathf.Tan(30*Mathf.Deg2Rad);
                        float withYaw=Probe(across,.40f,out _,float.PositiveInfinity,comparisonSpeed,-side*100);
                        car.ResetDrift();
                        float againstRecovery=Probe(across,.40f,out _,float.PositiveInfinity,comparisonSpeed,side*100);
                        Require(withYaw>againstRecovery+2 && withYaw<againstRecovery+car.spec.competition.keyboardCatchDegrees+2,
                            "Active countersteer from centre lost its bounded response to an outward slide");
                        report.AppendLine($"Keyboard {side}: .40 s from centre {fromCentre:F2}, after opposite lock {afterReversal:F2}, outward/recovering slide {withYaw:F2}/{againstRecovery:F2} deg.");
                        car.ResetDrift();
                        float held=Probe(0,4,out float fullTime,full-.01f,fastSpeed);
                        Require(fullTime>0 && Mathf.Abs(held-full)<.05f,"A continuous highway press cannot reach mechanical lock");
                        report.AppendLine($"Keyboard {side}: full {full:F2} deg, highway full in {fullTime:F2}s; 125 km/h tap straight/sliding {straight:F2}/{sideways:F2} deg.");
                    }
                }else if(test=="clutch"){
                    driver.command=new DriveCommand{throttle=1,clutch=1};
                    for(int i=0;i<100;i++)Step();
                    Require(car.Driveline.EngineRpm>car.spec.idle+1500 && car.Speed<.1f && car.Driveline.ClutchNm==0,"Free rev/uncoupled axle failed");
                    report.AppendLine($"Free rev: {car.Driveline.EngineRpm:F0} rpm, {car.Speed*3.6f:F2} km/h.");
                    driver.command=new DriveCommand{throttle=1,reverse=true};
                    for(int i=0;i<75;i++)Step();
                    Require(car.ForwardSpeed < -1,"Reverse failed");
                    car.ResetDrift();
                    Require(car.Gear==0 && car.Driveline.EngineRpm<=car.spec.idle+1 && car.Driveline.Engagement==0,"Reset retained crank or clutch state");
                    driver.command=new DriveCommand{clutch=1,gearChange=-1};Step();
                    Require(!car.ManualGears && car.Gear==0,"Invalid first-gear downshift enabled manual mode");
                    driver.command=new DriveCommand{clutch=1,gearChange=1};Step();
                    Require(car.ManualGears && car.Gear==1 && car.Driveline.Engagement==0,"Manual upshift/clutch failed");
                    driver.command.gearChange=-1;Step();
                    Require(car.Gear==0,"Manual downshift failed");
                    driver.command=new DriveCommand{resumeAutomatic=true};Step();
                    Require(!car.ManualGears,"Return to automatic failed");
                }else if(test=="surface"){
                    var strip=new GameObject("Lower-grip left strip");
                    SceneManager.MoveGameObjectToScene(strip,scene);
                    strip.transform.position=new Vector3(-2,-.478f,-500);
                    strip.AddComponent<BoxCollider>().size=new Vector3(4,1,12);
                    var wet=strip.AddComponent<TyreSurface>();wet.lateralScale=.55f;wet.longitudinalScale=.6f;
                    for(int i=0;i<40;i++)Step();
                    float l=car.wheels[0].sidewaysFriction.stiffness,r=car.wheels[1].sidewaysFriction.stiffness;
                    Require(l<r*.75f,"Split surface failed");
                    strip.SetActive(false);
                    for(int i=0;i<40;i++)Step();
                    Require(Mathf.Abs(car.wheels[0].sidewaysFriction.stiffness-car.wheels[1].sidewaysFriction.stiffness)<.15f,"Dry grip was not restored");
                    report.AppendLine($"Split surface FL/FR lateral stiffness {l:F2}/{r:F2}, dry recovery passed.");
                }else{
                    driver.command=new DriveCommand{throttle=1,digitalSteering=true};
                    bool driftTest=test.StartsWith("drift",StringComparison.Ordinal);
                    float target=test=="hand" || driftTest?70:125;
                    for(int i=0;i<2000 && car.Speed*3.6f<target;i++)Step();
                    Require(car.Speed*3.6f>=target,"Did not reach entry speed "+target);
                    if(test.StartsWith("highway") || test.StartsWith("catch")){
                        int side=test.EndsWith("right")?1:-1;
                        for(int i=0;i<10;i++)Step();
                        if(test.StartsWith("highway")){
                            driver.command.steer=side;
                            int innerWheel=side>0?1:0;
                            for(int i=0;i<Mathf.RoundToInt(.20f/dt);i++)Step();
                            float tap=side*car.wheels[innerWheel].steerAngle;
                            for(int i=0;i<Mathf.RoundToInt(.36f/dt);i++)Step();
                            float correction=side*car.wheels[innerWheel].steerAngle;
                            for(int i=0;i<Mathf.RoundToInt(.24f/dt);i++)Step();
                            float inner=side*car.wheels[innerWheel].steerAngle;
                            for(int i=0;i<Mathf.RoundToInt(1.20f/dt);i++)Step();
                            float held=side*car.wheels[innerWheel].steerAngle;
                            driver.command.steer=0;
                            for(int i=0;i<Mathf.RoundToInt(3/dt);i++)Step();
                            Require(tap>0 && correction>tap && inner>correction && held>inner+15 &&
                                Mathf.Abs(car.wheels[0].steerAngle)<.01f,"Highway tap, tapered correction, or release failed");
                            report.AppendLine($"{test}: .20/.56/.80/2.00 s inside angle {tap:F2}/{correction:F2}/{inner:F2}/{held:F2} deg, slip 3 s after release {car.SignedDriftAngle:F2} deg.");
                        }else{
                            // Use the same analog entry for both directions, then give only the driver a keyboard countersteer.
                            driver.command=new DriveCommand{steer=-side*.45f,throttle=1};
                            for(int i=0;i<150 && side*car.SignedDriftAngle<15;i++)Step();
                            Require(side*car.SignedDriftAngle>=15 && side*car.wheels[0].steerAngle<0,"Failed to establish outward slide before countersteering");
                            driver.command=new DriveCommand{steer=side,throttle=1,digitalSteering=true};
                            float cross=0,catchAngle=0;
                            for(int i=0;i<Mathf.RoundToInt(.5f/dt);i++){
                                Step();
                                float angle=side*car.wheels[side>0?1:0].steerAngle;
                                if(cross==0 && angle>0)cross=(i+1)*dt;
                                catchAngle=Mathf.Max(catchAngle,angle);
                            }
                            Require(cross>0 && cross<=.24f && catchAngle>=15,"Keyboard could not cross center and countersteer into a real slide");
                            report.AppendLine($"{test}: crossed center in {cross:F2} s, countersteered {catchAngle:F2} deg within .50 s.");
                        }
                    }else if(driftTest){
                        int mirror=test=="drift-left"?1:-1;
                        float sustained=0,best=0,peak=0,countersteering=0;
                        for(int i=0;i<400;i++){
                            float t=i*dt;
                            float yaw=mirror*Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg;
                            float slip=mirror*car.SignedDriftAngle;
                            driver.command=t<.50f?new DriveCommand{steer=-.7f*mirror,throttle=.8f,hand=t>.14f?1:0,gearChange=i==0?1:0}:
                                new DriveCommand{steer=mirror*Mathf.Clamp(.12f+(slip-23)*.032f-(yaw+40)*.007f,-1,1),
                                throttle=Mathf.Clamp01(.74f+(17-car.Speed)*.045f)};
                            Step();slip=mirror*car.SignedDriftAngle;peak=Mathf.Max(peak,slip);
                            bool balanced=t>.5f && car.Gear==1 && car.GroundedCount==4 && car.ForwardSpeed>8 &&
                                slip>=15 && slip<=35 && yaw< -8;
                            if(balanced && mirror*car.wheels[0].steerAngle>0 && mirror*car.wheels[1].steerAngle>0)countersteering+=dt;
                            sustained=balanced?sustained+dt:0;
                            best=Mathf.Max(sustained,best);
                        }
                        report.AppendLine($"Second-gear {test}: {best:F2}s continuously at 15–35 deg, countersteer {countersteering:F2}s, peak {peak:F1} deg, exit {car.Speed*3.6f:F1} km/h.");
                        Require(best>=4 && countersteering>=4,"Sustained second-gear drift failed: "+best+" / "+countersteering);
                    }else if(test=="hand"){
                        driver.command=new DriveCommand{steer=-.32f,hand=1,clutch=1};
                        float minWheel=float.MaxValue;
                        for(int i=0;i<35;i++){Step();minWheel=Mathf.Min(minWheel,Mathf.Max(Mathf.Abs(car.wheels[2].rpm),Mathf.Abs(car.wheels[3].rpm)));}
                        Require(car.wheels[0].brakeTorque==0 && car.wheels[1].brakeTorque==0 && car.wheels[2].brakeTorque>0 && minWheel<1,"Rear-only hydraulic handbrake failed");
                        report.AppendLine($"Handbrake {car.CompetitionChassis.HandBar:F1} bar, {car.wheels[2].brakeTorque:F0} Nm/rear, both wheels reached {minWheel:F1} rpm.");
                        driver.command=new DriveCommand{throttle=.5f};for(int i=0;i<25;i++)Step();
                        Require(car.wheels[2].brakeTorque==0 && car.CompetitionChassis.HandBar==0,"Hand brake did not release");
                        driver.command=new DriveCommand{brake=1,clutch=1};
                        for(int i=0;i<35;i++)Step();
                        Require(car.CompetitionChassis.HandBar==0 && car.CompetitionChassis.FrontBar>car.CompetitionChassis.RearBar &&
                            car.CompetitionChassis.DiscC(0)>car.spec.competition.ambientC,"Dual hydraulic circuit or rotor heating failed");
                        report.AppendLine($"Foot brake F/R {car.CompetitionChassis.FrontBar:F1}/{car.CompetitionChassis.RearBar:F1} bar, FL disc {car.CompetitionChassis.DiscC(0):F1} C.");
                        driver.command=new DriveCommand{throttle=.8f,brake=.2f};
                        bool both=false;
                        for(int i=0;i<50;i++){Step();both|=car.wheels[2].motorTorque+car.wheels[3].motorTorque>100 && car.CompetitionChassis.FrontBar>1;}
                        Require(both,"Independent throttle and service brake failed");
                    }else{
                        int side=test=="left"?-1:1;
                        driver.command.steer=-side;
                        for(int i=0;i<10;i++)Step();
                        driver.command.steer=side;
                        float peak=0,maxDelta=0,last=car.Driveline.EngineRpm;
                        int cut=0,samples=0;
                        for(int i=0;i<75;i++){
                            Step();peak=Mathf.Max(peak,-side*car.SignedDriftAngle);
                            float rpm=car.Driveline.EngineRpm;maxDelta=Mathf.Max(maxDelta,Mathf.Abs(rpm-last));last=rpm;
                            if(i>=34){samples++;if(Mathf.Abs(car.wheels[2].motorTorque+car.wheels[3].motorTorque)<.01f)cut++;}
                            Require(car.CompetitionChassis.HandBar==0 && car.CompetitionChassis.FrontBar==0,"Unexpected brake application during feint");
                        }
                        Require(peak>5 && cut<samples/2 && maxDelta<1000,"Feint failed: "+peak+" deg / zero "+cut+" / rpm step "+maxDelta);
                        report.AppendLine($"{test} 125 km/h feint: peak {peak:F1} deg, exit {car.Speed*3.6f:F1} km/h, zero rear torque {cut}/{samples}, max crank step {maxDelta:F0} rpm.");
                    }
                }
                car.gameObject.SetActive(false);
            }
        }finally{
            string folder=Path.Combine(Application.dataPath,"../Logs/HandlingChecks");Directory.CreateDirectory(folder);
            string file=Path.Combine(folder,"competition-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".csv");File.WriteAllText(file,trace.ToString());
            report.AppendLine(file);
            SceneManager.UnloadSceneAsync(scene);
        }
        return report.ToString();
    }
}
}
