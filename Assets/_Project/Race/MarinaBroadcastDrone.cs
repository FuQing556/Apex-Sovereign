using UnityEngine;

namespace Apex {
public sealed class MarinaBroadcastDrone : MonoBehaviour {
    public Vector3 sweep;
    public float across=7, period=40, phase;
    Vector3 centre,side;
    void Start(){
        centre=transform.localPosition;
        side=Vector3.Cross(Vector3.up,sweep).normalized*across;
    }
    void Update(){
        float t=phase+Time.time*(2*Mathf.PI/period);
        float sin=Mathf.Sin(t),cos=Mathf.Cos(t);
        transform.localPosition=centre+sweep*cos+side*sin+Vector3.up*(.65f*Mathf.Sin(2*t));
        var tangent=-sweep*sin+side*cos;
        transform.localRotation=Quaternion.LookRotation(tangent,Vector3.up)*Quaternion.Euler(3,0,7*cos);
    }
}
}
