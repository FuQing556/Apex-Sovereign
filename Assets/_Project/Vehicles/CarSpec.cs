using UnityEngine;

namespace Apex {
[CreateAssetMenu(menuName="Apex/Car configuration")]
public class CarSpec : ScriptableObject {
    public string displayName = "AS / R01";
    public GameObject body;
    public CompetitionSetup competition;
    [Header("Chassis (m, kg)")]
    public float mass=1350, wheelbase=2.6f, track=1.56f, radius=.32f, centerHeight=.32f;
    [Tooltip("Rear tyre radius in metres. Zero uses the shared radius.")] [Min(0)] public float rearRadius;
    [Tooltip("前、后轮视觉胎宽（米），用于接地印迹。")]
    public Vector2 visualTyreWidths=new Vector2(.23f,.23f);
    [Range(.3f,.7f)] public float frontWeight=.52f;
    [Header("Input and steering")]
    public AnimationCurve steeringBySpeed = AnimationCurve.Linear(0,30,50,8);
    public float steerRate=9, returnRate=12, countersteerRate=22;
    [Header("Pedals and road grip (m/s, per second, N m)")]
    public float directionThreshold=.18f, reverseLimit=9, pedalRise=3, pedalRelease=5, coastTorque=36;
    public float longitudinalGrip=1.45f, lateralGrip=1.28f;
    [Header("Steering assistance (degrees, seconds)")]
    [Min(0)] public float gripSlipAllowance=7, driftSlipAllowance=14, slipSteerGain=2.5f, yawPreview=.22f;
    [Range(.1f,1)] public float slidingGripFraction=.92f;
    [Header("Transmission")]
    [Tooltip("Let the automatic driver choose gears from chassis speed, even when the driven wheels are spinning.")]
    public bool roadSpeedAutomaticShifts;
    [Tooltip("Optional dyno or estimated power map. When assigned it replaces the baseline torque curve.")]
    public EnginePowerMap enginePowerMap;
    public AnimationCurve engineNm = new AnimationCurve(new Keyframe(1000,240),new Keyframe(2500,372),new Keyframe(4500,445),new Keyframe(6500,350));
    public float[] gears={3.2f,2.05f,1.5f,1.17f,.95f,.79f};
    [Min(1)] public float poweredTopKph=370;
    [Min(.1f)] public float limiterBandKph=12;
    public float finalDrive=3.9f, efficiency=.9f, upshift=6300, downshift=2200, idle=1000, differentialGain=1, differentialLimit=130;
    [Header("Brakes / boost")]
    [Tooltip("Game assist coupling the handbrake command to driveline disengagement.")]
    public bool handbrakeCutsDrive=true;
    public float brakeNm=4400, handbrakeNm=2500, handRamp=20, boostGain=.25f, boostRamp=4;
    [Header("Powered handbrake drift")]
    public bool poweredDrift=true;
    [Min(0)] public float driftBrakePulse=.18f;
    [Range(.1f,1)] public float driftRearGrip=.92f;
    [Min(.01f)] public float driftWheelSlipTarget=.22f;
    [Min(.01f)] public float driftWheelSlipBand=.45f;
    [Min(1)] public float driftGripRecoveryAngle=22;
    [Min(.1f)] public float driftBlendRate=4;
    [Header("Drift reward (m/s, degrees, deg/s, seconds)")]
    public float driftMinSpeed=7, driftEntryAngle=7.5f, driftExitAngle=6, driftSpinAngle=50, driftMinYaw=8;
    public float driftMinSeconds=.24f, driftFullSeconds=1.1f, driftExitWindow=1.1f, driftBoostMin=.5f, driftBoostMax=1.1f;
    [Header("Twin exhausts (local position of right tip, metres)")]
    public Vector3 exhaustTip=new Vector3(.6f,.47f,-2.08f);
    public float flameLength=1.05f, flameWidth=.24f;
    [Range(0,1)] public float brakeFront=.65f;
    [Header("Wheel suspension (N/m, N s/m, m)")]
    public float frontSpring=32000,rearSpring=30000,damper=3600,travel=.24f,wheelMass=35;
    [Header("Visual tire deflection")]
    public float tireStiffness=200000,maxFlatten=.04f;
    [Header("Follow camera")]
    public Vector3 cameraOffset=new Vector3(0,3.4f,-7);
    public float lookAhead=4,cameraResponse=14;
}
}
