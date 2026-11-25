using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Editor {
internal sealed class MarinaGeometry {
    readonly List<Vector3> verts=new();
    readonly List<int> tris=new();
    readonly List<Color32> colors=new();
    readonly List<Vector2> uv=new();
    // Only light sources, luminous lettering and lit windows bloom. Pale stone stays masonry.
    static readonly HashSet<int> illuminated=new(){
        0xfff5da,0xfff9ec,0xff559e,0xff559a,0x77e9fc,0x68e7ff,0xffca80,
        0x91c1d7,0xe2c28f,0x9abfbe,0xf5d4a4
    };
    public static Color32 Ink(int rgb)=>new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,(byte)(illuminated.Contains(rgb)?255:0));
    public void Triangle(Vector3 a,Vector3 b,Vector3 c,int color,bool twoSided=false){
        int k=verts.Count;verts.Add(a);verts.Add(b);verts.Add(c);
        var ink=Ink(color);colors.Add(ink);colors.Add(ink);colors.Add(ink);
        uv.Add(Vector2.zero);uv.Add(Vector2.up);uv.Add(Vector2.right);
        tris.Add(k);tris.Add(k+1);tris.Add(k+2);
        if(twoSided)Triangle(c,b,a,color);
    }
    public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int color,float s=0,float end=0)=>
        Quad(a,b,c,d,color,new Rect(0,s,1,end-s));
    public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int color,Rect tile){
        int k=verts.Count;verts.Add(a);verts.Add(b);verts.Add(c);verts.Add(d);
        var ink=Ink(color);for(int i=0;i<4;i++)colors.Add(ink);
        uv.Add(tile.min);uv.Add(new Vector2(tile.xMin,tile.yMax));uv.Add(tile.max);uv.Add(new Vector2(tile.xMax,tile.yMin));
        tris.Add(k);tris.Add(k+1);tris.Add(k+2);tris.Add(k);tris.Add(k+2);tris.Add(k+3);
    }
    public void Box(Vector3 p,float w,float h,float d,int color,Vector3 forward=default){
        if(forward==Vector3.zero)forward=Vector3.forward;
        var z=forward.normalized*(d*.5f);var x=Vector3.Cross(Vector3.up,forward).normalized*(w*.5f);
        var y=Vector3.up*(h*.5f);
        Quad(p-x+y-z,p-x+y+z,p+x+y+z,p+x+y-z,color);
        Quad(p-x-y+z,p-x-y-z,p+x-y-z,p+x-y+z,color);
        Quad(p-x-y-z,p-x+y-z,p+x+y-z,p+x-y-z,color);
        Quad(p+x-y+z,p+x+y+z,p-x+y+z,p-x-y+z,color);
        Quad(p-x-y+z,p-x+y+z,p-x+y-z,p-x-y-z,color);
        Quad(p+x-y-z,p+x+y-z,p+x+y+z,p+x-y+z,color);
    }
    public void Beam(Vector3 a,Vector3 b,float radius,int color,int sides=5){
        var axis=(b-a).normalized;if(axis.sqrMagnitude<.5f)return;
        var x=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized*radius;
        var y=Vector3.Cross(axis,x);
        for(int i=0;i<sides;i++){
            float t=i*2*Mathf.PI/sides,u=(i+1)*2*Mathf.PI/sides;
            var v=x*Mathf.Cos(t)+y*Mathf.Sin(t);var w=x*Mathf.Cos(u)+y*Mathf.Sin(u);
            Quad(a+v,a+w,b+w,b+v,color);
        }
    }
    // Vertical advertising artwork. Coalesce each horizontal run of pixels.
    static readonly Dictionary<char,string> Glyph=new(){
        ['A']="01110/10001/10001/11111/10001/10001/10001",['B']="11110/10001/10001/11110/10001/10001/11110",
        ['C']="01111/10000/10000/10000/10000/10000/01111",['D']="11110/10001/10001/10001/10001/10001/11110",
        ['E']="11111/10000/10000/11110/10000/10000/11111",['F']="11111/10000/10000/11110/10000/10000/10000",
        ['G']="01111/10000/10000/10111/10001/10001/01111",['H']="10001/10001/10001/11111/10001/10001/10001",
        ['I']="11111/00100/00100/00100/00100/00100/11111",['J']="00111/00010/00010/00010/00010/10010/01100",
        ['K']="10001/10010/10100/11000/10100/10010/10001",['L']="10000/10000/10000/10000/10000/10000/11111",
        ['M']="10001/11011/10101/10101/10001/10001/10001",['N']="10001/11001/11001/10101/10011/10011/10001",
        ['O']="01110/10001/10001/10001/10001/10001/01110",['P']="11110/10001/10001/11110/10000/10000/10000",
        ['Q']="01110/10001/10001/10001/10101/10010/01101",['R']="11110/10001/10001/11110/10100/10010/10001",
        ['S']="01111/10000/10000/01110/00001/00001/11110",['T']="11111/00100/00100/00100/00100/00100/00100",
        ['U']="10001/10001/10001/10001/10001/10001/01110",['V']="10001/10001/10001/10001/10001/01010/00100",
        ['W']="10001/10001/10001/10101/10101/11011/10001",['Y']="10001/10001/01010/00100/00100/00100/00100",
        ['0']="01110/10001/10011/10101/11001/10001/01110",['1']="00100/01100/00100/00100/00100/00100/01110",
        ['5']="11111/10000/10000/11110/00001/00001/11110",['8']="01110/10001/10001/01110/10001/10001/01110"
    };
    // forward points away from the reader. p is the centre of the lettering.
    public void Lettering(string value,Vector3 p,Vector3 forward,float px,int color){
        var right=Vector3.Cross(Vector3.up,forward).normalized;var up=Vector3.up;
        var corner=p-right*((value.Length*6-1)*.5f*px)+up*(3.5f*px);
        for(int j=0;j<value.Length;j++){
            if(!Glyph.TryGetValue(value[j],out var data))continue;
            var rows=data.Split('/');
            for(int r=0;r<7;r++)for(int k=0;k<5;k++){
                if(rows[r][k]!='1')continue;int first=k;while(k+1<5 && rows[r][k+1]=='1')k++;
                var a=corner+right*((j*6+first)*px)-up*(r*px);
                var b=a+right*((k-first+1)*px);
                Quad(a,b,b-up*px,a-up*px,color);
            }
        }
    }
    public GameObject Save(string folder,string name,Transform parent,Material material,bool collide=false){
        if(verts.Count==0)return null;
        string path=folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create=mesh==null;
        if(create)mesh=new Mesh{name=name};else mesh.Clear(false);
        mesh.indexFormat=verts.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(verts);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        if(collide)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return go;
    }
}
}
