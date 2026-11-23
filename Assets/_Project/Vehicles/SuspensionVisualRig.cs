using UnityEngine;

namespace Apex {
    // The physics back-end supplies the hub pose. Chassis links follow it for rendering.
    [DefaultExecutionOrder(400)]
    public sealed class SuspensionVisualRig : MonoBehaviour {
        public enum Attachment { Fixed, Stretch, Wheel }
        [System.Serializable]
        public struct Joint {
            public Transform pivot, carrier;
            public Vector3 chassisPoint, wheelOffset, wheelRest;
            public Quaternion bindRotation;
            public float bindLength;
            public bool followsSteer;
            public Attachment attachment;
        }
        public Joint[] joints;
        public Transform steeringWheel,frontLeft,frontRight;
        [Min(0)] public float steeringWheelRatio=7;
        public Renderer rearLightGuides;
        [ColorUsage(false,true)] public Color brakeEmission=new Color(3.4f,.025f,.012f);
        RaceCar car;
        Quaternion steeringRest;
        MaterialPropertyBlock brakeProperties;
        Color runningEmission;
        void Awake(){
            if(steeringWheel)steeringRest=Quaternion.Inverse(transform.rotation)*steeringWheel.rotation;
            if(TryGetComponent(out car) && rearLightGuides){
                brakeProperties=new MaterialPropertyBlock();
                runningEmission=rearLightGuides.sharedMaterial.GetColor("_EmissionColor");
            }
        }
        void LateUpdate(){
            if(steeringWheel){
                float Angle(Transform upright)=>Mathf.DeltaAngle(0,(Quaternion.Inverse(transform.rotation)*upright.rotation).eulerAngles.y);
                float wheelAngle=(Angle(frontLeft)+Angle(frontRight))*.5f;
                steeringWheel.rotation=transform.rotation*Quaternion.AngleAxis(-wheelAngle*steeringWheelRatio,
                    new Vector3(0,-.34f,1).normalized)*steeringRest;
            }
            if(brakeProperties!=null && car.CompetitionChassis!=null){
                float pressure=Mathf.Clamp01(car.CompetitionChassis.FrontBar);
                brakeProperties.SetColor("_EmissionColor",Color.Lerp(runningEmission,brakeEmission,pressure));
                rearLightGuides.SetPropertyBlock(brakeProperties);
            }
            foreach(var j in joints){
                var center=transform.InverseTransformPoint(j.carrier.position);
                var orientation=Quaternion.Inverse(transform.rotation)*j.carrier.rotation;
                var end=center+(j.followsSteer?orientation*j.wheelOffset:j.wheelOffset);
                var delta=end-j.chassisPoint;
                float length=delta.magnitude;
                if(length<.0001f)continue;
                var turn=Quaternion.FromToRotation(j.wheelRest-j.chassisPoint,delta);
                j.pivot.localRotation=turn*j.bindRotation;
                j.pivot.localPosition=j.attachment==Attachment.Wheel?
                    end-delta*(j.bindLength/length):j.chassisPoint;
                if(j.attachment==Attachment.Stretch)
                    j.pivot.localScale=new Vector3(1,1,length/j.bindLength);
            }
        }
    }
}
