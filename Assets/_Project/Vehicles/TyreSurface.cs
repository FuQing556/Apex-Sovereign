using UnityEngine;

namespace Apex {
public enum RoadCover { Custom, Asphalt, Kerb, Grass, Air }
// Optional on a collider or its parent. An unmarked road uses the dry tyre calibration.
public sealed class TyreSurface : MonoBehaviour {
    [Min(.01f)] public float longitudinalScale = 1, lateralScale = 1;
    public RoadCover cover = RoadCover.Custom;
    public RoadsideSettings settings;
    [Tooltip("连续赛道路面，每个横断面由七条固定顺序的三角带组成。")]
    public bool trackRibbon;
    MeshCollider ribbon;
    public RoadCover CoverAt(Vector3 point,Vector3 normal){
        if(!trackRibbon)return cover;
        if(!ribbon)ribbon=GetComponent<MeshCollider>();
        // WheelHit has no triangle index. Query only this collider at the measured contact.
        if(ribbon && ribbon.Raycast(new Ray(point+normal*.15f,-normal),out var hit,.30f)){
            int band=hit.triangleIndex/2%7;
            if(band==3)return RoadCover.Asphalt;
            if(band==2 || band==4)return RoadCover.Kerb;
            return RoadCover.Grass;
        }
        return cover;
    }
    public Vector2 Grip {
        get { return GripFor(cover); }
    }
    public Vector2 GripFor(RoadCover kind){
        if(settings && kind==RoadCover.Kerb)return settings.dryKerbGrip;
        if(settings && kind==RoadCover.Grass)return settings.dryGrassGrip;
        return new Vector2(longitudinalScale,lateralScale);
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
