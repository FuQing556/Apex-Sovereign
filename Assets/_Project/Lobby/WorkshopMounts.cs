using UnityEngine;

namespace Apex {
public sealed class WorkshopMounts : MonoBehaviour {
    public Transform rearLeft,rearRight;
    [Min(.01f)] public float coverRadius;
    [Min(0)] public float faceOffset;
    GameObject leftCover,rightCover;
    WorkshopCatalog.Part fitted;

    public void Fit(WorkshopCatalog.Part part){
        if(fitted==part)return;
        fitted=part;
        Remove(leftCover);Remove(rightCover);
        leftCover=Mount(rearLeft,-1,part.rearWheelCover);
        rightCover=Mount(rearRight,1,part.rearWheelCover);
    }
    static void Remove(GameObject cover){
        if(cover){cover.SetActive(false);Destroy(cover);}
    }
    GameObject Mount(Transform axle,int side,GameObject prefab){
        if(!prefab)return null;
        var cover=Instantiate(prefab,axle,false);
        cover.transform.localPosition=Vector3.right*(side*faceOffset);
        cover.transform.localRotation=side<0?Quaternion.Euler(0,180,0):Quaternion.identity;
        cover.transform.localScale=Vector3.one*coverRadius;
        return cover;
    }
}
}
