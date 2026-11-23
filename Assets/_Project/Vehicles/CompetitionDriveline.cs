using UnityEngine;

namespace Apex {
// Crank inertia and a capacity-limited friction clutch. WheelCollider owns wheel inertia
// and tyre reactions; this solver applies the same clutch impulse to each side via the gears.
public sealed class CompetitionDriveline {
    const float RpmToRad = 2 * Mathf.PI / 60;
    float omega, shiftTime, previousWheelSpeed, previousAxleTorque, reaction;
    bool sampled;
    public float EngineRpm => omega / RpmToRad;
    public float ShaftRpm { get; private set; }
    public float ClutchNm { get; private set; }
    public float Engagement { get; private set; }
    public float Charge { get; private set; }
    public bool FuelCut { get; private set; }

    public void Reset(float idle) {
        omega = idle * RpmToRad;
        ShaftRpm = ClutchNm = Engagement = Charge = shiftTime = 0;
        sampled = FuelCut = false;
        previousWheelSpeed = previousAxleTorque = reaction = 0;
    }

    public float Step(CarSpec spec, float dt, float pedal, float clutchPedal, float wheelRpm,
        float ratio, float wheelInertia, float reactionLimit, bool shifted, bool enabled) {
        var setup = spec.competition;
        if (shifted) {
            shiftTime = setup.shiftOpenSeconds;
            Engagement = 0;
        }
        ShaftRpm = wheelRpm * ratio;
        Charge = Mathf.MoveTowards(Charge, pedal, dt / (pedal > Charge ? setup.boostFillSeconds : setup.boostReleaseSeconds));
        float rpm = EngineRpm;
        if(rpm>=setup.limiterRpm) FuelCut=true;
        if(rpm<=setup.limiterResumeRpm) FuelCut=false;
        float mapRpm = Mathf.Max(spec.idle,rpm);
        float fullTorque = spec.enginePowerMap ? spec.enginePowerMap.TorqueNm(mapRpm) : spec.engineNm.Evaluate(mapRpm);
        float firingPedal = FuelCut?0:pedal;
        float gasTorque = fullTorque * firingPedal * Mathf.Lerp(setup.minimumBoostTorqueFraction,1,Charge);
        float drag = setup.overrunNm * Mathf.InverseLerp(spec.idle,spec.upshift,rpm) * (1-firingPedal);
        float inertia = setup.engineInertia;
        float freeOmega = Mathf.Max(0, omega + (gasTorque-drag) * dt / inertia);
        // Automatic anti-stall opens at walking speed; a held pedal always opens the clutch.
        float launch = Mathf.InverseLerp(spec.idle,setup.launchRpm,Mathf.Max(rpm,ShaftRpm));
        float target = enabled && shiftTime <= 0 ? launch * (1-Mathf.Clamp01(clutchPedal)) : 0;
        Engagement = Mathf.Min(1-Mathf.Clamp01(clutchPedal), Mathf.MoveTowards(Engagement,target,dt/setup.clutchEngagementSeconds));
        if (!enabled) Engagement = 0;
        shiftTime = Mathf.Max(0,shiftTime-dt);
        // Equivalent angular impulse required to synchronize both inertias this step.
        // The previous wheel step also contained tyre, brake and damping reactions.
        // Treating grounded wheels as free flywheels here makes the clutch slip indefinitely.
        float wheelSpeed=wheelRpm*RpmToRad;
        if(sampled){
            float measured=previousAxleTorque-wheelInertia*(wheelSpeed-previousWheelSpeed)/dt;
            float blend=1-Mathf.Exp(-dt/setup.axleLoadResponseSeconds);
            reaction=Mathf.Lerp(reaction,Mathf.Clamp(measured,-reactionLimit,reactionLimit),blend);
        }
        reaction=Mathf.Clamp(reaction,-reactionLimit,reactionLimit);
        previousWheelSpeed=wheelSpeed;
        sampled=true;
        float freeShaft=(wheelSpeed-reaction*dt/wheelInertia)*ratio;
        float efficiency=freeOmega>=freeShaft?spec.efficiency:1/spec.efficiency;
        float inverseInertia = 1/inertia + ratio*ratio*efficiency / wheelInertia;
        float syncTorque = (freeOmega-freeShaft)/(dt*inverseInertia);
        float capacity = setup.clutchCapacityNm * Engagement;
        ClutchNm = Mathf.Clamp(syncTorque,-capacity,capacity);
        omega = Mathf.Max(0,freeOmega - ClutchNm*dt/inertia);
        // Idle governor only recovers speed with the clutch released/slipping at launch.
        if (omega < spec.idle*RpmToRad && Engagement < 1) {
            float idleTorque=spec.enginePowerMap?spec.enginePowerMap.TorqueNm(spec.idle):spec.engineNm.Evaluate(spec.idle);
            omega = Mathf.MoveTowards(omega,spec.idle*RpmToRad,idleTorque*dt/inertia);
        }
        previousAxleTorque=ClutchNm * ratio * efficiency;
        return previousAxleTorque;
    }
}
}
