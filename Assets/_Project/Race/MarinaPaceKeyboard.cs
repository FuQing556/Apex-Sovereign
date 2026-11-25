#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Apex.Editor {
// Feed the same local driver as a person at the wheel so telemetry and the
// normal ghost recorder capture the actual keyboard state at every physics tick.
[DefaultExecutionOrder(-200)]
public sealed class MarinaPaceKeyboard:MonoBehaviour {
    RaceSession session;
    Func<int> readKeys;
    LocalDriver local;
    public void Bind(RaceSession race,Func<int> planner){
        session=race;readKeys=planner;local=(LocalDriver)race.Car.Driver;
    }
    void FixedUpdate(){
        if(!session || session.State==RaceSession.Phase.Results){Destroy(this);return;}
        if(session.State!=RaceSession.Phase.Driving)return;
        int mask=readKeys();
        local.CaptureDemonstrationKeyboard(new DriverInputFrame{
            frame=Time.frameCount,keyboardId=-1,forward=(mask&1)!=0,left=(mask&2)!=0,
            right=(mask&4)!=0,back=(mask&8)!=0,space=(mask&16)!=0
        });
    }
}
}
#endif
