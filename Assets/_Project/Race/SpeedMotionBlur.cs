using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apex {
public sealed class SpeedMotionBlur : MonoBehaviour {
    Volume volume;
    public void Bind(Camera camera,VolumeProfile profile){
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        volume=gameObject.AddComponent<Volume>();
        volume.sharedProfile=profile;volume.isGlobal=true;volume.priority=100;volume.weight=0;
    }
    public void SetStrength(float value)=>volume.weight=Mathf.Clamp01(value);
}
}
