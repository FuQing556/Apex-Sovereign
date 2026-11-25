using UnityEngine;

namespace Apex {
public sealed class GridDriver:IDriver {
    readonly GridRace race;
    readonly GridEntry self;
    readonly GridRaceSettings tune;
    float lane,idle,reverseFor,recoverySteer,slideAge,cooldown;
    int slideSign;
    public bool Drifting=>slideSign!=0;
    public GridEntry Pursuing{get;private set;}
    public GridDriver(GridRace race,GridEntry self){this.race=race;this.self=self;tune=race.tune;}
    public DriveCommand Read(){
        var route=race.route;var car=self.car;float dt=Time.fixedDeltaTime;
        float s=self.station,v=car.Speed,beta=car.SignedDriftAngle;
        float yaw=Vector3.Dot(car.Chassis.angularVelocity,car.transform.up)*Mathf.Rad2Deg;
        var pos=car.Chassis.position;
        var leader=race.ActiveLeader;
        Pursuing=self.role==GridRole.Hunter && leader!=self?leader:null;
        bool styleDrift=!car.spec.frontWheelDrive && (self.role==GridRole.Drift || (self.role==GridRole.Hunter && Pursuing==null));
        float look=tune.lookBase+tune.lookSeconds*v;
        float pace=route.Pace(s),desiredLane=0;
        if(self.passed==0)desiredLane=(self.slot-1.5f)*tune.slotSpacing*Mathf.Clamp01(1-s/60);
        bool attack=false;
        if(Pursuing!=null){
            var delta=Pursuing.car.Chassis.position-pos;
            float gap=route.Delta(Pursuing.station,s);
            attack=gap>=-2 && gap<tune.attackDistance && delta.sqrMagnitude<tune.attackDistance*tune.attackDistance;
            if(attack)desiredLane=Mathf.Clamp(route.Offset(Pursuing.car.Chassis.position,Pursuing.station),-6,6);
        }
        // Each driver sees the actual neighbours, including a player on a different line.
        foreach(var other in race.entries){
            if(other==self || other.Finished || (attack && other==Pursuing))continue;
            var rel=car.transform.InverseTransformPoint(other.car.Chassis.position);
            if(rel.z<-.8f || rel.z>look+4 || Mathf.Abs(rel.x)>tune.trafficClearance)continue;
            float theirLane=route.Offset(other.car.Chassis.position,other.station);
            float myLane=route.Offset(pos,s);
            float side=Mathf.Abs(myLane-theirLane)>.25f?Mathf.Sign(myLane-theirLane):(self.slot<other.slot?-1:1);
            desiredLane=Mathf.Clamp(theirLane+side*tune.trafficClearance,-6,6);
            if(rel.z>0 && rel.z<8 && Mathf.Abs(rel.x)<2.15f)
                pace=Mathf.Min(pace,Mathf.Max(6,other.car.Speed-1));
        }
        lane=Mathf.MoveTowards(lane,desiredLane,dt*5);
        var aim=route.Target(s+look,lane);
        if(attack){
            float forward=Vector3.Dot(Pursuing.car.Chassis.position-pos,car.transform.forward);
            if(forward>4){
                var intercept=Pursuing.car.Chassis.position+Pursuing.car.Chassis.linearVelocity*.24f;
                aim=Vector3.Lerp(aim,intercept,.70f);
                pace=Mathf.Min(tune.topSpeed,pace*1.16f);
            }
        }
        var point=car.transform.InverseTransformPoint(aim);
        float distance=Mathf.Max(2,new Vector2(point.x,point.z).magnitude);
        float k=2*point.x/(distance*distance);
        float angle=Mathf.Atan(car.spec.wheelbase*k)*Mathf.Rad2Deg*(1+.022f*v)+.45f*beta+.09f*(k*v*Mathf.Rad2Deg-yaw);
        if(point.z<0 && Mathf.Abs(beta)<25)angle=Mathf.Sign(point.x)*45;
        bool hand=false;
        float ahead=route.Bend(s+tune.driftLead);
        cooldown=Mathf.Max(0,cooldown-dt);
        if(styleDrift && slideSign==0 && cooldown<=0 && v>tune.driftMinSpeed && Mathf.Abs(beta)<16 &&
           Mathf.Abs(ahead)>route.driftCurvature && Mathf.Abs(ahead)>Mathf.Abs(route.Bend(s))+.002f){
            slideSign=(int)Mathf.Sign(ahead);slideAge=0;
        }
        if(slideSign!=0){
            slideAge+=dt;
            if(!styleDrift || slideAge>2.6f || (slideAge>.65f && (slideSign*route.Bend(s+6)<.009f || Mathf.Abs(beta)>42))){
                slideSign=0;cooldown=1.05f;
            }else if(slideAge<tune.handSeconds && -slideSign*beta<tune.driftAngle*.8f && slideSign*yaw<82){
                hand=true;angle=slideSign*27;
            }else{
                float velK=2*Mathf.Sin(Mathf.Atan2(point.x,point.z)-beta*Mathf.Deg2Rad)/distance;
                float targetSlip=tune.driftAngle*Mathf.Clamp01(slideSign*route.Bend(s+8)/route.driftCurvature);
                float desiredYaw=velK*v*Mathf.Rad2Deg+2.5f*(beta+slideSign*targetSlip);
                angle=Mathf.Atan(car.spec.wheelbase*velK)*Mathf.Rad2Deg+beta+.22f*(desiredYaw-yaw);
            }
        }
        if(v<1.7f && reverseFor<=0)idle+=dt;else idle=0;
        if(idle>tune.stuckSeconds){
            reverseFor=tune.reverseSeconds;recoverySteer=point.x>=0?-1:1;idle=0;slideSign=0;
        }
        if(reverseFor>0){reverseFor-=dt;return new DriveCommand{reverse=true,throttle=.38f,steer=recoverySteer*.65f};}
        float lockAngle=Mathf.Max(1,car.spec.steeringBySpeed.Evaluate(v));
        float error=pace-v;
        return new DriveCommand{steer=Mathf.Clamp(angle/lockAngle,-1,1),hand=hand?1:0,
            throttle=hand?0:Mathf.Clamp01(.30f+error*.38f),brake=hand?0:Mathf.Clamp01((-error-.7f)*.23f)};
    }
}
}
