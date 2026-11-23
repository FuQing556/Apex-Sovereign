using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Apex {
[Serializable]
public sealed class GhostHeader {
    public string id,title,track,car,differential,rear,sourceHash;
    public int version=1,count,revision;
    public float interval,firstLap;
    public bool practice,recovery,advanced;
    public float Duration=>(count-1)*interval;
}

public sealed class GhostSlot {
    public GhostHeader info;
    internal TextAsset seed;
    internal string path;
    string NameKey=>"Apex.Ghost.Name."+info.id;
    public string Title=>PlayerPrefs.GetString(NameKey,info.title);
    public void Rename(string value){
        value=value.Trim();
        if(value.Length==0 || value.Length>32)return;
        PlayerPrefs.SetString(NameKey,value);PlayerPrefs.Save();
    }
    public GhostRecording Open(CarSpec car,Material material){
        using var stream=seed?(Stream)new MemoryStream(seed.bytes,false):File.OpenRead(path);
        using var reader=new BinaryReader(stream);
        var header=GhostArchive.Header(reader);
        if(header.id!=info.id || car.name!=header.car)throw new InvalidDataException("幽灵车型或记录不匹配");
        var frames=new GhostRecording.Frame[header.count];
        for(int i=0;i<frames.Length;i++)frames[i]=GhostArchive.Read(reader);
        var clip=ScriptableObject.CreateInstance<GhostRecording>();
        clip.frames=frames;clip.header=header;clip.title=Title;clip.vehicle=car;clip.material=material;
        clip.firstLap=header.firstLap;clip.sampleInterval=header.interval;clip.tint=new Color(.30f,.93f,.82f);
        return clip;
    }
}

public static class GhostArchive {
    public const int Reset=64,Clutch=128,Auto=256,PadHand=512;
    const int BytesPerFrame=47*4+4*4;
    internal static string Folder {
        get {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath,"..","GhostSaves"));
#else
            return Path.Combine(Application.persistentDataPath,"GhostSaves");
#endif
        }
    }
    internal static List<GhostSlot> Scan(string track,string car){
        var slots=new List<GhostSlot>();
        void Add(Stream stream,TextAsset seed,string path){
            using(stream)using(var reader=new BinaryReader(stream)){
                var h=Header(reader);
                if(h.track==track && h.car==car)slots.Add(new GhostSlot{info=h,seed=seed,path=path});
            }
        }
        foreach(var seed in Resources.LoadAll<TextAsset>("GhostSeeds")){
            try{Add(new MemoryStream(seed.bytes,false),seed,null);}catch(IOException e){Debug.LogWarning(e.Message);}
        }
        if(Directory.Exists(Folder)){
            foreach(var path in Directory.EnumerateFiles(Folder,"*.ghost")){
                try{Add(File.OpenRead(path),null,path);}catch(IOException e){Debug.LogWarning(e.Message);}
            }
        }
        // The fastest occupies row zero exactly once. Names belong to records and survive a rank change.
        return slots.GroupBy(s=>s.info.id).Select(g=>g.Last()).OrderBy(s=>s.info.Duration).ThenBy(s=>s.info.id,StringComparer.Ordinal).ToList();
    }
    internal static GhostHeader Header(BinaryReader reader){
        int size=reader.ReadInt32();
        if(size<=0 || size>16384)throw new InvalidDataException("无法读取幽灵文件头");
        var h=JsonUtility.FromJson<GhostHeader>(Encoding.UTF8.GetString(reader.ReadBytes(size)));
        if(h==null || h.version!=1 || h.interval<=0 || h.count<2 ||
           reader.BaseStream.Length-reader.BaseStream.Position!=(long)h.count*BytesPerFrame)
            throw new InvalidDataException("幽灵帧数或版本有误");
        return h;
    }
    static Vector3 Vec(BinaryReader r)=>new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    static Pose PoseFrom(BinaryReader r)=>new(Vec(r),new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()));
    internal static GhostRecording.Frame Read(BinaryReader r)=>new(){
        chassis=PoseFrom(r),fl=PoseFrom(r),fr=PoseFrom(r),rl=PoseFrom(r),rr=PoseFrom(r),
        steer=new Vector2(r.ReadSingle(),r.ReadSingle()),temperature=new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),
        kph=r.ReadSingle(),beta=r.ReadSingle(),rpm=r.ReadSingle(),pad=Vec(r),
        gear=r.ReadInt32(),keys=r.ReadInt32(),turn=r.ReadInt32(),gearRequest=r.ReadInt32()
    };
    static void WriteVector(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
    static void WritePose(BinaryWriter w,Pose p){
        WriteVector(w,p.position);w.Write(p.rotation.x);w.Write(p.rotation.y);w.Write(p.rotation.z);w.Write(p.rotation.w);
    }
    internal static void WriteClip(GhostHeader h,List<GhostRecording.Frame> frames){
        Directory.CreateDirectory(Folder);
        h.count=frames.Count;
        var path=Path.Combine(Folder,h.id+".ghost");var pending=path+".tmp";
        try{
            using(var w=new BinaryWriter(File.Open(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None))){
                var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(h));w.Write(bytes.Length);w.Write(bytes);
                foreach(var f in frames){
                    WritePose(w,f.chassis);for(int i=0;i<4;i++)WritePose(w,f.Wheel(i));
                    w.Write(f.steer.x);w.Write(f.steer.y);for(int i=0;i<4;i++)w.Write(f.temperature[i]);
                    w.Write(f.kph);w.Write(f.beta);w.Write(f.rpm);WriteVector(w,f.pad);
                    w.Write(f.gear);w.Write(f.keys);w.Write(f.turn);w.Write(f.gearRequest);
                }
            }
            File.Move(pending,path);
        }finally{if(File.Exists(pending))File.Delete(pending);}
    }
    public static int Keys(DriverInputFrame f)=>(f.forward?1:0)|(f.left?2:0)|(f.right?4:0)|(f.back?8:0)|
        (f.space?16:0)|(f.shift?32:0)|(f.clutch?Clutch:0)|(f.padHand?PadHand:0);
}
}
