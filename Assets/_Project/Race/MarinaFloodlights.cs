using UnityEngine;

namespace Apex {
// The fixtures and their light on the tarmac are baked. Only the lamps near
// the chase camera need additional lights for the moving car and tyre smoke.
public sealed class MarinaFloodlights:MonoBehaviour {
    public Vector3[] fixtures;
    readonly Light[] nearby=new Light[4];
    int previous=-1;
    float next;
    void Start(){
        if(fixtures==null || fixtures.Length<4){enabled=false;return;}
        for(int i=0;i<nearby.Length;i++){
            var lamp=new GameObject("Track floodlight");lamp.transform.SetParent(transform,false);
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.86f,.92f,1);
            light.range=34;light.intensity=32;light.shadows=LightShadows.None;nearby[i]=light;
        }
    }
    void LateUpdate(){
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+.10f;
        var camera=Camera.main;if(!camera)return;
        float best=float.MaxValue;int index=0;
        for(int i=0;i<fixtures.Length;i++){
            float sq=(camera.transform.position-fixtures[i]).sqrMagnitude;
            if(sq<best){best=sq;index=i;}
        }
        if(index==previous)return;previous=index;
        for(int j=0;j<nearby.Length;j++)
            nearby[j].transform.position=fixtures[(index+j-1+fixtures.Length)%fixtures.Length];
    }
}
}
