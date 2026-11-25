using UnityEngine;
using UnityEngine.Rendering;

namespace Apex {
public sealed class BodyAirflow : MonoBehaviour {
    readonly TrailRenderer[] trails=new TrailRenderer[2];
    RaceCar car;
    int reset;
    public void Bind(RaceCar vehicle,Material material,Vector2 offset,float lifetime){
        car=vehicle;reset=car.ResetGeneration;
        for(int i=0;i<2;i++){
            var t=new GameObject(i==0?"左侧气流":"右侧气流").transform;
            t.SetParent(transform,false);
            t.localPosition=new Vector3((2*i-1)*vehicle.spec.track*.5f,offset.y,-vehicle.spec.wheelbase*.5f+offset.x);
            var trail=trails[i]=t.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial=material;trail.time=lifetime;trail.emitting=false;trail.minVertexDistance=.10f;
            trail.widthCurve=AnimationCurve.EaseInOut(0,1,1,0);
            trail.startColor=new Color(.86f,.94f,1,.32f);trail.endColor=new Color(.84f,.92f,1,0);
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
        }
    }
    public void SetStrength(float strength,float width){
        bool emit=strength>.001f && reset==car.ResetGeneration;
        foreach(var trail in trails){
            if(!emit && trail.emitting)trail.Clear();
            trail.emitting=emit;trail.widthMultiplier=width*Mathf.Clamp01(strength);
        }
        reset=car.ResetGeneration;
    }
}
}
