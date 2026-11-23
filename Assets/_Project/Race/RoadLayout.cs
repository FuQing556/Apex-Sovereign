using UnityEngine;
namespace Apex {
[CreateAssetMenu(menuName="Apex/Road layout")]
public class RoadLayout:ScriptableObject {
    public string title;
    public Vector3[] knots;
    [Min(4)] public int samplesPerSpan=16;
    [Min(5)] public float halfWidth=8;
    [Min(1)] public int laps=2;
    public float countdownSeconds=3;
    public Vector3 Sample(int i,float t){
        int n=knots.Length;
        var a=knots[(i+n-1)%n];var b=knots[i];var c=knots[(i+1)%n];var d=knots[(i+2)%n];
        return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
    }
}
}
