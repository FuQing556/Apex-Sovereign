using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.Editor {
// Runs the selected garage track. Only the player's input is substituted.
public sealed class GridRaceCheck {
    RaceSession session;
    GridRace race;
    bool parked,headStart;
    float oldScale,start;
    int[] gates=new int[4],slides=new int[4],hands=new int[4];
    int[] impactSamples=new int[4];
    string target,watch;
    readonly StringBuilder log=new();
    string path;
    [MenuItem("Apex/Checks/Four car race/Drive from the grid")]
    public static void Drive()=>Begin(false,false);
    [MenuItem("Apex/Checks/Four car race/Parked player cutoff")]
    public static void Park()=>Begin(true,false);
    [MenuItem("Apex/Checks/Four car race/Player win camera fixture")]
    public static void Spectate()=>Begin(false,true);
    static void Begin(bool parked,bool headStart){
        var session=UnityEngine.Object.FindAnyObjectByType<RaceSession>();
        if(!Application.isPlaying || !session || session.State!=RaceSession.Phase.Lobby)
            throw new InvalidOperationException("Enter Play in Bootstrap and return to the garage.");
        var probe=new GridRaceCheck{session=session,parked=parked,headStart=headStart,oldScale=Time.timeScale};
        session.StartCoroutine(probe.Run());
    }
    IEnumerator Run(){
        try{
            yield return Launch();
            var wait=new WaitForFixedUpdate();
            while(race!=null){yield return wait;Sample();}
        }finally{Time.timeScale=oldScale;}
    }
    IEnumerator Launch(){
        string scene=session.tracks.Single(t=>t.title==session.panel.location.text).scene;
        if(session.panel.launchLabel.text!="开始四车竞速")
            session.panel.garageCard.Find("比赛模式").GetComponent<Button>().onClick.Invoke();
        var choose=session.panel.garageCard.Find("选择车衣").GetComponent<Button>();
        for(int i=0;i<4;i++){choose.onClick.Invoke();log.AppendLine("garage "+choose.GetComponentInChildren<Text>().text);}
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/GridRace"));
        Directory.CreateDirectory(folder);path=Path.Combine(folder,scene+"-"+(parked?"parked":headStart?"spectator":"race")+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt");
        session.panel.launch.onClick.Invoke();
        while(session.State==RaceSession.Phase.Loading)yield return null;
        race=session.Grid;
        if(race==null)throw new InvalidOperationException("Garage did not launch the four-car race.");
        if(race.course.gameObject.scene.name!=scene)throw new InvalidOperationException("Loaded the wrong course.");
        log.AppendLine($"{scene}: {race.route.length:F1} m, {race.course.gates.Length} gates, {race.course.laps} laps");
        for(int g=0;g<race.course.gates.Length;g++){
            float s=race.route.Gate(g);
            if((g>0 && s<=race.route.Gate(g-1)) || Vector3.Distance(race.route.Target(s,0),race.course.gates[g].position)>race.tune.lineWidth+race.route.step)
                throw new InvalidOperationException("Race line does not pass timing gate "+g);
        }
        foreach(var e in race.entries){
            if(session.PureGrid && e.car.spec!=race.Player.car.spec)
                throw new InvalidOperationException("Pure grid contains mixed models.");
            if(!session.panel.catalog.differentials.Contains(e.car.Differential) || !session.panel.catalog.wheels.Contains(e.car.RearWheels))
                throw new InvalidOperationException("Grid car has an invalid parts draw.");
            if(e.role==GridRole.Player && (e.car.Differential!=session.panel.catalog.InstalledPart(e.car.spec,true) ||
                e.car.RearWheels!=session.panel.catalog.InstalledPart(e.car.spec,false)))
                throw new InvalidOperationException("Player did not receive the garage loadout for the starting car.");
            var p=race.course.grid.InverseTransformPoint(e.car.Chassis.position);
            log.AppendLine($"grid {e.Name} slot={e.slot} x={p.x:F2} z={p.z:F2} livery={e.livery.title}");
            if(Mathf.Abs(p.x-(e.slot-1.5f)*race.tune.slotSpacing)>.05f || Mathf.Abs(p.z)>.05f)
                throw new InvalidOperationException("The cars did not spawn abreast.");
        }
        if(race.entries.Select(e=>e.slot).Distinct().Count()!=4 || race.entries.Select(e=>e.livery.paint).Distinct().Count()!=4)
            throw new InvalidOperationException("Duplicate slot or race livery.");
        foreach(var e in race.entries)foreach(var other in race.entries)if(e.slot<other.slot &&
            e.car.GetComponent<BoxCollider>().bounds.Intersects(other.car.GetComponent<BoxCollider>().bounds))
            throw new InvalidOperationException("Overlapping grid bodies.");
        if(!parked)race.Player.car.Driver=new GridDriver(race,race.Player);
        Time.timeScale=4;
        File.WriteAllText(path,log.ToString());
    }
    void Sample(){
        if(race==null || session.State==RaceSession.Phase.Countdown)return;
        if(start==0)start=Time.fixedTime;
        float t=Time.fixedTime-start;
        if(headStart && t<9)for(int i=1;i<4;i++)race.entries[i].car.EnabledDrive=false;
        if(headStart && t>=9 && t<10)for(int i=1;i<4;i++)race.entries[i].car.EnabledDrive=true;
        foreach(var e in race.entries){
            int i=(int)e.role;
            if(e.car.CarImpulseSample!=impactSamples[i]){
                impactSamples[i]=e.car.CarImpulseSample;
                float delta=e.car.LastCarImpulse.magnitude/e.car.Chassis.mass;
                if(delta>.15f)log.AppendLine(FormattableString.Invariant($"{t:F2} impact {e.Name}/{e.car.LastCarContact.name} dv={delta:F2} closing={e.car.LastCarClosingSpeed:F2} yaw={e.car.Chassis.angularVelocity.y*Mathf.Rad2Deg:F1}"));
            }
            if(gates[i]!=e.passed){gates[i]=e.passed;log.AppendLine($"{t:F2} gate {e.Name} {e.passed}");}
            if(e.car.AppliedCommand.hand>.5f)hands[i]++;
            if(e.car.GroundedCount>=3 && e.car.ForwardSpeed>10 && Mathf.Abs(e.car.SignedDriftAngle)>=12 && Mathf.Abs(e.car.SignedDriftAngle)<=45)slides[i]++;
        }
        string hunting=(race.entries[3].car.Driver as GridDriver)?.Pursuing?.Name??"领跑/收车";
        if(hunting!=target){target=hunting;log.AppendLine($"{t:F2} hunter -> {hunting}");}
        if(watch!=race.Watching.Name){watch=race.Watching.Name;log.AppendLine($"{t:F2} camera -> {watch} order={string.Join(",",race.order.Select(e=>e.Name))}");}
        float timeout=Mathf.Max(300,race.route.length*race.course.laps/15+race.tune.finishWindow);
        if(session.State!=RaceSession.Phase.Results && t<timeout)return;
        log.AppendLine($"state={session.State} elapsed={t:F2} deadline={race.Deadline:F3} winner={race.order[0].finish:F3}");
        foreach(var e in race.order)
            log.AppendLine($"{e.Name} gates={e.passed} finish={e.finish:F3} resets={e.car.ResetGeneration} contacts={e.car.VehicleContacts} slide_s={slides[(int)e.role]*Time.fixedDeltaTime:F2} hand_s={hands[(int)e.role]*Time.fixedDeltaTime:F2}");
        File.WriteAllText(path,log.ToString());Debug.Log("Four-car check: "+path);
        bool valid=session.State==RaceSession.Phase.Results && race.order[0].passed==race.course.laps*race.course.gates.Length &&
            Mathf.Abs(race.Deadline-race.order[0].finish-race.tune.finishWindow)<.001f &&
            Mathf.Abs(t-Mathf.Min(race.order[3].finish,race.Deadline))<=2*Time.fixedDeltaTime && race.entries.All(e=>e.car.ResetGeneration==0) &&
            (!parked || !race.Player.Finished);
        race=null;
        if(!valid)throw new InvalidOperationException("Race acceptance failed; see "+path);
    }
}
}
