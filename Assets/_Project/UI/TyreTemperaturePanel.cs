using UnityEngine;
using UnityEngine.UI;

namespace Apex {
public sealed class TyreTemperaturePanel : MonoBehaviour {
    [System.Serializable]
    public struct Wheel {
        public RawImage tread;
        public Text reading;
    }
    public RectTransform diagram;
    public Text status;
    [Tooltip("左前、右前、左后、右后，与 RaceCar.wheels 的顺序相同。")]
    public Wheel[] wheels;
    [SerializeField, Min(0)] float panelSpacing=10;
    [SerializeField] Color cold = new Color32(110,191,255,255), warm = new Color32(99,226,185,255),
        hot = new Color32(255,180,77,255), overheated = new Color32(255,88,81,255);
    readonly int[] displayed = new int[4];

    public void Dock(RectTransform neighbour,bool below){
        var r=(RectTransform)transform;
        r.anchorMin=neighbour.anchorMin;r.anchorMax=neighbour.anchorMax;r.pivot=neighbour.pivot;
        float separation=neighbour.rect.height+panelSpacing;
        r.anchoredPosition=neighbour.anchoredPosition+Vector2.up*(below?-separation:separation);
    }
    public void Bind(RaceCar car){
        Bind(car.CompetitionChassis!=null);
    }
    public void Bind(bool measured){
        diagram.gameObject.SetActive(measured);
        float header=-diagram.anchoredPosition.y;
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,header+(measured?diagram.rect.height:0));
        for(int i=0;i<displayed.Length;i++)displayed[i]=int.MinValue;
        status.text=measured?"":"未采集";
        status.color=Color.gray;
    }
    Color Tint(float c,CompetitionSetup setup){
        if(c<setup.warmTyreC)return Color.Lerp(cold,warm,Mathf.InverseLerp(setup.ambientC,setup.warmTyreC,c));
        if(c<setup.hotTyreC)return Color.Lerp(warm,hot,Mathf.InverseLerp(setup.warmTyreC,setup.hotTyreC,c));
        return Color.Lerp(hot,overheated,Mathf.InverseLerp(setup.hotTyreC,setup.overheatC,c));
    }
    public void Refresh(RaceCar car){
        var chassis=car.CompetitionChassis;
        if(chassis==null)return;
        Refresh(car.spec.competition,new Vector4(chassis.TyreC(0),chassis.TyreC(1),chassis.TyreC(2),chassis.TyreC(3)),
            new Vector4(car.wheels[0].steerAngle,car.wheels[1].steerAngle,car.wheels[2].steerAngle,car.wheels[3].steerAngle));
    }
    public void Refresh(CompetitionSetup setup,Vector4 temperatures,Vector4 steer){
        int overheatMask=0;
        float highest=float.NegativeInfinity;
        for(int i=0;i<wheels.Length;i++){
            float c=temperatures[i];
            highest=Mathf.Max(highest,c);
            if(c>=setup.overheatC)overheatMask|=1<<i;
            var wheel=wheels[i];
            var tint=Tint(c,setup);
            wheel.tread.color=tint;
            wheel.tread.rectTransform.localRotation=Quaternion.Euler(0,0,-steer[i]);
            wheel.reading.color=Color.Lerp(tint,Color.white,.23f);
            int value=Mathf.RoundToInt(c);
            if(value==displayed[i])continue;
            displayed[i]=value;wheel.reading.text=value.ToString();
        }
        if(overheatMask!=0){
            status.text=overheatMask switch {
                1=>"左前过热",2=>"右前过热",4=>"左后过热",8=>"右后过热",
                3=>"前轮过热",12=>"后轮过热",15=>"四轮过热",_=>"多轮过热"
            };
        }else status.text=highest>=setup.hotTyreC?"高温":highest<setup.warmTyreC?"冷胎":"";
        status.color=Tint(highest,setup);
    }
}
}
