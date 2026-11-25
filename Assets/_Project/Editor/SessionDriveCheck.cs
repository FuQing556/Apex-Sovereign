using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Apex.Editor {
public static class SessionDriveCheck {
    sealed class CircuitDriver : IDriver {
        readonly RaceCar car;
        readonly Vector3[] line;
        int target;
        readonly bool showcase;
        readonly float[] speedPlan;
        float slide, cooldown, direction, longitudinal;
        float nextDrift,entryTime;
        int driftSide,lastDriftSide;
        bool catching;
        public CircuitDriver(RaceCar car,Circuit track,bool showcase=false,bool keyboardTrial=false){this.car=car;this.showcase=showcase;
            var points=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<track.gates.Length;i++){
                var a=track.gates[i];var b=track.gates[(i+1)%track.gates.Length];
                float length=Vector3.Distance(a.position,b.position);
                int steps=Mathf.Max(1,Mathf.CeilToInt(length/2));
                for(int j=0;j<steps;j++){
                    float u=(float)j/steps,v=1-u;
                    points.Add(v*v*v*a.position+3*v*v*u*(a.position+a.forward*length/3)
                        +3*v*u*u*(b.position-b.forward*length/3)+u*u*u*b.position);
                }
            }
            line=points.ToArray();
            if(!showcase && !keyboardTrial)return;
            // Use some track width through bends. Limit offsets to stay inside timing gates.
            var center=(Vector3[])line.Clone();
            for(int pass=0;pass<24;pass++){
                var smooth=(Vector3[])line.Clone();
                for(int i=0;i<line.Length;i++){
                    var mid=(line[(i+line.Length-2)%line.Length]+line[(i+2)%line.Length])*.5f;
                    smooth[i]=center[i]+Vector3.ClampMagnitude(Vector3.Lerp(line[i],mid,.45f)-center[i],3f);
                }
                line=smooth;
            }
            speedPlan=new float[line.Length];
            for(int i=0;i<line.Length;i++){
                var a=line[(i+line.Length-3)%line.Length];var b=line[i];var c=line[(i+3)%line.Length];
                float curvature=2*Vector3.Cross(b-a,c-b).magnitude/Mathf.Max(.01f,(b-a).magnitude*(c-b).magnitude*(c-a).magnitude);
                speedPlan[i]=Mathf.Min(keyboardTrial?42:48,Mathf.Sqrt((keyboardTrial?7.2f:11)/Mathf.Max(.002f,curvature)));
            }
            for(int j=2*line.Length-1;j>=0;j--){
                int i=j%line.Length,n=(i+1)%line.Length;
                float braking=keyboardTrial?6.5f:8;
                speedPlan[i]=Mathf.Min(speedPlan[i],Mathf.Sqrt(speedPlan[n]*speedPlan[n]+2*braking*Vector3.Distance(line[i],line[n])));
            }
        }
        void UpdateTarget(){
            var pos=car.transform.position;
            for(int i=0;i<line.Length;i++){
                int next=(target+1)%line.Length;
                if((line[next]-pos).sqrMagnitude>=(line[target]-pos).sqrMagnitude)break;
                target=next;
            }
        }
        public KeyboardState ReadKeys(bool demonstrateSlides){
            UpdateTarget();
            int look=Mathf.Clamp(Mathf.RoundToInt(3+car.Speed*.25f),5,14);
            var point=car.transform.InverseTransformPoint(line[(target+look)%line.Length]);
            float curvature=2*point.x/Mathf.Max(1,point.x*point.x+point.z*point.z);
            float yaw=Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg;
            float headingRate=curvature*car.Speed*Mathf.Rad2Deg;
            float desired=Mathf.Atan(car.spec.wheelbase*curvature)*Mathf.Rad2Deg*(1+.022f*car.Speed)
                + .45f*car.SignedDriftAngle + .09f*(headingRate-yaw);
            float current=(car.wheels[0].steerAngle+car.wheels[1].steerAngle)*.5f;
            var keys=new KeyboardState();
            float speedTarget=speedPlan[target];
            if(demonstrateSlides){
                float now=Time.time;
                int side=curvature>0?1:-1;
                float distanceToLine=Vector3.Distance(line[target],car.transform.position);
                bool room=distanceToLine<2.5f && speedPlan[(target+look)%line.Length]>car.Speed-1.8f;
                if(driftSide==0 && now>=nextDrift && car.Speed>18 && car.Speed<35 && car.GroundedCount==4 &&
                    side*yaw>8 && side*yaw<42 && side*current>1 && Mathf.Abs(curvature)<.045f && room &&
                    Mathf.Abs(car.SignedDriftAngle)<9 && (side!=lastDriftSide || now>nextDrift+9)){
                    driftSide=side;lastDriftSide=side;entryTime=now;catching=false;
                }
                if(driftSide!=0){
                    float age=now-entryTime;
                    float beta=car.SignedDriftAngle;
                    float outward=-driftSide*beta;
                    if(age>.64f || outward>14 || driftSide*yaw>55 || distanceToLine>3.5f)catching=true;
                    if(age>2 || distanceToLine>5){
                        driftSide=0;nextDrift=now+8;
                    }else if(!catching){
                        desired+=driftSide*14;
                        keys.Set(Key.Space,age>.08f && age<.24f && outward<12);
                    }else{
                        // Track the velocity direction while the chassis points into the bend.
                        float distance=Mathf.Max(1,new Vector2(point.x,point.z).magnitude);
                        float courseError=Mathf.Atan2(point.x,point.z)-beta*Mathf.Deg2Rad;
                        float pathCurvature=2*Mathf.Sin(courseError)/distance;
                        float slipTarget=14*(1-Mathf.InverseLerp(.85f,1.65f,age));
                        float yawTarget=pathCurvature*car.Speed*Mathf.Rad2Deg+(beta+driftSide*slipTarget)*2.5f;
                        desired=Mathf.Atan(car.spec.wheelbase*pathCurvature)*Mathf.Rad2Deg+beta+.25f*(yawTarget-yaw);
                    }
                    // Carry entry speed through the slide and respect the next braking zone.
                    speedTarget=Mathf.Min(speedTarget,speedPlan[(target+look)%line.Length]+.8f);
                }
            }
            keys.Set(Key.A,desired<-.30f && current>desired-.15f);
            keys.Set(Key.D,desired>.30f && current<desired+.15f);
            keys.Set(Key.W,car.Speed<speedTarget-.25f);
            keys.Set(Key.S,car.Speed>speedTarget+1);
            return keys;
        }
        public DriveCommand Read(){
            UpdateTarget();
            var local=car.transform.InverseTransformPoint(line[(target+4)%line.Length]);
            float angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            if(showcase){
                float dt=Time.fixedDeltaTime;
                int look=Mathf.Clamp(Mathf.RoundToInt(2+car.Speed*.21f),4,12);
                local=car.transform.InverseTransformPoint(line[(target+look)%line.Length]);
                angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                cooldown-=dt;
                if(cooldown<=0 && Mathf.Abs(angle)>11 && Mathf.Abs(angle)<32 && car.Speed>14 && Mathf.Abs(car.Chassis.angularVelocity.y)>.30f){
                    direction=Mathf.Sign(angle);slide=.48f;cooldown=11;
                }
                slide-=dt;
                float error=speedPlan[target]-car.Speed;
                longitudinal=Mathf.MoveTowards(longitudinal,Mathf.Clamp(error*.38f+.17f,-1,1),dt*3);
                float steer=Mathf.Clamp(angle/16-car.Chassis.angularVelocity.y*.07f,-1,1);
                if(slide>0 && car.DriftAngle<15)return new DriveCommand{steer=Mathf.Lerp(steer,direction,.72f),hand=1,throttle=1};
                return new DriveCommand{steer=steer,throttle=Mathf.Max(0,longitudinal),brake=Mathf.Max(0,-longitudinal)};
            }
            return new DriveCommand{steer=Mathf.Clamp(angle/28,-1,1),throttle=car.Speed<11?.55f:0,brake=car.Speed>12?.25f:0};
        }
    }
    static int step;static double start;static RaceCar driven;
    [MenuItem("Apex/Drive two laps with keyboard inputs")]
    public static void KeyboardLaps()=>StartKeyboardLaps(false);
    [MenuItem("Apex/Drive two keyboard laps with drift transitions")]
    public static void KeyboardDriftLaps()=>StartKeyboardLaps(true);
    static void StartKeyboardLaps(bool demonstrateSlides){
        var session=Object.FindAnyObjectByType<RaceSession>();
        if(!EditorApplication.isPlaying || !session || session.State!=RaceSession.Phase.Lobby)
            throw new System.InvalidOperationException("Start the keyboard drive from the garage.");
        session.showDriveMonitor=true;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        var originalKeyboard=Keyboard.current;
        bool background=Application.runInBackground;
        Application.runInBackground=true;
        var keyboard=InputSystem.AddDevice<Keyboard>("ApexLapKeyboard");
        CircuitDriver planner=null;
        void Stop(){
            Application.onBeforeRender-=Sample;
            InputSystem.onAfterUpdate-=Sample;
            EditorApplication.playModeStateChanged-=OnMode;
            if(keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(originalKeyboard!=null && originalKeyboard.added)originalKeyboard.MakeCurrent();
            Application.runInBackground=background;
        }
        void OnMode(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Stop();}
        void Sample(){
            if(!session || session.State==RaceSession.Phase.Results || session.State==RaceSession.Phase.Lobby){Stop();return;}
            if(!session.Car || session.State!=RaceSession.Phase.Driving)return;
            if(planner==null)planner=new CircuitDriver(session.Car,Object.FindAnyObjectByType<Circuit>(),keyboardTrial:true);
            InputState.Change(keyboard,planner.ReadKeys(demonstrateSlides));
            keyboard.MakeCurrent();
        }
        session.Primary();
        InputSystem.onAfterUpdate+=Sample;
        Application.onBeforeRender+=Sample;
        EditorApplication.playModeStateChanged+=OnMode;
    }
    [MenuItem("Apex/Drive two demonstration laps")]
    public static void Demonstrate(){
        var s=Object.FindAnyObjectByType<RaceSession>();
        if(!EditorApplication.isPlaying || !s || s.State!=RaceSession.Phase.Lobby)
            throw new System.InvalidOperationException("Start from the garage in Play Mode.");
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        s.Primary();
        void Bind(){
            if(!s || !EditorApplication.isPlaying){EditorApplication.update-=Bind;return;}
            if(!s.Car || s.State!=RaceSession.Phase.Countdown && s.State!=RaceSession.Phase.Driving)return;
            s.Car.Driver=new CircuitDriver(s.Car,Object.FindAnyObjectByType<Circuit>(),true);
            EditorApplication.update-=Bind;
        }
        EditorApplication.update+=Bind;
    }
    [MenuItem("Apex/Verify session in Play Mode")]
    public static void Run(){
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Start Bootstrap in Play Mode first.");
        step=0;start=EditorApplication.timeSinceStartup;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick(){
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup-start>100){
            EditorApplication.update-=Tick;Time.timeScale=1;Debug.LogError("Apex verification timed out at stage "+step);return;
        }
        var s=Object.FindAnyObjectByType<RaceSession>();
        if(!s)return;
        if(step==0 && s.State==RaceSession.Phase.Lobby){s.panel.primary.onClick.Invoke();step++;Time.timeScale=3;}
        else if(step==1 && s.State==RaceSession.Phase.Driving){driven=s.Car;driven.Driver=new CircuitDriver(driven,Object.FindAnyObjectByType<Circuit>());step++;}
        else if(step==2 && s.State==RaceSession.Phase.Results){Debug.Log("Apex finish: "+s.panel.resultTime.text);s.panel.primary.onClick.Invoke();step++;}
        else if(step==3 && s.State==RaceSession.Phase.Driving){s.panel.secondary.onClick.Invoke();step++;}
        else if(step==4 && s.State==RaceSession.Phase.Lobby){
            EditorApplication.update-=Tick;Time.timeScale=1;
            if(Object.FindAnyObjectByType<RaceCar>())Debug.LogError("Race car survived scene unload");
            else Debug.Log("Apex verified: selection, driving, finish, rematch and return to garage.");
        }
    }
}
}
