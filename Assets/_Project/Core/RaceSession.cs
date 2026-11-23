using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
namespace Apex {
public class RaceSession:MonoBehaviour {
    public string lobbyScene="Lobby",trackScene="Circuit";
    public TrackChoice[] tracks;
    [System.Serializable] public class TrackChoice { public string title,scene; public bool freeDrive; public RoadLayout layout; }
    int trackIndex;
    string LoadedTrack=>tracks!=null && tracks.Length>0?tracks[trackIndex].scene:trackScene;
    bool FreeDrive=>tracks!=null && tracks.Length>0 && tracks[trackIndex].freeDrive;
    public string defaultTrackTitle="钴蓝椭圆赛道";
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
    public AnimationCurve motionBlurByKph=AnimationCurve.EaseInOut(85,0,210,.58f);
    public UnityEngine.Rendering.VolumeProfile motionBlurProfile;
    SpeedMotionBlur speedBlur;
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
    Circuit circuit; DrivingSchool school; Camera chase; GameObject preview; int choice,nextGate,lap;
    float countdown,elapsed,lapStart,best,last; bool invalid,practice;
    Vector3 previous;
    public bool showDriveMonitor;
    [Min(.05f)] public float hudInterval=.1f;
    float nextHud;
    public int CurrentLap=>lap;
    public int NextCheckpoint=>school?school.NextGate+1:FreeDrive?0:nextGate+1;
    IEnumerator Start(){
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
        if(State==Phase.Lobby){choice=(choice+1)%entries.Length;PlayerPrefs.SetString("Apex.Garage.Car",SelectedCar.name);PlayerPrefs.Save();ShowCar();}
        else if(State!=Phase.Loading)StartCoroutine(Switch(false));
    }
    IEnumerator Switch(bool race){
        pending=null;pendingFrames=null;
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
        if(chase && motionBlurProfile){speedBlur=chase.gameObject.AddComponent<SpeedMotionBlur>();speedBlur.Bind(chase,motionBlurProfile);}
        if(sounds && sounds.wind && chase){
            wind=chase.gameObject.AddComponent<AudioSource>();
            wind.playOnAwake=false;wind.loop=true;wind.clip=sounds.wind;wind.volume=0;wind.Play();
        }
        Car=Instantiate(entries[choice],circuit.grid.position,circuit.grid.rotation);
        Car.Driver=new LocalDriver(Car,panel.AdvancedControls,panel.KeyboardRecovery);
        if(Catalog)Car.FitParts(Installed(true),Installed(false));
        var marks=tyreMarkPrefab?Instantiate(tyreMarkPrefab,Vector3.zero,Quaternion.identity):null;
        if(marks)marks.Track(Car);
        VehicleAudio.Attach(Car,sounds);
        school=circuit.GetComponent<DrivingSchool>();
        if(school)school.Begin(Car,entries[(choice+1)%entries.Length],sounds,marks);
        Car.gameObject.AddComponent<DriveTelemetry>().Begin(this,Car,LoadedTrack);
        previous=Car.transform.position;nextGate=1;lap=1;elapsed=lapStart=last=nextHud=0;best=float.PositiveInfinity;invalid=practice=false;
        countdown=circuit.startDelay;State=Phase.Countdown;
        if(!FreeDrive)Car.gameObject.AddComponent<PlayerGhostCapture>().Begin(this,LoadedTrack);
        ghost=personal?RaceGhost.Spawn(personal,circuit.transform):null;
        greenLight=Time.fixedTimeAsDouble;
        if(FreeDrive){Car.EnabledDrive=true;State=Phase.Driving;lap=0;}
        panel.Drive(Car,circuit,school,tracks!=null && tracks.Length>0?tracks[trackIndex].layout:null,TrackName,FreeDrive);
        panel.BindOpponent(ghost);
        UpdateHud();
        shownSecond=-1;
        if(!FreeDrive)StartSignal();
        Follow(true);
    }
    void ShowCar(){
        if(preview){preview.SetActive(false);Destroy(preview);}
        showroom=FindAnyObjectByType<Showroom>();
        preview=Instantiate(SelectedCar.body,showroom.display);
        previewMounts=preview.GetComponent<WorkshopMounts>();
        workshop=false;
        RefreshGarage();
    }
    WorkshopCatalog.Part Installed(bool diff)=>Catalog.Options(diff)[Catalog.Installed(SelectedCar,diff)];
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
        panel.Garage(entries[choice].spec,TrackName,FreeDrive,LoadedTrack=="DrivingSchool");
        if(Catalog){
            if(previewMounts)previewMounts.Fit(Installed(false));
            panel.garageKeys.text=Installed(true).title+"  /  "+Installed(false).title;
        }
        showroom.Present(false,false,null,panel.garageCard);
        shelf=FreeDrive?null:new GhostShelf(LoadedTrack,SelectedCar.name);
        LoadOpponent();panel.replays.Garage(shelf!=null && shelf.At(0)!=null);

    }
    void FixedUpdate(){
        if(State==Phase.Countdown){
            countdown-=Time.fixedDeltaTime;StartSignal();
            if(countdown<=0){Car.EnabledDrive=true;State=Phase.Driving;previous=Car.transform.position;greenLight=Time.fixedTimeAsDouble;}
            return;
        }
        if(State!=Phase.Driving)return;
        elapsed=(float)(Time.fixedTimeAsDouble-greenLight);var current=Car.transform.position;
        if(current.y<circuit.resetHeight){ResetCar();return;}
        if(!FreeDrive && circuit.Crossed(nextGate,previous,current)){
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
        if(school)school.Sample(previous,current,Time.fixedDeltaTime);
        previous=current;
        if(elapsed>1)panel.ClearSignal();
        if(elapsed<nextHud)return;
        nextHud=elapsed+hudInterval;UpdateHud();
    }
    void UpdateHud(){
        panel.Instruments(Car,circuit,school,FreeDrive,lap,nextGate,elapsed,lapStart,last,best,showDriveMonitor);
    }
    void ResetCar(){
        if(school)school.RestartAttempt();
        var t=FreeDrive?circuit.grid:circuit.gates[(nextGate+circuit.gates.Length-1)%circuit.gates.Length];
        Car.ResetDrift();Car.Chassis.position=t.position+Vector3.up*.6f;Car.Chassis.rotation=t.rotation;
        Car.Chassis.linearVelocity=Car.Chassis.angularVelocity=Vector3.zero;previous=Car.Chassis.position;invalid=practice=true;
    }
    void Update(){
        if(State!=Phase.Driving)return;
        if(Keyboard.current?.f7Key.wasPressedThisFrame==true)showDriveMonitor=!showDriveMonitor;
        if(Keyboard.current?.backspaceKey.wasPressedThisFrame==true)ResetCar();
        if(school && Keyboard.current!=null){
            var keys=Keyboard.current;
            if(keys.tKey.wasPressedThisFrame){school.NextLesson();ResetCar();}
            if(keys.cKey.wasPressedThisFrame)school.Coach=!school.Coach;
            if(keys.yKey.wasPressedThisFrame)school.NextStyle();
        }
    }
    void LateUpdate(){
        if(!Car || !chase || State==Phase.Loading)return;
        if(ghost && (State==Phase.Driving || State==Phase.Countdown))
            ghost.Sample(State==Phase.Countdown?0:(float)(Time.timeAsDouble-greenLight),Car.transform);
        panel.ShowKeys(GhostArchive.Keys(Car.AppliedInput));
        float kph=Car.Speed*3.6f;
        bool moving=State==Phase.Driving;
        if(speedBlur)speedBlur.SetStrength(moving && panel.BlurEnabled?motionBlurByKph.Evaluate(kph):0);
        if(wind){
            float strength=moving?Mathf.Clamp01(sounds.windByKph.Evaluate(kph)):0;
            float blend=1-Mathf.Exp(-Time.deltaTime/sounds.windResponseSeconds);
            wind.volume=Mathf.Lerp(wind.volume,sounds.windGain*strength,blend);
            wind.pitch=Mathf.Lerp(sounds.windPitch.x,sounds.windPitch.y,strength);
        }
        Follow(false);
    }
    void OnDestroy(){if(personal)Destroy(personal);}
    void Follow(bool snap){
        var t=Car.transform;
        var spec=Car.spec;
        var direction=t.rotation;
        var p=t.position+direction*spec.cameraOffset;
        chase.transform.position=snap?p:Vector3.Lerp(chase.transform.position,p,1-Mathf.Exp(-spec.cameraResponse*Time.deltaTime));
        chase.transform.LookAt(t.position+direction*Vector3.forward*spec.lookAhead+Vector3.up);
    }
}
}
