using UnityEngine;
using static Apex.Editor.MarinaStreets;

namespace Apex.Editor {
internal sealed class MarinaWaterfront {
    const int Steel=0x586679,Glass=0x27384d,Stone=0xbeb9a9,Cyan=0x77e9fc,Pink=0xff559e,Amber=0xffca80;
    readonly MarinaStreets streets;
    readonly System.Random random=new(20080928);
    readonly Transform parent;
    readonly MarinaRiver river;
    readonly System.Collections.Generic.List<Bounds> plots=new();
    readonly Material city,water;
    readonly string folder;
    MarinaGeometry g=new();
    readonly Vector3 up=Vector3.up;
    Vector3 At(float x,float y)=>streets.At(x,y);
    float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    public MarinaWaterfront(MarinaStreets streets,MarinaRiver river,Transform parent,string folder,Material city,Material water){
        this.streets=streets;this.river=river;this.parent=parent;this.folder=folder;this.city=city;this.water=water;
    }
    void Finish(string name){g.Save(folder,name,parent,city);g=new MarinaGeometry();}
    void BuildingCollider(Vector3 p,float w,float h,float d,Vector3 forward=default){
        var go=new GameObject("Building footprint");go.transform.SetParent(parent,false);
        go.transform.position=p+up*(h*.5f);
        if(forward!=Vector3.zero)go.transform.rotation=Quaternion.LookRotation(forward);
        go.AddComponent<BoxCollider>().size=new Vector3(w,h,d);
    }
    void Tower(Vector3 p,float w,float h,float d,int style){
        int[] glass={0x293a51,0x373850,0x2d4c59,0x3e4359};
        int[] windows={0x91c1d7,0xe2c28f,0x63798a,0x9abfbe,0xf5d4a4};
        plots.Add(new Bounds(p,new Vector3(w+3,6,d+3)));
        g.Box(p-up*.03f,w+1.6f,.22f,d+1.6f,0x747778);
        g.Box(p+up*(h*.5f),w,h,d,glass[style%4]);
        BuildingCollider(p,w,h,d);
        for(int face=0;face<4;face++){
            var normal=Quaternion.Euler(0,face*90,0)*Vector3.forward;
            Vector3 right=Vector3.Cross(up,normal);float length=face%2==0?w:d;
            var basePos=p+normal*((face%2==0?d:w)*.5f+.06f);
            int cols=Mathf.Max(2,Mathf.FloorToInt(length/4.8f));
            for(float y=4;y<h-1;y+=style%3==2?5.1f:3.9f){
                for(int c=0;c<cols;c++){
                    if(random.NextDouble()<.37)continue;
                    var mid=basePos+right*((c+.5f-cols*.5f)*length/cols)+up*y;
                    var a=right*(length/cols*R(.27f,.45f));var b=up*R(.55f,1.15f);
                    g.Quad(mid-a-b,mid+a-b,mid+a+b,mid-a+b,windows[random.Next(windows.Length)]);
                }
            }
            if(style%4==1){
                var a=basePos-right*length*.47f;
                g.Beam(a+up*4,a+up*(h-1),.22f,style%2==0?Cyan:Pink);
            }
        }
        g.Box(p+up*(h+.5f),w+1,1,d+1,style%3==1?0xb3a2ce:0x627583);
        if(h>48)g.Box(p+up*(h+2.9f),w*.60f,5,d*.62f,Glass);
    }
    void Blocks(string name,int xmin,int xmax,int ymin,int ymax,int spacing,float low,float high){
        for(int x=xmin;x<=xmax;x+=spacing)for(int y=ymin;y<=ymax;y+=spacing){
            var p=At(x+R(-6,6),y+R(-6,6));
            float w=R(22,46),d=R(23,49);
            float radius=Mathf.Sqrt(w*w+d*d)*.5f;
            if(streets.Clearance(p)<radius+5 || !river.Dry(p,radius))continue;
            Tower(p,w,R(low,high),d,random.Next(12));
        }
        Finish(name);
    }
    void Flyer(){
        var basePos=At(1514,664);float radius=73;var hub=basePos+up*85;
        var sideways=new Vector3(.76f,0,.65f).normalized;var ax=Vector3.Cross(up,sideways);
        for(int side=-1;side<=1;side+=2){
            var centre=hub+ax*(side*2.5f);
            for(int i=0;i<42;i++){
                float a=i*Mathf.PI/21,b=(i+1)*Mathf.PI/21;
                Vector3 Ring(float t,float r)=>centre+(sideways*Mathf.Cos(t)+up*Mathf.Sin(t))*r;
                g.Beam(Ring(a,radius),Ring(b,radius),.75f,i%7==0?Pink:Cyan);
                g.Beam(Ring(a,radius-3),Ring(b,radius-3),.35f,0x83afb6);
                g.Beam(Ring(a,radius),Ring(b,radius-3),.24f,0xadc5d2);
                if(i%2==0){
                    g.Beam(centre,Ring(a,radius-3),.22f,0x7592a6);
                    if(side==1)g.Box(Ring(a,radius)+ax*-2.5f-up*2,4.6f,2.7f,5,0xe8c4a0);
                }
            }
            foreach(int foot in new[]{-1,1})g.Beam(basePos+sideways*(foot*32)+ax*(side*14),centre,1.65f,0xa4a9bc);
        }
        g.Box(basePos-up*.04f,73,.32f,37,0x747d83,sideways);
        g.Box(basePos+up*3,64,6,25,Glass);Finish("Singapore Flyer");
    }
    void Hall(Vector3 p,float w,float d,float h,Vector3 f){
        var right=Vector3.Cross(up,f);
        g.Box(p-up*.05f,w+5,.27f,d+7,0x90908b,f);
        plots.Add(new Bounds(p,new Vector3(Mathf.Abs(right.x)*w+Mathf.Abs(f.x)*d+6,4,Mathf.Abs(right.z)*w+Mathf.Abs(f.z)*d+6)));
        g.Box(p+up*(h*.5f),w,h,d,Stone,f);
        g.Box(p+up*(h+1),w+3,2,d+4,0xe1d2b8,f);
        for(int s=-1;s<=1;s+=2){
            var front=p+f*(s*d*.5f);
            g.Box(front+f*(s*.78f)+up*.69f,w+3,1.63f,3.5f,0xa29685,f);
            for(int i=0;i<Mathf.RoundToInt(w/6);i++){
                var basePos=p+right*(-w*.45f+i*6)+f*(s*(d*.5f+.9f));
                g.Beam(basePos+up*1.5f,basePos+up*(h-.5f),.7f,0xf2d8af,8);
                g.Box(basePos+up*1.4f,2,1.2f,2,0xd1b997);
                var opening=basePos+right*2.9f-f*(s*.84f);
                for(float y=3.5f;y<h-1.8f;y+=4.0f){
                    g.Box(opening+up*y,2.3f,2.5f,.14f,0x52616a,f);
                    g.Box(opening+up*(y-1.28f),2.64f,.16f,.34f,0xb0a995,f);
                    g.Box(opening+up*y+f*s*.09f,.12f,2.5f,.10f,0xb0a995,f);
                }
            }
            for(int i=0;i<4;i++){
                float top=1.22f-i*.32f;
                g.Box(front+f*(s*(2.6f+i*.50f))+up*((top-.15f)*.5f),w+4,top+.15f,.58f,Stone,f);
            }
            for(float x=-d*.39f;x<=d*.39f;x+=5)for(float y=3.6f;y<h-1.6f;y+=4)
                g.Box(p+right*(s*(w*.5f+.07f))+f*x+up*y,.16f,2.3f,2.0f,0x53616a,f);
        }
    }
    void Civic(){
        var dir=(At(360,392)-At(220,651)).normalized;var right=Vector3.Cross(up,dir);
        var p=At(303,526);
        g.Box(p+right*68-up*.13f,110,.25f,260,0x395249,dir);
        for(int i=0;i<4;i++)g.Box(p+right*68+dir*(i*52-75)+up*.015f,84,.025f,.18f,0x8eaca1,dir);
        Hall(p-right*50,136,36,16,right);
        Hall(At(335,735),54,28,13,Vector3.forward);
        var clock=At(335,735)+up*21;g.Box(clock,8,17,8,Stone);g.Box(clock+up*10,9,3,9,0x868e96);
        Hall(At(350,1082),83,42,23,(At(360,963)-At(310,913)).normalized);
        Finish("Padang City Hall and Fullerton");
    }
    void Domes(){
        foreach(var p in new[]{At(646,617),At(713,603)}){
            float w=26,d=18,h=20;
            for(int j=0;j<30;j++){float a=2*Mathf.PI*j/30,b=2*Mathf.PI*(j+1)/30;
                var v=p+new Vector3(Mathf.Cos(a)*w,0,Mathf.Sin(a)*d);var q=p+new Vector3(Mathf.Cos(b)*w,0,Mathf.Sin(b)*d);
                g.Quad(v-up*.16f,v+up*3,q+up*3,q-up*.16f,0x76848c);
            }
            Vector3 Shell(int i,int j){
                float angle=2*Mathf.PI*i/30,lat=Mathf.PI*.5f*j/8;
                return p+new Vector3(Mathf.Cos(angle)*w*Mathf.Cos(lat),3+h*Mathf.Sin(lat),Mathf.Sin(angle)*d*Mathf.Cos(lat));
            }
            for(int i=0;i<30;i++)for(int j=0;j<8;j++){
                var a=Shell(i,j);var b=Shell(i,j+1);var c=Shell(i+1,j+1);var e=Shell(i+1,j);
                g.Quad(a,b,c,e,(i+j)%3==0?0x8497a3:0x536372);g.Beam(a,c,.18f,0xd1c29d);
            }
        }
        Finish("Esplanade twin shells");
    }
    public static void Stand(MarinaGeometry g,Vector3 front,Vector3 forward,float width,int rows){
        int[] clothes={0x425064,0x656682,0x9c7a8b,0x495c65,0xa1a4aa,0x907068,0x496978};
        var random=new System.Random(Mathf.RoundToInt(front.x*7+front.z*19));var right=Vector3.Cross(Vector3.up,forward);
        for(int i=0;i<rows;i++){
            var p=front+forward*(i*1.3f)+Vector3.up*(i*.66f+.5f);
            float top=i*.66f+1;
            g.Box(front+forward*(i*1.3f)+Vector3.up*(top*.5f-.06f),width,top+.12f,1.5f,i%3==0?0x648497:0x465b76,forward);
            g.Box(p+Vector3.up*.59f,width,.18f,.45f,i%5==0?0xff8a99:0x9bbdc5,forward);
            if(i%2==0)for(float x=-width*.48f;x<width*.48f;x+=1.15f){
                if(random.NextDouble()<.38)continue;
                var seat=p+right*x+Vector3.up*1.04f;
                g.Box(seat,.43f,.57f,.4f,clothes[random.Next(clothes.Length)],forward);
                g.Box(seat+Vector3.up*.40f,.25f,.25f,.26f,random.Next(2)==0?0xb19888:0x90796f,forward);
            }
        }
    }
    void WaterfrontStand(){
        var mid=At(1036,689);var right=Vector3.right;
        for(int i=-2;i<=2;i++)Stand(g,mid+right*(i*30),Vector3.forward,29,12);
        var roof=At(1095,684);g.Box(roof+up*8.4f,34,1.8f,20,0x7890a4);
        g.Box(roof+up*9.55f,34,.4f,20,0x94b1c6);
        foreach(int s in new[]{-1,1})foreach(int z in new[]{-1,1}){
            var foot=roof+Vector3.right*s*15+Vector3.forward*z*8;
            if(streets.Clearance(foot)>1){g.Box(foot,1.5f,.34f,1.5f,0x969b9c);g.Beam(foot,foot+up*8,.5f,Steel);}
        }
        var floatDeck=At(1034,845)+up*(MarinaRiver.WaterY+.65f);g.Box(floatDeck,127,1.1f,56,0x456a60);
        for(int j=-1;j<=1;j+=2){
            var land=At(1034+j*42,778);
            var pontoon=floatDeck+Vector3.right*(j*42*streets.scale)+Vector3.forward*26+up*.55f;
            var cross=Vector3.Cross(up,(pontoon-land).normalized).normalized;
            g.Quad(land-cross*1.3f,pontoon-cross*1.3f,pontoon+cross*1.3f,land+cross*1.3f,0xacadb0);
            foreach(int sign in new[]{-1,1})g.Beam(land+cross*sign*1.3f+up,pontoon+cross*sign*1.3f+up,.07f,Steel);
        }
        Finish("Bay grandstand and floating platform");
    }
    void Construction(){
        for(int i=0;i<3;i++){
            var p=At(995+i*96,1486-i*25);int floors=11+i*2;
            g.Box(p-up*.02f,51,.35f,26,0x6e7279);
            for(int j=0;j<floors;j++)g.Box(p+up*(j*4+2),49,.85f,24,0x878797);
            for(int k=-2;k<=2;k++)foreach(int sign in new[]{-1,1})
                g.Beam(p+new Vector3(k*10,0,sign*9),p+new Vector3(k*10,floors*4+2,sign*9),.65f,Steel);
            var mast=p+new Vector3(-32,0,6);var top=mast+up*(floors*4+19);
            g.Box(mast,4,.48f,4,0x777b80);
            g.Beam(mast,top,1.1f,Amber);g.Beam(top-Vector3.right*11,top+Vector3.right*43,.8f,Amber);
            g.Beam(top+up*5,top+Vector3.right*40,.29f,Amber);g.Beam(top,top+up*5,.4f,Amber);
            g.Beam(top+Vector3.right*31,top+Vector3.right*31-up*15,.13f,Stone);
        }
        Finish("Bayfront construction cranes");
    }
    void Palm(Vector3 p,float height){
        var top=p+up*height+Vector3.right*.65f;
        g.Beam(p,top,.25f,0x776a5c);
        for(int j=0;j<9;j++){
            var dir=Quaternion.Euler(0,j*40+height*11,0)*Vector3.forward;
            var span=Vector3.Cross(up,dir)*.72f;var mid=top+dir*2.7f+up*1.2f;var tip=top+dir*5.4f-up*.9f;
            int ink=j%3==0?0x426454:0x344d47;
            g.Quad(top,mid+span,tip,mid-span,ink);g.Quad(mid-span,tip,mid+span,top,ink);
        }
    }
    void PlantPalm(Vector3 p,Vector3 f){
        g.Box(p+up*.025f,3.1f,.26f,4.4f,0x8e9287,f);
        g.Box(p+up*.148f,2.75f,.05f,4.05f,0x4a5542,f);
        Palm(p+up*.12f,R(6,10));
    }
    void RiversideWalk(){
        int index=0;
        foreach(var site in river.Promenade()){
            var f=site.direction;var inland=site.inland;
            var paving=site.edge+inland*3.7f;var tree=site.edge+inland*9;
            if(plots.Exists(b=>b.Contains(paving)) || !river.Dry(paving,2))continue;
            var post=site.edge+inland*1.55f;
            g.Box(post, .63f,.36f,.63f,0x94918a,f);
            g.Beam(post,post+up*3.8f,.095f,0x565b60,8);
            g.Box(post+up*3.83f,.49f,.15f,.49f,0x4e5559,f);
            g.Box(post+up*4.18f,.36f,.60f,.36f,0xffca80,f);
            g.Box(post+up*4.51f,.59f,.10f,.59f,0x4e5559,f);
            if(index++%2==0){
                var seat=paving+f*2;
                foreach(int sign in new[]{-1,1})g.Box(seat+f*sign*.72f+up*.19f,.48f,.56f,.16f,0x737877,f);
                g.Box(seat+up*.51f,.64f,.12f,2.25f,0xa18d76,f);
                g.Box(seat+inland*.27f+up*.74f,.11f,.53f,2.25f,0x998773,f);
            }
            if(!river.Dry(tree,8.1f) || streets.Clearance(tree)<9 || plots.Exists(b=>b.Contains(tree)))continue;
            int station=Nearest(streets.main,tree);
            if(river.BridgeReserve(station,25) && streets.Clearance(tree)<28)continue;
            g.Box(tree-up*.049f,8,.13f,14,0x485c4f,f);
            PlantPalm(tree,f);
        }
    }
    void Streetscape(){
        // Public pavements and parallel access streets run between the hotel blocks.
        foreach(int y in new[]{70,150,220,293,365,542}){
            for(int x=450;x<1390;x+=22){
                var p=At(x,y);
                if(streets.Clearance(p)<16 || !river.Dry(p,9) || plots.Exists(b=>b.Contains(p)))continue;
                g.Box(p-up*.058f,22*streets.scale,.1f,9,0x474f5a);
                g.Box(p+Vector3.forward*5+up*.032f,22*streets.scale,.13f,1.2f,0x676971);
                if(x%66==10){g.Beam(p+Vector3.forward*6,p+Vector3.forward*6+up*5,.10f,Steel);g.Box(p+Vector3.forward*6+up*5,1.6f,.22f,.6f,Amber);}
            }
        }
        for(int i=0;i<streets.main.Length;i+=11){
            var p=streets.main[i];var f=Forward(streets.main,i);var side=Vector3.Cross(up,f);
            var coords=p/streets.scale;
            bool green=coords.x < -310 || coords.z < -135 || coords.x>240 && coords.z>40;
            if(!green || river.BridgeReserve(i,68))continue;
            int sign=(i/11)%3==0?-1:1;var basePos=p+side*(sign*(streets.Width(p)*.5f+R(4,7)));
            if(streets.Clearance(basePos)<3.7f || !river.Dry(basePos,4.5f) || plots.Exists(b=>b.Contains(basePos)))continue;
            PlantPalm(basePos,f);
        }
        RiversideWalk();
        Finish("Street markets boulevards and palms");
    }
    public void Build(){
        Flyer();Civic();Domes();WaterfrontStand();Construction();
        Blocks("Raffles Boulevard hotels",605,1350,112,418,71,27,116);
        Blocks("Marina Square arcades",716,1390,500,600,66,15,55);
        Blocks("City Hall blocks",-110,390,90,305,65,20,99);
        Blocks("Financial district skyline",-140,566,1110,1300,86,55,187);
        Blocks("North city skyline",425,1600,-190,28,112,45,153);
        Streetscape();
    }
}
}
