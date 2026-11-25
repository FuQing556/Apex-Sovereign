using System;
using UnityEditor;
using UnityEngine;

namespace Apex.Editor {
// Run through the garage with the installed car and normal lap timing.
// The pilot supplies analogue inputs. It cannot move or reset the chassis.
public static class MarinaBayCheck {
    sealed class Pilot:IDriver {
        readonly RaceCar car;
        readonly Vector3[] path;
        int closest;
        public int samples,allGround;
        public float peakSpeed;
        public Pilot(RaceCar car,Vector3[] path){this.car=car;this.path=path;}
        Vector3 At(int i)=>path[(i+path.Length)%path.Length];
        public DriveCommand Read(){
            var pos=car.transform.position;float best=(At(closest)-pos).sqrMagnitude;int next=closest;
            for(int i=1;i<=8;i++){
                float sq=(At(closest+i)-pos).sqrMagnitude;
                if(sq<best){next=(closest+i)%path.Length;best=sq;}
            }
            closest=next;
            float speed=car.Speed,desired=29;
            for(int i=0;i<=26;i+=2){
                int j=closest+i;var a=At(j)-At(j-2);var b=At(j+2)-At(j);
                float curvature=2*Vector3.Cross(a,b).magnitude/Mathf.Max(.001f,a.magnitude*b.magnitude*(a+b).magnitude);
                float corner=Mathf.Sqrt(4.2f/Mathf.Max(.001f,curvature));
                desired=Mathf.Min(desired,Mathf.Sqrt(corner*corner+2*3.1f*i*MarinaStreets.LapMetres/path.Length));
            }
            float ahead=5+speed*.38f;
            float target=closest+ahead*path.Length/MarinaStreets.LapMetres;int k=Mathf.FloorToInt(target);
            var aim=car.transform.InverseTransformPoint(Vector3.Lerp(At(k),At(k+1),target-k));
            float angle=Mathf.Atan2(2*car.spec.wheelbase*aim.x,Mathf.Max(.1f,aim.x*aim.x+aim.z*aim.z))*Mathf.Rad2Deg;
            float steer=(angle*(1+.018f*speed)+.36f*car.SignedDriftAngle)/Mathf.Max(1,car.spec.steeringBySpeed.Evaluate(speed));
            float error=desired-speed;samples++;if(car.GroundedCount==4)allGround++;peakSpeed=Mathf.Max(peakSpeed,speed);
            return new DriveCommand{steer=Mathf.Clamp(steer,-1,1),throttle=Mathf.Clamp01(.12f+error*.21f),brake=Mathf.Clamp01(-error*.23f)};
        }
    }
    public static string Result{get;private set;}
    static Pilot pilot;
    static RaceSession session;
    static float start,oldScale;
    public static void StartLap(){
        session=UnityEngine.Object.FindAnyObjectByType<RaceSession>();
        if(!EditorApplication.isPlaying || !session || session.State!=RaceSession.Phase.Lobby)
            throw new InvalidOperationException("Start from the garage in Play Mode.");
        for(int n=0;n<session.tracks.Length && !session.panel.location.text.Contains("滨海湾");n++)session.NextTrack();
        if(!session.panel.location.text.Contains("滨海湾"))throw new InvalidOperationException("Missing garage entry");
        if(session.RacingGrid)session.ToggleGrid();
        Result="Loading";pilot=null;oldScale=Time.timeScale;start=Time.time;session.Primary();
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Finish(string message){
        Result=message;EditorApplication.update-=Tick;Time.timeScale=oldScale;Debug.Log(message);
    }
    static void Tick(){
        if(!EditorApplication.isPlaying || !session){EditorApplication.update-=Tick;return;}
        if(session.State==RaceSession.Phase.Results){
            Finish($"Finished {session.panel.resultTime.text}, car {session.Car.spec.name}, four wheels {pilot.allGround}/{pilot.samples}, peak {pilot.peakSpeed*3.6f:F1} km/h");return;
        }
        if(Time.time-start>600){Finish("Timed out at gate "+session.NextCheckpoint);return;}
        if(!session.Car || session.State!=RaceSession.Phase.Countdown && session.State!=RaceSession.Phase.Driving)return;
        if(pilot==null){
            var c=UnityEngine.Object.FindAnyObjectByType<Circuit>();
            pilot=new Pilot(session.Car,c.mapCenterline);session.Car.Driver=pilot;Time.timeScale=6;
        }
        Result=$"Gate {session.NextCheckpoint}, time {Time.time-start:F1}, speed {session.Car.Speed*3.6f:F1}, four wheels {pilot.allGround}/{pilot.samples}";
    }
    public static string Measure(Circuit c){
        Physics.SyncTransforms();var road=c.transform.Find("Grand Prix asphalt").GetComponent<MeshCollider>();
        var wall=c.transform.Find("Concrete race barriers").GetComponent<MeshCollider>();
        var main=c.mapCenterline;
        var verts=road.sharedMesh.vertices;float length=0,min=float.PositiveInfinity,max=0,peakCurvature=0;int hits=0,wallHits=0;
        for(int i=0;i<main.Length;i++){
            length+=Vector3.Distance(main[i],main[(i+1)%main.Length]);
            var back=main[i]-main[(i+main.Length-3)%main.Length];var ahead=main[(i+3)%main.Length]-main[i];
            peakCurvature=Mathf.Max(peakCurvature,2*Vector3.Cross(back,ahead).magnitude/(back.magnitude*ahead.magnitude*(back+ahead).magnitude));
            float w=Vector3.Distance(verts[4*i],verts[4*i+3]);min=Mathf.Min(min,w);max=Mathf.Max(max,w);
            foreach(float t in new[]{.04f,.5f,.96f}){
                var p=Vector3.Lerp(verts[4*i],verts[4*i+3],t);
                if(!road.Raycast(new Ray(p+Vector3.up,Vector3.down),out var hit,1.1f))throw new Exception("Main contact lost at sample "+i);
                hits++;
            }
        }
        for(int i=0;i<main.Length;i++){
            int j=(i+1)%main.Length;var mid=(main[i]+main[j])*.5f;
            var side=Vector3.Cross(Vector3.up,(main[j]-main[i]).normalized);
            for(int sign=-1;sign<=1;sign+=2){
                float halfWidth=Vector3.Distance(verts[4*i],verts[4*i+3])*.5f;
                if(!wall.Raycast(new Ray(mid+Vector3.up*.5f,side*sign),out var hit,halfWidth+.25f) || Mathf.Abs(hit.distance-halfWidth)>.25f)
                    throw new Exception("Open race barrier at station "+i+" side "+sign);
                wallHits++;
            }
        }
        return $"{length:F2} m, asphalt {min:F2}–{max:F2} m, {1/peakCurvature:F1} m min radius, {c.gates.Length} timing lines, {hits} road and {wallHits} barrier rays. "+Grounding(c,verts);
    }
    static string Grounding(Circuit c,Vector3[] road){
        MeshCollider Surface(string name)=>c.transform.Find(name).GetComponent<MeshCollider>();
        var land=Surface("City ground");var deck=Surface("River bridge decks and approach roads");
        var surfaces=new[]{land,deck,Surface("Road foundations and public footpaths"),Surface("River bridge footways")};
        bool Supports(Vector3 p){
            var ray=new Ray(p+Vector3.up,Vector3.down);
            foreach(var surface in surfaces)if(surface.Raycast(ray,out _,1.30f))return true;
            return false;
        }
        int verge=0;
        for(int i=0;i<road.Length;i+=4){
            var right=(road[i+3]-road[i]).normalized;
            if(!Supports(road[i]-right*.8f) || !Supports(road[i+3]+right*.8f))
                throw new Exception("Missing roadside foundation at station "+i/4);
            verge+=2;
        }
        var map=new MarinaStreets();var river=new MarinaRiver(map);int bridgeSamples=0;
        int wetRuns=0;bool dryBefore=true;
        for(int i=0;i<c.mapCenterline.Length;i++){
            var p=c.mapCenterline[i];
            bool dry=land.Raycast(new Ray(p+Vector3.up,Vector3.down),out _,1.3f);
            if(!dry){
                if(dryBefore)wetRuns++;
                if(!deck.Raycast(new Ray(p+Vector3.up,Vector3.down),out _,1.3f))throw new Exception("Missing bridge at "+i);
            }
            dryBefore=dry;
        }
        if(wetRuns!=1)throw new Exception("Unexpected number of water crossings: "+wetRuns);
        for(float metres=-MarinaRiver.BridgeLength*.5f-MarinaBridgeRoads.Approach+1;
            metres<=MarinaRiver.BridgeLength*.5f+MarinaBridgeRoads.Approach-1;metres+=2){
            float sample=river.bridgeStation+metres*map.main.Length/MarinaStreets.LapMetres;
            int i=Mathf.FloorToInt(sample);var p=Vector3.Lerp(c.mapCenterline[i],c.mapCenterline[i+1],sample-i);
            var side=Vector3.Cross(Vector3.up,(c.mapCenterline[i+1]-c.mapCenterline[i]).normalized);
            foreach(int sign in new[]{-1,1}){
                var ray=new Ray(p+side*sign*(map.Width(p)*.5f+1)+Vector3.up,Vector3.down);
                if(!deck.Raycast(ray,out _,1.2f))throw new Exception("Bridge edge unsupported at "+metres);
                bridgeSamples++;
            }
        }
        var mesh=c.transform.Find("Street markets boulevards and palms").GetComponent<MeshFilter>().sharedMesh;
        var vertices=mesh.vertices;var colors=mesh.colors32;int trees=0;
        for(int i=48;i<colors.Length;i++){
            var color=colors[i];if(color.r!=0x77 || color.g!=0x6a || color.b!=0x5c)continue;
            // Five faces around the trunk. Its root must be embedded in the preceding soil bed.
            var root=Vector3.zero;for(int face=0;face<5;face++)root+=vertices[i+4*face]/5;
            for(int j=i-44;j<i-40;j++){
                var ray=new Ray(vertices[j]+Vector3.up,Vector3.down);
                if(!land.Raycast(ray,out var hit,1.35f) || vertices[j].y>hit.point.y+.005f)
                    throw new Exception("Floating planter corner at "+root);
            }
            var soil=vertices[i-24];var along=vertices[i-23]-soil;var across=vertices[i-21]-soil;
            var offset=root-soil;
            float u=Vector3.Dot(offset,along)/along.sqrMagnitude,v=Vector3.Dot(offset,across)/across.sqrMagnitude;
            if(u<0 || u>1 || v<0 || v>1 || root.y>soil.y || root.y<vertices[i-20].y-.02f)
                throw new Exception("Detached palm at "+root);
            trees++;i+=19;
        }
        if(trees==0)throw new Exception("No planted trees found");
        return $"{verge} grounded verges, {wetRuns} water crossing, {bridgeSamples} bridge and approach contacts, {trees} seated planters";
    }
}
}
