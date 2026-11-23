using UnityEngine;

namespace Apex {
// Angles are measured in the road tangent plane. One handbrake maneuver can
// earn one exit boost; a contact invalidates it until the handbrake is released.
public sealed class DriftReward {
    float qualifying, exitTime;
    bool tracking, blocked;
    public float Remaining {get;private set;}
    public void Clear(){
        qualifying=exitTime=Remaining=0;
        tracking=blocked=false;
    }
    public void Step(CarSpec s,float dt,float forward,float angle,float yaw,bool grounded,
        bool hand,bool brake,bool impact,bool enabled){
        if(!enabled || impact || !grounded || brake || forward<s.driftMinSpeed || angle>s.driftSpinAngle){
            Clear();blocked=hand;return;
        }
        if(blocked){blocked=hand;return;}
        if(Remaining>0){Remaining=hand?0:Mathf.Max(0,Remaining-dt);return;}
        if(hand){
            if(exitTime>0)qualifying=0;
            tracking=true;exitTime=0;
            if(angle>=s.driftEntryAngle && yaw>=s.driftMinYaw)qualifying+=dt;
            else if(qualifying<s.driftMinSeconds)qualifying=0;
            return;
        }
        if(!tracking)return;
        exitTime+=dt;
        if(qualifying<s.driftMinSeconds || exitTime>s.driftExitWindow){Clear();return;}
        if(angle<=s.driftExitAngle){
            Remaining=Mathf.Lerp(s.driftBoostMin,s.driftBoostMax,
                Mathf.InverseLerp(s.driftMinSeconds,s.driftFullSeconds,qualifying));
            tracking=false;qualifying=exitTime=0;
        }
    }
}
}
