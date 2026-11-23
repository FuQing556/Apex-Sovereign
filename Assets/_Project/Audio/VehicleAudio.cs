using UnityEngine;

namespace Apex {
    [DefaultExecutionOrder(40), DisallowMultipleComponent]
    public sealed class VehicleAudio : MonoBehaviour {
        RaceCar car;
        DrivingSounds bank;
        AudioSource low, high, redline, asphalt, kerb, grass, scrub, actions;
        float rpm, nextImpact;
        int lastGear;
        bool lastReverse, wasBoosting;

        // Attach only to participants in the session. Showroom models and offline solvers are silent.
        public static void Attach(RaceCar car, DrivingSounds bank){
            if(!bank)return;
            var audio=car.gameObject.AddComponent<VehicleAudio>();
            audio.car=car;
            audio.bank=bank;
            audio.rpm=car.spec.idle;
            audio.lastGear=car.Gear;
            audio.lastReverse=car.Reversing;
            audio.low=audio.Source(bank.engineLow);
            audio.high=audio.Source(bank.engineHigh);
            if(bank.engineRedline)audio.redline=audio.Source(bank.engineRedline);
            audio.asphalt=audio.Source(bank.asphalt);
            audio.kerb=audio.Source(bank.kerb);
            audio.grass=audio.Source(bank.grass);
            audio.scrub=audio.Source(bank.tyreScrub);
            audio.actions=audio.Source(null);
        }
        AudioSource Source(AudioClip loop){
            var source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.spatialBlend=1;
            source.dopplerLevel=0;
            source.minDistance=bank.fullVolumeDistance;
            if(loop){
                source.loop=true;
                source.clip=loop;
                source.volume=0;
                source.Play();
            }
            return source;
        }

        void FixedUpdate(){
            float blend=1-Mathf.Exp(-Time.fixedDeltaTime/bank.responseSeconds);
            rpm=Mathf.Lerp(rpm,car.EngineRpm,blend);
            float band=Mathf.SmoothStep(0,1,Mathf.InverseLerp(bank.crossfadeRpm.x,bank.crossfadeRpm.y,rpm));
            float top=redline?Mathf.SmoothStep(0,1,Mathf.InverseLerp(bank.redlineCrossfadeRpm.x,bank.redlineCrossfadeRpm.y,rpm)):0;
            float load=car.AppliedCommand.throttle;
            if(car.spec.competition && car.Driveline.FuelCut)load=0;
            float gain=Mathf.Lerp(bank.idleGain,bank.powerGain,load);
            Mix(low,gain*Mathf.Sqrt(1-band),rpm/bank.lowRpm,blend);
            Mix(high,gain*Mathf.Sqrt(band*(1-top)),rpm/bank.highRpm,blend);
            if(redline)Mix(redline,gain*Mathf.Sqrt(band*top),rpm/bank.redlineRpm,blend);

            float paved=0, ridges=0, verge=0, sliding=0;
            for(int i=0;i<car.wheels.Length;i++){
                var wheel=car.wheels[i];
                if(!wheel.GetGroundHit(out var hit))continue;
                var velocity=car.Chassis.GetPointVelocity(hit.point);
                float slip=RaceCar.ContactSlipSpeed(wheel,hit,velocity);
                float slide=Mathf.InverseLerp(bank.audibleSlip.x,bank.audibleSlip.y,slip);
                float roll=Mathf.Clamp01(Vector3.ProjectOnPlane(velocity,hit.normal).magnitude/bank.roadReferenceSpeed);
                switch(car.WheelCover(i)){
                    case RoadCover.Grass: verge+=Mathf.Max(roll,slide);break;
                    case RoadCover.Kerb: ridges+=roll;sliding+=slide*slide;break;
                    default: paved+=roll;sliding+=slide*slide;break;
                }
            }
            float n=car.wheels.Length;
            float pace=Mathf.Clamp(car.Speed/bank.roadReferenceSpeed,.35f,2.5f);
            Mix(asphalt,bank.asphaltGain*Mathf.Sqrt(paved/n),.70f+.3f*pace,blend);
            Mix(kerb,bank.kerbGain*Mathf.Sqrt(ridges/n),pace,blend);
            Mix(grass,bank.grassGain*Mathf.Sqrt(verge/n),.65f+.35f*pace,blend);
            float squeal=Mathf.Sqrt(sliding/n);
            Mix(scrub,bank.scrubGain*squeal,.82f+.32f*squeal,blend);

            if(car.EnabledDrive && (car.Gear!=lastGear || car.Reversing!=lastReverse))
                Play(bank.shift,bank.shiftGain);
            bool boosting=car.BoostRemaining>0;
            if(boosting && !wasBoosting)Play(bank.boost,bank.boostGain);
            lastGear=car.Gear;lastReverse=car.Reversing;wasBoosting=boosting;
        }

        static void Mix(AudioSource source,float volume,float pitch,float blend){
            source.volume=Mathf.Lerp(source.volume,volume,blend);
            source.pitch=Mathf.Clamp(pitch,.1f,3);
        }
        void Play(AudioClip clip,float gain){
            if(clip)actions.PlayOneShot(clip,gain);
        }
        void OnCollisionEnter(Collision hit){
            if(!bank || Time.fixedTime<nextImpact)return;
            float deltaSpeed=hit.impulse.magnitude/car.Chassis.mass;
            float strength=Mathf.InverseLerp(bank.impactDeltaSpeed.x,bank.impactDeltaSpeed.y,deltaSpeed);
            if(strength<=0)return;
            Play(bank.impact,bank.impactGain*Mathf.Sqrt(strength));
            nextImpact=Time.fixedTime+bank.impactCooldown;
        }
    }
}
