using UnityEngine;

namespace Apex {
public sealed class ExhaustFlame : MonoBehaviour {
    RaceCar car;
    readonly LineRenderer[] jets=new LineRenderer[2];
    Material material;
    public void Bind(RaceCar source){
        car=source;
        material=new Material(Resources.Load<Shader>("ApexExhaust"));
        for(int i=0;i<jets.Length;i++){
            var g=new GameObject(i==0?"Exhaust L":"Exhaust R");
            g.transform.SetParent(transform,false);
            var tip=car.spec.exhaustTip;tip.x*=i==0?-1:1;g.transform.localPosition=tip;
            var jet=jets[i]=g.AddComponent<LineRenderer>();
            jet.useWorldSpace=false;jet.sharedMaterial=material;jet.positionCount=3;
            jet.startColor=new Color(.12f,.8f,1);jet.endColor=new Color(.16f,.32f,1,.2f);
            jet.widthCurve=new AnimationCurve(new Keyframe(0,.7f),new Keyframe(.22f,1),new Keyframe(1,.03f));
            jet.SetPosition(0,Vector3.zero);jet.enabled=false;
        }
    }
    void LateUpdate(){
        for(int i=0;i<jets.Length;i++){
            var jet=jets[i];jet.enabled=car.EnabledDrive && car.BoostPower>0;
            if(!jet.enabled)continue;
            float len=car.spec.flameLength*car.BoostPower*(.91f+.09f*Mathf.Sin(Time.time*53+i));
            jet.widthMultiplier=car.spec.flameWidth*Mathf.Sqrt(car.BoostPower);
            jet.SetPosition(1,Vector3.back*(len*.22f));jet.SetPosition(2,Vector3.back*len);
        }
    }
    void OnDestroy(){if(material)Destroy(material);}
}
}
