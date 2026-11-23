using UnityEngine;

namespace Apex {
[CreateAssetMenu(menuName="Apex/Competition vehicle setup")]
public sealed class CompetitionSetup : ScriptableObject {
    [Min(1)] public int revision = 3;
    [Header("Estimated crankshaft and automatic clutch")]
    [Min(.01f)] public float engineInertia = .48f;
    [Min(1)] public float clutchCapacityNm = 1500;
    [Min(1)] public float launchRpm = 3000;
    [Min(.01f)] public float clutchEngagementSeconds = .18f;
    [Min(.01f)] public float shiftOpenSeconds = .09f;
    [Min(1)] public float overrunNm = 90;
    [Min(1)] public float limiterRpm = 8200, limiterResumeRpm = 8000;
    [Min(.001f)] public float axleLoadResponseSeconds = .055f;
    [Range(0,1)] public float minimumBoostTorqueFraction = .60f;
    [Min(.01f)] public float boostFillSeconds = .34f, boostReleaseSeconds = .14f;
    [Header("Estimated plate differential")]
    [Min(0)] public float differentialPreloadNm = 65;
    [Range(0,.5f)] public float powerLock = .25f, coastLock = .12f;
    [Header("Steering geometry and keyboard input")]
    [Range(-1,1)] public float ackermannFraction = .32f;
    [Tooltip("普通加舵角加速度，低速 / 高速（等效前轮度/秒²）。")]
    public Vector2 keyboardAcceleration = new Vector2(95, 10);
    [Tooltip("普通加舵过渡的车速范围（km/h）。")]
    public Vector2 keyboardPrecisionKph = new Vector2(40,95);
    [Min(0)] public float keyboardStartDegreesPerSecond = 4.5f;
    [Min(1)] public float keyboardMaxDegreesPerSecond = 165;
    [Tooltip("驾驶员主动反打时，可快速接近的最大等效前轮角（度），设为零可关闭侧滑反打提速。")]
    [Min(0)] public float keyboardCatchDegrees = 18;
    [Tooltip("反向横摆速度达到此值时，反打提速范围达到全量（度/秒）。")]
    [Min(1)] public float keyboardCatchYawRate = 30;
    [Tooltip("松键回正及主动退舵的时间常数（秒）。")]
    [Min(.02f)] public float keyboardCenterSeconds = .10f;
    [Min(.1f)] public float keyboardCenterDegreesPerSecond = 4;
    [Header("Optional keyboard recovery (equivalent front-wheel angles)")]
    [Min(0)] public float recoveryNeutralSlip=21, recoveryCounterSlip=5, recoveryYawRate=12;
    [Range(0,1)] public float recoveryCounterGain=.68f;
    [Min(0)] public float recoveryYawSeconds=.16f, recoveryFollowSeconds=1.1f;
    [Min(.02f)] public float recoveryResponseSeconds=.09f, recoveryHoldSeconds=.52f;
    [Header("Estimated brake hydraulics (N, mm, seconds)")]
    [Min(0)] public float pedalForce = 450, handForce = 250;
    [Min(.1f)] public float pedalRatio = 5, handLeverRatio = 6;
    [Range(.01f,.99f)] public float frontPushrodFraction = .54f;
    [Min(1)] public float frontMasterMm = 19.05f, rearMasterMm = 19.05f, handMasterMm = 15.875f;
    [Tooltip("Sum of piston areas on ONE face of each opposed-piston caliper, mm².")]
    [Min(1)] public float frontPistonArea = 3100, rearPistonArea = 2200, handPistonArea = 2050;
    [Min(.01f)] public float frontDiscRadius = .145f, rearDiscRadius = .132f;
    [Range(.05f,1)] public float padFriction = .41f;
    [Min(.001f)] public float serviceApplySeconds = .10f, serviceReleaseSeconds = .07f;
    [Header("Rotor temperature (estimated lumped thermal model)")]
    public float ambientC = 22, fadeStartC = 500, fadeEndC = 750;
    [Range(.1f,1)] public float hotPadFraction = .72f;
    [Min(1)] public float discHeatCapacity = 4200, discCoolingSeconds = 180;
    [Header("Front and rear tyres (empirical WheelCollider curves)")]
    public AxleTyre frontTyre = new AxleTyre(), rearTyre = new AxleTyre();
    [System.Serializable] public class AxleTyre {
        [Min(.01f)] public float longitudinal = 1.45f, lateral = 1.28f;
        [Min(.01f)] public float peakSideSlip = .20f, slidingSideSlip = .50f;
        [Range(.1f,1)] public float slidingFraction = .90f;
        [Min(.1f)] public float coldGaugeBar = 2;
    }
    [Range(0,.5f)] public float loadSensitivity = .12f;
    [Header("Estimated per-wheel thermal capacities (J/K) and conductances (W/K)")]
    [Min(1)] public float treadHeatCapacity = 4300, gasHeatCapacity = 96;
    [UnityEngine.Serialization.FormerlySerializedAs("tyreHeatCapacity"), Min(1)] public float carcassHeatCapacity = 14000;
    [UnityEngine.Serialization.FormerlySerializedAs("tyreCoolingSeconds"), Min(1)] public float carcassCoolingSeconds = 160;
    [Min(0)] public float treadCarcassConductance = 165, carcassGasConductance = 4;
    [Min(0)] public float treadAirConductance = 30, treadRoadConductance = 90;
    [Tooltip("Additional W/K of cooling per m/s relative to still air.")]
    [Min(0)] public float treadAirPerSpeed = 3;
    public float roadC = 22;
    [Tooltip("低速滚阻平滑衰减至静止的参考速度（m/s）。")]
    [Min(.01f)] public float rollingFadeSpeed = .5f;
    [Min(0)] public float rollingLossCoefficient = .012f;
    [Range(0,1)] public float treadGripWeight = .70f;
    [Range(0,1)] public float heatToTyre = .50f;
    public float warmTyreC = 60, hotTyreC = 95, overheatC = 140;
    [Range(.1f,1)] public float coldGrip = .96f, overheatedGrip = .78f;
    [Header("Chassis (N/m, N s/m, kg/m³, m²)")]
    [Min(0)] public float frontRollStiffness = 9500, rearRollStiffness = 6000;
    [Min(0)] public float reboundDamper = 4400;
    [Min(0)] public float airDensity = 1.225f, dragArea = .78f, sideDragArea = 2.4f, downforceArea = .48f;
    public Vector3 aeroCenter = new Vector3(0,.42f,-.26f);

    public static float Pressure(float force, float boreMm) {
        return force / (Mathf.PI * boreMm * boreMm * .25e-6f);
    }
    public float BrakeTorque(float pressurePa, float areaMm2, float radius, float temperature) {
        float fade = Mathf.InverseLerp(fadeStartC, fadeEndC, temperature);
        return 2 * pressurePa * areaMm2 * 1e-6f * radius * padFriction * Mathf.Lerp(1,hotPadFraction,fade);
    }
}
}
