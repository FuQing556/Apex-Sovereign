using UnityEngine;

namespace Apex {
public enum RoadCover { Custom, Asphalt, Kerb, Grass, Air }
// Optional on a collider or its parent. An unmarked road uses the dry tyre calibration.
public sealed class TyreSurface : MonoBehaviour {
    [Min(.01f)] public float longitudinalScale = 1, lateralScale = 1;
    public RoadCover cover = RoadCover.Custom;
    public RoadsideSettings settings;
    public Vector2 Grip {
        get {
            if(settings && cover==RoadCover.Kerb)return settings.dryKerbGrip;
            if(settings && cover==RoadCover.Grass)return settings.dryGrassGrip;
            return new Vector2(longitudinalScale,lateralScale);
        }
    }
    public static string Label(RoadCover kind){
        switch(kind){
            case RoadCover.Asphalt:return "沥青";
            case RoadCover.Kerb:return "路肩";
            case RoadCover.Grass:return "草地";
            case RoadCover.Air:return "离地";
            default:return "自定";
        }
    }
}
}
