using UnityEngine;

namespace Apex {
// The pad owns the lesson geometry. Cars (local or AI) only receive pedal and steering commands.
public sealed class DrivingSchool : MonoBehaviour {
    public enum Lesson { LeftCircle, RightCircle, FigureEight, Tandem }
    public Material lanePaint, targetPaint;
    [Min(10)] public float radius = 24;
    [Min(2)] public float halfWidth = 5;
    [Range(6,24)] public int sectorsPerCircle = 12;
    [Min(1)] public float targetSpeed = 12;
    [Min(0)] public float rivalLeadDegrees = 65;
    [Min(.1f)] public float lookAhead = 7;
    [Range(0,1)] public float requiredSlideFraction = .45f;
    [Tooltip("Desired centre-to-centre chase gap in metres, measured behind the lead car.")]
    public Vector2 chaseWindow = new Vector2(5,12);
    public RivalStyle[] styles = {
        new RivalStyle("Race", 1f, 0, 5),
        new RivalStyle("Attack", 1.12f, -1.3f, 4),
        new RivalStyle("Avoid", .88f, 1.5f, 10)
    };
    [System.Serializable] public class RivalStyle {
        public string label;
        public float speedFactor, passingOffset, clearance;
        public RivalStyle(string label,float speedFactor,float passingOffset,float clearance){
            this.label=label; this.speedFactor=speedFactor; this.passingOffset=passingOffset; this.clearance=clearance;
        }
    }
    public Lesson Current {get; private set;}
    public int NextGate {get; private set;}
    public int Completed {get; private set;}
    public float SlidingSeconds => sliding;
    public float FollowingSeconds => following;
    public RaceCar Rival {get; private set;}
    public bool Coach {get; set;} = true;
    RaceCar player, rivalPrefab;
    DrivingSounds sounds;
    TyreMarkField marks;
    GameObject markings;
    Transform target;
    int style;
    float elapsed, sliding, following;
    int initialContacts;
    bool started, offLine;
    string last = "";
    public int GateCount => sectorsPerCircle * (Current == Lesson.FigureEight ? 2 : 1);
    bool Right(int sector) => Current == Lesson.RightCircle || (Current == Lesson.FigureEight && sector >= sectorsPerCircle);
    public void Begin(RaceCar car, RaceCar partner, DrivingSounds audio=null,TyreMarkField tyreMarks=null) {
        player=car; rivalPrefab=partner; sounds=audio; marks=tyreMarks; Select(Lesson.LeftCircle);
    }
    public void NextLesson() => Select((Lesson)(((int)Current+1)%4));
    public void NextStyle() { if(styles.Length>0) style=(style+1)%styles.Length; }
    public void Select(Lesson lesson) {
        Current=lesson; Completed=0; last="";
        if(markings){markings.SetActive(false);Destroy(markings);}
        if(Rival){Rival.gameObject.SetActive(false);Destroy(Rival.gameObject);Rival=null;}
        markings=new GameObject("Active lesson markings");
        markings.transform.SetParent(transform,false);
        DrawCircle(Current==Lesson.RightCircle);
        if(Current==Lesson.FigureEight) DrawCircle(true);
        target=new GameObject("Next gate").transform;
        target.SetParent(markings.transform,false);
        for(int side=-1;side<=1;side+=2) {
            var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.transform.SetParent(target,false);
            post.transform.localPosition=new Vector3(side*halfWidth,.64f,0);
            post.transform.localScale=new Vector3(.45f,.64f,.45f);
            var collider=post.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            post.GetComponent<Renderer>().sharedMaterial=targetPaint;
        }
        if(Current==Lesson.Tandem && styles.Length>0){
            float angle=rivalLeadDegrees*Mathf.Deg2Rad;
            var p=Point(false,angle,radius); p.y=GetComponent<Circuit>().grid.position.y;
            Rival=Instantiate(rivalPrefab,p,Quaternion.LookRotation(Tangent(false,angle)),transform);
            Rival.Driver=new PartnerDriver(this); Rival.EnabledDrive=true;
            VehicleAudio.Attach(Rival,sounds);
            if(marks)marks.Track(Rival);
        }
        RestartAttempt();
    }
    public void RestartAttempt(){
        NextGate=0; started=offLine=false; elapsed=sliding=following=0;
        initialContacts=player.VehicleContacts;
        if(Rival){
            float angle=rivalLeadDegrees*Mathf.Deg2Rad;
            var position=Point(false,angle,radius);
            position.y=GetComponent<Circuit>().grid.position.y;
            Rival.Chassis.position=position;
            Rival.Chassis.rotation=Quaternion.LookRotation(Tangent(false,angle));
            Rival.Chassis.linearVelocity=Rival.Chassis.angularVelocity=Vector3.zero;
            Rival.ResetDrift();
        }
        MoveGate();
    }
    Vector3 Center(bool right)=>transform.position+Vector3.right*(right?radius:-radius);
    Vector3 Point(bool right,float a,float r)=>Center(right)+new Vector3((right?-1:1)*Mathf.Cos(a),0,Mathf.Sin(a))*r;
    static Vector3 Tangent(bool right,float a)=>new Vector3((right?1:-1)*Mathf.Sin(a),0,Mathf.Cos(a));
    void DrawCircle(bool right){
        foreach(float edge in new[]{radius-halfWidth,radius+halfWidth}){
            var go=new GameObject("Lane edge"); go.transform.SetParent(markings.transform,false);
            var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=lanePaint; line.alignment=LineAlignment.TransformZ; go.transform.rotation=Quaternion.Euler(90,0,0);
            line.loop=true; line.widthMultiplier=.16f;
            line.positionCount=sectorsPerCircle*8;
            for(int i=0;i<line.positionCount;i++)
                line.SetPosition(i,Point(right,2*Mathf.PI*i/line.positionCount,edge)+Vector3.up*.045f);
        }
    }
    void MoveGate(){
        float a=(NextGate%sectorsPerCircle)*2*Mathf.PI/sectorsPerCircle;
        // Gate 0 also finishes the preceding lobe at the common tangent.
        bool right=Right(NextGate);
        target.SetPositionAndRotation(Point(right,a,radius),Quaternion.LookRotation(Tangent(right,a)));
    }
    public void Sample(Vector3 before,Vector3 after,float dt){
        // While approaching a crossing, measure the lobe being finished.
        bool right=Right((NextGate+GateCount-1)%GateCount);
        var delta=Vector3.ProjectOnPlane(after-Center(right),Vector3.up);
        if(started){
            elapsed+=dt;
            if(Rival && InChaseWindow())following+=dt;
            if(Mathf.Abs(delta.magnitude-radius)>halfWidth)offLine=true;
            Vector3 tangent=Vector3.Cross(delta,Vector3.up)*(right?-1:1);
            float outwardSlip=right?-player.SignedDriftAngle:player.SignedDriftAngle;
            if(player.GroundedCount>=3 && player.ForwardSpeed>=player.spec.driftMinSpeed &&
               Vector3.Dot(player.Chassis.linearVelocity,tangent)>0 && outwardSlip>=player.spec.driftEntryAngle && outwardSlip<player.spec.driftSpinAngle)
                sliding+=dt;
        }
        var a=target.InverseTransformPoint(before); var b=target.InverseTransformPoint(after);
        if(a.z>=0 || b.z<0) return;
        var hit=Vector3.Lerp(a,b,-a.z/(b.z-a.z));
        if(Mathf.Abs(hit.x)>halfWidth || Mathf.Abs(hit.y)>GetComponent<Circuit>().gateHalfHeight) return;
        if(NextGate==0){
            if(started){
                float fraction=sliding/Mathf.Max(dt,elapsed);
                Completed++;
                last=$"第 {Completed} 圈　{elapsed:F1} 秒　"+(offLine?"越线":fraction>=requiredSlideFraction?"漂移达标":"走线达标")+$"　侧滑 {fraction:P0}";
                if(Rival)last+=$"\n跟车 {following/elapsed:P0}　碰撞 {player.VehicleContacts-initialContacts} 次";
            }
            started=true;offLine=false;elapsed=sliding=following=0;initialContacts=player.VehicleContacts;
        }
        NextGate=(NextGate+1)%GateCount;MoveGate();
    }
    bool InChaseWindow(){
        Vector3 local=Rival.transform.InverseTransformPoint(player.Chassis.position);
        float distance=Vector3.Distance(Rival.Chassis.position,player.Chassis.position);
        return local.z<0 && Mathf.Abs(local.x)<=halfWidth && distance>=chaseWindow.x && distance<=chaseWindow.y;
    }
    public string LessonName => Current switch {Lesson.LeftCircle=>"左定圆",Lesson.RightCircle=>"右定圆",Lesson.FigureEight=>"八字换向",_=>"双车追走"};
    public string StyleName => !Rival?"单车":styles[style].label switch {"Race"=>"竞速型","Attack"=>"进攻型","Avoid"=>"避让型",var label=>label};
    public string LastResult => last;
    public Vector3 NextPosition => target.position;
    public Vector3 MapPoint(int sector,float fraction){
        float angle=((sector%sectorsPerCircle)+fraction)*2*Mathf.PI/sectorsPerCircle;
        return Point(Right(sector),angle,radius);
    }
    public string Progress => $"标志门　{NextGate+1} / {GateCount}　　侧滑　{sliding/Mathf.Max(Time.fixedDeltaTime,elapsed):P0}";
    public string Chase => Rival?$"{StyleName}　车距 {Vector3.Distance(Rival.Chassis.position,player.Chassis.position):F1} 米":"";
    public string Advice(){
        if(!Coach)return "";
        string tip;
        if(player.DriftAngle>player.spec.driftSpinAngle && player.Speed>player.spec.driftMinSpeed)tip="反打，控制车尾";
        else if(!started)tip="从绿色标志柱之间驶入";
        else if(Rival && Vector3.Distance(Rival.Chassis.position,player.Chassis.position)<chaseWindow.x)tip="稍收油，拉开车距";
        else if(Rival && InChaseWindow())tip="保持跟车距离";
        else if(Current==Lesson.FigureEight && NextGate%sectorsPerCircle==0)tip=NextGate==0?"过交点后向左转":"过交点后向右转";
        else if(player.Speed>1.35f*targetSpeed)tip="入下一道门前收油";
        else if(player.DriftAngle>=player.spec.driftEntryAngle && player.HandbrakeApplied>.2f)tip="松开手刹，稳住侧滑";
        else tip=Right(NextGate)?"沿右侧圆弧行驶":"沿左侧圆弧行驶";
        return $"{tip}　{Vector3.Distance(target.position,player.transform.position):F0} 米";
    }
    public string Status()=>$"{LessonName}　{Progress}　{Chase}\n{Advice()}\n{LastResult}";
    sealed class PartnerDriver : IDriver {
        readonly DrivingSchool school;
        public PartnerDriver(DrivingSchool school){this.school=school;}
        public DriveCommand Read(){
            var car=school.Rival; var t=car.transform; var tune=school.styles[school.style];
            var p=t.position-school.Center(false);
            float a=Mathf.Atan2(p.z,p.x)+school.lookAhead/school.radius;
            var gap=t.InverseTransformPoint(school.player.transform.position);
            bool traffic=gap.z>0 && gap.z<tune.clearance*2 && Mathf.Abs(gap.x)<school.halfWidth;
            float offset=traffic?tune.passingOffset:0;
            var goal=t.InverseTransformPoint(school.Point(false,a,school.radius+offset));
            float steer=Mathf.Atan2(goal.x,goal.z)*Mathf.Rad2Deg/Mathf.Max(1,car.spec.steeringBySpeed.Evaluate(car.Speed));
            float speed=school.targetSpeed*tune.speedFactor;
            if(traffic && gap.z<tune.clearance) speed=Mathf.Min(speed,Mathf.Max(0,school.player.ForwardSpeed));
            float error=speed-car.ForwardSpeed;
            return new DriveCommand{steer=Mathf.Clamp(steer,-1,1),throttle=Mathf.Clamp01(error/3),brake=Mathf.Clamp01(-error/3)};
        }
    }
}
}
