"""Map the inverse solution to the project's digital controls, independently of Unity."""
import numpy as np
from inverse_drift import DT, car, setup, automatic, drive_limit, brake_limit, gears


def steer_step(angle,velocity,key,v,beta,yaw):
    direction=key
    blend=np.clip((v*3.6-setup["keyboardPrecisionKph"]["x"])/(setup["keyboardPrecisionKph"]["y"]-setup["keyboardPrecisionKph"]["x"]),0,1)
    accel=(1-blend)*setup["keyboardAcceleration"]["x"]+blend*setup["keyboardAcceleration"]["y"]
    before=np.maximum(setup["keyboardStartDegreesPerSecond"],direction*velocity)
    after=np.minimum(before+accel*DT,setup["keyboardMaxDegreesPerSecond"])
    catch=np.clip(direction*beta,0,setup["keyboardCatchDegrees"])*np.clip(-direction*yaw/setup["keyboardCatchYawRate"],0,1)
    catch*=np.clip(v*np.cos(np.radians(beta))/car["driftMinSpeed"],0,1)
    step=(before+after)*DT/2+np.maximum(0,catch-direction*angle)*(-np.expm1(-DT/setup["keyboardCenterSeconds"]))
    driven=np.clip(angle+direction*np.minimum(step,setup["keyboardMaxDegreesPerSecond"]*DT),-58,58)
    centered=np.sign(angle)*np.maximum(0,abs(angle)-(setup["keyboardCenterDegreesPerSecond"]+abs(angle)/setup["keyboardCenterSeconds"])*DT)
    return np.where(key==0,centered,driven),np.where(key==0,0,direction*after)


def pedal_step(pedal,pressure,charge,key):
    target=key==1
    pedal=np.where(key==-1,0,pedal)
    pedal+=np.clip(target.astype(float)-pedal,-car["pedalRelease"]*DT,car["pedalRise"]*DT)
    charge+=np.clip(pedal-charge,-DT/setup["boostReleaseSeconds"],DT/setup["boostFillSeconds"])
    pressure+=np.clip((key==-1).astype(float)-pressure,-DT/setup["serviceReleaseSeconds"],DT/setup["serviceApplySeconds"])
    return pedal,pressure,charge


def digital_plan(speed,beta,yaw,desired_steer,desired_force,neutral):
    gear=automatic(speed)
    full=drive_limit(speed,gear,np.radians(beta))
    rpm=speed*gears[gear]*60/(2*np.pi*car["rearRadius"])
    overrun=setup["overrunNm"]*np.clip((rpm-car["idle"])/(car["upshift"]-car["idle"]),0,1)*gears[gear]/(car["rearRadius"]*car["mass"]*car["efficiency"])
    shifts=np.flatnonzero(np.diff(gear)!=0)+1
    transmission=np.ones_like(speed)
    for i in shifts:
        ticks=np.arange(min(len(speed)-i,15))
        transmission[i+ticks]=np.minimum(transmission[i+ticks],np.clip((ticks*DT-setup["shiftOpenSeconds"])/setup["clutchEngagementSeconds"],0,1))
    def force(p,h,c,i):
        return transmission[i]*(full[i]*p*(setup["minimumBoostTorqueFraction"]+(1-setup["minimumBoostTorqueFraction"])*c)-overrun[i]*(1-p))-h*brake_limit
    def beam(steering):
        width,block=100,3
        initial=(0.,0.) if steering else (0.,0.,0.)
        states=np.array([initial])
        scores=np.zeros(1);last=np.zeros(1,dtype=int)
        parents,actions=[],[]
        for start in range(0,len(speed),block):
            old=np.repeat(np.arange(len(states)),3)
            key=np.tile([-1,0,1],len(states))
            vec=states[old].copy().T
            cost=scores[old]+(last[old]!=key)*(.085 if steering else .27)
            for i in range(start,min(start+block,len(speed))):
                if steering:
                    vec=np.array(steer_step(*vec,key,speed[i],beta[i],yaw[i]))
                    cost+=(vec[0]-desired_steer[i])**2
                    if neutral[i]: cost+=1e5*(key!=0)
                else:
                    vec=np.array(pedal_step(*vec,key))
                    cost+=(force(*vec,i)-desired_force[i])**2
            buckets=np.round(vec.T/([.22,1.5] if steering else [.06,.09,.12])).astype(int)
            indices=np.argsort(cost)
            _,unique=np.unique(np.column_stack([buckets,key])[indices],axis=0,return_index=True)
            selected=indices[np.sort(unique)[:width]]
            states=vec.T[selected];scores=cost[selected];last=key[selected]
            parents.append(old[selected]);actions.append(last.copy())
        history=[]
        best=int(np.argmin(scores))
        for row,keys in zip(reversed(parents),reversed(actions)):
            history.append(keys[best]);best=row[best]
        return np.repeat(history[::-1],block)[:len(speed)]
    turn,drive=beam(True),beam(False)
    road_wheel,thrust=realize(speed,beta,yaw,turn,drive)
    return turn,drive,road_wheel,thrust,gear+1


def realize(speed,beta,yaw,turn,drive):
    gear=automatic(speed)
    angle=velocity=pedal=pressure=charge=0.
    since=5.
    road_wheel=[]; thrust=[]
    for i,(a,p) in enumerate(zip(turn,drive)):
        if i and gear[i]!=gear[i-1]:since=0
        ratio=gears[gear[i]]
        rpm=speed[i]*ratio*60/(2*np.pi*car["rearRadius"])
        overrun=setup["overrunNm"]*np.clip((rpm-car["idle"])/(car["upshift"]-car["idle"]),0,1)*ratio/(car["rearRadius"]*car["mass"]*car["efficiency"])
        delivery=np.clip((since-setup["shiftOpenSeconds"])/setup["clutchEngagementSeconds"],0,1)
        angle,velocity=steer_step(angle,velocity,a,speed[i],beta[i],yaw[i])
        pedal,pressure,charge=pedal_step(pedal,pressure,charge,p)
        u=delivery*(drive_limit(speed[i],gear[i],np.radians(beta[i]))*pedal*(setup["minimumBoostTorqueFraction"]+(1-setup["minimumBoostTorqueFraction"])*charge)-overrun*(1-pedal))-pressure*brake_limit
        road_wheel.append(float(angle));thrust.append(float(u))
        since+=DT
    return np.array(road_wheel),np.array(thrust)
