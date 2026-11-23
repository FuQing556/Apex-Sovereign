using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.Editor {
// The saved prefab is the editable layout. Only the minimap geometry changes at runtime.
public static class SessionPanelBuilder {
    const string Path="Assets/_Project/UI/SessionPanel.prefab";
    static Font font;
    static readonly Color ink=new Color32(15,25,32,245), inset=new Color32(31,46,53,242),
        edge=new Color32(71,92,98,255), dim=new Color32(185,203,205,255), white=new Color32(244,247,241,255),
        mint=new Color32(95,222,186,255), amber=new Color32(255,189,82,255);
    static readonly Vector2 TL=new(0,1),TR=new(1,1),BL=Vector2.zero,BR=new(1,0),C=new(.5f,.5f);
    static RectTransform Node(string name,Transform parent){
        var obj=new GameObject(name,typeof(RectTransform));var r=(RectTransform)obj.transform;r.SetParent(parent,false);
        return r;
    }
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static RectTransform Group(string name,Transform parent){
        var r=Node(name,parent);Stretch(r);return r;
    }
    static RectTransform Box(string name,Transform parent,Vector2 anchor,float x,float y,float w,float h){
        var r=Node(name,parent);r.anchorMin=r.anchorMax=r.pivot=anchor;r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);return r;
    }
    static Image Paint(RectTransform r,Color color){
        var img=r.gameObject.AddComponent<Image>();img.color=color;img.raycastTarget=false;return img;
    }
    static RectTransform Card(string n,Transform p,Vector2 anchor,float x,float y,float w,float h){
        var r=Box(n,p,anchor,x,y,w,h);Paint(r,ink);return r;
    }
    static Text Type(RectTransform r,string value,int size,Color c,TextAnchor align,bool bold=false){
        var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
        t.text=value;t.color=c;t.alignment=align;t.raycastTarget=false;t.supportRichText=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.lineSpacing=1;return t;
    }
    static Text Label(Transform parent,string name,string value,int size,float x,float y,float width,float height,
        Color c,TextAnchor align=TextAnchor.MiddleLeft,bool bold=false){
        return Type(Box(name,parent,TL,x,-y,width,height),value,size,c,align,bold);
    }
    static void Rule(Transform p,float x,float y,float w,float h,Color color)=>Paint(Box("分隔线",p,TL,x,-y,w,h),color);
    static RawImage Illustration(RectTransform r,Texture2D texture){
        var image=r.gameObject.AddComponent<RawImage>();
        image.texture=texture;image.raycastTarget=false;
        return image;
    }
    static TyreTemperaturePanel Tyres(RectTransform screen){
        const string folder="Assets/_Project/UI/TyrePlan/";
        var shell=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"ChassisPlan.png");
        var tread=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"TyreTread.png");
        if(!shell || !tread)throw new InvalidOperationException("轮胎俯视图尚未导入");
        var card=Card("右下胎温",screen,BR,-24,187,282,197);
        var panel=card.gameObject.AddComponent<TyreTemperaturePanel>();
        Rule(card,0,0,282,2,edge);
        Label(card,"标题","胎面温度  °C",17,15,6,150,28,white,TextAnchor.MiddleLeft,true);
        panel.status=Label(card,"状态","",15,145,6,122,28,dim,TextAnchor.MiddleRight);
        var body=Box("四轮透视",card,TL,0,-40,282,157);panel.diagram=body;
        var plan=Box("底盘俯视",body,C,0,0,76.8f,147.2f);
        Illustration(plan,shell);
        panel.wheels=new TyreTemperaturePanel.Wheel[4];
        string[] names={"左前","右前","左后","右后"};
        // Orthographic plan at 32 UI units per metre, with the nose at the top.
        for(int i=0;i<names.Length;i++){
            bool left=i%2==0,front=i<2;
            float axleY=(front?1:-1)*1.2875f*32;
            float wheelX=(left?-1:1)*(front?.835f:.805f)*32;
            float row=body.rect.height*.5f-axleY;
            float x=left?10:192;
            var align=left?TextAnchor.MiddleRight:TextAnchor.MiddleLeft;
            Label(body,names[i]+"轮位",names[i],14,x,row-26,80,23,dim,align);
            var reading=Label(body,names[i]+"读数","—",23,x,row-4,80,36,white,align,true);
            Rule(body,left?96:173,row,13,1,edge);
            var tire=Box(names[i]+"胎面",plan,C,wheelX,axleY,front?11:12,front?23:25);
            panel.wheels[i]=new TyreTemperaturePanel.Wheel {tread=Illustration(tire,tread),reading=reading};
        }
        return panel;
    }
    static Button Button(Transform parent,string name,Vector2 anchor,float x,float y,float w,float h,string value,bool main,out Text label){
        var r=Box(name,parent,anchor,x,y,w,h);
        var bg=Paint(r,main?amber:inset);bg.raycastTarget=true;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=bg;var colors=b.colors;colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1.13f,1.13f,1.13f);colors.pressedColor=new Color(.78f,.84f,.84f);
        colors.selectedColor=new Color(.89f,.96f,.98f);b.colors=colors;
        label=Type(Group("文字",r),value,main?21:17,main?ink:white,TextAnchor.MiddleCenter,main);return b;
    }
    static void Garage(SessionPanel p,RectTransform screen){
        var brand=Card("车库标题",screen,TL,32,-28,253,72);
        Rule(brand,0,0,6,72,amber);
        Label(brand,"标题","巅峰竞速",32,23,8,209,53,white,TextAnchor.MiddleLeft,true);
        var r=Card("车辆与场地",screen,new Vector2(0,.5f),32,-28,354,400);p.garageCard=r;
        Rule(r,0,0,354,3,mint);
        Label(r,"车辆标签","当前车辆",15,20,16,245,25,dim);
        p.carName=Label(r,"车型","柴田 31 号 · GR86",29,19,40,318,49,white,TextAnchor.MiddleLeft,true);
        p.carData=Label(r,"车辆说明","后轮驱动",15,20,90,318,28,dim);
        p.changeCar=Button(r,"选择车辆",TL,20,-126,148,41,"切换车辆",false,out _);
        p.track=Button(r,"选择场地",TL,178,-126,156,41,"切换场地",false,out _);
        Rule(r,20,183,314,1,edge);
        Label(r,"场地标签","出发地点",15,20,194,80,28,dim);
        p.location=Label(r,"赛道","山脊大奖赛道",23,107,190,227,36,white,TextAnchor.MiddleRight,true);
        p.garageKeys=Label(r,"操作","",16,20,232,316,84,dim);
        p.launch=Button(r,"出发",BL,20,20,314,54,"开始计时赛",true,out p.launchLabel);
    }
    public static void AddDrivingSettings(SessionPanel p){
        if(p.settings){AddVolumeControl(p);return;}
        font=p.garageKeys.font;
        p.garageKeys.rectTransform.sizeDelta=new Vector2(316,50);
        p.openSettings=Button(p.garageCard,"驾驶设置",TL,20,-282,314,35,"驾驶设置",false,out _);
        var screen=Group("驾驶设置页",(RectTransform)p.transform);
        p.settings=screen.gameObject;
        var card=Card("设置卡片",screen,C,0,0,354,280);
        Rule(card,0,0,354,3,mint);
        Label(card,"标题","驾驶设置",28,20,12,310,42,white,TextAnchor.MiddleLeft,true);
        p.toggleAdvanced=Button(card,"高级驾驶开关",TL,20,-68,314,46,"高级驾驶操作",false,out p.advancedLabel);
        p.advancedKeys=Label(card,"驾驶说明","",17,20,124,314,80,dim);
        p.backFromSettings=Button(card,"返回",BL,20,18,314,45,"返回车库",true,out _);
        AddVolumeControl(p);
        screen.gameObject.SetActive(false);
    }
    [MenuItem("Apex/Setup/Install keyboard recovery setting")]
    public static void InstallRecoverySetting(){
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{AddRecoverySetting(root.GetComponent<SessionPanel>());PrefabUtility.SaveAsPrefabAsset(root,Path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void AddRecoverySetting(SessionPanel p){
        if(p.toggleRecovery)return;
        font=p.garageKeys.font;
        var card=(RectTransform)p.backFromSettings.transform.parent;
        card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,444);
        p.toggleRecovery=Button(card,"键盘纠偏辅助",TL,20,-307,314,44,"键盘纠偏辅助",false,out p.recoveryLabel);
    }
    public static void AddVolumeControl(SessionPanel p){
        if(p.cycleVolume)return;
        font=p.garageKeys.font;
        var card=(RectTransform)p.backFromSettings.transform.parent;
        card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,334);
        p.cycleVolume=Button(card,"音效音量",TL,20,-200,314,44,"音效音量　75%",false,out p.volumeLabel);
    }
    [MenuItem("Apex/Setup/Install race replay menu")]
    static void InstallReplayControls(){
        var root=PrefabUtility.LoadPrefabContents(Path);
        try{
            var p=root.GetComponent<SessionPanel>();AddReplayControls(p);
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void AddReplayControls(SessionPanel p){
        if(p.replays){
            p.replays.open.GetComponentInChildren<Text>(true).text="参赛幽灵";
            p.replays.open.name="参赛幽灵";
            p.replays.yes.GetComponentInChildren<Text>(true).text="保存幽灵";
            p.replays.prompt.transform.Find("标题").GetComponent<Text>().text="保存这场驾驶的幽灵？";
            return;
        }
        font=p.garageKeys.font;
        foreach(string old in new[]{"幽灵路线","观摩幽灵","命名记录","按原操作实跑"}){
            var t=p.garageCard.Find(old);if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        var obsolete=p.garage.transform.Find("幽灵命名");
        if(obsolete)UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
        p.ghostMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Tracks/RidgeGhosts/Ghost.mat");
        var menu=p.gameObject.AddComponent<ReplayMenu>();p.replays=menu;
        menu.open=Button(p.garageCard,"参赛幽灵",TL,20,-324,314,36,"参赛幽灵",false,out _);
        var resultCard=(RectTransform)p.retry.transform.parent;
        resultCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,354);
        var prompt=Box("完赛保存选择",resultCard,BL,24,20,392,86);menu.prompt=prompt.gameObject;
        Label(prompt,"标题","保存这场驾驶的幽灵？",17,0,0,392,29,white,TextAnchor.MiddleLeft);
        menu.no=Button(prompt,"不保存",BL,0,0,158,46,"不保存",false,out _);
        menu.yes=Button(prompt,"保存幽灵",BR,0,0,220,46,"保存幽灵",true,out _);
        prompt.gameObject.SetActive(false);
        var veil=Group("比赛回放弹窗",p.transform);menu.dialog=veil.gameObject;
        Paint(veil,new Color(0,0,0,.70f)).raycastTarget=true;
        var card=Card("回放面板",veil,C,0,0,460,496);
        Rule(card,0,0,460,3,mint);
        menu.heading=Label(card,"标题","比赛回放",25,20,10,420,44,white,TextAnchor.MiddleLeft,true);
        var view=Box("槽位列表",card,TL,20,-61,420,204);
        Paint(view,inset).raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();
        var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        var content=Node("槽位",view);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
        content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;scroll.viewport=view;scroll.content=content;menu.rows=content;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=3;
        layout.childControlWidth=true;layout.childControlHeight=false;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        menu.rowTemplate=Button(content,"记录槽",TL,0,0,420,38,"",false,out var rowLabel);
        rowLabel.resizeTextForBestFit=true;rowLabel.resizeTextMinSize=12;rowLabel.resizeTextMaxSize=17;
        menu.rowTemplate.gameObject.SetActive(false);
        menu.hint=Label(card,"当前选择","",16,20,269,420,49,dim);
        var input=Box("回放名称",card,TL,20,-320,420,44);
        var graphic=Paint(input,inset);graphic.raycastTarget=true;
        menu.nameField=input.gameObject.AddComponent<InputField>();menu.nameField.targetGraphic=graphic;
        menu.nameField.textComponent=Type(Box("文字",input,TL,10,0,400,44),"",20,white,TextAnchor.MiddleLeft);
        menu.nameField.characterLimit=32;menu.nameField.lineType=InputField.LineType.SingleLine;
        menu.opponent=Button(card,"参赛选择",TL,20,-374,420,36,"设为参赛回放",false,out menu.opponentText);
        menu.back=Button(card,"返回",BL,20,22,150,48,"返回",false,out _);
        menu.primary=Button(card,"确定",BR,-20,22,250,48,"播放",true,out menu.primaryText);
        veil.gameObject.SetActive(false);
        if(p.keyCaps!=null && p.keyCaps.Length==5 && p.keyCaps[0])return;
        var board=Box("实时键盘",p.hud.transform,BL,24,78,212,72);
        p.keyCaps=new Image[5];
        string[] names={"W","A","D","S","空格"};
        int[] xs={39,0,78,39,119},ys={38,0,0,0,0};
        for(int i=0;i<names.Length;i++){
            var key=Box(names[i],board,BL,xs[i],ys[i],i==4?82:34,32);
            p.keyCaps[i]=Paint(key,inset);Type(Group("键名",key),names[i],17,white,TextAnchor.MiddleCenter,true);
        }
        ((RectTransform)p.monitor.transform).anchoredPosition=new Vector2(24,158);
    }
    public static void AddWorkshop(SessionPanel p){
        if(p.openWorkshop)return;
        font=p.garageKeys.font;
        p.catalog=AssetDatabase.LoadAssetAtPath<WorkshopCatalog>("Assets/_Project/Lobby/Parts/Workshop.asset");
        var title=p.garage.transform.Find("车库标题/标题");
        if(title)title.GetComponent<Text>().text="车队车库";
        p.garageKeys.fontSize=15;
        p.openSettings.GetComponent<RectTransform>().sizeDelta=new Vector2(124,35);
        p.openSettings.GetComponent<RectTransform>().anchoredPosition=new Vector2(210,-282);
        p.openWorkshop=Button(p.garageCard,"零件仓库",TL,20,-282,181,35,"零件仓库",false,out _);
        var screen=Group("零件仓库页",p.transform);p.workshop=screen.gameObject;
        var card=Card("配件工作台",screen,new Vector2(0,.5f),32,-28,374,470);p.workshopCard=card;
        Rule(card,0,0,374,3,amber);
        p.partCar=Label(card,"车型","",16,22,14,324,28,dim);
        Label(card,"标题","零件仓库",31,20,42,315,48,white,TextAnchor.MiddleLeft,true);
        p.chooseAxle=Button(card,"后桥",TL,20,-102,162,40,"后桥差速器",false,out _);
        p.chooseWheels=Button(card,"轮面",TL,192,-102,162,40,"后轮外观",false,out _);
        Rule(card,20,158,334,1,edge);
        p.partName=Label(card,"待装配件","",24,20,167,257,39,white,TextAnchor.MiddleLeft,true);
        p.partNumbers=Label(card,"序号","",15,278,170,76,33,mint,TextAnchor.MiddleRight);
        p.partInfo=Label(card,"规格","",17,20,216,334,75,dim,TextAnchor.UpperLeft);
        p.fittedName=Label(card,"当前装车","",16,20,294,334,31,mint);
        p.previousPart=Button(card,"前一件",TL,20,-338,162,40,"上一件",false,out _);
        p.nextPart=Button(card,"后一件",TL,192,-338,162,40,"下一件",false,out _);
        p.closeWorkshop=Button(card,"收起仓库",BL,20,22,116,50,"返回车库",false,out _);
        p.installPart=Button(card,"装车",BR,-20,22,204,50,"安装到本车",true,out p.installLabel);
        var settingsCard=(RectTransform)p.backFromSettings.transform.parent;
        settingsCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,390);
        p.blurToggle=Button(settingsCard,"高速动态模糊",TL,20,-254,314,44,"高速动态模糊",false,out p.blurLabel);
        screen.gameObject.SetActive(false);
    }
    static void Driving(SessionPanel p,RectTransform screen){
        var map=Card("左上赛道图",screen,TL,24,-22,236,208);
        Rule(map,0,0,236,3,mint);
        p.mapTitle=Label(map,"场地","山脊大奖赛道",18,14,7,186,28,white,TextAnchor.MiddleLeft,true);
        Label(map,"方位","北",14,194,8,28,25,dim,TextAnchor.MiddleRight);
        var view=Box("地图视口",map,TL,12,-40,212,133);Paint(view,new Color32(32,48,54,255));
        view.gameObject.AddComponent<RectMask2D>();var drawing=Group("路线与车辆",view);
        drawing.gameObject.AddComponent<CanvasRenderer>();p.map=drawing.gameObject.AddComponent<RaceMap>();p.map.raycastTarget=false;
        p.mapKey=Label(map,"图例","",14,14,176,214,25,dim);
        var info=Card("右上赛事信息",screen,TR,-24,-22,298,190);
        Rule(info,0,0,298,3,amber);
        p.mode=Label(info,"模式","计时赛",15,16,10,267,24,amber);
        p.sessionTitle=Label(info,"本圈或课程","00:00.00",29,15,29,271,48,white,TextAnchor.MiddleLeft,true);
        p.sessionData=Label(info,"进度","",16,16,77,268,44,white);
        p.lapData=Label(info,"历史成绩","",14,16,119,270,65,dim);
        var dial=Card("右下仪表",screen,BR,-24,22,282,155);
        Rule(dial,0,0,282,3,mint);
        p.speed=Label(dial,"时速","0",62,9,-2,143,94,white,TextAnchor.MiddleRight,true);
        Label(dial,"车速单位","公里/时",14,155,53,62,26,dim);
        var g=Box("挡位底",dial,TR,-14,-12,59,65);Paint(g,inset);
        p.gear=Label(g,"当前挡位","1",29,2,-2,55,46,amber,TextAnchor.MiddleCenter,true);
        Label(g,"挡位标签","挡位",13,2,41,55,20,dim,TextAnchor.MiddleCenter);
        var rail=Box("转速槽",dial,TL,15,-93,252,6);Paint(rail,edge);p.revFill=Paint(Group("转速",rail),mint);
        p.rpm=Label(dial,"曲轴转速","1000",19,14,107,64,32,white,TextAnchor.MiddleLeft,true);
        p.rpmLabel=Label(dial,"转速单位","转／分",13,76,108,81,30,dim);
        p.driveStatus=Label(dial,"传动状态","",14,119,104,149,37,mint,TextAnchor.MiddleRight);
        p.tyreTemperatures=Tyres(screen);
        var nav=Box("驾驶导航",screen,BL,24,22,312,48);
        p.leave=Button(nav,"返回车库",BL,0,0,105,44,"返回车库",false,out _);
        p.driveKeys=Label(nav,"快捷键","",14,114,0,242,48,white);
        var mon=Card("左下遥测",screen,BL,24,82,347,197);p.monitor=mon.gameObject;
        Rule(mon,0,0,347,3,mint);Label(mon,"标题","驾驶遥测",16,14,8,302,24,mint,TextAnchor.MiddleLeft,true);
        p.diagnostics=Label(mon,"实时参数","",16,14,38,324,151,white,TextAnchor.UpperLeft);
        var coach=Card("教练",screen,new Vector2(.5f,0),0,28,338,70);p.coach=coach.gameObject;
        Label(coach,"教练标签","副驾教练",14,15,6,304,22,mint);
        p.advice=Label(coach,"建议","",18,15,29,309,32,white);
        var signal=Card("中央发车信号",screen,C,0,0,178,143);p.startSignal=signal.gameObject;
        Rule(signal,0,0,178,4,amber);
        p.signal=Label(signal,"发车信号","3",64,5,3,168,97,white,TextAnchor.MiddleCenter,true);
        p.signalCaption=Label(signal,"提示","准备发车",18,5,102,168,30,amber,TextAnchor.MiddleCenter);
    }
    static void Results(SessionPanel p,RectTransform screen){
        Paint(screen,new Color(0.025f,.045f,.055f,.58f));
        var r=Card("中央成绩单",screen,C,0,0,440,318);Rule(r,0,0,440,4,amber);
        p.resultTitle=Label(r,"成绩标题","计时赛完赛",30,25,17,390,48,white,TextAnchor.MiddleLeft,true);
        Rule(r,25,75,390,1,edge);
        Label(r,"总用时标签","总用时",16,25,87,110,27,dim);
        p.resultTime=Label(r,"总用时","00:00.00",48,23,109,391,70,white,TextAnchor.MiddleLeft,true);
        Label(r,"最快圈标签","最快有效圈",16,25,190,151,32,dim);
        p.resultBest=Label(r,"最快圈","—",22,184,185,228,40,mint,TextAnchor.MiddleRight,true);
        p.returnFromResults=Button(r,"车库",BL,25,22,170,52,"返回车库",false,out _);
        p.retry=Button(r,"重赛",BR,-25,22,207,52,"再跑一次",true,out _);
    }
    public static void Bake(){
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/UI/Fonts/NotoSansSC-Medium.otf");
        if(!font)throw new InvalidOperationException("中文字体尚未导入");
        var r=Node("比赛界面",null);Stretch(r);
        try{
            var p=r.gameObject.AddComponent<SessionPanel>();
            p.garage=Group("车库",r).gameObject;Garage(p,(RectTransform)p.garage.transform);AddDrivingSettings(p);AddWorkshop(p);AddRecoverySetting(p);
            p.hud=Group("驾驶",r).gameObject;Driving(p,(RectTransform)p.hud.transform);
            p.results=Group("完赛",r).gameObject;Results(p,(RectTransform)p.results.transform);AddReplayControls(p);
            var load=Group("载入",r);p.loading=load.gameObject;Paint(load,ink);
            Type(Box("状态",load,C,0,0,340,80),"正在载入场地…",28,white,TextAnchor.MiddleCenter);
            p.hud.SetActive(false);p.results.SetActive(false);p.loading.SetActive(false);p.monitor.SetActive(false);p.coach.SetActive(false);p.startSignal.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(r.gameObject,Path);
        }finally{UnityEngine.Object.DestroyImmediate(r.gameObject);}
    }
    public static SessionPanel Place(Transform parent){
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if(!asset){Bake();asset=AssetDatabase.LoadAssetAtPath<GameObject>(Path);}
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);return instance.GetComponent<SessionPanel>();
    }
}
}
