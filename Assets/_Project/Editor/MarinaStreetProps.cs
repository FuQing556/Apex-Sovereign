using UnityEngine;

namespace Apex.Editor {
internal static class MarinaStreetProps {
    static readonly Vector3 up=Vector3.up;
    const int metal=0x536371, rubber=0x30363e, pearl=0xd8d6c9;
    static readonly int[] shirts={0xb55672,0x4d91a4,0xd2b36b,0x7383ab,0xd1c7b0,0xa66c50,0x5a766d};
    static readonly int[] skin={0xca9f80,0xb58766,0xe0b59a,0xa27660};
    public static void Person(MarinaGeometry g,Vector3 p,Vector3 f,int variation,bool crew=false){
        float h=.92f+(variation%9)*.02f;var r=Vector3.Cross(up,f);
        Vector3 P(float x,float y,float z=0)=>p+(r*x+up*y+f*z)*h;
        int coat=crew?0xe48a46:shirts[variation%shirts.Length],face=skin[variation%skin.Length];
        for(int s=-1;s<=1;s+=2){
            var foot=P(s*.14f,.09f,s*.045f);
            g.Box(foot,.17f*h,.18f*h,.28f*h,rubber,f);
            g.Beam(foot,P(s*.105f,.82f),.091f*h,0x434d5b,4);
        }
        g.Box(P(0,1.07f),.40f*h,.55f*h,.26f*h,coat,f);
        if(crew)g.Box(P(0,1.14f),.416f*h,.072f*h,.272f*h,0xd9d2ad,f);
        g.Beam(P(0,1.38f),P(0,1.62f),.126f*h,face,7);
        g.Box(P(0,1.62f),.23f*h,.095f*h,.24f*h,crew?pearl:0x544b48,f);
        if(variation%3==0)g.Box(P(0,1.61f,.115f),.22f*h,.042f*h,.18f*h,crew?pearl:coat,f);
        for(int s=-1;s<=1;s+=2){
            bool cheer=!crew && ((s>0 && variation%3==0) || variation%7==0);
            var elbow=P(s*(cheer?.33f:.25f),cheer?1.48f:1.04f,.035f);
            var hand=P(s*(cheer?.42f:.27f),cheer?1.74f:.87f,cheer?.10f:.14f);
            g.Beam(P(s*.17f,1.28f),elbow,.075f*h,coat,4);
            g.Beam(elbow,hand,.054f*h,face,4);
        }
    }
    public static void Van(MarinaGeometry g,Vector3 p,Vector3 f,int paint,bool service){
        var r=Vector3.Cross(up,f);
        Vector3 P(float x,float y,float z)=>p+r*x+up*y+f*z;
        float length=service?5.1f:4.4f;
        g.Box(P(0,.63f,0),1.87f,.68f,length,paint,f);
        g.Box(P(0,1.27f,-.46f),1.76f,service?1.03f:.75f,length*.64f,paint,f);
        var baseL=P(-.88f,.97f,1.65f);var roofL=P(-.80f,service?1.80f:1.60f,.94f);
        g.Quad(baseL,baseL+r*1.76f,roofL+r*1.60f,roofL,0x486271);
        g.Quad(roofL,roofL+r*1.60f,baseL+r*1.76f,baseL,0x486271);
        g.Box(P(0,service?1.82f:1.65f,-.30f),1.68f,.12f,length*.56f,paint,f);
        foreach(int s in new[]{-1,1}){
            g.Box(P(s*.89f,1.35f,.55f),.045f,.47f,.96f,0x415666,f);
            g.Box(P(s*.90f,1.13f,-1.0f),.05f,.14f,1.75f,service?0xd89d5f:0x77909c,f);
            foreach(int end in new[]{-1,1}){
                var wheel=P(s*.90f,.34f,end*length*.31f);
                g.Beam(wheel-r*.13f,wheel+r*.13f,.33f,rubber,8);
                var hub=wheel+r*(s*.135f);
                for(int k=0;k<8;k++){
                    float a=k*Mathf.PI/4,b=(k+1)*Mathf.PI/4;
                    var v=up*Mathf.Cos(a)+f*Mathf.Sin(a);var t=up*Mathf.Cos(b)+f*Mathf.Sin(b);
                    if(s<0)(v,t)=(t,v);
                    g.Quad(hub+v*.33f,hub+t*.33f,hub+t*.15f,hub+v*.15f,rubber);
                    g.Triangle(hub,hub+v*.15f,hub+t*.15f,0xb3b8b1);
                }
            }
            g.Box(P(s*.66f,.70f,length*.505f),.38f,.19f,.07f,0xe7cfa1,f);
            g.Box(P(s*.72f,.77f,-length*.505f),.24f,.24f,.07f,0xbd5653,f);
        }
        if(service)g.Box(P(0,1.99f,.22f),.85f,.22f,.28f,0xffca80,f);
    }
    public static void MotorPool(MarinaGeometry g,Vector3 p,Vector3 f,bool plaza){
        var r=Vector3.Cross(up,f);
        Vector3 P(float x,float y,float z)=>p+r*x+up*y+f*z;
        g.Box(p-up*.045f,39,.12f,20,0x60676b,f);
        if(plaza){
            for(int k=-1;k<=1;k++){
                Kiosk(g,P(k*8,.015f,-5),f,k==0?0xbc6577:0x427f84);
                Bunting(g,P(k*8,.015f,-7.4f),f,7.8f);
                for(int j=0;j<5;j++)Person(g,P(k*8+(j-2)*.82f,.015f,-1.2f+(j%2)*.65f),-f,19+k*5+j);
            }
            PavementMural(g,P(0,.031f,4.5f),f,2,0xc76f83);
        }else{
            var roof=P(-2,4.9f,-4);
            g.Box(roof,26,.48f,9,0xa6b6b8,f);
            g.Box(roof+f*4.52f,26,.57f,.12f,0x3c6474,f);
            for(int j=0;j<4;j++)foreach(int side in new[]{-1,1}){
                var pillar=P(-14.5f+j*8.35f,0,-4+side*4.1f);
                g.Box(pillar+up*2.35f,.28f,4.7f,.28f,0x79878d,f);
                g.Box(pillar+up*.52f,.46f,1.04f,.46f,0xc49a62,f);
            }
            for(int j=0;j<3;j++){
                float x=-10.5f+j*8.35f;
                g.Box(P(x,4.63f,-.15f),6.8f,.12f,.24f,0xfff5da,f);
                Van(g,P(x,.02f,-3.7f),f,j%2==0?0x8a9ca8:0xb16a5e,j!=1);
                Person(g,P(x+2.2f,.02f,-.8f),f,j+87,true);
            }
            for(int j=0;j<4;j++){
                var basePos=P(12.8f+(j%2)*2.1f,.015f,-4.5f+(j/2)*1.7f);
                Case(g,basePos,f,1.9f,1.3f,1.5f,0x80695a);
                if(j%2==0)Case(g,basePos+up*1.3f,f,1.7f,1,1.3f,0x566b72);
            }
            g.Lettering("PIT CREW",P(-2,4.94f,.62f),-f,.063f,0xfff5da);
        }
        for(int j=-3;j<=3;j++)g.Box(P(j*5,.031f,6.5f),3.2f,.012f,.13f,0xe0cda5,f);
        foreach(int s in new[]{-1,1}){
            var post=P(s*17.6f,0,5);
            g.Beam(post,post+up*6.9f,.105f,metal);
            var top=post+up*6.8f;
            g.Box(top+r*.72f-up*1.75f,1.34f,3.4f,.08f,plaza?0x45828e:0xa05c72,f);
            g.Box(top+r*.72f-up*.45f,1.35f,.17f,.10f,0xd7c6ac,f);
        }
    }
    public static void Case(MarinaGeometry g,Vector3 p,Vector3 f,float w,float h,float d,int color){
        var r=Vector3.Cross(up,f);
        g.Box(p+up*h*.50f,w,h,d,color,f);
        foreach(int s in new[]{-1,1}){
            g.Box(p+r*(s*w*.36f)+up*h*.5f,.065f,h+.014f,d+.025f,0xb7ae96,f);
            g.Box(p+f*(s*(d*.5f+.012f))+up*h*.65f,.32f,.09f,.035f,rubber,f);
        }
        g.Box(p+up*(h*.79f),w+.02f,.035f,d+.02f,metal,f);
    }
    public static void Kiosk(MarinaGeometry g,Vector3 p,Vector3 f,int cloth){
        var r=Vector3.Cross(up,f);
        Vector3 P(float x,float y,float z)=>p+r*x+up*y+f*z;
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})g.Beam(P(x*2.5f,0,z*1.35f),P(x*2.5f,2.92f,z*1.35f),.075f,metal);
        for(int k=0;k<8;k++){
            float x=-2.9f+k*.725f;int color=k%2==0?cloth:0xd9ceaf;
            foreach(int side in new[]{-1,1}){
                var a=P(x,2.88f,side*1.72f);var b=P(x+.725f,2.88f,side*1.72f);
                var c=P(x+.725f,3.56f,0);var d=P(x,3.56f,0);
                g.Quad(a,b,c,d,color);g.Quad(d,c,b,a,color);
                g.Box((a+b)*.5f-up*.12f,.725f,.24f,.065f,color,f);
            }
        }
        g.Box(P(0,.55f,.90f),4.55f,1.10f,.72f,0x55596a,f);
        g.Box(P(0,1.13f,.90f),4.72f,.09f,.82f,0xdbb778,f);
        for(int i=-2;i<=2;i++){
            var lamp=P(i,2.49f,1.18f);
            g.Beam(lamp+up*.16f,lamp+up*.55f,.022f,metal);
            g.Beam(lamp-up*.17f,lamp+up*.17f,.16f,0xffca80,7);
            g.Box(P(i*.76f,1.28f,.92f),.29f,.23f,.24f,i%2==0?cloth:pearl,f);
        }
    }
    public static void Bunting(MarinaGeometry g,Vector3 p,Vector3 f,float width){
        var r=Vector3.Cross(up,f);var top=p+up*3.9f;
        foreach(int s in new[]{-1,1})g.Beam(p+r*(s*width/2),top+r*(s*width/2),.085f,metal);
        Vector3 Line(float u)=>top+r*(width*(u-.5f))-up*(.72f*4*u*(1-u));
        for(int i=0;i<12;i++){
            float t=i/12f;g.Beam(Line(t),Line((i+1)/12f),.025f,metal,4);
            g.Triangle(Line(t+.008f),Line(t+.068f),Line(t+.046f)-up*.48f+f*.12f,shirts[i%shirts.Length],true);
        }
    }
    // A painted ribbon emblem with staggered facets; it lies flat and has no collider.
    public static void PavementMural(MarinaGeometry g,Vector3 p,Vector3 f,float size,int accent){
        var r=Vector3.Cross(up,f);
        Vector3 P(float x,float z)=>p+(r*x+f*z)*size;
        void Stripe(float x,float z,float reach,int color){
            g.Quad(P(x,z),P(x+.56f,z+1.25f),P(x+reach,z+2),P(x+.58f,z+.78f),color);
        }
        Stripe(-2.8f,-.9f,3f,accent);Stripe(-1.45f,-1.11f,3.8f,0x85bfc1);
        Stripe(-.12f,-1.25f,3.05f,0xcba680);
        g.Quad(P(-3.0f,-1.17f),P(-2.9f,-1.0f),P(-1.6f,-.86f),P(-1.9f,-1.01f),0xc5bfae);
    }
}
}
