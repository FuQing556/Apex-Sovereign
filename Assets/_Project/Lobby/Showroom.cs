using UnityEngine;
namespace Apex {
public class Showroom:MonoBehaviour {
    public Transform display,bench;
    public Vector3 overviewEye=new(3.25f,1.45f,3.65f),overviewTarget=new(0,.56f,0);
    public Vector3 wheelEye=new(2.85f,.94f,-2.2f),wheelTarget=new(.67f,.42f,-1.06f);
    public Vector3 benchEyeOffset=new(.72f,.42f,.85f);
    GameObject sample;
    Camera view;
    Vector3 eye,target;
    RectTransform panel;
    public void Present(bool workshop,bool axle,WorkshopCatalog.Part part,RectTransform dock){
        if(sample){sample.SetActive(false);Destroy(sample);}
        panel=dock;view=Camera.main;
        eye=overviewEye;target=overviewTarget;
        if(workshop && axle && bench){
            sample=Instantiate(part.displayModel,bench,false);
            target=bench.position+Vector3.up*.14f;eye=target+benchEyeOffset;
        }else if(workshop){eye=wheelEye;target=wheelTarget;}
        Frame();
    }
    void LateUpdate()=>Frame();
    void Frame(){
        if(!view || !panel)return;
        var rt=panel.rect;
        var edge=RectTransformUtility.WorldToScreenPoint(null,panel.TransformPoint(new Vector3(rt.xMax,rt.yMax)));
        bool narrow=Screen.width<Screen.height;
        float x=narrow?.5f:Mathf.Lerp(Mathf.Clamp01(edge.x/Screen.width),1,.49f);
        float y=narrow?Mathf.Lerp(Mathf.Clamp01(edge.y/Screen.height),1,.48f):.49f;
        float tangent=Mathf.Tan(.5f*view.fieldOfView*Mathf.Deg2Rad);
        var ray=new Vector3((2*x-1)*view.aspect*tangent,(2*y-1)*tangent,1);
        var position=target+(eye-target)*(narrow?1.45f:1);
        view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position)*Quaternion.FromToRotation(ray,Vector3.forward));
    }
}
}
