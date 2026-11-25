using System;
using UnityEngine;

namespace Apex {
[CreateAssetMenu(menuName="Apex/车库配件目录")]
public sealed class WorkshopCatalog : ScriptableObject {
    [Serializable] public class Part {
        public string id, title;
        [TextArea] public string description;
        public GameObject displayModel;
        public GameObject rearWheelCover;
        [Range(0,1)] public float transferFraction=1;
    }
    public Part[] differentials, wheels;
    public Part[] Options(bool axle)=>axle?differentials:wheels;
    string Key(CarSpec car,bool axle)=>"Apex.Workshop."+car.name+(axle?".Differential":".RearWheels");
    public int Installed(CarSpec car,bool axle){
        var options=Options(axle);
        var id=PlayerPrefs.GetString(Key(car,axle),options[0].id);
        int match=Array.FindIndex(options,p=>p.id==id);
        return Mathf.Max(0,match);
    }
    public Part InstalledPart(CarSpec car,bool axle)=>Options(axle)[Installed(car,axle)];
    public void Install(CarSpec car,bool axle,int index){
        PlayerPrefs.SetString(Key(car,axle),Options(axle)[index].id);
        PlayerPrefs.Save();
    }
}
}
