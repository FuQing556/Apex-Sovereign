using UnityEngine;
namespace Apex {
public class Circuit : MonoBehaviour {
    public Transform grid;
    public Vector3[] mapCenterline;
    public MeshFilter padMarkings;
    public BoxCollider padBoundary;
    public Transform[] gates;
    [Tooltip("各计数门局部 X 轴上左右护栏内侧的位置，由赛道构建工具烘焙。")]
    public Vector2[] gateLateralLimits;
    public int laps=1;
    public float gateHalfWidth=7, gateHalfHeight=3, startDelay=3, resetHeight=-4;
    public bool Crossed(int gate,Vector3 a,Vector3 b){
        var t=gates[gate];a=t.InverseTransformPoint(a);b=t.InverseTransformPoint(b);
        if(a.z>=0 || b.z<0)return false;
        var hit=Vector3.Lerp(a,b,-a.z/(b.z-a.z));
        var limits=gateLateralLimits!=null && gateLateralLimits.Length==gates.Length ?
            gateLateralLimits[gate] : new Vector2(-gateHalfWidth,gateHalfWidth);
        return hit.x>=limits.x && hit.x<=limits.y && Mathf.Abs(hit.y)<=gateHalfHeight;
    }
}
}
