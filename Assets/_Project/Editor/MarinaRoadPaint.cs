using System;
using UnityEngine;

namespace Apex.Editor {
// Racing marks follow the existing grid. Civil markings belong to the streets
// closed for the event. Stations are measured on the baked driving surface.
internal sealed class MarinaRoadPaint {
    const string MeshName="Street and grid road markings";
    const int White=0xe8e5dc, LaneWhite=0xc9c6bd, Yellow=0xf4c447;
    // Atlas rows run from the top: solid mask, SLOW, BUS, XING, AHEAD, then 01–16.
    const int Solid=0,Slow=1,Bus=2,Xing=3,Ahead=4,FirstNumber=5;
    readonly MarinaGeometry paint=new();
    readonly Circuit course;
    readonly Vector3[] center,right;
    readonly float[] distance,halfWidth,gate;
    readonly (float start,float end)[] busSections;
    readonly float length,bridge,crossing;

    public MarinaRoadPaint(Circuit course){
        this.course=course;center=course.mapCenterline;
        var road=course.transform.Find("Grand Prix asphalt").GetComponent<MeshFilter>();
        var vertices=road.sharedMesh.vertices;int n=center.Length;
        if(vertices.Length!=4*n)throw new InvalidOperationException("Expected four asphalt vertices per road section.");
        right=new Vector3[n];halfWidth=new float[n];distance=new float[n+1];
        for(int i=0;i<n;i++){
            right[i]=Vector3.Cross(Vector3.up,MarinaStreets.Forward(center,i));
            halfWidth[i]=Vector3.Distance(road.transform.TransformPoint(vertices[4*i]),road.transform.TransformPoint(vertices[4*i+3]))*.5f;
            distance[i+1]=distance[i]+Vector3.Distance(center[i],center[(i+1)%n]);
        }
        length=distance[n];
        gate=Array.ConvertAll(course.gates,t=>distance[MarinaStreets.Nearest(center,t.position)]);
        busSections=new[]{(gate[3]+48,gate[4]-45),(gate[8]+45,gate[9]-70),(gate[12]+40,gate[13]-50)};
        var streets=new MarinaStreets();
        bridge=distance[new MarinaRiver(streets).bridgeStation];
        crossing=Mathf.Lerp(gate[7],gate[8],.55f);
    }

