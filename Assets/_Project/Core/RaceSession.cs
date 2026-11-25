using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
namespace Apex {
public class RaceSession:MonoBehaviour {
    public string lobbyScene="Lobby",trackScene="Ridge";
    public TrackChoice[] tracks;
    [System.Serializable] public class TrackChoice { public string title,scene; public RoadLayout layout; public int routeRevision; }
    int trackIndex;
    public GridRaceSettings gridSettings=new GridRaceSettings();
    public GridRace Grid{get;private set;}
    Vector3 cameraKick,appliedKick;
    bool competitionMode;
    const string PureGridKey="Apex.Grid.Pure";
    public bool PureGrid {get;private set;}=true;
    public void ToggleGridRules(){
        if(State!=Phase.Lobby || !RacingGrid)return;
        PureGrid=!PureGrid;
        PlayerPrefs.SetInt(PureGridKey,PureGrid?1:0);PlayerPrefs.Save();
        RefreshGarage();
    }
    int skin;
    public bool RacingGrid=>competitionMode && SelectedCar.competition;
    public void ToggleGrid(){
        if(State!=Phase.Lobby)return;
        competitionMode=!RacingGrid;
        if(competitionMode && !SelectedCar.competition){
            int index=System.Array.FindIndex(entries,e=>e.spec.competition);
            if(index<0){competitionMode=false;return;}
            choice=index;ShowCar();return;
        }
        RefreshGarage();
    }
    public void NextLivery(){
        if(State!=Phase.Lobby || !SelectedCar.competition)return;
        skin=(skin+1)%gridSettings.liveries.Length;
        PlayerPrefs.SetInt("Apex.Garage.Livery",skin);ShowCar();
    }
    string LoadedTrack=>tracks!=null && tracks.Length>0?tracks[trackIndex].scene:trackScene;
    string RecordingTrack=>tracks!=null && tracks.Length>0 && tracks[trackIndex].routeRevision>0
        ? LoadedTrack+"-v"+tracks[trackIndex].routeRevision : LoadedTrack;
    public string defaultTrackTitle="山脊大奖赛道";
    string TrackName=>tracks!=null && tracks.Length>0?tracks[trackIndex].title:defaultTrackTitle;
    public void NextTrack(){if(State!=Phase.Lobby || tracks==null || tracks.Length==0)return;trackIndex=(trackIndex+1)%tracks.Length;RefreshGarage();}
    RaceGhost ghost;
    GhostShelf shelf;
    GhostRecording personal;
    GhostHeader pending;
    List<GhostRecording.Frame> pendingFrames;
    double greenLight;
    public void OpenReplays(){
        if(State==Phase.Lobby && shelf!=null)panel.replays.Library(shelf);
    }
    public void OfferReplay(GhostHeader header,List<GhostRecording.Frame> frames){
        if(State!=Phase.Results)return;
        pending=header;pendingFrames=frames;
        panel.replays.Offer();
    }
    public void ChooseSave(){
        if(pending!=null)panel.replays.SaveDialog(shelf,pending);
    }
    public void SkipSave(){pending=null;pendingFrames=null;panel.replays.Decided();}
    public void SaveReplay(int slot,string title,bool useForRace){
        if(pending==null || State!=Phase.Results)return;
        shelf.Save(pending,pendingFrames,title,slot,useForRace);SkipSave();
    }
    public void UseReplay(int slot){
        if(State!=Phase.Lobby)return;
        shelf.SetActive(slot);LoadOpponent();
    }
    void LoadOpponent(){
        if(personal)Destroy(personal);
        personal=shelf?.Active?.Open(SelectedCar,panel.ghostMaterial);
    }
    public RaceCar[] entries;
    public SessionPanel panel;
    [UnityEngine.Serialization.FormerlySerializedAs("motionBlurByKph")]
    public AnimationCurve speedEffectsByKph=new AnimationCurve(
        new Keyframe(35,0,0,0),new Keyframe(80,.27f,.007f,.007f),
        new Keyframe(120,.55f,.0058f,.0058f),new Keyframe(180,.78f,.0026f,.0026f),
        new Keyframe(260,.92f,.0011f,.0011f),new Keyframe(360,1,0,0));
    [Range(0,40)] public float speedFovGain=24;
    [Min(.01f)] public float speedViewResponseSeconds=.18f;
    public UnityEngine.Rendering.VolumeProfile motionBlurProfile;
    public Material airflowMaterial;
    public AnimationCurve airflowByKph=AnimationCurve.EaseInOut(90,0,180,1);
    [Tooltip("相对后轴的纵向位置、相对车身原点的高度（米）")]
    public Vector2 airflowOffset=new Vector2(-.25f,.92f);
    [Min(.01f)] public float airflowLifetime=.13f,airflowWidth=.11f;
    BodyAirflow airflow;
    SpeedMotionBlur speedBlur;
    float baseFov,speedEffect;
    Showroom showroom;
    WorkshopMounts previewMounts;
    bool workshop,axle=true;
    int candidate;
    WorkshopCatalog Catalog=>panel.catalog;
    CarSpec SelectedCar=>entries[choice].spec;
    public DrivingSounds sounds;
    public TyreMarkField tyreMarkPrefab;
    AudioSource signals,wind;
    int shownSecond;
    public enum Phase {Loading,Lobby,Countdown,Driving,Results}
    public Phase State {get;private set;}
    public RaceCar Car {get;private set;}
    Circuit circuit; Camera chase; GameObject preview; int choice,nextGate,lap;
    float countdown,elapsed,lapStart,best,last; bool invalid,practice;
    Vector3 previous;
    public bool showDriveMonitor;
    [Min(.05f)] public float hudInterval=.1f;
    float nextHud;
    public int CurrentLap=>lap;
    public int NextCheckpoint=>nextGate+1;
    IEnumerator Start(){
        PureGrid=PlayerPrefs.GetInt(PureGridKey,1)!=0;
        skin=Mathf.Clamp(PlayerPrefs.GetInt("Apex.Garage.Livery",0),0,gridSettings.liveries.Length-1);
        var saved=PlayerPrefs.GetString("Apex.Garage.Car","");
        choice=System.Array.FindIndex(entries,e=>e.spec.name==saved);
        if(choice<0)choice=Mathf.Max(0,System.Array.FindIndex(entries,e=>e.spec.competition));
        if(sounds){signals=gameObject.AddComponent<AudioSource>();signals.playOnAwake=false;}
        panel.Connect(this);yield return Switch(false);
    }
    void StartSignal(){
        int second=Mathf.Max(0,Mathf.CeilToInt(countdown));
        if(second==shownSecond)return;
        shownSecond=second;
        panel.Countdown(second);
        if(signals)PlaySignal(second==0?sounds.go:sounds.ready);
    }
    void PlaySignal(AudioClip clip){
        if(clip)signals.PlayOneShot(clip,sounds.signalGain);
    }
    public void Primary(){
        if(State==Phase.Lobby || State==Phase.Results)StartCoroutine(Switch(true));
    }
    public void Secondary(){
        if(State==Phase.Lobby){if(entries.Length<2)return;choice=(choice+1)%entries.Length;PlayerPrefs.SetString("Apex.Garage.Car",SelectedCar.name);PlayerPrefs.Save();ShowCar();}
        else if(State!=Phase.Loading)StartCoroutine(Switch(false));
    }
    IEnumerator Switch(bool race){
        pending=null;pendingFrames=null;
        Grid=null;
        cameraKick=appliedKick=Vector3.zero;
        State=Phase.Loading;panel.Loading();
        if(signals)signals.Stop();
        var boot=gameObject.scene; SceneManager.SetActiveScene(boot);
        foreach(var name in new[]{LoadedTrack,lobbyScene}){
            var scene=SceneManager.GetSceneByName(name);
            if(scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
        var dest=race?LoadedTrack:lobbyScene;
        yield return SceneManager.LoadSceneAsync(dest,LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(dest));
        if(!race){
            Car=null;ghost=null;workshop=false;State=Phase.Lobby;ShowCar();yield break;
        }
        circuit=FindAnyObjectByType<Circuit>(); chase=Camera.main;
        speedEffect=0;
        if(chase)baseFov=chase.fieldOfView;
        if(chase && motionBlurProfile){speedBlur=chase.gameObject.AddComponent<SpeedMotionBlur>();speedBlur.Bind(chase,motionBlurProfile);}
        if(sounds && sounds.wind && chase){
            wind=chase.gameObject.AddComponent<AudioSource>();
            wind.playOnAwake=false;wind.loop=true;wind.clip=sounds.wind;wind.volume=0;wind.Play();
        }
        var marks=tyreMarkPrefab?Instantiate(tyreMarkPrefab,Vector3.zero,Quaternion.identity):null;
        if(RacingGrid){
            Grid=new GridRace(circuit,tracks!=null && tracks.Length>0?tracks[trackIndex].layout:null,gridSettings,entries[choice],
                entries,PureGrid,skin,Catalog,marks,sounds);
            Car=Grid.Player.car;
            foreach(var e in Grid.entries)e.car.CarContactImpulse+=OnContactImpulse;
        }else{
            Car=Instantiate(entries[choice],circuit.grid.position,circuit.grid.rotation);
            if(SelectedCar.competition)GarageLivery.Apply(Car.gameObject,skin==0);
            if(Catalog)Car.FitParts(Installed(true),Installed(false));
            if(marks)marks.Track(Car);
            VehicleAudio.Attach(Car,sounds);
        }
        if(airflowMaterial){
            airflow=Car.gameObject.AddComponent<BodyAirflow>();
            airflow.Bind(Car,airflowMaterial,airflowOffset,airflowLifetime);
        }
        Car.Driver=new LocalDriver(Car,panel.AdvancedControls,panel.KeyboardRecovery);
        Car.gameObject.AddComponent<DriveTelemetry>().Begin(this,Car,LoadedTrack);
        previous=Car.transform.position;nextGate=1;lap=1;elapsed=lapStart=last=nextHud=0;best=float.PositiveInfinity;invalid=practice=false;
        countdown=circuit.startDelay;State=Phase.Countdown;
        if(Grid==null)Car.gameObject.AddComponent<PlayerGhostCapture>().Begin(this,RecordingTrack);
        ghost=personal && Grid==null?RaceGhost.Spawn(personal,circuit.transform):null;
        greenLight=Time.fixedTimeAsDouble;
        var paint=Grid!=null?Grid.Player.livery.paint:SelectedCar.competition?GarageLivery.paint:SelectedCar.FactoryColor;
        panel.Drive(Car,circuit,tracks!=null && tracks.Length>0?tracks[trackIndex].layout:null,TrackName,paint);
        panel.BindOpponent(ghost);
        panel.map.Field=Grid;
        UpdateHud();
        shownSecond=-1;
        StartSignal();
        Follow(true);
    }
    void ShowCar(){
        if(preview){preview.SetActive(false);Destroy(preview);}
        showroom=FindAnyObjectByType<Showroom>();
        preview=Instantiate(SelectedCar.body,showroom.display);
        if(SelectedCar.competition)GarageLivery.Apply(preview,skin==0);
        previewMounts=preview.GetComponent<WorkshopMounts>();
        workshop=false;
        RefreshGarage();
    }
    GridLivery GarageLivery=>gridSettings.liveries[skin].ForCar(SelectedCar,skin==0);
    WorkshopCatalog.Part Installed(bool diff)=>Catalog.InstalledPart(SelectedCar,diff);
    public void OpenWorkshop(){if(State!=Phase.Lobby || !Catalog)return;workshop=true;SelectSlot(true);}
    public void SelectSlot(bool diff){
        if(!workshop)return;
        axle=diff;candidate=Catalog.Installed(SelectedCar,axle);PreviewPart();
    }
    public void BrowsePart(int step){
        if(!workshop)return;
        int count=Catalog.Options(axle).Length;
        candidate=(candidate+step+count)%count;PreviewPart();
    }
    public void InstallPart(){
        if(!workshop)return;
        Catalog.Install(SelectedCar,axle,candidate);PreviewPart();
    }
    public void CloseWorkshop(){workshop=false;RefreshGarage();}
    void PreviewPart(){
        var options=Catalog.Options(axle);
        var part=options[candidate];
        if(previewMounts)previewMounts.Fit(axle?Installed(false):part);
        panel.ShowPart(SelectedCar,axle,part,Installed(axle),candidate,options.Length);
        showroom.Present(true,axle,part,panel.workshopCard);
    }
    void RefreshGarage(){
        panel.Garage(entries[choice].spec,TrackName);
        if(Catalog){
            if(previewMounts)previewMounts.Fit(Installed(false));
            panel.garageKeys.text=Installed(true).title+"  /  "+Installed(false).title;
        }
        showroom.Present(false,false,null,panel.garageCard);
        shelf=RacingGrid?null:new GhostShelf(RecordingTrack,SelectedCar.name);
        LoadOpponent();panel.replays.Garage(shelf!=null && shelf.At(0)!=null);
        panel.GridGarage(SelectedCar.competition,RacingGrid,PureGrid,GarageLivery);

    }
    void FixedUpdate(){
        if(State==Phase.Countdown){
            countdown-=Time.fixedDeltaTime;StartSignal();
            if(countdown<=0){Car.EnabledDrive=true;State=Phase.Driving;previous=Car.transform.position;greenLight=Time.fixedTimeAsDouble;}
            if(State==Phase.Driving)Grid?.Green();
            return;
        }
        if(State!=Phase.Driving)return;
        elapsed=(float)(Time.fixedTimeAsDouble-greenLight);var current=Car.transform.position;
        if(Grid!=null){
            if(!Grid.Player.Finished && current.y<circuit.resetHeight)ResetCar();
            bool ended=Grid.Step(elapsed,Time.fixedDeltaTime);
            lap=Grid.Lap(Grid.Player);nextGate=Grid.Player.next;
            if(ended){
                State=Phase.Results;Grid.Stop();panel.GridFinish(Grid);
                if(signals)PlaySignal(sounds.finish);
                return;
            }
            if(elapsed>1)panel.ClearSignal();
            if(elapsed>=nextHud){nextHud=elapsed+hudInterval;UpdateHud();}
            return;
        }
        if(current.y<circuit.resetHeight){ResetCar();return;}
        if(circuit.Crossed(nextGate,previous,current)){
            if(nextGate==0){
                last=elapsed-lapStart;lapStart=elapsed;if(!invalid)best=Mathf.Min(best,last);invalid=false;
                if(lap==circuit.laps){
                    State=Phase.Results;Car.EnabledDrive=false;panel.Finish(elapsed,best,practice);
                    if(signals)PlaySignal(sounds.finish);
                    return;
                }
                lap++;
            }
            nextGate=(nextGate+1)%circuit.gates.Length;
        }
        previous=current;
        if(elapsed>1)panel.ClearSignal();
        if(elapsed<nextHud)return;
        nextHud=elapsed+hudInterval;UpdateHud();
    }
    void UpdateHud(){
        if(Grid!=null){
            var e=Grid.Watching;
            panel.Instruments(e.car,circuit,Grid.Lap(e),e.next,elapsed,0,0,float.PositiveInfinity,false);
            panel.GridHud(Grid,elapsed);
            return;
        }
        panel.Instruments(Car,circuit,lap,nextGate,elapsed,lapStart,last,best,showDriveMonitor);
    }
    void ResetCar(){
        var t=circuit.gates[(nextGate+circuit.gates.Length-1)%circuit.gates.Length];
        Car.ResetDrift();Car.Chassis.position=t.position+Vector3.up*.6f;Car.Chassis.rotation=t.rotation;
        Car.Chassis.linearVelocity=Car.Chassis.angularVelocity=Vector3.zero;previous=Car.Chassis.position;invalid=practice=true;
        speedEffect=0;
        Grid?.ResetPlayer();
    }
    void Update(){
        if(State!=Phase.Driving)return;
        if(Keyboard.current?.f7Key.wasPressedThisFrame==true)showDriveMonitor=!showDriveMonitor;
        if(Keyboard.current?.backspaceKey.wasPressedThisFrame==true && !(Grid?.Player.Finished??false))ResetCar();
    }
    void LateUpdate(){
        if(!Car || !chase || State==Phase.Loading)return;
        if(ghost && (State==Phase.Driving || State==Phase.Countdown))
            ghost.Sample(State==Phase.Countdown?0:(float)(Time.timeAsDouble-greenLight),Car.transform);
        panel.ShowKeys(GhostArchive.Keys(Car.AppliedInput));
        float kph=(Grid?.Watching.car??Car).Speed*3.6f;
        bool moving=State==Phase.Driving;
        bool speedView=moving && panel.SpeedEffectsEnabled;
        if(speedView){
            float target=Mathf.Clamp01(speedEffectsByKph.Evaluate(kph));
            float blend=1-Mathf.Exp(-Time.deltaTime/Mathf.Max(.01f,speedViewResponseSeconds));
            speedEffect=Mathf.Lerp(speedEffect,target,blend);
            if(target==0 && speedEffect<.001f)speedEffect=0;
        }else speedEffect=0;
        chase.fieldOfView=Mathf.Min(160,baseFov+speedFovGain*speedEffect);
        if(speedBlur)speedBlur.SetStrength(speedEffect);
        if(airflow)airflow.SetStrength(speedView?airflowByKph.Evaluate(Car.Speed*3.6f):0,airflowWidth);
        if(wind){
            float strength=moving?Mathf.Clamp01(sounds.windByKph.Evaluate(kph)):0;
            float blend=1-Mathf.Exp(-Time.deltaTime/sounds.windResponseSeconds);
            wind.volume=Mathf.Lerp(wind.volume,sounds.windGain*strength,blend);
            wind.pitch=Mathf.Lerp(sounds.windPitch.x,sounds.windPitch.y,strength);
        }
        Follow(false);
    }
    void OnDestroy(){if(personal)Destroy(personal);}
    void OnContactImpulse(RaceCar car){
        if(Grid==null || Grid.Watching.car!=car || State!=Phase.Driving)return;
        float delta=car.LastCarImpulse.magnitude/car.Chassis.mass;
        float strength=Mathf.InverseLerp(gridSettings.cameraImpactRange.x,gridSettings.cameraImpactRange.y,delta);
        float distance=strength*gridSettings.cameraImpactTravel;
        if(distance<=cameraKick.magnitude)return;
        var inward=Vector3.ProjectOnPlane(car.Chassis.worldCenterOfMass-car.LastCarContactPoint,Vector3.up);
        cameraKick=-inward.normalized*distance;
    }
    void Follow(bool snap){
        var subject=Grid?.Watching.car??Car;
        var t=subject.transform;
        var spec=subject.spec;
        var direction=t.rotation;
        var p=t.position+direction*spec.cameraOffset;
        chase.transform.position=snap?p:Vector3.Lerp(chase.transform.position-appliedKick,p,1-Mathf.Exp(-spec.cameraResponse*Time.deltaTime));
        appliedKick=cameraKick;chase.transform.position+=appliedKick;
        cameraKick*=Mathf.Exp(-Time.deltaTime/Mathf.Max(.01f,gridSettings.cameraImpactDecay));
        var aim=t.position+direction*Vector3.forward*spec.lookAhead+Vector3.up-chase.transform.position;
        if(aim.sqrMagnitude>.001f){
            var rot=Quaternion.LookRotation(aim);
            chase.transform.rotation=Grid!=null && Grid.Player.Finished && !snap?
                Quaternion.Slerp(chase.transform.rotation,rot,1-Mathf.Exp(-spec.cameraResponse*Time.deltaTime)):rot;
        }
    }
}
}
