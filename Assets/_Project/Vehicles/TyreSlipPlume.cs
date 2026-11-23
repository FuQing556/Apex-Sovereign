using UnityEngine;

namespace Apex {
    [RequireComponent(typeof(RaceCar))]
    public sealed class TyreSlipPlume : MonoBehaviour {
        public Material plume;
        [Min(0)] public float onsetMetresPerSecond = 2.4f;
        [Min(.1f)] public float fullPlumeMetresPerSecond = 14;
        [Min(0)] public float particlesPerSecond = 42;
        public Color rubberColour = new Color(.83f,.85f,.87f,.46f);
        public Color dustColour = new Color(.54f,.46f,.29f,.32f);

        RaceCar car;
        ParticleSystem smoke;
        const float MaxLifetime=1.55f;
        float[] remainder;

        void Start() {
            car=GetComponent<RaceCar>();
            if(!plume){enabled=false;return;}
            remainder=new float[car.wheels.Length];
            var emitter=new GameObject("轮胎烟尘");
            emitter.transform.SetParent(transform,false);
            smoke=emitter.AddComponent<ParticleSystem>();
            smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.playOnAwake=false; main.maxParticles=Mathf.Max(1,car.wheels.Length*Mathf.CeilToInt(particlesPerSecond*MaxLifetime)); main.startLifetime=1.4f;
            var emission=smoke.emission; emission.enabled=false;
            var shape=smoke.shape; shape.enabled=false;
            var size=smoke.sizeOverLifetime; size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.24f,1,1.8f));
            var fade=smoke.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(.3f,0),new GradientAlphaKey(1,.11f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var renderer=smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=plume;renderer.sortMode=ParticleSystemSortMode.Distance;
            smoke.Play();
        }

        void FixedUpdate() {
            if(!smoke)return;
            for(int i=0;i<car.wheels.Length;i++){
                var wheel=car.wheels[i];
                if(!wheel.GetGroundHit(out var hit)){remainder[i]=0;continue;}
                var velocity=car.Chassis.GetPointVelocity(hit.point);
                float slip=RaceCar.ContactSlipSpeed(wheel,hit,velocity);
                float amount=Mathf.InverseLerp(onsetMetresPerSecond,fullPlumeMetresPerSecond,slip);
                if(amount<=0){remainder[i]=0;continue;}
                remainder[i]+=amount*particlesPerSecond*Time.fixedDeltaTime;
                int count=Mathf.FloorToInt(remainder[i]);remainder[i]-=count;
                if(count==0)continue;
                var p=new ParticleSystem.EmitParams {
                    position=hit.point+hit.normal*.12f,
                    velocity=velocity*.10f+hit.normal*(.6f+.65f*amount),
                    startSize=.62f+.75f*amount,startLifetime=Mathf.Lerp(.8f,MaxLifetime,amount),
                    rotation=(Time.fixedTime*137+i*53)%360,
                    startColor=car.WheelCover(i)==RoadCover.Grass?dustColour:rubberColour
                };
                smoke.Emit(p,count);
            }
        }
    }
}
