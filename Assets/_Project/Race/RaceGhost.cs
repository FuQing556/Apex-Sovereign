using UnityEngine;
using UnityEngine.Rendering;

namespace Apex {
public sealed class RaceGhost : MonoBehaviour {
    public GhostRecording Recording {get;private set;}
    public GhostRecording.Frame Current {get;private set;}
    public float Clock {get;private set;}
    public bool Finished=>Clock>=Recording.Duration;
    readonly Transform[] wheels=new Transform[4],brakes=new Transform[4];
    Material skin;
    static readonly int Opacity=Shader.PropertyToID("_Opacity"),Shade=Shader.PropertyToID("_Shade");
    public static RaceGhost Spawn(GhostRecording recording,Transform parent){
        var root=new GameObject("幽灵 · "+recording.title);
        root.transform.SetParent(parent,false);
        var g=root.AddComponent<RaceGhost>();g.Recording=recording;
        var body=Instantiate(recording.vehicle.body,root.transform,false);
        string[] axles={"FL","FR","RL","RR"};
        foreach(var t in body.GetComponentsInChildren<Transform>(true)){
            for(int i=0;i<axles.Length;i++){
                if(t.name==axles[i]+" wheel pose")g.wheels[i]=t;
                if(t.name==axles[i]+" caliper pose")g.brakes[i]=t;
            }
        }
        g.skin=new Material(recording.material);g.skin.SetColor("_Tint",recording.tint);
        var props=new MaterialPropertyBlock();
        foreach(var r in body.GetComponentsInChildren<MeshRenderer>(true)){
            var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++){
                var m=materials[i];
                var c=m && m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):
                    m && m.HasProperty("_Color")?m.GetColor("_Color"):Color.gray;
                props.Clear();props.SetFloat(Shade,Mathf.Lerp(.28f,1,Mathf.Sqrt(c.grayscale)));
                r.SetPropertyBlock(props,i);materials[i]=g.skin;
            }
            r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        g.Sample(0,null);return g;
    }
    public void Sample(float seconds,Transform player){
        var data=Recording.frames;Clock=Mathf.Clamp(seconds,0,Recording.Duration);
        float f=Clock/Recording.sampleInterval;
        float nearest=Mathf.Round(f);if(Mathf.Abs(f-nearest)<.001f)f=nearest;
        int i=Finished?data.Length-1:Mathf.Clamp(Mathf.FloorToInt(f),0,data.Length-1);
        var a=data[i];var b=data[Mathf.Min(i+1,data.Length-1)];float t=f-i;
        if((b.keys&GhostArchive.Reset)!=0){b=a;t=0;}
        transform.SetPositionAndRotation(Vector3.Lerp(a.chassis.position,b.chassis.position,t),Quaternion.Slerp(a.chassis.rotation,b.chassis.rotation,t));
        for(int w=0;w<4;w++){
            var p=a.Wheel(w);var q=b.Wheel(w);
            Vector3 centre=transform.TransformPoint(Vector3.Lerp(p.position,q.position,t));
            if(wheels[w])wheels[w].SetPositionAndRotation(centre,transform.rotation*Quaternion.Slerp(p.rotation,q.rotation,t));
            float steer=w<2?Mathf.Lerp(a.steer[w],b.steer[w],t):0;
            if(brakes[w])brakes[w].SetPositionAndRotation(centre,transform.rotation*Quaternion.Euler(0,steer,0));
        }
        Current=a;
        float distance=player?Vector3.Distance(player.position,transform.position):10;
        skin.SetFloat(Opacity,Mathf.Lerp(.06f,.70f,Mathf.InverseLerp(1.5f,7,distance)));
    }
    void OnDestroy(){if(skin)Destroy(skin);}
}
}
