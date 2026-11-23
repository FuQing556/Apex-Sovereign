using UnityEngine;

namespace Apex {
public sealed class CompetitionChassis {
    readonly RaceCar car;
    readonly CompetitionSetup setup;
    readonly float[] compression = new float[4], temperature = new float[4], previousRpm = new float[4];
    readonly TyreThermalState[] tyres = new TyreThermalState[4];
    readonly float[] slipPower = new float[4], rollingPower = new float[4];
    public float RollingPowerW(int wheel) => rollingPower[wheel];
    readonly bool[] grounded = new bool[4];
    public Vector3 AppliedForce { get; private set; }
    public Vector3 AppliedTorque { get; private set; }
    public Vector3 AeroTorque { get; private set; }
    public Vector3 RollBarTorque { get; private set; }
    public Vector3 RollingTorque => AppliedTorque - AeroTorque - RollBarTorque;
    Vector3 ApplyAt(Vector3 force,Vector3 point) {
        var moment=Vector3.Cross(point-car.Chassis.worldCenterOfMass,force);
        AppliedForce+=force;
        AppliedTorque+=moment;
        car.Chassis.AddForceAtPosition(force,point);
        return moment;
    }
    float brake;
    public float FrontBar { get; private set; }
    public float RearBar { get; private set; }
    public float HandBar { get; private set; }
    public float DiscC(int wheel) => temperature[wheel];
    public float TyreC(int wheel) => tyres[wheel].TreadC;
    public float CarcassC(int wheel) => tyres[wheel].CarcassC;
    public float GasC(int wheel) => tyres[wheel].AirC;
    public float SlipPowerW(int wheel) => slipPower[wheel];
    public float TyreBar(int wheel){
        float cold=(wheel<2?setup.frontTyre:setup.rearTyre).coldGaugeBar;
        const float atmosphereBar=1.01325f;
        return (cold+atmosphereBar)*(tyres[wheel].AirC+273.15f)/(setup.ambientC+273.15f)-atmosphereBar;
    }
    public float Deflection(int wheel,float load){
        float cold=(wheel<2?setup.frontTyre:setup.rearTyre).coldGaugeBar;
        float stiffness=car.spec.tireStiffness*TyreBar(wheel)/cold;
        return Mathf.Clamp(load/Mathf.Max(1,stiffness),0,car.spec.maxFlatten);
    }

