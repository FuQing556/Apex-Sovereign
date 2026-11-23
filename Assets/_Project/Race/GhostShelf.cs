using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Apex {
// Slot zero is reserved for the fastest saved run. Each recording appears exactly once.
public sealed class GhostShelf {
    [Serializable] sealed class Index {
        public string active;
        public string[] ids;
    }
    readonly string indexPath;
    readonly Dictionary<string,GhostSlot> records;
    Index index;
    public int Count=>index.ids.Length;
    public GhostSlot At(int i)=>i>=0 && i<Count && index.ids[i]!=null && records.TryGetValue(index.ids[i],out var s)?s:null;
    public GhostSlot Active=>index.active!=null && records.TryGetValue(index.active,out var s)?s:null;
    public bool IsBest(GhostHeader h)=>At(0)==null || h.Duration<At(0).info.Duration-.0001f;
    public bool MovesBest(GhostHeader h)=>IsBest(h) && At(0)!=null;
    public GhostShelf(string track,string car){
        using var sha=SHA256.Create();
        var key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(track+"\n"+car))).Replace("-","");
        indexPath=Path.Combine(GhostArchive.Folder,key+".slots.json");
        var existing=GhostArchive.Scan(track,car);
        records=existing.ToDictionary(s=>s.info.id);
        if(File.Exists(indexPath)){
            index=JsonUtility.FromJson<Index>(File.ReadAllText(indexPath));
            if(index?.ids==null || index.ids.Length<2)throw new InvalidDataException("回放槽位文件损坏");
        }else{
            index=new Index{ids=new string[Math.Max(5,existing.Count)],active=existing.FirstOrDefault()?.info.id};
            for(int i=0;i<existing.Count;i++)index.ids[i]=existing[i].info.id;
        }
    }
    void Publish(Index next){
        Directory.CreateDirectory(GhostArchive.Folder);
        string temp=indexPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            File.WriteAllText(temp,JsonUtility.ToJson(next));
            if(File.Exists(indexPath))File.Replace(temp,indexPath,null);
            else File.Move(temp,indexPath);
            index=next;
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void SetActive(int slot){
        var next=new Index{ids=index.ids,active=At(slot)?.info.id};
        Publish(next);
    }
    public void Save(GhostHeader h,List<GhostRecording.Frame> frames,string name,int slot,bool useForRace){
        if(string.IsNullOrWhiteSpace(name) || name.Trim().Length>32)throw new ArgumentException("请输入 1 至 32 个字的名称");
        h.count=frames.Count;
        bool first=At(0)==null,best=IsBest(h);
        if(first)slot=0;
        if(slot<0 || slot>=Count || !first && slot==0)throw new ArgumentException("请选择普通槽位");
        var previous=At(slot);
        if(previous!=null && previous==Active && !useForRace)throw new InvalidOperationException("此槽位正在参赛。请选择其他槽位，或同时更换参赛回放。");
        // A missing index must be committed before adding a clip so a failed write cannot auto-import it.
        if(!File.Exists(indexPath))Publish(index);
        var next=new Index{ids=(string[])index.ids.Clone(),active=useForRace?h.id:index.active};
        next.ids[slot]=best && !first?index.ids[0]:h.id;
        if(best)next.ids[0]=h.id;
        h.title=name.Trim();
        GhostArchive.WriteClip(h,frames);
        string path=Path.Combine(GhostArchive.Folder,h.id+".ghost");
        try{Publish(next);}catch{File.Delete(path);throw;}
        records[h.id]=new GhostSlot{info=h,path=path};
        if(previous!=null){
            records.Remove(previous.info.id);
            if(previous.path!=null){
                try{File.Delete(previous.path);}catch(IOException e){Debug.LogWarning(e.Message);}
            }
        }
    }
}
}
