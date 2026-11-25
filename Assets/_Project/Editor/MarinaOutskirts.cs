using System.Collections.Generic;
using UnityEngine;

namespace Apex.Editor {
internal static class MarinaOutskirts {
    // Distance from the actual last street. Relief starts outside the developed blocks.
    static float Outside(List<Vector3> rim,Vector3 p){
        float nearest=float.MaxValue;bool inside=false;
        for(int i=0;i<rim.Count;i++){
            var a=rim[i];var b=rim[(i+1)%rim.Count];var d=b-a;
            float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude);
            nearest=Mathf.Min(nearest,(a+d*t-p).sqrMagnitude);
            if((a.z>p.z)!=(b.z>p.z) && p.x<a.x+(p.z-a.z)*d.x/d.z)inside=!inside;
        }
        return inside?0:Mathf.Sqrt(nearest);
    }
    public static void Build(List<Vector3> rim,MarinaRiver river,Transform root,string folder,Material mat){
        const int step=85, radius=50;
        var nodes=new Vector3[2*radius+1,2*radius+1];
        var inks=new[]{0x374d49,0x405b50,0x415851,0x4a6255,0x465e58};
        for(int x=-radius;x<=radius;x++)for(int z=-radius;z<=radius;z++){
            var p=new Vector3(x*step,0,z*step);float edge=Outside(rim,p);
            var nz=river.Bank(p.x,false,out _).z;var sz=river.Bank(p.x,true,out _).z;
            float dry=Mathf.Max(p.z-nz,sz-p.z);
            float slope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(40,570,edge))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(55,260,dry));
            float hills=10+140*Mathf.PerlinNoise(x*.085f+7.1f,z*.105f+4.2f)+35*Mathf.Sin(x*.16f+z*.23f);
            p.y=MarinaRiver.LandY-.35f+slope*Mathf.Max(6,hills);nodes[x+radius,z+radius]=p;
        }
        for(int sector=0;sector<4;sector++){
            var g=new MarinaGeometry();var random=new System.Random(513+sector);
            int xmin=sector%2==0?0:radius,zmin=sector/2==0?0:radius;
            for(int x=xmin;x<xmin+radius;x++)for(int z=zmin;z<zmin+radius;z++){
                var a=nodes[x,z];var b=nodes[x,z+1];var c=nodes[x+1,z+1];var d=nodes[x+1,z];
                if(Mathf.Max(Mathf.Max(a.y,b.y),Mathf.Max(c.y,d.y))<0)continue;
                var mid=(a+c)*.5f;mid.y=0;if(!river.Dry(mid,step*.72f))continue;
                int shade=Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise(x*.21f,z*.22f)*inks.Length),0,inks.Length-1);
                g.Quad(a,b,c,d,inks[shade]);
                if(mid.sqrMagnitude>3600f*3600f || random.Next(4)!=0)continue;
                for(int k=0;k<4;k++){
                    // Barycentric placement on the same two rendered triangles.
                    float u=(float)random.NextDouble(),v=(float)random.NextDouble();
                    var p=v>=u?a+(b-a)*v+(c-b)*u:a+(d-a)*u+(c-d)*v;
                    if(p.y<.5f)continue;
                    MarinaCityForms.Tree(g,p,10+random.Next(7),5.5f,random.Next(64));
                }
            }
            g.Save(folder,"Coastal greenbelt "+sector,root,mat);
        }
    }
}
}
