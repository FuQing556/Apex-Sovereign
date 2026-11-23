using UnityEngine;

namespace Apex {
    [CreateAssetMenu(menuName="Apex/驾驶音效")]
    public sealed class DrivingSounds : ScriptableObject {
        [Header("发动机 · 各段排气采样的基准转速")]
        public AudioClip engineLow, engineHigh, engineRedline;
        [Min(1)] public float lowRpm=1200, highRpm=4200, redlineRpm=7200;
        public Vector2 crossfadeRpm=new Vector2(2000,3400);
        public Vector2 redlineCrossfadeRpm=new Vector2(5000,6600);
        [Range(0,1)] public float idleGain=.27f, powerGain=.53f;
        [Header("跟车视角 · 风噪")]
        public AudioClip wind;
        [Tooltip("横轴为车速（公里/时），纵轴为风噪相对强度。")]
        public AnimationCurve windByKph=new AnimationCurve(
            new Keyframe(0,0),new Keyframe(25,0),new Keyframe(60,.13f,.005f,.005f),
            new Keyframe(100,.40f,.006f,.006f),new Keyframe(160,.72f,.0035f,.0035f),
            new Keyframe(240,.90f,.0013f,.0013f),new Keyframe(370,1));
        [Range(0,1)] public float windGain=.60f;
        public Vector2 windPitch=new Vector2(.85f,1.17f);
        [Min(.01f)] public float windResponseSeconds=.24f;
        [Header("接地 · 速度单位为米/秒")]
        public AudioClip asphalt, kerb, grass, tyreScrub;
        [Min(.1f)] public float roadReferenceSpeed=20;
        public Vector2 audibleSlip=new Vector2(1.8f,13);
        [Range(0,1)] public float asphaltGain=.19f, kerbGain=.50f, grassGain=.46f, scrubGain=.51f;
        [Header("动作与发车信号")]
        public AudioClip shift, impact, boost, ready, go, finish;
        [Range(0,1)] public float shiftGain=.34f, impactGain=.78f, boostGain=.55f, signalGain=.42f;
        [Tooltip("碰撞冲量除以车重，从可闻到最大音量的速度变化（米/秒）。")]
        public Vector2 impactDeltaSpeed=new Vector2(.65f,7);
        [Min(0)] public float impactCooldown=.16f;
        [Header("混音")]
        [Min(.001f)] public float responseSeconds=.065f;
        [Min(.1f)] public float fullVolumeDistance=9;
    }
}