    int Section(float s,out float t){
        s=Mathf.Repeat(s,length);int i=Array.BinarySearch(distance,s);
        i=Math.Min(i<0?~i-1:i,center.Length-1);
        t=(s-distance[i])/(distance[i+1]-distance[i]);return i;
    }
    Vector3 Point(float s,float offset){
        int i=Section(s,out float t),j=(i+1)%center.Length;
        return Vector3.Lerp(center[i]+right[i]*offset,center[j]+right[j]*offset,t)+Vector3.up*.009f;
    }
    float RoadHalf(float s){
        int i=Section(s,out float t);return Mathf.Lerp(halfWidth[i],halfWidth[(i+1)%center.Length],t);
    }
    float LaneWidth(float s)=>(2*RoadHalf(s)-1.8f)/5;
    static Rect Tile(int id){
        // Each 256 x 128 cell has a 10 px bleed margin for mipmapping.
        return new Rect((id%4*256+10)/1024f,((7-id/4)*128+10)/1024f,236/1024f,108/1024f);
    }
    void Band(float a,float b,float leftA,float rightA,float leftB,float rightB,int color,int tile=Solid){
        paint.Quad(Point(a,leftA),Point(b,leftB),Point(b,rightB),Point(a,rightA),color,Tile(tile));
    }
    void Stamp(float s,float x,float width,float depth,int tile,int color){
        float l=x-width/2,r=x+width/2;Band(s-depth/2,s+depth/2,l,r,l,r,color,tile);
    }
    void Stripe(float a,float b,float x1,float x2,float width,int color){
        int count=Mathf.CeilToInt((b-a)/4);
        for(int k=0;k<count;k++){
            float u=(float)k/count,v=(float)(k+1)/count;
            float x=Mathf.Lerp(x1,x2,u),y=Mathf.Lerp(x1,x2,v),h=width/2;
            Band(Mathf.Lerp(a,b,u),Mathf.Lerp(a,b,v),x-h,x+h,y-h,y+h,color);
        }
    }
    void Grid(){
        // Match the white brackets baked by MarinaBayBuilder, including their
        // actual forward vector rather than a perpendicular at a nearby sample.
        var start=course.gates[0];
        for(int i=1;i<=16;i++){
            int side=i%2==0?1:-1;var p=start.position-start.forward*(i*6)+start.right*(side*3)+Vector3.up*.009f;
            void Mark(float x,float z,float w,float h,int tile,int color){
                var mid=p+start.right*x+start.forward*z;
                var r=start.right*(w/2);var f=start.forward*(h/2);
                paint.Quad(mid-r-f,mid-r+f,mid+r+f,mid+r-f,color,Tile(tile));
            }
            Mark(side*2.42f,-.43f,2.24f,.15f,Solid,Yellow);
            Mark(side*2.00f,-1.98f,.91f,1.86f,FirstNumber+i-1,White);
        }
    }
    void Arrows(float s){
        for(int lane=-1;lane<=1;lane++){
            float x=LaneWidth(s)*lane;
            Band(s-2.8f,s+.65f,x-.115f,x+.115f,x-.115f,x+.115f,White);
            Band(s+.65f,s+2.85f,x-.71f,x+.71f,x-.02f,x+.02f,White);
        }
    }
    bool BusLane(float start,float end){
        foreach(var section in busSections)if(end>=section.start && start<=section.end)return true;
        return false;
    }
    void StreetLanes(){
        for(float s=94;s<length-160;s+=9){
            if(Mathf.Abs(s-crossing)<33 || Mathf.Abs(s-bridge)<100)continue;
            float a=LaneWidth(s),b=LaneWidth(s+3);
            for(int k=0;k<4;k++){
                float lane=k-1.5f;
                if(k==0 && BusLane(s,s+3))continue;
                Stripe(s,s+3,lane*a,lane*b,.15f,LaneWhite);
            }
        }
        foreach(var (a,b) in busSections){
            for(float s=a;s<b;s+=4){
                float end=Mathf.Min(s+4,b);
                Stripe(s,end,-1.5f*LaneWidth(s),-1.5f*LaneWidth(end),.30f,Yellow);
            }
            for(float s=a+21;s<b-10;s+=82)Stamp(s,-2*LaneWidth(s),2.45f,5.1f,Bus,Yellow);
        }
        for(int sign=-1;sign<=1;sign+=2)Stripe(bridge-96,bridge+96,sign*.14f,sign*.14f,.12f,White);
        foreach(int g in new[]{1,3,6,11,14}){
            float s=gate[g]-65;
            Stamp(s,0,3.35f,5.6f,Slow,White);
            if(RoadHalf(s)>10.75f)Stamp(s,2*LaneWidth(s),3.35f,5.6f,Slow,White);
        }
        foreach(int g in new[]{1,3,4,6,8,12,13})Arrows(gate[g]-113);
    }
    void PedestrianCrossing(){
        float w=RoadHalf(crossing)-1.1f;
        for(float x=-Mathf.Floor(w/1.08f)*1.08f;x<=w;x+=1.08f)Stamp(crossing,x,.53f,3.6f,Solid,White);
        foreach(int side in new[]{-1,1}){
            for(int i=0;i<6;i++){
                float a=crossing-28+i*4,b=a+4;
                float x=side*(RoadHalf(a)-(i%2==0?1.1f:1.75f));
                float y=side*(RoadHalf(b)-(i%2==0?1.75f:1.1f));
                Stripe(a,b,x,y,.14f,White);
            }
        }
        Stamp(crossing-63,0,3.1f,5.3f,Slow,White);
        Stamp(crossing-44,0,2.95f,5.3f,Xing,White);
        Stamp(crossing-35,0,3.3f,5.3f,Ahead,White);
    }
    public void Bake(string folder,Material material){
        Grid();StreetLanes();PedestrianCrossing();
        var existing=course.transform.Find(MeshName);
        var created=paint.Save(folder,MeshName,course.transform,material);
        if(existing){
            existing.GetComponent<MeshFilter>().sharedMesh=created.GetComponent<MeshFilter>().sharedMesh;
            existing.GetComponent<MeshRenderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(created);
        }
    }
}
}
