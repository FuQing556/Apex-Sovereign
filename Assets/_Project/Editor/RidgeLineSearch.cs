using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Apex.Editor {
public static class RidgeLineSearch {
    const string Root="Assets/_Project/";
    public static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/RidgeSearch"));
    static readonly float[] apex={116,212,304,374,442,506,588,664,734,826,920,992,1082,1198,1298,1374};
    static readonly int[] side={1,1,1,1,-1,-1,1,1,1,-1,-1,1,1,1,1,-1};
    static readonly CultureInfo ci=CultureInfo.InvariantCulture;
    [Serializable] public class Policy {
        public string name="baseline", modes="0000000000000000";
        public float lat=8, braking=7, top=42, width=3.5f, look=.48f, baseLook=6, lead=19, kick=.54f, hand=.24f, flick=.22f, slip=18;
        public float[] localLat,localLead,localKick,localHand,localFlick;
        public float[] cornerPace,brakeMargin;
        public float safeAngle;
        public int cadence=1;
    }
    static int Mod(int i,int n)=>(i%n+n)%n;
    static float Cross(Vector3 a,Vector3 b)=>a.z*b.x-a.x*b.z;
    sealed class Line {
        public readonly Vector3[] center,line;
        public readonly float[] speed;
        public readonly float step,length;
        int nearest;
        public Line(Mesh mesh,Policy p){
            var verts=mesh.vertices;
            int n=verts.Length/4;
            var ring=new Vector3[n];var dist=new float[n+1];
            for(int i=0;i<n;i++)ring[i]=(verts[i*4+1]+verts[i*4+2])*.5f;
            for(int i=0;i<n;i++)dist[i+1]=dist[i]+Vector3.Distance(ring[i],ring[(i+1)%n]);
            length=dist[n];int count=Mathf.RoundToInt(length/2);step=length/count;
            center=new Vector3[count];line=new Vector3[count];speed=new float[count];
            int seg=0;
            for(int i=0;i<count;i++){
                float s=i*step;while(dist[seg+1]<s)seg++;
                center[i]=line[i]=Vector3.Lerp(ring[seg],ring[(seg+1)%n],Mathf.InverseLerp(dist[seg],dist[seg+1],s));
            }
            var next=new Vector3[count];
            for(int pass=0;pass<75;pass++){
                for(int i=0;i<count;i++){
                    var left=line[Mod(i-2,count)];var right=line[(i+2)%count];
                    var tangent=center[(i+1)%count]-center[Mod(i-1,count)];
                    var normal=Vector3.Cross(Vector3.up,tangent).normalized;
                    next[i]=center[i]+normal*Mathf.Clamp(Vector3.Dot((left+right)*.5f-center[i],normal),-p.width,p.width);
                }
                Array.Copy(next,line,count);
            }
            for(int i=0;i<count;i++){
                var a=line[Mod(i-3,count)];var b=line[i];var c=line[(i+3)%count];
                a.y=b.y=c.y=0;
                float k=2*Mathf.Abs(Cross(b-a,c-b))/Mathf.Max(.01f,(b-a).magnitude*(c-b).magnitude*(c-a).magnitude);
                var turn=Turn(i*step);float lat=p.localLat!=null && p.localLat.Length==16?p.localLat[turn]:p.lat;
                speed[i]=Mathf.Min(p.top,Mathf.Sqrt(lat/Mathf.Max(.0005f,k)));
            }
            for(int j=count*2-1;j>=0;j--){int i=j%count,k=(i+1)%count;
                speed[i]=Mathf.Min(speed[i],Mathf.Sqrt(speed[k]*speed[k]+2*p.braking*Vector3.Distance(line[i],line[k])));
            }
        }
        public int Index(Vector3 pos){
            int best=nearest;float d=(center[best]-pos).sqrMagnitude;
            for(int j=-3;j<=12;j++){int i=Mod(nearest+j,line.Length);float v=(center[i]-pos).sqrMagnitude;if(v<d){best=i;d=v;}}
            return nearest=best;
        }
        public float Offset(int i,Vector3 pos)=>Vector3.Dot(pos-center[i],Vector3.Cross(Vector3.up,center[(i+1)%line.Length]-center[Mod(i-1,line.Length)]).normalized);
        public int Turn(float s){
            int best=0;float d=float.MaxValue;
            for(int i=0;i<apex.Length;i++){float v=Mathf.Abs(s-apex[i]);v=Mathf.Min(v,length-v);if(v<d){d=v;best=i;}}
            return best;
        }
        public float ToApex(int turn,float s)=>Mathf.Repeat(apex[turn]-s+length*.5f,length)-length*.5f;
        public float Station(int i,Vector3 p){
            var tangent=(center[(i+1)%center.Length]-center[Mod(i-1,center.Length)]).normalized;
            return Mathf.Repeat(i*step+Vector3.Dot(p-center[i],tangent),length);
        }
    }
    sealed class Buttons : IDriver {
        public int mask;public RaceCar car;
        public DriveCommand Read(){
            var cmd=DriveCommand.Pedals((mask&1)!=0?1:0,(mask&8)!=0?1:0,car.ForwardSpeed,car.spec.directionThreshold);
            if(car.spec.competition && car.Speed>car.spec.directionThreshold){
                cmd.reverse=car.Reversing;cmd.brake=(mask&8)!=0?1:0;cmd.throttle=(mask&1)!=0?1:0;
            }
            cmd.steer=(mask&4)!=0?1:(mask&2)!=0?-1:0;cmd.digitalSteering=true;cmd.hand=(mask&16)!=0?1:0;
            return cmd;
        }
    }
    class Pilot {
        public int phase,active=-1;
        int last=-1;float age;
        public int Keys(RaceCar car,Policy p,Line track,int i,float ds,float dt,int lap){
            int turn=track.Turn(ds),sign=side[turn];float gap=track.ToApex(turn,ds);
            int slot=lap*16+turn;
            if(last!=turn && active<0 && p.modes[slot%p.modes.Length]!='0' && gap<=Local(p.localLead,p.lead,slot) && gap>0 && car.Speed>8){
                last=active=turn;phase=1;age=0;
            }
            float speed=car.Speed,beta=car.SignedDriftAngle,yaw=Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg;
            int look=Mathf.Clamp(Mathf.RoundToInt((p.baseLook+p.look*speed)/track.step),3,24);
            var point=car.transform.InverseTransformPoint(track.line[(i+look)%track.line.Length]);
            float d=Mathf.Max(1,new Vector2(point.x,point.z).magnitude);
            float k=2*point.x/(d*d);
            float desired=Mathf.Atan(car.spec.wheelbase*k)*Mathf.Rad2Deg*(1+.022f*speed)+.45f*beta+.09f*(k*speed*Mathf.Rad2Deg-yaw);
            float target=track.speed[i]+Local(p.cornerPace,0,slot);
            bool hand=false;
            if(active>=0){
                slot=lap*16+active;
                sign=side[active];bool flick=p.modes[slot%p.modes.Length]=='2';
                float feint=flick?Local(p.localFlick,p.flick,slot):0, kick=Local(p.localKick,p.kick,slot);
                if(age<feint && phase==1)desired=-sign*18;
                else if(phase==1 && age<feint+kick && -sign*beta<p.slip && sign*yaw<90){
                    desired=sign*36;hand=!flick && age<Local(p.localHand,p.hand,slot);
                }else{
                    phase=2;
                    float velocityCurvature=2*Mathf.Sin(Mathf.Atan2(point.x,point.z)-beta*Mathf.Deg2Rad)/d;
                    float remain=track.ToApex(active,ds);
                    float targetSlip=p.slip*Mathf.InverseLerp(-20,-3,remain);
                    float yawTarget=velocityCurvature*speed*Mathf.Rad2Deg+(beta+sign*targetSlip)*2.8f;
                    desired=Mathf.Atan(car.spec.wheelbase*velocityCurvature)*Mathf.Rad2Deg+beta+.23f*(yawTarget-yaw);
                    if(remain < -22 || (age>2.8f && Mathf.Abs(beta)<8)){active=-1;phase=0;}
                }
                age+=dt;
            }
            float actual=(car.wheels[0].steerAngle+car.wheels[1].steerAngle)*.5f;
            int key=desired<-.3f && actual>desired-.15f?2:desired>.3f && actual<desired+.15f?4:0;
            if(speed<target-.25f)key|=1;
            else if(speed>target+1+(Mathf.Abs(gap)<=32?Local(p.brakeMargin,0,lap*16+turn):0))key|=8;
            if(hand)key|=16;
            return key;
        }
    }
    static float Local(float[] arr,float fallback,int n)=>arr!=null && arr.Length>0?arr[n%arr.Length]:fallback;
    static Scene LocalScene()=>SceneManager.GetSceneByName("Ridge");
    public static string Load(){
        if(!Application.isPlaying)throw new InvalidOperationException("Start Bootstrap Play Mode first.");
        if(!LocalScene().isLoaded)SceneManager.LoadSceneAsync("Ridge",new LoadSceneParameters(LoadSceneMode.Additive,LocalPhysicsMode.Physics3D));
        return "Loading Ridge in a local physics scene";
    }
    public static Action<RaceCar,int,int,int,float> OnFrame;
    public static string Run(string json,bool trace=false,int[] replay=null,int stopSlot=-1){
        var p=JsonUtility.FromJson<Policy>(json);
        var scene=LocalScene();if(!scene.isLoaded || scene.GetPhysicsScene()==Physics.defaultPhysicsScene)throw new InvalidOperationException("Load the isolated Ridge first.");
        var course=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Circuit>()).Single();
        var track=new Line(course.transform.Find("Road and kerbs").GetComponent<MeshFilter>().sharedMesh,p);
        var prefab=AssetDatabase.LoadAssetAtPath<RaceCar>(Root+"Vehicles/SHIBATA31/OkuibukiCar.prefab");
        var car=UnityEngine.Object.Instantiate(prefab,course.grid.position,course.grid.rotation);
        SceneManager.MoveGameObjectToScene(car.gameObject,scene);
        car.Chassis.interpolation=RigidbodyInterpolation.None;
        var collision=car.gameObject.AddComponent<RidgeSearchContact>();
        var physics=scene.GetPhysicsScene();float dt=Time.fixedDeltaTime;
        var buttons=new Buttons{car=car};car.Driver=buttons;
        var original=Keyboard.current;
        Keyboard keyboard=null;
        LocalDriver local=null;
        if(replay!=null){
            keyboard=InputSystem.AddDevice<Keyboard>("RidgeReplay");
            // Editor-driven runs must read the play-mode device buffer.
            InputSystem.Update();
            local=new LocalDriver(car);car.Driver=local;
        }
        var advance=(Action)Delegate.CreateDelegate(typeof(Action),car,"FixedUpdate");
        var pilot=new Pilot();int gates=0,next=1,lap=0,grass=0,tick=0,counter=0,idx=0;
        float first=0,maxOffset=0,peak=0;
        var duration=new int[32];var best=new int[32];var peaks=new float[32];var sectors=new float[32];var entries=new float[32];
        var data=trace?new StringBuilder("tick,t,lap,station_m,turn,phase,keys,x,y,z,qx,qy,qz,qw,kph,beta,yaw,FL,FR,offset,grass,wall,gate,FLrpm,FRrpm,RLrpm,RRrpm,hand,gear\n"):null;
        Directory.CreateDirectory(Folder);
        string status="timeout";
        try {
            for(int z=0;z<Mathf.RoundToInt(course.startDelay/dt);z++){advance();physics.Simulate(dt);}
            car.EnabledDrive=true;
            OnFrame?.Invoke(car,-1,0,0,0);
            for(tick=0;tick<9000;tick++){
                var pos=car.Chassis.position;idx=track.Index(pos);float ds=idx*track.step;
                int turn=track.Turn(ds),slot=lap*16+turn;
                if(replay!=null && tick>=replay.Length){status="end-input";break;}
                if(replay!=null){
                    buttons.mask=replay[tick];var input=new KeyboardState();
                    input.Set(Key.W,(buttons.mask&1)!=0);input.Set(Key.A,(buttons.mask&2)!=0);
                    input.Set(Key.D,(buttons.mask&4)!=0);input.Set(Key.S,(buttons.mask&8)!=0);
                    input.Set(Key.Space,(buttons.mask&16)!=0);
                    InputState.Change(keyboard,input);keyboard.MakeCurrent();local.Capture();
                }else if(tick%p.cadence==0)buttons.mask=pilot.Keys(car,p,track,idx,ds,dt*p.cadence,lap);
                advance();physics.Simulate(dt);
                float off=track.Offset(idx,car.Chassis.position);maxOffset=Mathf.Max(maxOffset,Mathf.Abs(off));
                bool onGrass=false,ground=true;for(int j=0;j<4;j++){var cover=car.WheelCover(j);onGrass|=cover==RoadCover.Grass;ground&=cover!=RoadCover.Air;}
                if(onGrass)grass++;
                float beta=car.SignedDriftAngle,yaw=Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg;
                peak=Mathf.Max(peak,Mathf.Abs(beta));
                float gap=track.ToApex(turn,ds);
                if(Mathf.Abs(gap)<=26){
                    if(entries[slot]==0)entries[slot]=car.Speed*3.6f;
                    float outward=-side[turn]*beta;peaks[slot]=Mathf.Max(peaks[slot],outward);
                    bool drifting=ground && !onGrass && outward>=12 && outward<=45 && side[turn]*yaw>5 && car.ForwardSpeed>8;
                    duration[slot]=drifting?duration[slot]+1:0;best[slot]=Math.Max(best[slot],duration[slot]);
                }else duration[slot]=0;
                sectors[slot]+=dt;
                OnFrame?.Invoke(car,tick,buttons.mask,slot,track.Station(idx,car.Chassis.position));
                if(trace){
                    var q=car.Chassis.rotation;var z=car.Chassis.position;
                    data.AppendLine(FormattableString.Invariant($"{tick},{(tick+1)*dt:F3},{lap+1},{ds:F2},{turn+1},{pilot.phase},{buttons.mask},{z.x:F4},{z.y:F4},{z.z:F4},{q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6},{car.Speed*3.6f:F3},{beta:F3},{yaw:F3},{car.wheels[0].steerAngle:F3},{car.wheels[1].steerAngle:F3},{off:F3},{(onGrass?1:0)},{collision.contacts},{gates},{car.wheels[0].rpm:F2},{car.wheels[1].rpm:F2},{car.wheels[2].rpm:F2},{car.wheels[3].rpm:F2},{car.HandbrakeApplied:F3},{car.Gear+1}"));
                }
                if(course.Crossed(next,pos,car.Chassis.position)){
                    gates++;
                    if(next==0){
                        if(lap==0)first=(tick+1)*dt;
                        lap++;if(lap==2){status="finish";break;}
                    }
                    next=(next+1)%course.gates.Length;
                }
                counter=car.Speed<1?counter+1:0;
                if(collision.contacts>0 || onGrass || car.Chassis.position.y<course.resetHeight || counter>130 || Mathf.Abs(off)>13){
                    status=collision.contacts>0?"contact":onGrass?"grass":counter>130?"stalled":"off";break;
                }
                if(p.safeAngle>0 && Mathf.Abs(beta)>p.safeAngle){status="spin";break;}
                if(stopSlot>=0 && lap==stopSlot/16 && ds>=apex[stopSlot%16]+28 && slot>=stopSlot){status="sector";break;}
            }
            string Pack(IEnumerable<float> x)=>string.Join("/",x.Select(v=>v.ToString("0.00",ci)));
            string summary=string.Join(",",new[]{p.name,status,((tick+1)*dt).ToString("F3",ci),first.ToString("F3",ci),gates.ToString(),(idx*track.step).ToString("F1",ci),maxOffset.ToString("F2",ci),peak.ToString("F1",ci),grass.ToString(),collision.contacts.ToString(),
                Pack(best.Select(v=>v*dt)),Pack(peaks),Pack(sectors),Pack(entries)});
            string file=Path.Combine(Folder,"trials.csv");
            if(!File.Exists(file))File.WriteAllText(file,"id,result,time,lap1,gates,station,max_offset,peak_beta,grass_ticks,contacts,drift_seconds,peak_outward,sector_seconds,entry_kph\n");
            File.AppendAllText(file,summary+"\n");File.AppendAllText(Path.Combine(Folder,"policies.jsonl"),JsonUtility.ToJson(p)+"\n");
            if(trace)File.WriteAllText(Path.Combine(Folder,p.name+".csv"),data.ToString());
            return summary;
        } finally {
            if(keyboard!=null){InputSystem.RemoveDevice(keyboard);if(original!=null && original.added)original.MakeCurrent();}
            UnityEngine.Object.DestroyImmediate(car.gameObject);
        }
    }
    public static string Validate(string id){
        var policy=File.ReadAllLines(Path.Combine(Folder,"policies.jsonl")).Select(JsonUtility.FromJson<Policy>).Last(x=>x.name==id);
        var keys=File.ReadLines(Path.Combine(Folder,id+".csv")).Skip(1).Select(x=>int.Parse(x.Split(',')[6])).ToArray();
        policy.name=id+"-keys";
        return Run(JsonUtility.ToJson(policy),true,keys);
    }
    public static Policy Saved(string id)=>File.ReadLines(Path.Combine(Folder,"policies.jsonl"))
        .Where(line=>line.Contains("\"name\":\""+id+"\"")).Select(JsonUtility.FromJson<Policy>).Last(p=>p.name==id);
    public static void ExportGeometry(){
        var circuit=LocalScene().GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Circuit>()).Single();
        var mesh=circuit.transform.Find("Road and kerbs").GetComponent<MeshFilter>().sharedMesh;
        var vertices=mesh.vertices;
        var road=new StringBuilder("x_left,y_left,z_left,x_right,y_right,z_right\n");
        for(int i=0;i<vertices.Length;i+=4){
            var a=vertices[i+1];var b=vertices[i+2];
            road.AppendLine(FormattableString.Invariant($"{a.x:R},{a.y:R},{a.z:R},{b.x:R},{b.y:R},{b.z:R}"));
        }
        File.WriteAllText(Path.Combine(Folder,"road.csv"),road.ToString());
        var line=new Line(mesh,Saved("best-time"));
        var data=new StringBuilder("s,x,y,z,line_x,line_y,line_z,curvature_per_m,plan_kph\n");
        for(int i=0;i<line.line.Length;i++){
            var c=line.center[i];var p=line.line[i];var a=line.line[Mod(i-3,line.line.Length)];var b=line.line[(i+3)%line.line.Length];
            var u=new Vector2(p.x-a.x,p.z-a.z);var v=new Vector2(b.x-p.x,b.z-p.z);
            float k=2*(u.y*v.x-u.x*v.y)/Mathf.Max(.001f,u.magnitude*v.magnitude*(u+v).magnitude);
            data.AppendLine(FormattableString.Invariant($"{i*line.step:R},{c.x:R},{c.y:R},{c.z:R},{p.x:R},{p.y:R},{p.z:R},{k:R},{line.speed[i]*3.6f:R}"));
        }
        File.WriteAllText(Path.Combine(Folder,"line.csv"),data.ToString());
    }
    // Sequential search keeps the already driven prefix, including the actual tyre temperatures.
    // Independent entries allow a warm second lap to use a different timing than the standing lap.
    public static string Stage(int slot,int start,int tries,string branch="show"){
        var path=Path.Combine(Folder,branch+"-"+(slot-1)+".json");
        var p=slot>0?JsonUtility.FromJson<Policy>(File.ReadAllText(path)):new Policy{
            lat=10,width=3,braking=8,top=46,slip=22,safeAngle=50,modes=new string('0',32),
            localLead=Enumerable.Repeat(23f,32).ToArray(),localKick=Enumerable.Repeat(.76f,32).ToArray(),
            localHand=Enumerable.Repeat(.34f,32).ToArray(),localFlick=Enumerable.Repeat(.32f,32).ToArray()
        };
        var output=Path.Combine(Folder,branch+"-"+slot+".json");
        string bestJson=null;float bestTime=float.PositiveInfinity,bestDuration=0;int passed=0;
        for(int j=start;j<start+tries;j++){
            var rnd=new System.Random(1723+slot*151+j*1217);
            float Range(float lo,float hi)=>lo+(hi-lo)*(float)rnd.NextDouble();
            var m=p.modes.ToCharArray();m[slot]=j==0?'1':j%4==1?'2':'1';p.modes=new string(m);
            p.localLead[slot]=j==0?23:Range(12,36);
            p.localKick[slot]=j==0?.76f:Range(.55f,1.45f);
            p.localHand[slot]=j==0?.34f:Range(.22f,.62f);
            p.localFlick[slot]=j==0?.32f:Range(.18f,.48f);
            p.name=branch+"-s"+slot+"-"+j;
            string json=JsonUtility.ToJson(p);var result=Run(json,stopSlot:slot<30?slot+1:-1).Split(',');
            if(result[1]!="sector" && result[1]!="finish")continue;
            var drift=result[10].Split('/');float dur=float.Parse(drift[slot],ci);
            bestDuration=Mathf.Max(dur,bestDuration);
            if(dur<.40f || drift.Take(slot).Any(x=>float.Parse(x,ci)<.40f))continue;
            passed++;
            float time=float.Parse(result[2],ci);
            if(time<bestTime){bestTime=time;bestJson=json;}
            if(passed>=3)break;
        }
        if(bestJson!=null)File.WriteAllText(output,bestJson);
        return $"{slot} passed={passed} bestTime={bestTime} maxDuration={bestDuration}";
    }
    public static void ScheduleStages(string branch,int first){
        int slot=first,index=0;
        string log=Path.Combine(Folder,branch+"-progress.txt");
        void Batch(){
            try{
                string result=Stage(slot,index,8,branch);
                File.AppendAllText(log,result+" seed="+index+"\n");
                if(result.Contains("passed=0 ")){
                    index+=8;if(index>=96)return;
                }else{slot++;index=0;}
                if(slot<32)EditorApplication.delayCall+=Batch;
            }catch(Exception e){File.AppendAllText(log,e.ToString());}
        }
        EditorApplication.delayCall+=Batch;
    }
}
public class RidgeSearchContact:MonoBehaviour {
    public int contacts;
    void OnCollisionEnter(Collision c){if(c.collider.name.EndsWith("Barrier",StringComparison.Ordinal))contacts++;}
}
}

