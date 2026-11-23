using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apex {
[DefaultExecutionOrder(510)]
public sealed class PlayerGhostCapture:MonoBehaviour {
    RaceSession race;
    RaceCar car;
    GhostHeader header;
    readonly List<GhostRecording.Frame> frames=new();
    int generation;
    bool closed;
    public void Begin(RaceSession session,string track){
        race=session;car=race.Car;generation=car.ResetGeneration;
        header=new GhostHeader{id=Guid.NewGuid().ToString("N"),title=DateTime.Now.ToString("MM-dd HH:mm:ss"),
            track=track,car=car.spec.name,interval=Time.fixedDeltaTime,revision=car.spec.competition?car.spec.competition.revision:0,
            differential=car.Differential?.id??"original",rear=car.RearWheels?.id??"original",
            advanced=race.panel.AdvancedControls,recovery=race.panel.KeyboardRecovery};
    }
    void FixedUpdate(){
        if(closed || !race || (race.State!=RaceSession.Phase.Driving && race.State!=RaceSession.Phase.Results))return;
        if(car.Driver is not LocalDriver driver){closed=true;return;}
        var body=car.Chassis;
        var inverse=Quaternion.Inverse(body.rotation);
        Pose Wheel(int i){
            var w=car.wheels[i];w.GetWorldPose(out var p,out var q);
            if(car.CompetitionChassis!=null && w.GetGroundHit(out var hit))p-=hit.normal*car.CompetitionChassis.Deflection(i,hit.force);
            return new Pose(inverse*(p-body.position),inverse*q);
        }
        var input=car.AppliedInput;var command=car.AppliedCommand;
        int keys=GhostArchive.Keys(input)|(command.resumeAutomatic?GhostArchive.Auto:0)|(driver.CapturedGearEdges<<10);
        if(generation!=car.ResetGeneration){generation=car.ResetGeneration;keys|=GhostArchive.Reset;header.practice=true;}
        if(race.CurrentLap==2 && header.firstLap==0)header.firstLap=frames.Count*header.interval;
        var ch=car.CompetitionChassis;
        frames.Add(new GhostRecording.Frame{chassis=new Pose(body.position,body.rotation),fl=Wheel(0),fr=Wheel(1),rl=Wheel(2),rr=Wheel(3),
            steer=new Vector2(car.wheels[0].steerAngle,car.wheels[1].steerAngle),
            temperature=ch==null?Vector4.zero:new Vector4(ch.TyreC(0),ch.TyreC(1),ch.TyreC(2),ch.TyreC(3)),
            kph=car.Speed*3.6f,beta=car.SignedDriftAngle,rpm=car.EngineRpm,gear=car.Gear+1,turn=race.NextCheckpoint,
            keys=keys,gearRequest=command.gearChange,pad=new Vector3(input.padX,input.padForward,input.padBack)});
        if(race.State==RaceSession.Phase.Results){
            closed=true;
            header.count=frames.Count;
            race.OfferReplay(header,frames);
        }
    }
}
}
