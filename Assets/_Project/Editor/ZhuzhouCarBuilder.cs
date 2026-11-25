using System;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Apex.Editor {
public static class ZhuzhouCarBuilder {
    const string Folder="Assets/_Project/Vehicles/Lynk155/";
    public const string CarPath=Folder+"Zhuzhou155Car.prefab";
    static readonly string[] Corners={"FL","FR","RL","RR"};

    [MenuItem("Apex/Art/Build Zhuzhou 155 car")]
    public static void Build(){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请在编辑模式生成车辆。");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Lynk155.fbx");
        if(!model)throw new InvalidOperationException("缺少株洲 155 号模型。");
        var visual=new GameObject("Zhuzhou155Visual");
        try{
            var geometry=Object.Instantiate(model,visual.transform,false);
            if(PrefabUtility.IsPartOfPrefabInstance(geometry))
                PrefabUtility.UnpackPrefabInstance(geometry,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var mounts=visual.AddComponent<WorkshopMounts>();
            foreach(var corner in Corners){
                var wheel=MakePose(geometry.transform,visual.transform,"Wheel_"+corner);
                MakePose(geometry.transform,visual.transform,"Caliper_"+corner);
                if(corner=="RL")mounts.rearLeft=wheel;
                if(corner=="RR")mounts.rearRight=wheel;
            }
            mounts.coverRadius=.218f;mounts.faceOffset=.126f;
            var body=PrefabUtility.SaveAsPrefabAsset(visual,Folder+"Zhuzhou155Visual.prefab");
            BuildCar(BuildSpec(body));
            AssetDatabase.SaveAssets();
        }finally{Object.DestroyImmediate(visual);}
    }
    static Transform MakePose(Transform geometry,Transform visual,string name){
        var mesh=geometry.Find(name);
        if(!mesh)throw new InvalidOperationException("缺少模型节点 "+name);
        var pose=new GameObject(name+" pose").transform;
        pose.SetParent(visual,false);pose.position=mesh.position;
        mesh.SetParent(pose,true);return pose;
    }
    static T Asset<T>(string name) where T:ScriptableObject{
        var path=Folder+name+".asset";
        var value=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!value){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}
        EditorUtility.SetDirty(value);return value;
    }
    static CarSpec BuildSpec(GameObject visual){
        var setup=Asset<CompetitionSetup>("Zhuzhou155Setup");
        setup.engineInertia=.24f;setup.clutchCapacityNm=540;setup.launchRpm=2500;setup.overrunNm=38;
        setup.limiterRpm=7000;setup.limiterResumeRpm=6800;setup.shiftOpenSeconds=.08f;
        setup.differentialPreloadNm=35;setup.powerLock=.20f;setup.coastLock=.06f;
        setup.frontDiscRadius=.155f;setup.rearDiscRadius=.112f;setup.frontPushrodFraction=.53f;
        setup.ackermannFraction=.65f;setup.keyboardCatchDegrees=8;
        setup.frontTyre.lateral=1.44f;setup.rearTyre.lateral=1.44f;
        setup.frontTyre.longitudinal=1.46f;setup.rearTyre.longitudinal=1.46f;
        setup.frontRollStiffness=12500;setup.rearRollStiffness=9800;
        setup.dragArea=.76f;setup.downforceArea=.42f;
        var s=Asset<CarSpec>("Zhuzhou155");
        s.displayName="领克 03 FL TCR · 155";s.factoryRaceNumber="155";s.factoryLiveryTitle="株洲原厂";s.frontWheelDrive=true;
        s.body=visual;s.factoryPaint=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Materials/Cyan enamel.mat");s.competition=setup;
        s.mass=1265;s.wheelbase=2.76f;s.track=1.658f;s.radius=s.rearRadius=.33f;
        s.visualTyreWidths=new Vector2(.26f,.26f);s.frontWeight=.60f;s.centerHeight=.32f;
        s.frontSpring=74000;s.rearSpring=56000;s.travel=.13f;s.damper=4000;s.wheelMass=22;
        s.upshift=6600;s.downshift=3300;s.idle=1000;s.finalDrive=3.70f;s.gears=new[]{3.0f,2.15f,1.68f,1.38f,1.17f,1.0f};
        s.engineNm=new AnimationCurve(new Keyframe(1000,125),new Keyframe(2200,295),new Keyframe(3300,420),
            new Keyframe(5000,420),new Keyframe(5750,410),new Keyframe(6300,384),new Keyframe(6700,350),new Keyframe(7000,300));
        var curve=s.engineNm;
        for(int i=0;i<curve.length;i++){
            AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);
        }
        s.engineNm=curve;s.enginePowerMap=null;
        s.roadSpeedAutomaticShifts=true;s.handbrakeCutsDrive=false;s.poweredDrift=false;s.boostGain=0;
        s.steeringBySpeed=new AnimationCurve(new Keyframe(0,32),new Keyframe(22,21),new Keyframe(50,10),new Keyframe(72,8));
        return s;
    }
    static void BuildCar(CarSpec spec){
        var root=new GameObject("Zhuzhou155Car");
        try{
            var visual=((GameObject)PrefabUtility.InstantiatePrefab(spec.body,root.transform)).transform;
            var rb=root.AddComponent<Rigidbody>();rb.mass=spec.mass;rb.interpolation=RigidbodyInterpolation.Interpolate;
            var hull=root.AddComponent<BoxCollider>();hull.size=new Vector3(1.89f,.69f,4.49f);hull.center=new Vector3(0,.485f,.06f);
            var cabin=root.AddComponent<BoxCollider>();cabin.size=new Vector3(1.53f,.52f,2.05f);cabin.center=new Vector3(0,1.09f,-.10f);
            var car=root.AddComponent<RaceCar>();car.spec=spec;car.tires=new Transform[4];car.carriers=new Transform[4];car.wheels=new WheelCollider[4];
            for(int i=0;i<Corners.Length;i++){
                var tire=car.tires[i]=visual.Find("Wheel_"+Corners[i]+" pose");
                car.carriers[i]=visual.Find("Caliper_"+Corners[i]+" pose");
                var hub=new GameObject("Suspension "+Corners[i]).transform;hub.SetParent(root.transform,false);
                hub.position=tire.position+Vector3.up*(spec.travel*.5f);
                var wheel=car.wheels[i]=hub.gameObject.AddComponent<WheelCollider>();wheel.radius=spec.radius;
                wheel.mass=spec.wheelMass;wheel.suspensionDistance=spec.travel;
                var spring=wheel.suspensionSpring;spring.spring=i<2?spec.frontSpring:spec.rearSpring;spring.damper=spec.damper;
                spring.targetPosition=.5f;wheel.suspensionSpring=spring;
            }
            PrefabUtility.SaveAsPrefabAsset(root,CarPath);
        }finally{Object.DestroyImmediate(root);}
    }
}
}