    public CompetitionChassis(RaceCar car) {
        this.car = car;
        setup = car.spec.competition;
        for (int i=0;i<4;i++) {
            temperature[i]=setup.ambientC;
            tyres[i]=new TyreThermalState(setup.ambientC);
        }
    }
    public void ResetControls() {
        brake=FrontBar=RearBar=HandBar=0;
        System.Array.Clear(grounded,0,grounded.Length);
        System.Array.Clear(previousRpm,0,previousRpm.Length);
    }
    public void Step(DriveCommand command,float hand,float dt) {
        AppliedForce=AppliedTorque=AeroTorque=RollBarTorque=Vector3.zero;
        var spec=car.spec;
        float target=Mathf.Clamp01(command.brake);
        brake=Mathf.MoveTowards(brake,target,dt/(target>brake?setup.serviceApplySeconds:setup.serviceReleaseSeconds));
        float push=brake*setup.pedalForce*setup.pedalRatio;
        FrontBar=CompetitionSetup.Pressure(push*setup.frontPushrodFraction,setup.frontMasterMm)*1e-5f;
        RearBar=CompetitionSetup.Pressure(push*(1-setup.frontPushrodFraction),setup.rearMasterMm)*1e-5f;
        HandBar=CompetitionSetup.Pressure(hand*setup.handForce*setup.handLeverRatio,setup.handMasterMm)*1e-5f;
        for(int i=0;i<4;i++) {
            var wheel=car.wheels[i];
            bool front=i<2;
            var tyre=front?setup.frontTyre:setup.rearTyre;
            float rpm=wheel.rpm;
            float spinSpeed=(Mathf.Abs(rpm)+Mathf.Abs(previousRpm[i]))*Mathf.PI/60;
            temperature[i]+=(wheel.brakeTorque*spinSpeed/setup.discHeatCapacity
                -(temperature[i]-setup.ambientC)/setup.discCoolingSeconds)*dt;
            previousRpm[i]=rpm;
            float pressure=front?FrontBar:RearBar;
            float radius=front?setup.frontDiscRadius:setup.rearDiscRadius;
            wheel.brakeTorque=setup.BrakeTorque(pressure*1e5f,front?setup.frontPistonArea:setup.rearPistonArea,radius,temperature[i]);
            if(!front) wheel.brakeTorque+=setup.BrakeTorque(HandBar*1e5f,setup.handPistonArea,radius,temperature[i]);

            bool wasGrounded=grounded[i];
            grounded[i]=wheel.GetGroundHit(out var hit);
            float previous=compression[i];
            compression[i]=grounded[i]?Mathf.Clamp(wheel.suspensionDistance+wheel.radius+
                wheel.transform.InverseTransformPoint(hit.point).y-wheel.center.y,0,wheel.suspensionDistance):0;
            var spring=wheel.suspensionSpring;
            float damper=wasGrounded && grounded[i] && compression[i]<previous ? setup.reboundDamper : spec.damper;
            if(spring.damper!=damper){spring.damper=damper;wheel.suspensionSpring=spring;}
            var collider=grounded[i]?hit.collider:null;
            float normalReference=spec.mass*Physics.gravity.magnitude*(front?spec.frontWeight:1-spec.frontWeight)*.5f;
            float relativeLoad=grounded[i]?Mathf.Clamp(hit.force/Mathf.Max(1,normalReference),.25f,4):1;
            var road=grounded[i]?car.WheelGrip(i):Vector2.one;
            rollingPower[i]=0;
            slipPower[i]=0;
            bool loaded=grounded[i] && hit.force>0;
            if(loaded){
                var speed=car.Chassis.GetPointVelocity(hit.point);
                var other=collider.attachedRigidbody;
                if(other)speed-=other.GetPointVelocity(hit.point);
                float rollingSpeed=rpm*(2*Mathf.PI/60)*wheel.radius;
                float groundSpeed=Vector3.Dot(speed,hit.forwardDir);
                float longitudinalSlipSpeed=rollingSpeed-groundSpeed;
                float lateralSlipSpeed=Vector3.Dot(speed,hit.sidewaysDir);
                slipPower[i]=TyreThermalState.SlipPower(hit.force,wheel.forwardFriction,wheel.sidewaysFriction,
                    hit.forwardSlip,hit.sidewaysSlip,longitudinalSlipSpeed,lateralSlipSpeed);
                // Taper at rest and during wheel lock. Equal and opposite contact forces
                // dissipate exactly -F · relative velocity, including on a moving surface.
                float rollingFraction=Mathf.Min(Mathf.Abs(rollingSpeed),Mathf.Abs(groundSpeed))/
                    Mathf.Max(Mathf.Max(.001f,setup.rollingFadeSpeed),Mathf.Abs(groundSpeed));
                float resistance=setup.rollingLossCoefficient*hit.force*rollingFraction*Mathf.Sign(groundSpeed);
                var forceOnCar=-resistance*hit.forwardDir;
                ApplyAt(forceOnCar,hit.point);
                if(other && !other.isKinematic)other.AddForceAtPosition(-forceOnCar,hit.point);
                rollingPower[i]=resistance*groundSpeed;
            }
            tyres[i].Step(setup,slipPower[i],rollingPower[i],loaded,car.Speed,dt);
            float rubberC=Mathf.Lerp(tyres[i].CarcassC,tyres[i].TreadC,setup.treadGripWeight);
            float warm=Mathf.InverseLerp(setup.ambientC,setup.warmTyreC,rubberC);
            float hot=Mathf.InverseLerp(setup.hotTyreC,setup.overheatC,rubberC);
            float gripFactor=Mathf.Pow(relativeLoad,-setup.loadSensitivity)*Mathf.Lerp(setup.coldGrip,1,warm)*Mathf.Lerp(1,setup.overheatedGrip,hot);
            var lateral=wheel.sidewaysFriction;
            lateral.extremumSlip=tyre.peakSideSlip;
            lateral.asymptoteSlip=tyre.slidingSideSlip;
            lateral.asymptoteValue=lateral.extremumValue*tyre.slidingFraction;
            lateral.stiffness=tyre.lateral*gripFactor*road.y;
            wheel.sidewaysFriction=lateral;
            var longitudinal=wheel.forwardFriction;
            longitudinal.stiffness=tyre.longitudinal*gripFactor*road.x;
            wheel.forwardFriction=longitudinal;
        }
        ApplyRollBar(0,setup.frontRollStiffness);
        ApplyRollBar(2,setup.rearRollStiffness);
        var body=car.Chassis;
        var local=Quaternion.Inverse(body.rotation)*body.linearVelocity;
        float q=.5f*setup.airDensity;
        var force=new Vector3(-q*setup.sideDragArea*local.x*Mathf.Abs(local.x),
            -q*setup.downforceArea*local.z*local.z,-q*setup.dragArea*local.z*Mathf.Abs(local.z));
        AeroTorque=ApplyAt(body.rotation*force,body.position+body.rotation*setup.aeroCenter);
    }
    void ApplyRollBar(int left,float stiffness) {
        int right=left+1;
        if(!grounded[left] || !grounded[right])return;
        var force=car.transform.up*((compression[left]-compression[right])*stiffness);
        RollBarTorque+=ApplyAt(force,car.wheels[left].transform.position);
        RollBarTorque+=ApplyAt(-force,car.wheels[right].transform.position);
    }
}
}
