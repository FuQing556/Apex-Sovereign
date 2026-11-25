using UnityEngine;
using UnityEngine.InputSystem;
namespace Apex {
public struct DriveCommand {
    public float steer, throttle, brake, hand, clutch;
    public bool reverse, digitalSteering, keyboardRecovery;
    public int gearChange;
    public bool resumeAutomatic;
    public static DriveCommand Pedals(float forward,float backward,float speed,float threshold){
        if(forward>0 && backward>0)return new DriveCommand{brake=Mathf.Max(forward,backward)};
        bool reverse=backward>0;
        float amount=reverse?backward:forward;
        bool opposing=reverse?speed>threshold:speed < -threshold;
        return new DriveCommand{reverse=reverse && !opposing,brake=opposing?amount:0,throttle=opposing?0:amount};
    }
}
public interface IDriver { DriveCommand Read(); }
public struct DriverInputFrame {
    public int frame,keyboardId;
    public bool left,right,forward,back,space,shift,padHand,clutch;
    public float padX,padForward,padBack;
}
public sealed class LocalDriver : IDriver {
    readonly RaceCar car;
    int pendingGear;
    bool pendingAutomatic;
    public bool AdvancedControls {get;}
    public int CapturedGearEdges {get;private set;}
    int pendingEdges;
    bool keyboardSteering = true;
    public DriverInputFrame Input {get;private set;}
    readonly bool keyboardRecovery;
    public LocalDriver(RaceCar car,bool advancedControls=false,bool keyboardRecovery=true){
        this.car=car;AdvancedControls=advancedControls;this.keyboardRecovery=keyboardRecovery;
    }
    public void Capture(){
        var k=Keyboard.current; var p=Gamepad.current;
        if(car.EnabledDrive && car.spec.competition){
            bool up=k?.eKey.wasPressedThisFrame==true || p?.dpad.up.wasPressedThisFrame==true;
            bool down=k?.qKey.wasPressedThisFrame==true || p?.dpad.down.wasPressedThisFrame==true;
            bool auto=k?.xKey.wasPressedThisFrame==true || p?.dpad.left.wasPressedThisFrame==true;
            pendingEdges|=(down?1:0)|(up?2:0)|(auto?4:0);
            if(AdvancedControls){
                if(up!=down){pendingGear=up?1:-1;pendingAutomatic=false;}
                if(auto){pendingAutomatic=true;pendingGear=0;}
            }
        }
        Input=new DriverInputFrame{frame=Time.frameCount,keyboardId=k?.deviceId??-1,
            left=k?.aKey.isPressed==true,right=k?.dKey.isPressed==true,forward=k?.wKey.isPressed==true,back=k?.sKey.isPressed==true,
            space=k?.spaceKey.isPressed==true,shift=k?.shiftKey.isPressed==true,
            clutch=AdvancedControls && (k?.leftCtrlKey.isPressed==true || p?.leftShoulder.isPressed==true),
            padX=p?.leftStick.x.ReadValue()??0,padForward=p?.rightTrigger.ReadValue()??0,padBack=p?.leftTrigger.ReadValue()??0,padHand=p?.buttonEast.isPressed==true};
    }
    public void Release(){Input=new DriverInputFrame{frame=Time.frameCount,keyboardId=-1};pendingGear=pendingEdges=CapturedGearEdges=0;pendingAutomatic=false;}
#if UNITY_EDITOR
    internal void CaptureDemonstrationKeyboard(DriverInputFrame keys){
        Input=keys;
        pendingGear=pendingEdges=0;pendingAutomatic=false;
    }
#endif
    public DriveCommand Read(){
        CapturedGearEdges=pendingEdges;pendingEdges=0;
        var f=Input;
        var command=DriveCommand.Pedals(Mathf.Max(f.padForward,f.forward?1:0),
            Mathf.Max(f.padBack,f.back?1:0),car.ForwardSpeed,car.spec.directionThreshold);
        command.steer=Mathf.Clamp(f.padX+(f.right?1:0)-(f.left?1:0),-1,1);
        command.hand=f.space || f.padHand?1:0;
        command.clutch=f.clutch?1:0;
        command.gearChange=pendingGear;command.resumeAutomatic=pendingAutomatic;
        pendingGear=0;pendingAutomatic=false;
        if(f.padX!=0)keyboardSteering=false;
        else if(f.left || f.right)keyboardSteering=true;
        command.digitalSteering=keyboardSteering;
        command.keyboardRecovery=keyboardRecovery && keyboardSteering;
        if(car.spec.competition && car.Speed>car.spec.directionThreshold){
            bool reverse=car.Reversing;
            float gas=Mathf.Max(f.padForward,f.forward?1:0),back=Mathf.Max(f.padBack,f.back?1:0);
            command.reverse=reverse;
            command.brake=reverse?gas:back;
            command.throttle=command.brake>0?0:(reverse?back:gas);
        }
        if(car.spec.competition && !f.forward && !f.back && f.padForward>0 && f.padBack>0){
            command.reverse=car.Reversing;
            command.throttle=command.reverse?f.padBack:f.padForward;
            command.brake=command.reverse?f.padForward:f.padBack;
        }
        return command;
    }
}
}
