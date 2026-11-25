using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Apex {
public sealed class SessionPanel : MonoBehaviour {
    public GameObject garage,hud,loading,results,startSignal,monitor,coach;
    public RectTransform garageCard;
    public WorkshopCatalog catalog;
    public GameObject workshop;
    public RectTransform workshopCard;
    public Button openWorkshop,closeWorkshop,chooseAxle,chooseWheels,previousPart,nextPart,installPart,blurToggle;
    public Text partCar,partName,partNumbers,partInfo,fittedName,installLabel,blurLabel;
    const string BlurKey="Apex.SpeedMotionBlur";
    public bool SpeedEffectsEnabled {get;private set;}
    public GameObject settings;
    public Button openSettings,backFromSettings,toggleAdvanced;
    public Text advancedLabel,advancedKeys;
    public Button toggleRecovery;
    public Text recoveryLabel;
    const string RecoveryKey="Apex.KeyboardRecovery";
    public bool KeyboardRecovery {get;private set;}
    public Button cycleVolume;
    public Text volumeLabel;
    public Button toggleMapView;
    public Text mapViewLabel,mapOrientation;
    const string MapViewKey="Apex.Minimap.Follow";
    const string VolumeKey="Apex.SfxVolumePercent";
    int volumePercent;
    const string ControlsKey="Apex.AdvancedDrivingControls";
    public bool AdvancedControls {get;private set;}
    public Button launch,changeCar,track,leave,retry,returnFromResults;
    public ReplayMenu replays;
    public Material ghostMaterial;
    public Image[] keyCaps;
    int lastKeys=-1;
    Button gridMode,liveryButton,gridRules;
    Text modeChoice,liveryChoice,rulesChoice,standings;
    GameObject timeCaption,bestCaption;
    public Text carName,carData,location,garageKeys,launchLabel;
    public Text mapTitle,mapKey,mode,sessionTitle,sessionData,lapData;
    public Text speed,gear,rpm,rpmLabel,driveStatus,signal,signalCaption,advice,driveKeys,diagnostics;
    public Text resultTitle,resultTime,resultBest;
    public Image revFill;
    public RaceMap map;
    public TyreTemperaturePanel tyreTemperatures;
    Rect lastSafe;
    Vector2Int lastScreen;
    public Button primary => results.activeSelf?retry:launch;
    public Button secondary => results.activeSelf?returnFromResults:garage.activeSelf?changeCar:leave;
    public void Connect(RaceSession session){
        changeCar.gameObject.SetActive(session.entries.Length>1);
        gridMode=GarageButton("比赛模式",session.ToggleGrid,out modeChoice);
        gridRules=GarageButton("四车赛抽选规则",session.ToggleGridRules,out rulesChoice);
        liveryButton=GarageButton("选择车衣",session.NextLivery,out liveryChoice);
        var card=resultTitle.transform.parent;
        timeCaption=card.Find("总用时标签").gameObject;bestCaption=card.Find("最快圈标签").gameObject;
        standings=Instantiate(resultTime,card);
        standings.name="竞速排名";standings.fontSize=21;standings.alignment=TextAnchor.MiddleLeft;
        standings.rectTransform.pivot=new Vector2(0,1);standings.rectTransform.anchoredPosition=new Vector2(25,-88);
        standings.rectTransform.sizeDelta=new Vector2(390,152);standings.gameObject.SetActive(false);
        if(openWorkshop){
            openWorkshop.onClick.AddListener(()=>{session.OpenWorkshop();Select(chooseAxle);});
            closeWorkshop.onClick.AddListener(()=>{session.CloseWorkshop();Select(openWorkshop);});
            chooseAxle.onClick.AddListener(()=>session.SelectSlot(true));
            chooseWheels.onClick.AddListener(()=>session.SelectSlot(false));
            previousPart.onClick.AddListener(()=>session.BrowsePart(-1));nextPart.onClick.AddListener(()=>session.BrowsePart(1));
            installPart.onClick.AddListener(()=>{session.InstallPart();Select(nextPart);});
        }
        SpeedEffectsEnabled=PlayerPrefs.GetInt(BlurKey,1)!=0;
        if(blurToggle){
            RefreshBlur();
            blurToggle.onClick.AddListener(()=>{
                SpeedEffectsEnabled=!SpeedEffectsEnabled;PlayerPrefs.SetInt(BlurKey,SpeedEffectsEnabled?1:0);PlayerPrefs.Save();RefreshBlur();
            });
        }
        launch.onClick.AddListener(session.Primary);retry.onClick.AddListener(session.Primary);
        changeCar.onClick.AddListener(session.Secondary);leave.onClick.AddListener(session.Secondary);
        returnFromResults.onClick.AddListener(session.Secondary);track.onClick.AddListener(session.NextTrack);
        replays.Connect(session,this);
        AdvancedControls=PlayerPrefs.GetInt(ControlsKey,0)!=0;
        KeyboardRecovery=PlayerPrefs.GetInt(RecoveryKey,1)!=0;
        if(toggleRecovery)toggleRecovery.onClick.AddListener(()=>{
            KeyboardRecovery=!KeyboardRecovery;PlayerPrefs.SetInt(RecoveryKey,KeyboardRecovery?1:0);
            PlayerPrefs.Save();UpdateControls();
        });
        volumePercent=Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey,75),0,100);
        ApplyVolume();
        SetMapView(PlayerPrefs.GetInt(MapViewKey,1)!=0);
        toggleMapView.onClick.AddListener(()=>{
            SetMapView(!map.FollowCar);
            PlayerPrefs.SetInt(MapViewKey,map.FollowCar?1:0);PlayerPrefs.Save();
        });
        cycleVolume.onClick.AddListener(()=>{
            volumePercent=volumePercent==0?100:Mathf.Max(0,volumePercent-25);
            ApplyVolume();PlayerPrefs.SetInt(VolumeKey,volumePercent);PlayerPrefs.Save();
        });
        openSettings.onClick.AddListener(()=>{Show(settings);UpdateControls();Select(toggleAdvanced);});
        toggleAdvanced.onClick.AddListener(()=>{
            AdvancedControls=!AdvancedControls;
            PlayerPrefs.SetInt(ControlsKey,AdvancedControls?1:0);PlayerPrefs.Save();UpdateControls();
        });
        backFromSettings.onClick.AddListener(()=>{Show(garage);Select(openSettings);});
        FitScreen();
    }
    Button GarageButton(string title,UnityEngine.Events.UnityAction action,out Text label){
        var b=Instantiate(track,garageCard);b.name=title;
        b.onClick=new Button.ButtonClickedEvent();b.onClick.AddListener(action);
        label=b.GetComponentInChildren<Text>();label.fontSize=15;return b;
    }
    public void GridGarage(bool supportsSkin,bool grid,bool pure,GridLivery skin){
        liveryButton.gameObject.SetActive(supportsSkin);
        gridRules.gameObject.SetActive(grid);
        liveryButton.interactable=!grid || pure;
        bool ghostRow=replays.open.gameObject.activeSelf;
        float y=ghostRow?-368:-324;
        void Place(Button b,float x,float width){
            var r=(RectTransform)b.transform;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,36);
        }
        if(changeCar.gameObject.activeSelf){
            Place(gridMode,20,supportsSkin?148:314);Place(liveryButton,178,156);
        }else{
            var slot=(RectTransform)changeCar.transform;
            var button=(RectTransform)liveryButton.transform;
            button.anchoredPosition=slot.anchoredPosition;button.sizeDelta=slot.sizeDelta;
            Place(gridMode,20,314);
        }
        if(grid){
            y-=44;Place(gridRules,20,314);
            rulesChoice.text=pure?"车辆规则 · 纯净":"车辆规则 · 全随机";
            garageKeys.text="本车装配　"+garageKeys.text;
            carData.text=pure?"全场同车型 · 仅对手随机配件":"车型随机 · 玩家沿用抽中车型的装配";
        }
        garageCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,(ghostRow?492:448)+(grid?44:0));
        modeChoice.text=grid?"模式 · 四车竞速":"模式 · 计时赛";
        liveryChoice.text=grid && !pure?"车衣 · 随机":"车衣 · "+skin.title;
        if(grid)launchLabel.text="开始四车竞速";
    }
    public void GridHud(GridRace race,float elapsed){
        var e=race.Watching;bool spectator=race.Player.Finished;
        mode.text=spectator?"观战　"+e.Name:"四车竞速 · "+e.car.spec.displayName;
        sessionTitle.text=float.IsInfinity(race.Deadline)?Clock(elapsed):$"收车　{Mathf.Max(0,race.Deadline-elapsed):F1} 秒";
        sessionData.text=$"第 {System.Array.IndexOf(race.order,e)+1} / 4 名　　 第 {race.Lap(e)} / {race.course.laps} 圈";
        var o=race.order;
        lapData.text=$"1　{o[0].Name}　　 2　{o[1].Name}\n3　{o[2].Name}　　 4　{o[3].Name}";
        mapKey.text=spectator?"箭头 自车　圆点 对手（观战中）":"描边箭头 自车　圆点 对手";
        if(spectator)driveKeys.text="冲线后观战";
    }
    void ResultLayout(bool grid){
        timeCaption.SetActive(!grid);bestCaption.SetActive(!grid);resultTime.gameObject.SetActive(!grid);
        resultBest.gameObject.SetActive(!grid);standings.gameObject.SetActive(grid);
    }
    public void GridFinish(GridRace race){
        Show(results);replays.Decided();ResultLayout(true);
        resultTitle.text=$"四车竞速　你排第 {System.Array.IndexOf(race.order,race.Player)+1}";
        var rows=new string[race.order.Length];
        for(int i=0;i<rows.Length;i++){
            var e=race.order[i];string time=e.Finished?Clock(e.finish):$"未完赛 · 第{race.Lap(e)}圈";
            rows[i]=$"{i+1}　{e.Name}　　 {time}";
        }
        standings.text=string.Join("\n",rows);Select(retry);
    }
    static void Select(Button button){
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
    void RefreshBlur()=>blurLabel.text="广角、动态模糊与气流　"+(SpeedEffectsEnabled?"开启":"关闭");
    public void ShowPart(CarSpec spec,bool axle,WorkshopCatalog.Part part,WorkshopCatalog.Part fitted,int index,int total){
        Show(workshop);partCar.text=spec.displayName;partName.text=part.title;
        chooseAxle.GetComponentInChildren<Text>().text=spec.frontWheelDrive?"前桥差速器":"后桥差速器";
        partNumbers.text=(index+1).ToString("00")+"  /  "+total.ToString("00");
        partInfo.text=part.description;fittedName.text="车上已装　"+fitted.title;
        chooseAxle.targetGraphic.color=axle?new Color(.20f,.42f,.39f):new Color(.13f,.20f,.23f);
        chooseWheels.targetGraphic.color=!axle?new Color(.20f,.42f,.39f):new Color(.13f,.20f,.23f);
        installPart.interactable=part!=fitted;installLabel.text=part==fitted?"已装车":"安装到本车";
    }
    void ApplyVolume(){
        AudioListener.volume=volumePercent*.01f;
        volumeLabel.text=volumePercent==0?"音效音量　静音":"音效音量　"+volumePercent+"%";
    }
    void SetMapView(bool follow){
        map.SetFollow(follow);
        mapTitle.rectTransform.parent.localScale=Vector3.one*(follow?1.14f:1f);
        mapViewLabel.text=follow?"小地图　跟车 · 车头向上":"小地图　全图 · 北向上";
        mapOrientation.text=follow?"车头向上":"北向上";
    }
    void UpdateControls(){
        if(recoveryLabel)recoveryLabel.text=KeyboardRecovery?"键盘纠偏辅助　已开启":"键盘纠偏辅助　已关闭";
        advancedLabel.text=AdvancedControls?"高级驾驶操作　已开启":"高级驾驶操作　已关闭";
        advancedKeys.text=(AdvancedControls?"竞赛车：Q / E 降升挡，左 Ctrl 离合，X 恢复自动。":"自动换挡、自动离合。驾驶只需 WASD 和空格。")+"\n退格复位　F7 遥测　F8 标记";
    }
    void Update(){
        if(Screen.width!=lastScreen.x || Screen.height!=lastScreen.y || Screen.safeArea!=lastSafe)FitScreen();
    }
    void FitScreen(){
        if(Screen.width<1 || Screen.height<1)return;
        lastSafe=Screen.safeArea;lastScreen=new Vector2Int(Screen.width,Screen.height);
        var r=(RectTransform)transform;
        r.anchorMin=lastSafe.min/(Vector2)lastScreen;r.anchorMax=lastSafe.max/(Vector2)lastScreen;
        r.offsetMin=r.offsetMax=Vector2.zero;
        bool narrow=lastSafe.width<lastSafe.height;
        garageCard.anchorMin=garageCard.anchorMax=garageCard.pivot=narrow?new Vector2(.5f,0):new Vector2(0,.5f);
        garageCard.anchoredPosition=narrow?new Vector2(0,32):new Vector2(32,-28);
        if(workshopCard){
            workshopCard.anchorMin=workshopCard.anchorMax=workshopCard.pivot=garageCard.pivot;
            workshopCard.anchoredPosition=garageCard.anchoredPosition;
        }
        ((RectTransform)coach.transform).anchoredPosition=new Vector2(0,narrow?272:28);
        var neighbour=narrow?sessionTitle.rectTransform.parent:speed.rectTransform.parent;
        tyreTemperatures.Dock((RectTransform)neighbour,narrow);
    }
    void Show(GameObject screen){
        replays.Hide();
        if(workshop)workshop.SetActive(screen==workshop);
        settings.SetActive(screen==settings);garage.SetActive(screen==garage);hud.SetActive(screen==hud);loading.SetActive(screen==loading);results.SetActive(screen==results);
    }
    public void Loading()=>Show(loading);
    public void ShowKeys(int mask){
        mask&=31;
        if(mask==lastKeys || keyCaps==null || keyCaps.Length!=5)return;
        lastKeys=mask;
        for(int i=0;i<keyCaps.Length;i++)
            keyCaps[i].color=(mask&(1<<i))!=0?new Color32(24,136,109,255):new Color32(28,43,49,239);
    }
    public void BindOpponent(RaceGhost opponent){
        map.Ghost=opponent;
        if(opponent)mapKey.text="箭头 自车　圆点 "+opponent.Recording.title;
    }
    public void Garage(CarSpec spec,string course){
        bool entering=!garage.activeSelf;
        Show(garage);
        if(entering && EventSystem.current)EventSystem.current.SetSelectedGameObject(launch.gameObject);
        carName.text=spec.displayName;
        carData.text=spec.frontWheelDrive?"前轮驱动　/　株洲 2024 · 2.0T 六速序列":
            spec.competition?"后轮驱动　/　奥伊吹 2024 · 估算动力曲线":"后轮驱动　/　原型赛车 · 漂移短喷";
        location.text=course;
        garageKeys.text="W 给油　 S 刹车／倒车　 A / D 转向\n空格 手刹"+(spec.competition?"":"　 漂移出弯自动短喷");
        launchLabel.text="开始计时赛";
    }
    public void Drive(RaceCar car,Circuit c,RoadLayout layout,string name,Color paint){
        map.Bind(car,c,layout,paint);mapTitle.text=name;
        tyreTemperatures.Bind(car);
        mapKey.text="箭头 自车　◇ 下一计时门";
        mode.text="计时赛";
        driveKeys.text="W 给油　S 刹车／倒车\nA / D 转向　空格 手刹";
        Show(hud);startSignal.SetActive(false);coach.SetActive(false);monitor.SetActive(false);
    }
    public void Countdown(int seconds){
        startSignal.SetActive(true);signal.text=seconds>0?seconds.ToString():"起步";
        signalCaption.text=seconds>0?"准备发车":"计时开始";
    }
    public void ClearSignal(){if(startSignal.activeSelf)startSignal.SetActive(false);}
    public static string Clock(float value){
        if(float.IsInfinity(value))return "—";
        int cs=Mathf.RoundToInt(value*100);
        return $"{cs/6000:00}:{cs/100%60:00}.{cs%100:00}";
    }
    public void Instruments(RaceCar car,Circuit circuit,int lap,int next,
        float elapsed,float lapStart,float last,float best,bool showMonitor){
        map.SetGate(next);
        tyreTemperatures.Refresh(car);
        var spec=car.spec;
        speed.text=Mathf.RoundToInt(car.Speed*3.6f).ToString();
        gear.text=car.Reversing?"倒":car.ManualGears?$"手{car.Gear+1}":(car.Gear+1).ToString();
        float rev=car.EngineRpm;
        rpm.text=$"{rev:0}";rpmLabel.text=spec.competition?"转／分":"估算转／分";
        float fraction=rev/(spec.competition?spec.competition.limiterRpm:spec.upshift);
        revFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(fraction),1);revFill.color=fraction>.92f?new Color(1,.38f,.27f):new Color(.32f,.86f,.72f);
        driveStatus.text=car.BoostRemaining>0?$"短喷　{car.BoostRemaining:F1} 秒":
            $"{(car.ManualGears?"手动":"自动")} · 侧滑 {car.SignedDriftAngle:+0;-0;0}°";
        sessionTitle.text=Clock(elapsed-lapStart);
        sessionData.text=$"圈数　{lap} / {circuit.laps}　　计时门　{next+1} / {circuit.gates.Length}";
        lapData.text=$"上一圈　{(last>0?Clock(last):"—")}\n最快圈　{Clock(best)}";
        monitor.SetActive(showMonitor);
        if(!showMonitor)return;
        var input=car.AppliedInput;var cmd=car.AppliedCommand;
        diagnostics.text=$"按键　{(input.forward?"W":"·")}　{(input.left?"A":"·")}　{(input.back?"S":"·")}　{(input.right?"D":"·")}　{(input.space?"空格":"·")}\n"
            +$"油门 {cmd.throttle:P0}　脚刹 {cmd.brake:P0}　手刹 {cmd.hand:P0}\n"
            +$"转向 {cmd.steer:+0.0;-0.0;0}　左前 {car.wheels[0].steerAngle:F0}°　右前 {car.wheels[1].steerAngle:F0}°\n"
            +$"横摆 {Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg:F0}°/秒　接地 {car.GroundedCount}/4";
        if(spec.competition)diagnostics.text+=$"\n离合接合 {car.Driveline.Engagement:P0}　前 / 后制动 {car.CompetitionChassis.FrontBar:F0} / {car.CompetitionChassis.RearBar:F0} 巴";
        diagnostics.text+=$"\n左/右　前 {TyreSurface.Label(car.WheelCover(0))}/{TyreSurface.Label(car.WheelCover(1))}　后 {TyreSurface.Label(car.WheelCover(2))}/{TyreSurface.Label(car.WheelCover(3))}";
    }
    public void Finish(float elapsed,float best,bool practice){
        Show(results);replays.Decided();ResultLayout(false);resultTitle.text=practice?"练习完赛":"计时赛完赛";
        resultTime.text=Clock(elapsed);resultBest.text=Clock(best);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(retry.gameObject);
    }
}
}
