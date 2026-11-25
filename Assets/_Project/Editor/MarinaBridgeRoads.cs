using UnityEngine;
using static Apex.Editor.MarinaStreets;

namespace Apex.Editor {
internal static class MarinaBridgeRoads {
    static readonly Vector3 Up=Vector3.up;
    public const float Approach=42;
    public static void Build(MarinaStreets map,MarinaRiver river,string folder,Transform parent,Material city){
        var deck=new MarinaGeometry();var walks=new MarinaGeometry();
        var rails=new MarinaGeometry();var steel=new MarinaGeometry();
        float half=MarinaRiver.BridgeLength*.5f;
        var axis=map.main[river.bridgeStation];var f=river.bridgeForward;var side=Vector3.Cross(Up,f);
        float roadHalf=map.Width(axis)*.5f;
        float edge=roadHalf+3.55f;
        Vector3 Point(float s,float x,float y)=>axis+f*s+side*x+Up*y;

        // After the straight span, both footpaths follow the actual track tangents into town.
        Vector3 At(float s,float x,float y){
            float index=river.bridgeStation+s*Count/LapMetres;
            int i=Mathf.FloorToInt(index);float t=index-i;
            var centre=Vector3.Lerp(map.main[i],map.main[i+1],t);
            var forward=Vector3.Slerp(Forward(map.main,i),Forward(map.main,i+1),t);
            return centre+Vector3.Cross(Up,forward)*x+Up*y;
        }
        void Segment(float a,float b,bool onBridge){
            float bottom=onBridge?-1.35f:-.40f;
            deck.Quad(At(a,-edge,-.046f),At(b,-edge,-.046f),At(b,edge,-.046f),At(a,edge,-.046f),0x686c73);
            deck.Quad(At(a,edge,bottom),At(b,edge,bottom),At(b,-edge,bottom),At(a,-edge,bottom),0x697179);
            foreach(int sign in new[]{-1,1}){
                float x=sign*edge,inner=sign*(roadHalf+.55f);
                var lip=At(a,x,-.04f);var next=At(b,x,-.04f);
                if(sign>0)deck.Quad(lip,next,At(b,x,bottom),At(a,x,bottom),0x899099);
                else deck.Quad(next,lip,At(a,x,bottom),At(b,x,bottom),0x899099);
                float hA=Mathf.Lerp(.17f,-.04f,Mathf.Clamp01((Mathf.Abs(a)-half)/Approach));
                float hB=Mathf.Lerp(.17f,-.04f,Mathf.Clamp01((Mathf.Abs(b)-half)/Approach));
                var left=Mathf.Min(inner,x);var right=Mathf.Max(inner,x);
                walks.Quad(At(a,left,hA),At(b,left,hB),At(b,right,hB),At(a,right,hA),0xafaea7);
                if(onBridge){
                    rails.Beam(lip+Up*1.2f,next+Up*1.2f,.13f,0xd4d2c5);
                    rails.Beam(lip,lip+Up*1.2f,.11f,0x87919b);
                }
            }
        }
        void Strip(float a,float b,bool span){
            int n=Mathf.CeilToInt((b-a)/3);
            for(int k=0;k<n;k++)Segment(Mathf.Lerp(a,b,(float)k/n),Mathf.Lerp(a,b,(k+1f)/n),span);
        }
        Strip(-half-Approach,-half,false);Strip(-half,half,true);Strip(half,half+Approach,false);
        foreach(int end in new[]{-1,1})
            deck.Box(Point(end*(half-1.3f),0,-2.7f),edge*2+1.2f,5.2f,4,0x858582,f);

        // Two exterior arch ribs leave the whole racing surface open from kerb to kerb.
        for(int sign=-1;sign<=1;sign+=2){
            float x=sign*(edge-.6f);
            Vector3 Crown(float t)=>Point(Mathf.Lerp(-half+2,half-2,t),x,1.4f+11.6f*4*t*(1-t));
            steel.Box(Point(0,x,.61f),.58f,1.3f,half*2-3,0xb9bcb6,f);
            for(int k=0;k<24;k++){
                float a=k/24f,b=(k+1f)/24;var p=Crown(a);var q=Crown(b);
                var foot=new Vector3(p.x,.95f,p.z);var nextFoot=new Vector3(q.x,.95f,q.z);
                steel.Beam(p,q,.48f,0xefdfc9,8);steel.Beam(foot,p,.17f,0xd5d1c5,6);
                if(k<12)steel.Beam(p,nextFoot,.19f,0xafb9ba);else steel.Beam(foot,q,.19f,0xafb9ba);
            }
        }
        deck.Save(folder,"River bridge decks and approach roads",parent,city,true);
        walks.Save(folder,"River bridge footways",parent,city,true);
        rails.Save(folder,"River crossing kerbs and parapets",parent,city);
        steel.Save(folder,"Anderson Bridge steelwork",parent,city);
    }
}
}
