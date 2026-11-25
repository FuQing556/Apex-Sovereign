using UnityEngine;

namespace Apex {
[System.Serializable]
public sealed class GridRaceSettings {
    public float slotSpacing=4, finishWindow=30;
    public float firstParkingStation=65, parkingInterval=9, parkingOffset=8;
    [Header("碰撞镜头 · 冲量/质量（米每秒）、最大位移（米）、衰减（秒）")]
    public Vector2 cameraImpactRange=new Vector2(.65f,5);
    public float cameraImpactTravel=.23f,cameraImpactDecay=.16f;
    [Header("走线 · 米、秒、米每二次方秒")]
    public float lineWidth=3.5f, lateralAcceleration=9.3f, brakingAcceleration=7.4f, topSpeed=42;
    public float lookBase=6.5f, lookSeconds=.50f, trafficClearance=3.1f;
    [Header("漂移 · 提前量、目标角、单次最大手刹时间")]
    public float driftLead=26, driftAngle=23, handSeconds=.62f, driftMinSpeed=15, driftCurvature=.022f;
    [Header("追击与脱困")]
    public float attackDistance=34, reverseSeconds=1.15f, stuckSeconds=1.15f;
    public GridLivery[] liveries=GridLivery.Collection();
}

// One measured centreline and speed envelope shared by the entire field.
public sealed class GridRoute {
    readonly Vector3[] centre,line;
    readonly float[] curvature,speed,gateStation;
    public readonly float step,length;
    public readonly float driftCurvature;
    public GridRoute(Circuit course,RoadLayout layout,GridRaceSettings tune){
        int n=layout?layout.knots.Length*layout.samplesPerSpan:course.mapCenterline.Length;
        var raw=new Vector3[n];var arc=new float[n+1];
        for(int i=0;i<n;i++){
            var point=layout?layout.Sample(i/layout.samplesPerSpan,(i%layout.samplesPerSpan)/(float)layout.samplesPerSpan):course.mapCenterline[i];
            raw[i]=course.transform.TransformPoint(point);
        }
        for(int i=0;i<n;i++)arc[i+1]=arc[i]+Vector3.Distance(raw[i],raw[(i+1)%n]);
        length=arc[n];int count=Mathf.RoundToInt(length/2);step=length/count;
        centre=new Vector3[count];line=new Vector3[count];speed=new float[count];curvature=new float[count];
        gateStation=new float[course.gates.Length];
        // Both track builders place timing gates on centreline samples. Their spacing varies.
        for(int g=0;g<gateStation.Length;g++){
            var point=course.gates[g].position;int nearest=0;
            for(int i=1;i<n;i++)
                if((raw[i]-point).sqrMagnitude<(raw[nearest]-point).sqrMagnitude)nearest=i;
            gateStation[g]=arc[nearest];
        }
        int segment=0;
        for(int i=0;i<count;i++){
            float s=i*step;while(segment<n-1 && arc[segment+1]<s)segment++;
            centre[i]=line[i]=Vector3.Lerp(raw[segment],raw[(segment+1)%n],Mathf.InverseLerp(arc[segment],arc[segment+1],s));
        }
        var scratch=new Vector3[count];
        for(int pass=0;pass<75;pass++){
            for(int i=0;i<count;i++){
                var side=Right(i);var mid=(line[Wrap(i-2)]+line[Wrap(i+2)])*.5f;
                scratch[i]=centre[i]+side*Mathf.Clamp(Vector3.Dot(mid-centre[i],side),-tune.lineWidth,tune.lineWidth);
            }
            System.Array.Copy(scratch,line,count);
        }
        float peakBend=0;
        for(int i=0;i<count;i++){
            var a=line[i]-line[Wrap(i-3)];var b=line[Wrap(i+3)]-line[i];a.y=b.y=0;
            curvature[i]=2*Vector3.Cross(a,b).y/Mathf.Max(.001f,a.magnitude*b.magnitude*(a+b).magnitude);
            peakBend=Mathf.Max(peakBend,Mathf.Abs(curvature[i]));
            speed[i]=Mathf.Min(tune.topSpeed,Mathf.Sqrt(tune.lateralAcceleration/Mathf.Max(.0005f,Mathf.Abs(curvature[i]))));
        }
        // Broad street-circuit turns need a lower entry threshold than mountain hairpins.
        driftCurvature=Mathf.Max(.001f,Mathf.Min(tune.driftCurvature,peakBend*.7f));
        for(int j=count*2-1;j>=0;j--){
            int i=j%count,k=Wrap(i+1);
            speed[i]=Mathf.Min(speed[i],Mathf.Sqrt(speed[k]*speed[k]+2*tune.brakingAcceleration*Vector3.Distance(line[i],line[k])));
        }
    }
    int Wrap(int i)=>(i%line.Length+line.Length)%line.Length;
    int At(float station)=>Wrap(Mathf.RoundToInt(station/step));
    Vector3 Right(int i)=>Vector3.Cross(Vector3.up,centre[Wrap(i+1)]-centre[Wrap(i-1)]).normalized;
    public Vector3 Target(float s,float offset)=>line[At(s)]+Right(At(s))*offset;
    public float Offset(Vector3 point,float s){int i=At(s);return Vector3.Dot(point-line[i],Right(i));}
    public float Bend(float s)=>curvature[At(s)];
    public float Pace(float s)=>speed[At(s)];
    public float Station(Vector3 pos,float seed){
        int start=At(seed),best=start;float min=float.PositiveInfinity;
        for(int j=-15;j<=15;j++){int i=Wrap(start+j);float d=(pos-centre[i]).sqrMagnitude;if(d<min){best=i;min=d;}}
        var tangent=(centre[Wrap(best+1)]-centre[Wrap(best-1)]).normalized;
        return Mathf.Repeat(best*step+Mathf.Clamp(Vector3.Dot(pos-centre[best],tangent),-step,step),length);
    }
    public float Gate(int i)=>gateStation[i];
    public float Delta(float to,float from)=>Mathf.Repeat(to-from+length*.5f,length)-length*.5f;
    public float Fraction(int next,float s){
        float last=gateStation[(next+gateStation.Length-1)%gateStation.Length];
        return Mathf.Clamp01(Delta(s,last)/Mathf.Repeat(gateStation[next]-last,length));
    }
}
}
