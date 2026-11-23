#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Apex.Editor {
[DefaultExecutionOrder(-1000)]
public sealed class HandlingGarageCheck:MonoBehaviour {
    const string KeyName="Apex.KeyboardRecovery";
    static readonly string Folder=Path.GetFullPath("Logs/HandlingChecks/handling-v9");
    Keyboard original,device;
    RaceSession session;
    SessionPanel panel;
    bool initial,hadPref,running,prepared;
    int frames;

    [MenuItem("Apex/Tests/Handling garage keyboard")]
    public static void Begin(){
        var s=FindAnyObjectByType<RaceSession>();
        if(!Application.isPlaying || !s || s.State!=RaceSession.Phase.Lobby || s.GetComponent<HandlingGarageCheck>())
            throw new InvalidOperationException("Start in the garage with the recorded 31 and Ridge selected.");
        var test=s.gameObject.AddComponent<HandlingGarageCheck>();test.session=s;test.panel=FindAnyObjectByType<SessionPanel>();
        test.StartCoroutine(test.Run());
    }
    IEnumerator Run(){
        initial=panel.KeyboardRecovery;hadPref=PlayerPrefs.HasKey(KeyName);original=Keyboard.current;
        device=InputSystem.AddDevice<Keyboard>("HandlingGarageKeyboard");prepared=true;
        try{
            foreach(bool assistance in new[]{false,true}){
                panel.openSettings.onClick.Invoke();
                if(panel.KeyboardRecovery!=assistance)panel.toggleRecovery.onClick.Invoke();
                panel.backFromSettings.onClick.Invoke();
                var before=new HashSet<string>(Directory.GetFiles("Logs/DriveTelemetry","*.csv"));
                panel.launch.onClick.Invoke();
                yield return new WaitUntil(()=>session.State==RaceSession.Phase.Driving);
                var car=session.Car;
                if(car.spec.name!="Okuibuki" || car.Differential?.id!="club-lsd" || car.RearWheels?.id!="original")
                    throw new InvalidOperationException("Select the recorded car and parts in the garage.");
                frames=0;running=true;
                yield return new WaitUntil(()=>frames>=290);
                running=false;
                if(!(car.Driver is LocalDriver) || car.AppliedCommand.keyboardRecovery!=assistance)
                    throw new InvalidOperationException("Garage selection did not reach the physical vehicle");
                var files=Directory.GetFiles("Logs/DriveTelemetry","*-Ridge-*.csv").Where(f=>!before.Contains(f)).ToArray();
                if(files.Length!=1)throw new InvalidOperationException("Expected one new Ridge telemetry file.");
                session.Secondary();yield return new WaitUntil(()=>session.State==RaceSession.Phase.Lobby);
                yield return null;
                File.Copy(files[0],Path.Combine(Folder,assistance?"garage-on.csv":"garage-off.csv"),true);
            }
            File.WriteAllText(Path.Combine(Folder,"garage-complete.txt"),"Two production launches from the garage, 290 physical input steps each. "+Application.unityVersion);
        }finally{Restore();Destroy(this);}
    }
    void FixedUpdate(){
        if(!running || frames>=290)return;
        var k=new KeyboardState();
        k.Set(UnityEngine.InputSystem.Key.W,frames<205);k.Set(UnityEngine.InputSystem.Key.D,frames>=135 && frames<170);
        k.Set(UnityEngine.InputSystem.Key.A,frames>=180 && frames<190);k.Set(UnityEngine.InputSystem.Key.S,frames>=205 && frames<280);
        InputState.Change(device,k);device.MakeCurrent();((LocalDriver)session.Car.Driver).Capture();frames++;
    }
    void Restore(){
        if(!prepared)return;prepared=running=false;
        if(panel && panel.KeyboardRecovery!=initial)panel.toggleRecovery.onClick.Invoke();
        if(!hadPref){PlayerPrefs.DeleteKey(KeyName);PlayerPrefs.Save();}
        if(device!=null && device.added)InputSystem.RemoveDevice(device);
        if(original!=null && original.added)original.MakeCurrent();
    }
    void OnDestroy()=>Restore();
}
}
#endif
