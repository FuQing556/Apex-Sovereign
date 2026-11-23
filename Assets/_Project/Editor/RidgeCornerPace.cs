using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Apex.Editor {
public static class RidgeCornerPace {
    static readonly int[] ApexM={116,212,304,374,442,506,588,664,734,826,920,992,1082,1198,1298,1374};
    static string FileAt(string file)=>Path.Combine(RidgeLineSearch.Folder,file);
    static float Number(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
    static RidgeLineSearch.Policy Copy(RidgeLineSearch.Policy p)=>JsonUtility.FromJson<RidgeLineSearch.Policy>(JsonUtility.ToJson(p));
    static float Interpolate(float a,float b,float crossing,float sa,float sb)=>Mathf.Lerp(a,b,Mathf.InverseLerp(sa,sb,crossing));

    // Each rival starts from the cold grid with the same inputs until the selected corner.
    static void Compare(int slot,int mode,RidgeLineSearch.Policy tune=null,int variant=-1){
        var p=RidgeLineSearch.Saved("pair-base");
        var modes=new string('0',32).ToCharArray();modes[slot]=(char)('0'+mode);p.modes=new string(modes);
        p.lead=tune?.lead??23;p.kick=tune?.kick??.76f;p.hand=tune?.hand??.34f;
        p.flick=tune?.flick??.32f;p.slip=tune?.slip??22;p.name=variant<0?$"control-{slot}-{mode}":$"exit-{slot}-{mode}-{variant}";
        int apex=ApexM[slot%16];
        float s0=apex-40,s1=apex+26,prevS=0,prevV=0,prevT=0,t0=0,t1=0,entry=0,exit=0,minimum=float.PositiveInfinity;
        float foot=0,hand=0,peak=0,betaAtEntry=0;Vector3 previous=default,start=default;
        try{
            RidgeLineSearch.OnFrame=(car,tick,keys,section,station)=>{
                if(section/16!=slot/16 || tick<0)return;
                float time=(tick+1)*Time.fixedDeltaTime,speed=car.Speed*3.6f;
                if(station>prevS && station-prevS<12){
                    if(prevS<s0 && station>=s0 && t0==0){
                        t0=Interpolate(prevT,time,s0,prevS,station);entry=Interpolate(prevV,speed,s0,prevS,station);
                        start=Vector3.Lerp(previous,car.Chassis.position,Mathf.InverseLerp(prevS,station,s0));
                        betaAtEntry=car.SignedDriftAngle;minimum=entry;
                    }
                    if(prevS<s1 && station>=s1 && t0>0 && t1==0){
                        t1=Interpolate(prevT,time,s1,prevS,station);exit=Interpolate(prevV,speed,s1,prevS,station);
                    }
                }
                if(t0>0 && (t1==0 || prevT<t1)){
                    float dt=Mathf.Max(0,Mathf.Min(time,t1>0?t1:time)-Mathf.Max(t0,prevT));
                    if((keys&8)!=0)foot+=dt;if((keys&16)!=0)hand+=dt;
                    minimum=Mathf.Min(minimum,t1>0?exit:speed);peak=Mathf.Max(peak,Mathf.Abs(car.SignedDriftAngle));
                }
                prevS=station;prevT=time;prevV=speed;previous=car.Chassis.position;
            };
            var row=RidgeLineSearch.Run(JsonUtility.ToJson(p),stopSlot:slot).Split(',');
            string status=t1>t0 && t0>0 && row[8]=="0" && row[9]=="0"?"ok":row[1];
            string detail=variant<0?"":FormattableString.Invariant($",{variant},{p.lead:R},{p.kick:R},{p.hand:R},{p.flick:R},{p.slip:R}");
            File.AppendAllText(FileAt(variant<0?"corner-controls.csv":"exit-search.csv"),FormattableString.Invariant(
                $"{slot/16+1},{slot%16+1},{mode},{status},{t0:F5},{entry:F3},{minimum:F3},{exit:F3},{t1-t0:F4},{foot:F3},{hand:F3},{peak:F3},{start.x:F5},{start.z:F5},{betaAtEntry:F4},{row[10].Split('/')[slot]}{detail}\n"));
        }finally{RidgeLineSearch.OnFrame=null;}
    }
    public static void ExploreExit(){
        File.WriteAllText(FileAt("exit-search.csv"),File.ReadLines(FileAt("corner-controls.csv")).First()+",variant,lead,kick,hand,flick,slip\n");
        var rng=new System.Random(23306);int index=0;
        float Range(float lo,float hi)=>lo+(hi-lo)*(float)rng.NextDouble();
        void Batch(){
            try{
                for(int j=0;j<6 && index<240;j++,index++){
                    int slot=index%2==0?21:17,mode=(index/2)%3==0?2:1;
                    var p=new RidgeLineSearch.Policy{lead=Range(11,37),kick=Range(.3f,1.15f),hand=Range(.12f,.55f),flick=Range(.1f,.45f),slip=Range(13,25)};
                    Compare(slot,mode,p,index);
                }
                if(index<240)EditorApplication.delayCall+=Batch;
                else File.AppendAllText(FileAt("pace-progress.txt"),"exit sweep complete\n");
            }catch(Exception e){File.AppendAllText(FileAt("pace-progress.txt"),e.ToString());Debug.LogException(e);}
        }
        EditorApplication.delayCall+=Batch;
    }
    public static void Schedule(){
        RidgeLineSearch.ExportGeometry();
        string progress=FileAt("pace-progress.txt");File.WriteAllText(progress,"starting\n");
        File.WriteAllText(FileAt("corner-controls.csv"),"lap,corner,mode,status,start_t,entry_kph,min_kph,exit_kph,seconds,foot_s,hand_s,peak_slip,start_x,start_z,entry_slip,qualifying_s\n");
        var original=RidgeLineSearch.Saved("all-drift");
        var best=Copy(original);best.cornerPace=new float[32];best.brakeMargin=new float[32];
        float bestTime=128.42f;float mixedTime=bestTime;var mixed=Copy(best);
        int index=0;
        void Trial(RidgeLineSearch.Policy p){
            p.name="pace-trial-"+index;var row=RidgeLineSearch.Run(JsonUtility.ToJson(p)).Split(',');
            if(row[1]!="finish" || row[4]!="68" || row[8]!="0" || row[9]!="0")return;
            int drift=row[10].Split('/').Count(s=>Number(s)>=.40f);float time=Number(row[2]);
            if(drift==32 && time<bestTime){
                bestTime=time;best=Copy(p);File.WriteAllText(FileAt("pace32.json"),JsonUtility.ToJson(best));
            }
            if(drift>=20 && time<mixedTime){
                mixedTime=time;mixed=Copy(p);File.WriteAllText(FileAt("pace-mixed.json"),JsonUtility.ToJson(mixed));
            }
        }
        void Batch(){
            try{
                for(int j=0;j<5;j++,index++){
                    if(index<96){Compare(index/3,index%3);continue;}
                    int n=index-96;
                    if(n<32){
                        var p=Copy(best);float blend=(n%8+1)/8f;
                        p.lat=10+blend*(n/8%2==0?2.4f:1.1f);p.braking=8+1.4f*blend;
                        p.width=3+blend*(n/8>=2?4.2f:1.4f);
                        p.brakeMargin=Enumerable.Repeat(n/8>=2?2f:0,32).ToArray();
                        Trial(p);
                    }else if(n<32+32*6){
                        int local=n-32,slot=31-local/6,k=local%6;var p=Copy(best);
                        float[] speeds={.6f,1.4f,2.8f,0,1.2f,3.5f};
                        float[] coast={0,0,0,12,3,12};
                        p.cornerPace[slot]+=speeds[k];p.brakeMargin[slot]=coast[k];Trial(p);
                    }else {
                        best.name="carry32";var a=RidgeLineSearch.Run(JsonUtility.ToJson(best),true);
                        mixed.name="carry-mixed";var b=RidgeLineSearch.Run(JsonUtility.ToJson(mixed),true);
                        File.AppendAllText(progress,"done "+a+"\n"+b+"\n");return;
                    }
                }
                File.AppendAllText(progress,$"{index} | {bestTime:F2} | {mixedTime:F2}\n");
                EditorApplication.delayCall+=Batch;
            }catch(Exception e){File.AppendAllText(progress,e.ToString());Debug.LogException(e);}
        }
        EditorApplication.delayCall+=Batch;
    }
}
}
