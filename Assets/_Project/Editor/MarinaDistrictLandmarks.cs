using UnityEngine;
using static Apex.Editor.MarinaCityForms;

namespace Apex.Editor {
internal static class MarinaDistrictLandmarks {
    static readonly Vector3 up=Vector3.up;
    const int stone=0xc4b9a1, trim=0x9e9b92, glass=0x506975, copper=0x648982;
    public static void Gallery(MarinaGeometry g,Vector3 p,Vector3 f){
        var r=Vector3.Cross(up,f);
        g.Box(p,85,.36f,54,0x9c9c92,f);p+=up*.18f;
        Shell(g,p+f*14,f,77,18,13,stone,trim,3);
        for(int s=-1;s<=1;s+=2){
            var wing=p+r*(s*32)-f*6;
            Shell(g,wing,f,13,31,11.2f,stone,trim,3);
            g.Box(wing+up*11.6f,14.3f,.8f,32.2f,0xccbeaa,f);
        }
        var loggia=p-f*17;
        g.Box(loggia+up*7.7f,77,.85f,6.6f,stone,f);
        for(int j=-7;j<=7;j++)g.Beam(loggia+r*(j*5.2f),loggia+r*(j*5.2f)+up*7.3f,.36f,0xdfccb0,8);
        var lantern=p+f*14+up*13;
        for(int i=0;i<12;i++){
            Vector3 Spoke(int k)=>r*Mathf.Cos(k*Mathf.PI/6)+f*Mathf.Sin(k*Mathf.PI/6);
            var a=Spoke(i);var b=Spoke(i+1);
            g.Quad(lantern+a*8,lantern+a*8+up*5,lantern+b*8+up*5,lantern+b*8,stone);
            g.Triangle(lantern+a*9+up*5,lantern+up*13,lantern+b*9+up*5,copper,true);
        }
        g.Box(p+up*.15f,34,.26f,15,0x69897e,f);
        foreach(int s in new[]{-1,1})Tree(g,p+r*(s*21),9,4,s+8);
    }
    public static void Expo(MarinaGeometry g,Vector3 p,Vector3 f){
        var r=Vector3.Cross(up,f);
        g.Box(p,91,.38f,59,0x798991,f);p+=up*.19f;
        foreach(int side in new[]{-1,1}){
            var centre=p+r*(side*21);
            Shell(g,centre,f,39,49,10,glass,trim,1);
            Vector3 Vault(int index,float depth){
                float a=Mathf.PI*index/12;
                return centre+r*(20*Mathf.Cos(a))+up*(10+12*Mathf.Sin(a))+f*depth;
            }
            for(int i=0;i<12;i++){
                var a=Vault(i,-25.7f);var b=Vault(i,25.7f);var c=Vault(i+1,25.7f);var d=Vault(i+1,-25.7f);
                g.Quad(d,c,b,a,i==5 || i==6?0x88b1ba:side<0?0xb6b4ab:0x8fabb0);
                g.Triangle(centre-f*25.65f+up*10,d,a,0x65818a,true);
                g.Triangle(centre+f*25.65f+up*10,b,c,0x65818a,true);
                for(int rib=-3;rib<=3;rib++)g.Beam(Vault(i,rib*8),Vault(i+1,rib*8),.19f,0xd7d1be,5);
            }
        }
        g.Box(p-f*26.5f+up*5.25f,86,.55f,5.8f,0xb4c4bd,f);
        for(int j=-4;j<=4;j++)g.Beam(p+r*j*10-f*28,p+r*j*10-f*28+up*5,.22f,trim);
    }
    public static void BayHotel(MarinaGeometry g,Vector3 p,Vector3 f){
        var r=Vector3.Cross(up,f);
        Shell(g,p,f,107,52,15,stone,0x82938d,2);
        foreach(int side in new[]{-1,1}){
            var q=p+r*(side*29)+up*15;
            float height=side<0?83:108;
            Shell(g,q,f,30,34,height,glass,0x819198,1);
            for(int i=1;i<height/8;i++)g.Box(q+up*(i*8),31,.54f,35,0x9bada9,f);
            g.Box(q+up*(height+.45f),32,.9f,36,copper,f);
            Shell(g,q+up*(height+.9f),f,23,24,6,glass,0xa6a99a,1);
        }
        var gallery=p+up*67;
        Shell(g,gallery,f,33,12,7,0x739098,0xa7bab0,1);
        g.Box(gallery+up*7.3f,34,.6f,14,0x839e81,f);
        for(int k=-2;k<=2;k++)Tree(g,p+up*15+r*k*8-f*18,5.5f,2.4f,k+9);
    }
    public static void Port(MarinaGeometry g,Vector3 p,Vector3 f){
        var r=Vector3.Cross(up,f);g.Box(p,116,.25f,64,0x687278,f);p+=up*.13f;
        int[] color={0x815753,0x647f89,0xa3835d,0x577d77,0x757887};
        for(int x=-3;x<=3;x++)for(int z=-2;z<=2;z++){
            var basePos=p+r*x*14+f*z*8;
            for(int h=0;h<1+(x*x+z*z+2*x+6)%3;h++)
                g.Box(basePos+up*(1.4f+h*2.85f),12.6f,2.8f,3.55f,color[(x*x+z+2+h)%5],f);
        }
        foreach(int x in new[]{-1,1}){
            var crane=p+r*(x*24);
            for(int side=-1;side<=1;side+=2){
                var foot=crane+f*(side*23);
                g.Beam(foot-r*3,foot+r*.9f+up*26,.76f,0xb9966b);
                g.Beam(foot+r*5,foot+r*.9f+up*26,.76f,0xb9966b);
            }
            g.Box(crane+up*26,2.5f,2.2f,56,0xc2a06b,f);
            g.Box(crane+up*23+f*4,3.9f,3.5f,4.2f,0x607b88,f);
        }
    }
}
}
