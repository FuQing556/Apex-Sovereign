using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apex {
// Sample after the controller has issued wheel commands. Body velocities and
// wheel contacts describe the last completed physics step.
[DefaultExecutionOrder(500)]
public sealed class DriveTelemetry : MonoBehaviour {
    RaceSession session;
    RaceCar car;
    StreamWriter writer;
    int tick;
    bool mark,collision;
    string driverName,vehicleName;
    IDriver previousDriver;
    readonly StringBuilder row=new StringBuilder(768);
    readonly VehicleStepTrace steps=new VehicleStepTrace();
    static readonly CultureInfo Culture=CultureInfo.InvariantCulture;

    public void Begin(RaceSession owner,RaceCar vehicle,string track){
        session=owner; car=vehicle;
        vehicleName=car.spec.name.Replace("\"","\"\"");
        try {
#if UNITY_EDITOR
            var root=Path.Combine(Application.dataPath,"..","Logs","DriveTelemetry");
#else
            var root=Path.Combine(Application.persistentDataPath,"DriveTelemetry");
#endif
            Directory.CreateDirectory(root);
            string file=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",Culture)+"-"+track+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv";
            writer=new StreamWriter(Path.Combine(root,file),false,Encoding.UTF8,32768);
            writer.Write("tick,sim_s,wall_s,phase,lap,next_gate,driver,focused,drive_enabled,mark,contact_body,A,D,W,S,space,shift,pad_x,steer_cmd,throttle,brake,hand,boost,reverse,x,y,z,speed_mps,body_lateral_mps,body_forward_mps,yaw_deg_s,gear,boost_left_s");
            foreach(var wheel in new[]{"FL","FR","RL","RR"})
                foreach(var field in new[]{"steer_deg","rpm","drive_nm","brake_nm","ground","load_n","forward_slip","side_slip"})
                    writer.Write(","+wheel+"_"+field);
            writer.Write(",input_frame,keyboard_id,drift_engaged,hand_applied,signed_drift_deg,front_axle_x,front_axle_y,front_axle_z,rear_axle_x,rear_axle_y,rear_axle_z");
            writer.Write(",vehicle,engine_rpm,gear_input_rpm,clutch_pedal,clutch_engagement,clutch_nm,charge_fraction,front_bar,rear_bar,hand_bar,FL_disc_c,FR_disc_c,RL_disc_c,RR_disc_c");
            writer.Write(",manual_gears,fuel_cut,FL_tyre_c,FR_tyre_c,RL_tyre_c,RR_tyre_c,FL_tyre_bar,FR_tyre_bar,RL_tyre_bar,RR_tyre_bar");
            writer.Write(",setup_revision,FL_road_long,FL_road_lat,FR_road_long,FR_road_lat,RL_road_long,RL_road_lat,RR_road_long,RR_road_lat");
            foreach(var wheel in new[]{"FL","FR","RL","RR"})
                writer.Write(","+wheel+"_carcass_c,"+wheel+"_gas_c,"+wheel+"_slip_w");
            writer.Write(",advanced_controls,down_edge,up_edge,auto_edge,gear_request,auto_request,FL_rolling_w,FR_rolling_w,RL_rolling_w,RR_rolling_w,FL_cover,FR_cover,RL_cover,RR_cover,differential_part,rear_wheel_part");
            row.Clear();VehicleStepTrace.Headers(row);writer.WriteLine(row.ToString());writer.Flush();
        } catch(IOException e){Fail(e);}catch(UnauthorizedAccessException e){Fail(e);}
    }
    void Update(){if(Keyboard.current?.f8Key.wasPressedThisFrame==true)mark=true;}
    void OnCollisionStay(Collision hit){collision=true;if(car)steps.Contact(hit,car.Chassis);}
    void OnCollisionEnter(Collision hit){collision=true;if(car)steps.Contact(hit,car.Chassis);}
    void Field(float value){row.Append(',').Append(value.ToString("0.####",Culture));}
    void Flag(bool value){row.Append(value?",1":",0");}
    void FixedUpdate(){
        if(writer==null)return;
        try {
            row.Clear();row.Append(++tick);
            row.Append(',').Append(Time.fixedTimeAsDouble.ToString("F4",Culture));
            row.Append(',').Append(Time.realtimeSinceStartupAsDouble.ToString("F4",Culture));
            row.Append(',').Append(session.State).Append(',').Append(session.CurrentLap).Append(',').Append(session.NextCheckpoint);
            if(previousDriver!=car.Driver){
                previousDriver=car.Driver;
                driverName=previousDriver?.GetType().Name;
            }
            row.Append(',').Append(driverName);
            Flag(Application.isFocused);Flag(car.EnabledDrive);Flag(mark);Flag(collision);
            var input=car.AppliedInput;
            Flag(input.left);Flag(input.right);Flag(input.forward);Flag(input.back);
            Flag(input.space);Flag(input.shift);Field(input.padX);
            var c=car.AppliedCommand;Field(c.steer);Field(c.throttle);Field(c.brake);Field(c.hand);Flag(car.BoostPower>0);Flag(c.reverse);
            var body=car.Chassis;var pos=body.position;var velocity=Quaternion.Inverse(body.rotation)*body.linearVelocity;
            Field(pos.x);Field(pos.y);Field(pos.z);Field(car.Speed);Field(velocity.x);Field(velocity.z);
            Field((Quaternion.Inverse(body.rotation)*body.angularVelocity).y*Mathf.Rad2Deg);
            Field(car.Gear+1);Field(car.BoostRemaining);
            foreach(var w in car.wheels){
                Field(w.steerAngle);Field(w.rpm);Field(w.motorTorque);Field(w.brakeTorque);
                bool grounded=w.GetGroundHit(out var hit);Flag(grounded);Field(hit.force);Field(hit.forwardSlip);Field(hit.sidewaysSlip);
            }
            row.Append(',').Append(input.frame).Append(',').Append(input.keyboardId);
            Flag(car.DriftEngaged);Field(car.HandbrakeApplied);Field(car.SignedDriftAngle);
            for(int axle=0;axle<2;axle++){
                int first=axle*2;
                var local=(car.transform.InverseTransformPoint(car.wheels[first].transform.position)+
                    car.transform.InverseTransformPoint(car.wheels[first+1].transform.position))*.5f;
                var axlePosition=body.position+body.rotation*local;
                Field(axlePosition.x);Field(axlePosition.y);Field(axlePosition.z);
            }
            row.Append(',').Append('"').Append(vehicleName).Append('"');
            if(car.CompetitionChassis!=null){
                var drive=car.Driveline;var chassis=car.CompetitionChassis;
                Field(drive.EngineRpm);Field(drive.ShaftRpm);Field(c.clutch);Field(drive.Engagement);Field(drive.ClutchNm);Field(drive.Charge);
                Field(chassis.FrontBar);Field(chassis.RearBar);Field(chassis.HandBar);
                for(int i=0;i<4;i++)Field(chassis.DiscC(i));
                Flag(car.ManualGears);Flag(drive.FuelCut);
                for(int i=0;i<4;i++)Field(chassis.TyreC(i));
                for(int i=0;i<4;i++)Field(chassis.TyreBar(i));
                Field(car.spec.competition.revision);
            }else row.Append(',',24);
            for(int i=0;i<4;i++){var grip=car.WheelGrip(i);Field(grip.x);Field(grip.y);}
            if(car.CompetitionChassis!=null){
                var chassis=car.CompetitionChassis;
                for(int i=0;i<4;i++){Field(chassis.CarcassC(i));Field(chassis.GasC(i));Field(chassis.SlipPowerW(i));}
            }else row.Append(',',12);
            var driver=car.Driver as LocalDriver;
            int edges=car.EnabledDrive && driver!=null?driver.CapturedGearEdges:0;
            Flag(driver!=null && driver.AdvancedControls);Flag((edges&1)!=0);Flag((edges&2)!=0);Flag((edges&4)!=0);
            Field(c.gearChange);Flag(c.resumeAutomatic);
            for(int i=0;i<4;i++)Field(car.CompetitionChassis?.RollingPowerW(i)??0);
            for(int i=0;i<4;i++)row.Append(',').Append(TyreSurface.Label(car.WheelCover(i)));
            row.Append(',').Append(car.Differential?.id??"original").Append(',').Append(car.RearWheels?.id??"original");
            steps.Append(row,car,Time.fixedDeltaTime);writer.WriteLine(row.ToString());
            if(tick%50==0 || mark)writer.Flush();
            mark=collision=false;
            if(session.State==RaceSession.Phase.Results)Close();
        } catch(IOException e){Fail(e);}catch(UnauthorizedAccessException e){Fail(e);}
    }
    void Fail(Exception e){Debug.LogWarning("Drive telemetry stopped: "+e.Message);Close();}
    void Close(){var stream=writer;writer=null;try{stream?.Dispose();}catch(IOException e){Debug.LogWarning(e.Message);}}
    void OnDestroy(){Close();}
    void OnApplicationQuit(){Close();}
}
}
