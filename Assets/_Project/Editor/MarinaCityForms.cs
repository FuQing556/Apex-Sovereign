using UnityEngine;

namespace Apex.Editor {
internal static class MarinaCityForms {
    public enum Use { Terraces,Court,Hotel,Offices,Hall,Depot,Park }
    static readonly Vector3 up=Vector3.up;
    static readonly int[] plaster={0xbaab92,0xaca49a,0xa8b5ac,0xb79c90,0x919da4};
    static readonly int[] roofs={0x785952,0x8e6c62,0x716366,0x87695d};
    static readonly int[] glass={0x3f5263,0x4a5c68,0x484e66,0x4a6369};
    static readonly int[] leaves={0x3d6457,0x4d7059,0x537661,0x48665b};
    internal static void Shell(MarinaGeometry g,Vector3 basePos,Vector3 f,float w,float d,float h,int wall,int roof,int style){
        var r=Vector3.Cross(up,f)*w*.5f;var end=f*d*.5f;var y=up*h;
        var a=basePos-r-end;var b=basePos-r+end;var c=basePos+r+end;var e=basePos+r-end;
        void Face(Vector3 l,Vector3 v)=>g.Quad(v,v+y,l+y,l,wall,style,style);
        Face(e,a);Face(a,b);Face(b,c);Face(c,e);
        g.Quad(a+y,b+y,c+y,e+y,roof);
    }
    static void Gable(MarinaGeometry g,Vector3 p,Vector3 f,float w,float d,float rise,int wall,int tile){
        var r=Vector3.Cross(up,f)*w/2;var z=f*d/2;var top=p+up*rise;
        g.Quad(p-r-z,p-r+z,top+z,top-z,tile);
        g.Quad(top-z,top+z,p+r+z,p+r-z,tile);
        g.Triangle(p-r-z,top-z,p+r-z,wall);g.Triangle(p+r+z,top+z,p-r+z,wall);
    }
    internal static void Tree(MarinaGeometry g,Vector3 p,float h,float radius,int seed){
        g.Beam(p,p+up*h*.69f,.25f+h*.019f,0x6d6150,5);
        Vector3 Ring(int j,float breadth,float y){
            float a=(j%7)*2*Mathf.PI/7+seed*.16f;
            return p+up*y+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*breadth;
        }
        for(int j=0;j<7;j++){
            var a=Ring(j,.58f,h*.48f);var b=Ring(j+1,.58f,h*.48f);
            var c=Ring(j,.98f,h*.70f);var d=Ring(j+1,.98f,h*.70f);
            int ink=leaves[(j+seed)%leaves.Length];
            g.Quad(a,c,d,b,ink);g.Triangle(c,p+up*h+Vector3.right*.4f,d,ink);
        }
    }
    public static void Draw(MarinaGeometry g,Use use,Vector3 plot,Vector3 f,float width,float depth,float height,int seed){
        var r=Vector3.Cross(up,f);
        var p=plot+up*.10f;
        g.Box(plot+up*.045f,width,.09f,depth,use==Use.Park?0x4e6758:use==Use.Depot?0x697177:0x8c8b83,f);
        float w=width-3.6f,d=depth-4;int stone=plaster[seed%plaster.Length],ink=glass[seed%glass.Length];
        switch(use){
        case Use.Terraces:{
            int units=Mathf.Clamp(Mathf.FloorToInt(w/8.2f),2,8);float bay=w/units;
            for(int i=0;i<units;i++){
                var q=p+r*((i+.5f)*bay-w*.5f)+f*d*.08f;
                float h=height+(i%3==0?1.25f:0);int tint=plaster[(seed+i)%plaster.Length];
                Shell(g,q,f,bay-.13f,d*.74f,h,tint,stone,3);
                Gable(g,q+up*h,f,bay-.05f,d*.76f,2.15f,tint,roofs[(i+seed)%roofs.Length]);
                var shop=q-f*d*.39f;
                g.Box(shop+up*3.4f,bay-.15f,.34f,2.5f,i%2==0?0x688785:0xae897b,f);
                g.Box(shop-f*1.08f+r*(bay*.43f)+up*1.6f,.32f,3.2f,.36f,stone,f);
            }break;
        }
        case Use.Court:{
            float wing=w*.25f;var back=p+f*d*.33f;
            Shell(g,back,f,w,d*.28f,height,stone,0x8c8982,2);
            for(int s=-1;s<=1;s+=2){
                var q=p+r*(s*(w-wing)*.5f)-f*d*.15f;
                Shell(g,q,f,wing,d*.68f,height*.81f,stone,0x89857f,2);
                g.Box(q+up*(height*.81f+.16f),wing+.5f,.32f,d*.68f+.5f,0xa6aaa0,f);
            }
            g.Box(p-f*d*.14f+up*.08f,w*.41f,.15f,d*.51f,0x55735d,f);
            Tree(g,p-f*d*.13f,7,3.3f,seed);break;
        }
        case Use.Hotel:
        case Use.Offices:{
            bool hotel=use==Use.Hotel;float podium=Mathf.Min(height*.24f,13);
            Shell(g,p,f,w,d,podium,hotel?stone:ink,0x92968c,2);
            float tw=w*(hotel?.8f:.65f),td=d*(hotel?.54f:.69f);
            var tower=p+up*podium+f*(d*.075f);
            Shell(g,tower,f,tw,td,height-podium,hotel?stone:ink,0x8b9493,hotel?2:1);
            var top=tower+up*(height-podium);
            g.Box(top,tw+.7f,.68f,td+.7f,hotel?0xb7b6a8:0x95a4aa,f);
            Shell(g,top+up*.34f,f,tw*.68f,td*.71f,Mathf.Max(2,height*.08f),ink,0x89949b,1);
            var front=p-f*d*.42f+up*(podium+.1f);
            g.Box(front,w*.75f,.20f,d*.10f,hotel?0x568387:0x617357,f);
            if(height>65){
                var edge=tower+r*(tw*.5f+.02f)-f*(td*.46f);
                g.Box(edge+up*(height-podium)*.5f,.28f,height-podium,.30f,seed%3==0?0x77e9fc:0xb7a88b,f);
            }
            break;
        }
        case Use.Hall:
        case Use.Depot:{
            bool depot=use==Use.Depot;int aisles=Mathf.Clamp(Mathf.RoundToInt(w/16),2,5);
            float bay=w/aisles;float shedDepth=d*(depot?.66f:.86f);
            var body=p+f*(d-shedDepth)*.42f;
            Shell(g,body,f,w,shedDepth,height,depot?0x74828a:stone,0x858e91,4);
            for(int i=0;i<aisles;i++){
                var q=body+r*((i+.5f)*bay-w*.5f)+up*height;
                Gable(g,q,f,bay,shedDepth+.6f,depot?2.6f:4.1f,stone,i%2==0?0x91a3a7:0x809398);
                g.Box(q+up*(depot?2.65f:4.15f),bay*.19f,.30f,shedDepth*.8f,0x91bec4,f);
                var door=body-f*(shedDepth*.5f+.05f)+r*((i+.5f)*bay-w*.5f);
                g.Box(door+up*2.2f,bay*.64f,4.4f,.12f,0x465867,f);
                g.Box(door+up*4.62f,bay*.84f,.22f,1.4f,0xb5b2a4,f);
            }
            if(depot)for(int j=0;j<4;j++){
                var crate=p-f*d*.41f+r*w*((j-1.5f)*.19f);
                g.Box(crate+up*1.26f,w*.15f,2.5f,2.55f,j%2==0?0x986758:0x5e7886,f);
            }
            break;
        }
        case Use.Park:{
            g.Box(p,w*.10f,.08f,d,0xa49c87,f);g.Box(p,w,.10f,2.2f,0xa49c87,f);
            int n=Mathf.Clamp(Mathf.FloorToInt(w/8),2,9);
            for(int i=0;i<n;i++)for(int side=-1;side<=1;side+=2){
                var tree=p+r*((i+.5f)*w/n-w*.5f)+f*(side*d*(.22f+.045f*((seed+i)%3)));
                Tree(g,tree,8+(seed+i)%5,Mathf.Min(w/n*.48f,d*.19f),seed+i);
            }
            break;
        }}
    }
}
}
