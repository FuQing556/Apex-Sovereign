using System;
using UnityEngine;

namespace Apex {
[PreferBinarySerialization]
public sealed class GhostRecording : ScriptableObject {
    public string title;
    public CarSpec vehicle;
    public Material material;
    public Color tint;
    public float sampleInterval,firstLap;
    public Frame[] frames;
    public GhostHeader header;
    public float Duration => (frames.Length-1)*sampleInterval;
    [Serializable] public struct Frame {
        public Pose chassis,fl,fr,rl,rr;
        public Vector2 steer;
        public Vector4 temperature;
        public float kph,beta,rpm;
        public int gear,keys,turn;
        public int gearRequest;
        public Vector3 pad;
        public Pose Wheel(int i)=>i switch{0=>fl,1=>fr,2=>rl,_=>rr};
    }
}
}
