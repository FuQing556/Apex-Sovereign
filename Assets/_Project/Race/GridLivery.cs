using UnityEngine;

namespace Apex {
[System.Serializable]
public class GridLivery {
    public string title;
    public Color paint, accent;
    public GridLivery(string title,Color paint,Color accent){
        this.title=title;this.paint=paint;this.accent=accent;
    }
    public static GridLivery[] Collection()=>new[]{
        new GridLivery("原厂绯红",new Color32(188,26,43,255),Color.white),
        new GridLivery("霜白靛蓝",new Color32(237,232,216,255),new Color32(30,59,115,255)),
        new GridLivery("石墨熔橙",new Color32(54,59,66,255),new Color32(250,103,30,255)),
        new GridLivery("孔雀青金",new Color32(13,143,134,255),new Color32(248,207,87,255))
    };
    static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
    public GridLivery ForCar(CarSpec spec,bool factory){
        if(!factory)return this;
        return new GridLivery(string.IsNullOrEmpty(spec.factoryLiveryTitle)?title:spec.factoryLiveryTitle,spec.FactoryColor,accent);
    }
    static void Tint(Renderer renderer,Color color){
        var block=new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);block.SetColor(BaseColor,color);renderer.SetPropertyBlock(block);
    }
    public void Apply(GameObject root,bool factory){
        if(factory)return;
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
            var group=renderer.transform.parent;
            if(!group || !renderer.sharedMaterial)continue;
            if(renderer.name=="Body"){
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++){
                    if(!materials[i])continue;
                    var name=materials[i].name;
                    if(name!="Cyan enamel" && name!="Hood blue ink")continue;
                    var block=new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block,i);block.SetColor(BaseColor,name=="Cyan enamel"?paint:accent);renderer.SetPropertyBlock(block,i);
                }
            }
            string surface=renderer.sharedMaterial.name;
            if(!(group.name=="01_Pressed_panels" || group.name=="02_Bonnet_lid" || group.name=="03_Deck_lid" ||
                 group.name=="04_Seals_and_hardware" || group.name=="07_Aero" || group.name=="08_Livery"))continue;
            if(surface=="Candy scarlet")Tint(renderer,paint);
            if(surface=="Oxblood pearl")Tint(renderer,accent);
        }
    }
}
}
