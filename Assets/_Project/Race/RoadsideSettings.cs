using UnityEngine;

namespace Apex {
    [CreateAssetMenu(menuName = "Apex/Roadside construction")]
    public class RoadsideSettings : ScriptableObject {
        [Min(.1f)] public float barrierHeight = 1.35f;
        [Min(.1f)] public float barrierWidth = .45f;
        [Min(0)] public float shoulderWidth = 3.2f;
        [Min(0)] public float embankmentRunPerRise = .55f;
        [Range(.05f,.35f)] public float minimumInsideAdvance = .10f;
        [Range(.08f,.5f)] public float insideGradeLimit = .18f;
        [Range(.1f,1)] public float shoulderTaper = .24f;
        [Min(1)] public float shoulderBlendMetres = 9;
        [Min(.5f)] public float grassClusterSpacing = 1.4f;
        [Min(.05f)] public float grassHeight = .21f;
        [Min(5)] public float approachMarkInterval = 18;
        public Material concrete, marking, earth, grass;
        [Tooltip("干燥防滑漆，相对本车干沥青的纵向、横向抓地倍率")]
        public Vector2 dryKerbGrip = new Vector2(.95f,.95f);
        [Tooltip("干草，相对本车干沥青的纵向、横向抓地倍率")]
        public Vector2 dryGrassGrip = new Vector2(.455f,.455f);
        [Tooltip("车身与两侧护栏接触时的摩擦和回弹")]
        public PhysicsMaterial barrierContact;
    }
}
