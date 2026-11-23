using System.Globalization;
using System.Text;
using UnityEngine;

namespace Apex {
// Sample immediately before Simulate. Delta H belongs to the interval ending
// at this sample; applied forces, wheel commands and inertia belong to its start.
public sealed class VehicleStepTrace {
    public static Vector3 Momentum(Rigidbody body) {
        Quaternion axes=body.rotation*body.inertiaTensorRotation;
        return axes*Vector3.Scale(body.inertiaTensor,Quaternion.Inverse(axes)*body.angularVelocity);
    }
    public static void Headers(StringBuilder s){
        foreach(var key in new[]{"world_com","world_v","world_omega","mass_axes_I","world_H","next_script_F","next_script_M",
                "next_aero_M","next_bar_M","next_rolling_M","prev_net_M","prev_residual_M","prev_collision_J","prev_collision_K"})
            s.Append(',').Append(key).Append("_x,").Append(key).Append("_y,").Append(key).Append("_z");
        s.Append(",body_qx,body_qy,body_qz,body_qw,inertia_local_qx,inertia_local_qy,inertia_local_qz,inertia_local_qw,reset_generation,prev_balance_valid,prev_contact_count,prev_contact_object,keyboard_recovery_enabled,next_step_s");
        foreach(var w in new[]{"FL","FR","RL","RR"})
            foreach(var k in new[]{"hit_x","hit_y","hit_z","normal_x","normal_y","normal_z","wheel_accel_rad_s2","long_stiff","lat_stiff"})
                s.Append(',').Append(w).Append('_').Append(k);
    }
    static void N(StringBuilder s,float x)=>s.Append(',').Append(x.ToString("G9",CultureInfo.InvariantCulture));
    static void V(StringBuilder s,Vector3 v){N(s,v.x);N(s,v.y);N(s,v.z);}
    static void Q(StringBuilder s,Quaternion q){V(s,new Vector3(q.x,q.y,q.z));N(s,q.w);}
    readonly float[] rpm=new float[4];
    Vector3 previousH,previousTorque,impulse,angularImpulse;
    int previousReset,contacts;
    float previousDt;
    string contactName="";
    bool sampled;
    public void Contact(Collision c,Rigidbody rb){
        contactName=c.collider.name.Replace(',','_').Replace('\n','_').Replace('\r','_');impulse+=c.impulse;
        for(int i=0;i<c.contactCount;i++){
            var point=c.GetContact(i);
            angularImpulse+=Vector3.Cross(point.point-rb.worldCenterOfMass,point.impulse);
            contacts++;
        }
    }
    public void Append(StringBuilder s,RaceCar car,float dt){
        var b=car.Chassis;var chassis=car.CompetitionChassis;
        var h=Momentum(b);
        bool valid=sampled && previousReset==car.ResetGeneration;
        var net=valid?(h-previousH)/previousDt:Vector3.zero;
        V(s,b.worldCenterOfMass);V(s,b.linearVelocity);V(s,b.angularVelocity);V(s,b.inertiaTensor);V(s,h);
        V(s,chassis?.AppliedForce??Vector3.zero);V(s,chassis?.AppliedTorque??Vector3.zero);
        V(s,chassis?.AeroTorque??Vector3.zero);V(s,chassis?.RollBarTorque??Vector3.zero);V(s,chassis?.RollingTorque??Vector3.zero);
        V(s,net);V(s,valid?net-previousTorque:Vector3.zero);V(s,impulse);V(s,angularImpulse);
        Q(s,b.rotation);Q(s,b.inertiaTensorRotation);s.Append(',').Append(car.ResetGeneration).Append(valid?",1":",0").Append(',').Append(contacts).Append(',').Append(contactName);
        s.Append(car.AppliedCommand.keyboardRecovery?",1":",0");N(s,dt);
        for(int i=0;i<4;i++){
            var w=car.wheels[i];w.GetGroundHit(out var hit);
            V(s,hit.point);V(s,hit.normal);N(s,valid?(w.rpm-rpm[i])*Mathf.PI/(30*previousDt):0);
            N(s,w.forwardFriction.stiffness);N(s,w.sidewaysFriction.stiffness);rpm[i]=w.rpm;
        }
        previousReset=car.ResetGeneration;previousH=h;previousTorque=chassis?.AppliedTorque??Vector3.zero;
        previousDt=dt;sampled=true;contacts=0;contactName="";impulse=angularImpulse=Vector3.zero;
    }
}
}
