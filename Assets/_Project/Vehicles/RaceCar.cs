using UnityEngine;
namespace Apex {
[RequireComponent(typeof(Rigidbody))]
public class RaceCar : MonoBehaviour {
    public CarSpec spec;
    public WorkshopCatalog.Part Differential {get;private set;}
    public WorkshopCatalog.Part RearWheels {get;private set;}
    float DifferentialFraction=>Differential?.transferFraction??1f;
    public void FitParts(WorkshopCatalog.Part differential,WorkshopCatalog.Part rearWheels){
        Differential=differential;RearWheels=rearWheels;
        var mounts=GetComponentInChildren<WorkshopMounts>();
        if(mounts && rearWheels!=null)mounts.Fit(rearWheels);
    }
    public WheelCollider[] wheels;
    public Transform[] tires;
    public Transform[] carriers;
    public IDriver Driver {get;set;}
    public bool EnabledDrive {get;set;}
    public Rigidbody Chassis {get;private set;}
    public float Speed=>Chassis.linearVelocity.magnitude;
    public int Gear {get;private set;}
    int DrivenLeft=>spec.frontWheelDrive?0:2;
    float AxleRpm=>(wheels[DrivenLeft].rpm+wheels[DrivenLeft+1].rpm)*.5f;
    public float EngineRpm=>spec.competition?driveline.EngineRpm:
        Mathf.Max(spec.idle,Mathf.Abs(AxleRpm)*spec.finalDrive*spec.gears[Gear]);
    public static float ContactSlipSpeed(WheelCollider wheel,WheelHit hit,Vector3 velocity){
        float spin=wheel.rpm*(2*Mathf.PI/60)*wheel.radius;
        float longitudinal=Vector3.Dot(velocity,hit.forwardDir)-spin;
        float lateral=Vector3.Dot(velocity,hit.sidewaysDir);
        return Mathf.Sqrt(longitudinal*longitudinal+lateral*lateral);
    }
    readonly DriftReward reward=new DriftReward();
    public float BoostRemaining=>reward.Remaining;
    public float BoostPower=>boost;
    public float DriftAngle {get;private set;}
    public float SignedDriftAngle {get;private set;}
    public float HandbrakeApplied=>hand;
    public bool DriftEngaged=>poweredSlide;
    bool impact;
    readonly CompetitionDriveline driveline=new CompetitionDriveline();
    CompetitionChassis competitionChassis;
    TyreContactVisual[] contactVisuals;
    public bool ManualGears {get;private set;}
    public CompetitionDriveline Driveline=>driveline;
    public CompetitionChassis CompetitionChassis=>competitionChassis;
    public int ResetGeneration {get;private set;}
    float correctionKey,correctionAge,releaseSupport;
    public void ResetDrift(){
        ResetGeneration++;
        reward.Clear();boost=hand=driftAge=gripReduction=0;poweredSlide=false;
        if(competitionChassis!=null){
            Gear=0;pedal=steering=keyboardVelocity=correctionKey=correctionAge=releaseSupport=0;Reversing=impact=ManualGears=false;
            driveline.Reset(spec.idle);competitionChassis.ResetControls();
            foreach(var wheel in wheels){wheel.motorTorque=0;wheel.brakeTorque=0;wheel.steerAngle=0;wheel.rotationSpeed=0;}
        }
    }
    public int VehicleContacts {get;private set;}
    public event System.Action<RaceCar> CarContactImpulse;
    public Vector3 LastCarImpulse {get;private set;}
    public Vector3 LastCarContactPoint {get;private set;}
    public float LastCarClosingSpeed {get;private set;}
    public RaceCar LastCarContact {get;private set;}
    public int CarImpulseSample {get;private set;}
    void ObserveCarContact(Collision hit,RaceCar other){
        if(hit.contactCount==0)return;
        var point=hit.GetContact(0);
        LastCarImpulse=hit.impulse;LastCarContactPoint=point.point;
        LastCarClosingSpeed=Mathf.Abs(Vector3.Dot(hit.relativeVelocity,point.normal));
        LastCarContact=other;CarImpulseSample++;CarContactImpulse?.Invoke(this);
    }
    void OnCollisionEnter(Collision hit){
        impact=true;
        if(hit.rigidbody && hit.rigidbody.TryGetComponent<RaceCar>(out var other)){
            VehicleContacts++;ObserveCarContact(hit,other);
        }
    }
    void OnCollisionStay(Collision hit){
        impact=true;
        if(hit.rigidbody && hit.rigidbody.TryGetComponent<RaceCar>(out var other))ObserveCarContact(hit,other);
    }
    public float ForwardSpeed=>Vector3.Dot(Chassis.linearVelocity,transform.forward);
    public bool Reversing {get;private set;}
    public int GroundedCount {get;private set;}
    readonly Collider[] groundColliders=new Collider[4];
    readonly TyreSurface[] surfaces=new TyreSurface[4];
    readonly RoadCover[] contactCover=new RoadCover[4];
    public RoadCover WheelCover(int i)=>!groundColliders[i]?RoadCover.Air:contactCover[i];
    public Vector2 WheelGrip(int i)=>!groundColliders[i]?Vector2.zero:surfaces[i]?surfaces[i].GripFor(contactCover[i]):Vector2.one;
    public DriveCommand AppliedCommand {get;private set;}
    public DriverInputFrame AppliedInput {get;private set;}
    void Update(){if(Driver is LocalDriver local)local.Capture();}
    void OnApplicationFocus(bool focused){if(!focused && Driver is LocalDriver local)local.Release();}
    float steering,keyboardVelocity,hand,boost,pedal;
    float driftAge,gripReduction;
    bool poweredSlide;
    void Awake(){
        Chassis=GetComponent<Rigidbody>(); Chassis.mass=spec.mass;
        Chassis.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        Chassis.centerOfMass=new Vector3(0,spec.centerHeight,(spec.frontWeight-.5f)*spec.wheelbase);
        gameObject.AddComponent<ExhaustFlame>().Bind(this);
        for(int i=0;i<4;i++){
            var w=wheels[i]; w.radius=i>=2 && spec.rearRadius>0?spec.rearRadius:spec.radius; w.mass=spec.wheelMass; w.suspensionDistance=spec.travel;
            if(spec.competition)w.wheelDampingRate=0; // The competition chassis accounts for rolling losses at contact.
            var longitudinal=w.forwardFriction; longitudinal.stiffness=spec.longitudinalGrip; w.forwardFriction=longitudinal;
            var lateral=w.sidewaysFriction;
            lateral.stiffness=spec.lateralGrip;
            lateral.asymptoteValue=lateral.extremumValue*spec.slidingGripFraction;
            w.sidewaysFriction=lateral;
            var spring=w.suspensionSpring; spring.spring=i<2?spec.frontSpring:spec.rearSpring;spring.damper=spec.damper;w.suspensionSpring=spring;
        }
        wheels[0].ConfigureVehicleSubsteps(10,5,3);
        if(spec.competition){
            competitionChassis=new CompetitionChassis(this);driveline.Reset(spec.idle);
            contactVisuals=new TyreContactVisual[tires.Length];
            for(int i=0;i<tires.Length;i++)contactVisuals[i]=new TyreContactVisual(tires[i]);
        }
    }
    void UpdateSteering(DriveCommand command, float along, float roadSpeed, float yawRate, float lockAngle, float dt){
        float target=command.steer;
        var setup=spec.competition;
        if(!command.keyboardRecovery || !command.digitalSteering || target!=correctionKey || command.hand>0){
            correctionKey=0;correctionAge=0;
        }
        if(!setup || !command.digitalSteering){
            keyboardVelocity=0;
            float rate=target*steering<0?spec.countersteerRate:
                Mathf.Abs(target)<Mathf.Abs(steering)?spec.returnRate:spec.steerRate;
            steering=Mathf.MoveTowards(steering,target,rate*dt);
            return;
        }
        float lockDegrees=Mathf.Max(.01f,lockAngle);
        float angle=steering*lockDegrees;
        bool assist=command.keyboardRecovery && command.hand==0 && along>spec.driftMinSpeed && GroundedCount>=3;
        if(!assist){correctionKey=0;correctionAge=0;}
        float beta=SignedDriftAngle;
        releaseSupport=assist?Mathf.Max(0,releaseSupport-dt):0;
        float aligningAngle=Mathf.Clamp(beta*setup.recoveryCounterGain-yawRate*setup.recoveryYawSeconds,
            -lockDegrees,lockDegrees);
        if(target==0){
            keyboardVelocity=0;
            float returnRate=setup.keyboardCenterDegreesPerSecond+Mathf.Abs(angle)/setup.keyboardCenterSeconds;
            float rest=0;
            if(assist){
                float demand=Mathf.InverseLerp(setup.recoveryNeutralSlip,setup.recoveryNeutralSlip+setup.recoveryCounterSlip,Mathf.Abs(beta));
                demand=Mathf.Max(demand,Mathf.Clamp01(releaseSupport/setup.recoveryResponseSeconds));
                rest=aligningAngle*demand;
                returnRate=Mathf.Max(returnRate,Mathf.Abs(rest-angle)/setup.recoveryResponseSeconds);
            }
            steering=Mathf.MoveTowards(angle,rest,returnRate*dt)/lockDegrees;
            return;
        }
        if(assist && (correctionKey==target || (target*beta>setup.recoveryCounterSlip && -target*yawRate>setup.recoveryYawRate))){
            correctionKey=target;correctionAge+=dt;
            releaseSupport=setup.recoveryFollowSeconds;
            // Start additional rack travel continuously from zero, using the ordinary
            // keyboard acceleration. A long deliberate reversal can still reach full lock.
            float held=Mathf.Max(0,correctionAge-setup.recoveryHoldSeconds);
            float accel=Mathf.Lerp(setup.keyboardAcceleration.x,setup.keyboardAcceleration.y,
                Mathf.InverseLerp(setup.keyboardPrecisionKph.x,setup.keyboardPrecisionKph.y,roadSpeed*3.6f));
            float extra=held*(setup.keyboardStartDegreesPerSecond+.5f*held*accel);
            float requested=Mathf.Clamp(aligningAngle+target*extra,-lockDegrees,lockDegrees);
            float movement=(requested-angle)*(1-Mathf.Exp(-dt/setup.recoveryResponseSeconds));
            float maximum=setup.keyboardMaxDegreesPerSecond*dt;
            steering=(angle+Mathf.Clamp(movement,-maximum,maximum))/lockDegrees;
            keyboardVelocity=0;return;
        }
        float speedBlend=Mathf.InverseLerp(setup.keyboardPrecisionKph.x,setup.keyboardPrecisionKph.y,roadSpeed*3.6f);
        float acceleration=Mathf.Lerp(setup.keyboardAcceleration.x,setup.keyboardAcceleration.y,speedBlend);
        float direction=Mathf.Sign(target);
        float incoming=Mathf.Max(setup.keyboardStartDegreesPerSecond,direction*keyboardVelocity);
        float outgoing=Mathf.MoveTowards(incoming,setup.keyboardMaxDegreesPerSecond,acceleration*dt);
        keyboardVelocity=direction*outgoing;
        float normalStep=(incoming+outgoing)*.5f*dt;

        // Fast travel is local to the remaining angle. Only the ordinary key rate carries through centre.
        float catchDegrees=0;
        if(GroundedCount>=3 && along>0){
            float opposingYaw=Mathf.Clamp01(-direction*yawRate/setup.keyboardCatchYawRate);
            float forwardBlend=Mathf.InverseLerp(0,spec.driftMinSpeed,along);
            catchDegrees=Mathf.Clamp(direction*SignedDriftAngle,0,setup.keyboardCatchDegrees)*opposingYaw*forwardBlend;
        }
        float remaining=Mathf.Max(0,catchDegrees-direction*angle);
        float unwindStep=remaining*(1-Mathf.Exp(-dt/setup.keyboardCenterSeconds));
        float maxStep=setup.keyboardMaxDegreesPerSecond*dt;
        steering=Mathf.MoveTowards(angle,target*lockDegrees,Mathf.Min(normalStep+unwindStep,maxStep))/lockDegrees;
    }
    void FixedUpdate()=>StepDrive(Time.fixedDeltaTime);
    void StepDrive(float dt){
        var c=EnabledDrive && Driver!=null?Driver.Read():new DriveCommand{brake=1};
        AppliedInput=Driver is LocalDriver local?local.Input:default;
        AppliedCommand=c;
        var setup=spec.competition;
        int left=DrivenLeft;
        float axleRpm=AxleRpm;
        float shiftWheelRpm=Mathf.Abs(axleRpm);
        if(spec.roadSpeedAutomaticShifts){
            float roadSpeed=Vector3.ProjectOnPlane(Chassis.linearVelocity,transform.up).magnitude;
            shiftWheelRpm=roadSpeed*60/(2*Mathf.PI*wheels[left].radius);
        }
        float shiftRpm=shiftWheelRpm*spec.finalDrive*spec.gears[Gear];
        int oldGear=Gear;
        if(setup && c.resumeAutomatic)ManualGears=false;
        bool requestShift=setup && !c.resumeAutomatic && !c.reverse && c.gearChange!=0 && EnabledDrive;
        int requestedGear=requestShift?Mathf.Clamp(Gear+Mathf.Clamp(c.gearChange,-1,1),0,spec.gears.Length-1):Gear;
        if(setup && c.reverse)Gear=0;
        else if(requestedGear!=Gear){
            Gear=requestedGear;ManualGears=true;
        }else if(!ManualGears){
            if(shiftRpm>spec.upshift && Gear<spec.gears.Length-1)Gear++;
            else if(shiftRpm<spec.downshift && Gear>0)Gear--;
        }
        bool reverse=c.reverse;
        bool shifted=oldGear!=Gear || reverse!=Reversing;
        if(reverse!=Reversing || (c.brake>0 && (!setup || c.throttle<=0)))pedal=0;
        Reversing=reverse;
        pedal=Mathf.MoveTowards(pedal,c.throttle,(c.throttle>pedal?spec.pedalRise:spec.pedalRelease)*dt);
        if(reverse)pedal*=Mathf.Clamp01((spec.reverseLimit+1+ForwardSpeed));
        GroundedCount=0;
        Vector3 normal=Vector3.zero;
        bool rearContact=false;
        for(int i=0;i<wheels.Length;i++){
            bool touching=wheels[i].GetGroundHit(out var hit);
            var collider=touching?hit.collider:null;
            if(groundColliders[i]!=collider){
                groundColliders[i]=collider;
                surfaces[i]=collider?collider.GetComponentInParent<TyreSurface>():null;
            }
            if(!touching)continue;
            contactCover[i]=surfaces[i]?surfaces[i].CoverAt(hit.point,hit.normal):RoadCover.Asphalt;
            normal+=hit.normal;GroundedCount++;if(i>=2)rearContact=true;
        }
        normal=normal.sqrMagnitude>0?normal.normalized:transform.up;
        var forward=Vector3.ProjectOnPlane(transform.forward,normal).normalized;
        float along=Vector3.Dot(Chassis.linearVelocity,forward);
        float across=Vector3.Dot(Chassis.linearVelocity,Vector3.Cross(normal,forward));
        SignedDriftAngle=across*across+along*along<spec.directionThreshold*spec.directionThreshold?0:Mathf.Atan2(across,along)*Mathf.Rad2Deg;
        DriftAngle=Mathf.Abs(SignedDriftAngle);
        float availableAngle=spec.steeringBySpeed.Evaluate(Speed);
        float yawRate=Vector3.Dot(Chassis.angularVelocity,normal)*Mathf.Rad2Deg;
        UpdateSteering(c,along,Mathf.Sqrt(along*along+across*across),yawRate,availableAngle,dt);
        float angle=steering*availableAngle;
        bool canPowerSlide=c.brake==0 && along>=spec.driftMinSpeed &&
            rearContact && GroundedCount>=3 && !impact;
        if(!spec.poweredDrift || c.hand<=0 || reverse || !EnabledDrive){
            poweredSlide=false;
            driftAge=0;
        }else if(canPowerSlide && Mathf.Abs(angle)>=spec.driftEntryAngle){
            poweredSlide=true;
        }
        // One brake pulse per held drift command, including lift-off entries.
        // Contact loss or an impact ends the pulse without re-arming it on landing.
        if(poweredSlide){
            driftAge+=dt;
            if(impact || GroundedCount<3 || !rearContact)
                driftAge=Mathf.Max(driftAge,spec.driftBrakePulse+dt);
        }
        bool assistSlide=poweredSlide && canPowerSlide;
        if(EnabledDrive && GroundedCount>=3 && along>spec.driftMinSpeed){
            float beta=SignedDriftAngle;
            float predicted=beta-yawRate*spec.yawPreview;
            float allowance=assistSlide?spec.driftSlipAllowance:spec.gripSlipAllowance;
            float correction=predicted-Mathf.Clamp(predicted,-allowance,allowance);
            float lockAngle=spec.steeringBySpeed.Evaluate(0);
            angle=Mathf.Clamp(angle+correction*spec.slipSteerGain,-lockAngle,lockAngle);
        }
        float radius=Mathf.Abs(angle)>.001f?spec.wheelbase/Mathf.Tan(Mathf.Abs(angle)*Mathf.Deg2Rad):float.PositiveInfinity;
        for(int i=0;i<2;i++){
            bool inner=(i==0)==(angle<0);
            float offset=spec.track*.5f*(setup?setup.ackermannFraction:1);
            wheels[i].steerAngle=Mathf.Sign(angle)*Mathf.Atan2(spec.wheelbase,radius+(inner?-offset:offset))*Mathf.Rad2Deg;
        }
        bool releaseBrake=poweredSlide && driftAge>spec.driftBrakePulse;
        float brakeTarget=releaseBrake?0:c.hand;
        hand=Mathf.MoveTowards(hand,brakeTarget,spec.handRamp*dt);
        // Restore lateral authority as sideslip grows so countersteer can catch the tail.
        float gripTarget=assistSlide?c.hand*(1-Mathf.InverseLerp(spec.driftEntryAngle,spec.driftGripRecoveryAngle,DriftAngle)):0;
        gripReduction=Mathf.MoveTowards(gripReduction,gripTarget,spec.driftBlendRate*dt);
        for(int i=0;!setup && i<4;i++){
            var surfaceGrip=groundColliders[i]?WheelGrip(i):Vector2.one;
            var longitudinal=wheels[i].forwardFriction;
            longitudinal.stiffness=spec.longitudinalGrip*surfaceGrip.x;
            wheels[i].forwardFriction=longitudinal;
            var lateral=wheels[i].sidewaysFriction;
            float stiffness=spec.lateralGrip*surfaceGrip.y*(i<2?1:Mathf.Lerp(1,spec.driftRearGrip,gripReduction));
            if(!Mathf.Approximately(stiffness,lateral.stiffness)){
                lateral.stiffness=stiffness;wheels[i].sidewaysFriction=lateral;
            }
        }
        reward.Step(spec,dt,along,DriftAngle,Mathf.Abs(yawRate),
            GroundedCount>=3 && rearContact,c.hand>.1f,c.brake>.05f,impact,EnabledDrive && !reverse && spec.boostGain>0);
        impact=false;
        boost=reward.Remaining>0?Mathf.MoveTowards(boost,1,spec.boostRamp*dt):0;
        float ratio=reverse?spec.gears[0]:spec.gears[Gear];
        if(setup){
            float signedRatio=(reverse?-1:1)*ratio*spec.finalDrive;
            float inertia=spec.wheelMass*wheels[left].radius*wheels[left].radius;
            float reactionLimit=0;
            for(int i=left;i<left+2;i++){
                var wheel=wheels[i];
                reactionLimit+=wheel.brakeTorque;
                if(wheel.GetGroundHit(out var hit))reactionLimit+=Mathf.Max(0,hit.force)*wheel.forwardFriction.stiffness*wheel.radius;
            }
            float torqueAtAxle=driveline.Step(spec,dt,pedal,c.clutch,axleRpm,signedRatio,inertia,reactionLimit,shifted,EnabledDrive);
            float limit=setup.differentialPreloadNm+Mathf.Abs(torqueAtAxle)*
                (driveline.ClutchNm>=0?setup.powerLock:setup.coastLock);
            float deltaSpeed=(wheels[left].rpm-wheels[left+1].rpm)*Mathf.PI/30;
            float transfer=DifferentialFraction*Mathf.Clamp(deltaSpeed*inertia/(4*dt),-limit,limit);
            wheels[left].motorTorque=.5f*torqueAtAxle-transfer;
            wheels[left+1].motorTorque=.5f*torqueAtAxle+transfer;
            competitionChassis.Step(c,hand,dt);
            return;
        }
        float engineRpm=Mathf.Max(spec.idle,Mathf.Abs(axleRpm)*spec.finalDrive*ratio);
        float engineTorque=spec.enginePowerMap?spec.enginePowerMap.TorqueNm(engineRpm):spec.engineNm.Evaluate(engineRpm);
        float torque=engineTorque*ratio*spec.finalDrive*spec.efficiency;
        float coupling=spec.handbrakeCutsDrive?1-hand:1;
        torque*= (reverse?-1:1)*(pedal+boost*spec.boostGain)*coupling;
        if(!reverse)torque*=Mathf.Clamp01((spec.poweredTopKph-ForwardSpeed*3.6f)/Mathf.Max(.1f,spec.limiterBandKph));
        float bias=DifferentialFraction*Mathf.Clamp((wheels[left].rpm-wheels[left+1].rpm)*spec.differentialGain,-spec.differentialLimit,spec.differentialLimit);
        // Engine braking follows the same driveline coupling as propulsion.
        float coastBrake=c.throttle<=0 && pedal<=.01f && boost==0 && !poweredSlide
            ? spec.coastTorque*coupling : 0;
        for(int i=0;i<4;i++){
            float drive=torque*.5f;
            if(i>=2 && poweredSlide && releaseBrake && wheels[i].GetGroundHit(out var contact)){
                // Measure driven-wheel overspeed in its contact plane. A locked
                // wheel must be allowed to spin up again after the brake pulse.
                float roadSpeed=Vector3.Dot(Chassis.GetPointVelocity(contact.point),contact.forwardDir);
                float treadSpeed=wheels[i].rpm*(2*Mathf.PI/60)*wheels[i].radius;
                float slip=(treadSpeed-roadSpeed)/Mathf.Max(spec.driftMinSpeed,Mathf.Abs(roadSpeed));
                drive*=1-Mathf.Clamp01((slip-spec.driftWheelSlipTarget)/spec.driftWheelSlipBand);
            }
            bool driven=i==left || i==left+1;
            wheels[i].motorTorque=driven?drive+(i==left?-bias:bias):0;
            wheels[i].brakeTorque=c.brake*spec.brakeNm*.5f*(i<2?spec.brakeFront:1-spec.brakeFront)
                +(i>=2?hand*spec.handbrakeNm:0)+(driven?coastBrake:0);
        }
    }
    void LateUpdate(){
        for(int i=0;i<4;i++){
            wheels[i].GetWorldPose(out var pos,out var rot);
            WheelHit hit=default;
            bool contact=contactVisuals!=null && wheels[i].GetGroundHit(out hit);
            if(contact)pos-=hit.normal*competitionChassis.Deflection(i,hit.force);
            tires[i].SetPositionAndRotation(pos,rot);
            if(carriers!=null && i<carriers.Length && carriers[i])
                carriers[i].SetPositionAndRotation(pos,wheels[i].transform.rotation*Quaternion.Euler(0,wheels[i].steerAngle,0));
            if(contactVisuals!=null)contactVisuals[i].Apply(contact,hit.point,hit.normal);
        }
    }
    void OnDestroy(){if(contactVisuals!=null)foreach(var visual in contactVisuals)visual?.Dispose();}
}
}
