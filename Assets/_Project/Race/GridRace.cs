using System;
using UnityEngine;

namespace Apex {
public enum GridRole { Player, Line, Drift, Hunter }
public sealed class GridEntry {
    static readonly string[] Names={"玩家","走线手","漂移手","追击手"};
    public readonly RaceCar car;
    public readonly GridRole role;
    public readonly GridLivery livery;
    public readonly int slot;
    public string Name=>Names[(int)role]+" "+car.spec.factoryRaceNumber;
    public int passed, next=1;
    public float station,progress,finish=float.PositiveInfinity;
    public bool Finished=>!float.IsPositiveInfinity(finish);
    public Vector3 previous;
    public GridEntry(RaceCar car,GridRole role,GridLivery livery,int slot){
        this.car=car;this.role=role;this.livery=livery;this.slot=slot;previous=car.Chassis.position;
    }
}
public sealed class GridRace {
    public readonly GridEntry[] entries=new GridEntry[4], order=new GridEntry[4];
    public readonly GridRoute route;
    public readonly Circuit course;
    public readonly GridRaceSettings tune;
    public GridEntry Player=>entries[0];
    public GridEntry Watching{get;private set;}
    public float Deadline{get;private set;}=float.PositiveInfinity;
    int finishCount;
    public GridEntry ActiveLeader {get{foreach(var e in order)if(!e.Finished)return e;return null;}}
    public GridRace(Circuit course,RoadLayout layout,GridRaceSettings settings,RaceCar chosen,RaceCar[] garage,
        bool pure,int selected,WorkshopCatalog catalog,TyreMarkField marks,DrivingSounds sounds){
        this.course=course;tune=settings;route=new GridRoute(course,layout,tune);
        var pool=pure?null:Array.FindAll(garage,car=>car && car.spec && car.spec.competition);
        var slots=new[]{0,1,2,3};Shuffle(slots,0);
        var skins=new[]{0,1,2,3};(skins[0],skins[selected])=(skins[selected],skins[0]);Shuffle(skins,pure?1:0);
        for(int i=0;i<4;i++){
            var grid=course.grid;
            var pos=grid.position+grid.right*((slots[i]-1.5f)*tune.slotSpacing);
            var prefab=pure?chosen:pool[UnityEngine.Random.Range(0,pool.Length)];
            var car=UnityEngine.Object.Instantiate(prefab,pos,grid.rotation);
            var livery=tune.liveries[skins[i]].ForCar(car.spec,skins[i]==0);
            var entry=new GridEntry(car,(GridRole)i,livery,slots[i]);
            car.name=entry.Name;entries[i]=order[i]=entry;
            if(catalog){
                if(entry.role==GridRole.Player)
                    car.FitParts(catalog.InstalledPart(car.spec,true),catalog.InstalledPart(car.spec,false));
                else car.FitParts(Draw(catalog.differentials),Draw(catalog.wheels));
            }
            entry.livery.Apply(car.gameObject,skins[i]==0);
            if(marks)marks.Track(car);
            VehicleAudio.Attach(car,sounds);
            if(i>0)car.Driver=new GridDriver(this,entry);
        }
        Watching=Player;
    }
    static WorkshopCatalog.Part Draw(WorkshopCatalog.Part[] options)=>
        options!=null && options.Length>0?options[UnityEngine.Random.Range(0,options.Length)]:null;
    static void Shuffle(int[] items,int start){
        for(int i=items.Length-1;i>start;i--){int j=UnityEngine.Random.Range(start,i+1);(items[i],items[j])=(items[j],items[i]);}
    }
    public void Green(){foreach(var e in entries){e.previous=e.car.Chassis.position;e.car.EnabledDrive=true;}}
    public int Lap(GridEntry e)=>Mathf.Min(course.laps,1+e.passed/course.gates.Length);
    public void ResetPlayer(){
        int last=(Player.next+course.gates.Length-1)%course.gates.Length;
        Player.previous=Player.car.Chassis.position;Player.station=route.Gate(last);
    }
    public bool Step(float now,float dt){
        foreach(var e in entries){
            var pos=e.car.Chassis.position;
            e.station=route.Station(pos,e.station);
            if(e.Finished)continue;
            if(course.Crossed(e.next,e.previous,pos)){
                var gate=course.gates[e.next];
                float a=Vector3.Dot(e.previous-gate.position,gate.forward),b=Vector3.Dot(pos-gate.position,gate.forward);
                float crossTime=now-dt+dt*Mathf.Clamp01(-a/(b-a));
                if(crossTime<=Deadline){
                    e.passed++;e.next=(e.next+1)%course.gates.Length;
                    if(e.passed==course.gates.Length*course.laps){
                        e.finish=crossTime;Deadline=Mathf.Min(Deadline,crossTime+tune.finishWindow);
                        float parkAt=tune.firstParkingStation+(entries.Length-1-finishCount++)*tune.parkingInterval;
                        e.car.Driver=new FinishDriver(this,e,parkAt);
                    }
                }
            }
            e.previous=pos;e.progress=e.passed+route.Fraction(e.next,e.station);
        }
        Array.Sort(order,Compare);
        if(Watching.Finished && ActiveLeader!=null)Watching=ActiveLeader;
        return finishCount==entries.Length || now>=Deadline;
    }
    static int Compare(GridEntry a,GridEntry b){
        int c=a.finish.CompareTo(b.finish);
        if(c!=0)return c;
        c=b.progress.CompareTo(a.progress);return c!=0?c:a.slot.CompareTo(b.slot);
    }
    // Roll out to staggered positions along the verge. Finished cars still have solid bodies.
    sealed class FinishDriver:IDriver {
        readonly GridRace race;
        readonly GridEntry entry;
        readonly float end;
        bool parked;
        public FinishDriver(GridRace race,GridEntry entry,float end){this.race=race;this.entry=entry;this.end=end;}
        public DriveCommand Read(){
            var car=entry.car;
            float remaining=race.route.Delta(end,entry.station);
            if(remaining<1 || parked){parked=true;return new DriveCommand{brake=1};}
            var target=race.route.Target(entry.station+Mathf.Min(remaining,Mathf.Max(8,car.Speed*.65f)),race.tune.parkingOffset);
            var p=car.transform.InverseTransformPoint(target);
            float angle=Mathf.Atan2(2*car.spec.wheelbase*p.x,Mathf.Max(1,p.x*p.x+p.z*p.z))*Mathf.Rad2Deg;
            float pace=Mathf.Min(17,Mathf.Sqrt(5*remaining));
            return new DriveCommand{steer=Mathf.Clamp(angle/car.spec.steeringBySpeed.Evaluate(car.Speed),-1,1),
                throttle=Mathf.Clamp01((pace-car.Speed)*.25f),brake=Mathf.Clamp01((car.Speed-pace)*.25f)};
        }
    }
    public void Stop(){foreach(var e in entries)e.car.EnabledDrive=false;}
}
}
