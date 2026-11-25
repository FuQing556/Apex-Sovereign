using UnityEngine;

namespace Apex.Editor {
internal sealed class MarinaHarbour {
    readonly MarinaStreets streets;
    readonly MarinaRiver river;
    readonly Transform root;
    readonly Material city;
    readonly string folder;
    readonly Vector3 up=Vector3.up;
    static readonly int[] cargo={0xaa5250,0x54777c,0xc69254,0x59718b,0x924c61,0x778572};
    public MarinaHarbour(MarinaStreets streets,MarinaRiver river,Transform root,string folder,Material city){
        this.streets=streets;this.river=river;this.root=root;this.folder=folder;this.city=city;
    }
    // Stations from transom to stem. The narrower waterline gives the bow its flare.
    static void Hull(MarinaGeometry g,Vector3 origin,Vector3 f,float length,float width,float height,int paint){
        float[] z={-.50f,-.44f,-.26f,.22f,.34f,.44f,.5f};
        float[] beam={.70f,.94f,1,1,.89f,.55f,.025f};
        var r=Vector3.Cross(Vector3.up,f);
        Vector3 P(int i,int side,float y,float breadth)=>origin+f*(z[i]*length)+r*(side*width*.5f*beam[i]*breadth)+Vector3.up*y;
        for(int i=0;i+1<z.Length;i++){
            int n=i+1;
            g.Quad(P(i,-1,height,1),P(n,-1,height,1),P(n,1,height,1),P(i,1,height,1),0x898783);
            foreach(int s in new[]{-1,1}){
                void Plate(float low,float a,float high,float b,int color){
                    var p=P(i,s,low,a);var q=P(n,s,low,a);var t=P(n,s,high,b);var v=P(i,s,high,b);
                    if(s>0)g.Quad(v,t,q,p,color);else g.Quad(p,q,t,v,color);
                }
                Plate(-height*.30f,.83f,height*.18f,.92f,0x9a4d42);
                Plate(height*.18f,.92f,height,1,paint);
            }
        }
        g.Quad(P(0,-1,-height*.30f,.83f),P(0,-1,height,1),P(0,1,height,1),P(0,1,-height*.30f,.83f),paint);
        int bow=z.Length-1;
        g.Quad(P(bow,1,-height*.30f,.83f),P(bow,1,height,1),P(bow,-1,height,1),P(bow,-1,-height*.30f,.83f),paint);
    }
    static void Mast(MarinaGeometry g,Vector3 p,Vector3 f,float h){
        var r=Vector3.Cross(Vector3.up,f);var top=p+Vector3.up*h;
        g.Beam(p,top,.14f,0xc6c5b5);g.Beam(top-r*2.7f,top+r*2.7f,.13f,0xaeb9bf);
        g.Box(top+Vector3.up*.5f,.35f,.5f,.35f,0xfff5da,f);
    }
    bool WetFootprint(Vector3 p,Vector3 f,float length,float width){
        var r=Vector3.Cross(up,f);
        for(int i=-4;i<=4;i++)foreach(int side in new[]{-1,1})
            if(river.Dry(p+f*(i*length/8)+r*(side*width/2)))return false;
        return true;
    }
    void Ship(string name,float x,float y,float angle,float length,bool liner){
        var p=streets.At(x,y)+up*MarinaRiver.WaterY;var f=Quaternion.Euler(0,angle,0)*Vector3.forward;
        var r=Vector3.Cross(up,f);float w=liner?33:30,h=liner?8:9;
        if(!WetFootprint(p,f,length+10,w+8))throw new System.InvalidOperationException(name+" crosses a quay");
        var g=new MarinaGeometry();Hull(g,p,f,length,w,h,liner?0x344761:0x354850);
        Vector3 P(float across,float above,float along)=>p+r*across+up*(h+above)+f*along;
        if(liner){
            for(int level=0;level<6;level++){
                float span=length*(.76f-level*.047f),width=w-3-level*.85f;
                var mid=P(0,level*3.15f+1.57f,-length*.026f);
                g.Box(mid,width,3.15f,span,0xcbd3cf,f);
                g.Box(mid+up*1.32f,width+.9f,.38f,span+.9f,0xefddc7,f);
                foreach(int side in new[]{-1,1}){
                    var flank=mid+r*side*(width*.5f+.03f);
                    g.Box(flank,.11f,1.55f,span-2,0x3d5368,f);
                    for(float z=-span/2+3;z<span/2-2;z+=4.9f)
                        g.Box(flank+r*(side*.10f)+f*z,.15f,1.16f,2.7f,((int)(z+180)+level)%4==0?0xe2c28f:0x9abfbe,f);
                }
                foreach(int end in new[]{-1,1}){
                    var front=mid+f*(end*(span*.5f+.07f));
                    g.Box(front,width-1,1.55f,.16f,0x455c70,f);
                    for(float across=-width*.43f;across<width*.45f;across+=3.1f)
                        g.Box(front+r*across+f*(end*.1f),1.95f,.96f,.10f,level%2==0?0xa4bfc2:0xe2c28f,f);
                }
            }
            foreach(int side in new[]{-1,1})for(int k=-3;k<=3;k++){
                var boat=P(side*w*.495f,6.1f,k*12f);g.Box(boat,2.8f,1.65f,8.8f,0xc67543,f);
                g.Box(boat+up*.92f,2.1f,.44f,7,0xead6ae,f);
            }
            g.Box(P(0,20.2f,-19),8,6,13,0xb95d63,f);g.Box(P(0,23.5f,-19),8.6f,.9f,13.6f,0x343c50,f);
            g.Box(P(0,18.8f,32),w-5,3,9,0xcbd3cf,f);g.Box(P(0,19,36.55f),w-6,1.5f,.16f,0x77a0b2,f);
            Mast(g,P(0,19,23),f,6);
            for(int s=-1;s<=1;s+=2){
                var a=P(s*w*.46f,1.1f,length*.32f);var b=P(s*w*.24f,1.1f,length*.445f);var tip=P(0,1.1f,length*.493f);
                g.Beam(a,b,.12f,0xd4c8ad);g.Beam(b,tip,.12f,0xd4c8ad);
                for(int k=0;k<6;k++){
                    var rail=k<3?Vector3.Lerp(a,b,k/3f):Vector3.Lerp(b,tip,(k-3)/3f);
                    g.Beam(rail-up*1.1f,rail,.08f,0xbcbcae);
                }
                var capstan=P(s*3.2f,.10f,length*.375f);
                g.Beam(capstan,capstan+up*1.5f,.78f,0x738193,8);
                g.Lettering("MERIDIAN",P(s*w*.501f,-2.45f,length*.105f),-r*s,.26f,0xf2dec2);
            }
        }else{
            for(int bay=0;bay<8;bay++)for(int row=-2;row<=2;row++){
                int stack=2+(bay*13+row*row+row+8)%3;
                for(int k=0;k<stack;k++){
                    var mid=P(row*4.85f,1.55f+k*3.14f,(bay-3.0f)*15.7f);
                    int paint=cargo[(bay*7+row+2+k*3)%cargo.Length];
                    g.Box(mid,4.65f,3.05f,15.2f,paint,f);
                    if(Mathf.Abs(row)==2)for(int rib=-3;rib<=3;rib++)
                        g.Box(mid+r*(Mathf.Sign(row)*2.35f)+f*rib*2f,.08f,2.75f,.13f,0x525d65,f);
                }
            }
            float aft=-length*.355f;
            g.Box(P(0,7,aft),w*.69f,14,17,0xc2c5b9,f);g.Box(P(0,15.5f,aft),w*.89f,4.2f,19,0xded2b7,f);
            foreach(int side in new[]{-1,1})g.Box(P(side*w*.448f,16,aft),.12f,1.45f,15,0x9abfbe,f);
            g.Box(P(0,16,aft+9.6f),w*.73f,1.4f,.2f,0x91c1d7,f);
            g.Box(P(0,10,aft-12),6,17,7,0xb16449,f);g.Box(P(0,19,aft-12),6.2f,1.4f,7.2f,0x313d47,f);
            Mast(g,P(0,17.6f,aft+2),f,7);
        }
        for(int side=-1;side<=1;side+=2)for(int k=-4;k<=4;k++)
            g.Box(P(side*w*.49f,.45f,k*length*.068f),.35f,.5f,.7f,0xffca80,f);
        g.Save(folder,name,root,city);
    }
    void Launch(MarinaGeometry g,Vector3 p,Vector3 f,float length,bool sail,int paint){
        var r=Vector3.Cross(up,f);float beam=length*(sail?.27f:.33f);
        Hull(g,p,f,length,beam,.86f,paint);
        var cabin=p+up*1.23f-f*.7f;g.Box(cabin,beam*.69f,.80f,length*.43f,0xe1d8bb,f);
        if(sail){
            var heel=p+up*.86f+f*.8f;var top=heel+up*(length*.86f);
            g.Beam(heel,top,.065f,0xabab9b);
            var tack=heel+up*1.2f;var clew=tack-f*length*.4f+r*.95f;
            g.Triangle(top-up*.2f,clew,tack,0xd4c9b5,true);
            g.Triangle(top-up*1.25f,tack+f*.26f,heel+f*length*.36f,0x9eaec0,true);
            g.Beam(tack,clew,.07f,0x747c84);
        }else{
            g.Box(cabin+up*.65f,beam*.62f,.62f,length*.35f,0x496379,f);
            g.Box(cabin+up*1.03f,beam*.76f,.18f,length*.40f,0xe8d8bd,f);
            g.Box(cabin+up*1.22f,.27f,.28f,.32f,0xffca80,f);
        }
    }
    public void Build(){
        Ship("Harbour - Meridian ocean liner",865,948,264,218,true);
        Ship("Harbour - Eastbound container ship",1190,1100,78,235,false);
        Ship("Harbour - Outer anchorage freighter",1518,987,280,190,false);
        var boats=new MarinaGeometry();int index=0,moorings=0;
        var platform=streets.At(1034,845);
        foreach(var site in river.Promenade()){
            if(site.inland.z<.4f || streets.Clearance(site.edge)>148 || index++%3!=0)continue;
            var p=site.edge-site.inland*11+up*MarinaRiver.WaterY;
            if(Vector3.Distance(p,platform)<93 || !WetFootprint(p,site.direction,23,9))continue;
            var dock=site.edge-site.inland*5+up*(MarinaRiver.WaterY+.35f);
            boats.Box(dock,3,.62f,18,0x806f5b,site.direction);
            var shore=site.edge+site.inland*.25f+up*.08f;var end=dock+up*.31f;
            var along=site.direction*.8f;
            boats.Quad(shore-along,shore+along,end+along,end-along,0x9c9689);
            boats.Quad(end-along,end+along,shore+along,shore-along,0x9c9689);
            foreach(int s in new[]{-1,1}){
                var bollard=dock+site.direction*s*6;boats.Beam(bollard,bollard+up*1.1f,.13f,0x515665);
                boats.Beam(bollard+up*.7f,p+site.direction*s*4.2f+site.inland*1.3f+up*.85f,.045f,0xb5aa91);
            }
            Launch(boats,p,site.direction,moorings%2==0?12:10,moorings%3==0,moorings%2==0?0x537a85:0xb5634d);moorings++;
        }
        if(moorings==0)throw new System.InvalidOperationException("No sheltered moorings were found");
        boats.Save(folder,"Harbour - Moored launches sails and pontoons",root,city);
    }
}
}
