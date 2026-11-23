using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Apex {
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RaceMap : MaskableGraphic {
    [SerializeField, Min(20)] float padViewMetres=245;
    [SerializeField] Color route=new Color(.70f,.80f,.81f), target=new Color(1,.70f,.23f), rivalColor=new Color(.20f,.86f,.73f);
    readonly List<Vector2> path=new();
    readonly List<Vector2> padLines=new();
    RaceCar car;
    public RaceGhost Ghost {get;set;}
    Circuit circuit;
    DrivingSchool school;
    RoadLayout layout;
    DrivingSchool.Lesson lesson;
    Vector2 centre,worldMin,worldMax;
    Rect area;
    float scale,nextDraw;
    int gate;
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);

    public void Bind(RaceCar player,Circuit track,DrivingSchool course,RoadLayout road){
        car=player;circuit=track;school=course;layout=road;Ghost=null;
        gate=0;padLines.Clear();
        if(track.padMarkings && !course){
            var vertices=track.padMarkings.sharedMesh.vertices;
            var matrix=track.padMarkings.transform.localToWorldMatrix;
            for(int i=0;i+3<vertices.Length;i+=4){
                padLines.Add(XZ(matrix.MultiplyPoint3x4((vertices[i]+vertices[i+2])*.5f)));
                padLines.Add(XZ(matrix.MultiplyPoint3x4((vertices[i+1]+vertices[i+3])*.5f)));
            }
        }
        RebuildPath();
    }
    void RebuildPath(){
        path.Clear();
        if(school){
            lesson=school.Current;
            for(int g=0;g<school.GateCount;g++)for(int j=0;j<8;j++)path.Add(XZ(school.MapPoint(g,j/8f)));
        }else if(layout && layout.knots.Length>1){
            for(int i=0;i<layout.knots.Length;i++)
                for(int j=0;j<layout.samplesPerSpan;j++)path.Add(XZ(circuit.transform.TransformPoint(layout.Sample(i,(float)j/layout.samplesPerSpan))));
        }else if(circuit.mapCenterline!=null && circuit.mapCenterline.Length>1){
            foreach(var p in circuit.mapCenterline)path.Add(XZ(circuit.transform.TransformPoint(p)));
        }else if(circuit.gates!=null){
            foreach(var g in circuit.gates)if(g)path.Add(XZ(g.position));
        }
        worldMin=worldMax=XZ(circuit.grid.position);
        foreach(var p in path){worldMin=Vector2.Min(worldMin,p);worldMax=Vector2.Max(worldMax,p);}
        SetVerticesDirty();
    }
    public void SetGate(int index){gate=index;}
    void LateUpdate(){
        if(!car || Time.unscaledTime<nextDraw)return;
        nextDraw=Time.unscaledTime+.04f;
        if(school && school.Current!=lesson)RebuildPath();
        SetVerticesDirty();
    }
    Vector2 Project(Vector2 p)=>area.center+(p-centre)*scale;
    void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color col){
        Vector2 d=(b-a).normalized;
        Vector2 n=new Vector2(-d.y,d.x)*(width*.5f);
        int i=vh.currentVertCount;vh.AddVert(a+n,col,Vector2.zero);vh.AddVert(b+n,col,Vector2.zero);
        vh.AddVert(b-n,col,Vector2.zero);vh.AddVert(a-n,col,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    void Diamond(VertexHelper v,Vector2 p,float size,Color color){
        Line(v,p+Vector2.up*size,p+Vector2.right*size,2,color);Line(v,p+Vector2.right*size,p-Vector2.up*size,2,color);
        Line(v,p-Vector2.up*size,p-Vector2.right*size,2,color);Line(v,p-Vector2.right*size,p+Vector2.up*size,2,color);
    }
    void Arrow(VertexHelper v,Transform vehicle,float size,Color c){
        var p=Project(XZ(vehicle.position));
        p.x=Mathf.Clamp(p.x,area.xMin+size,area.xMax-size);p.y=Mathf.Clamp(p.y,area.yMin+size,area.yMax-size);
        var d=XZ(vehicle.forward).normalized;
        var n=new Vector2(d.y,-d.x);
        int i=v.currentVertCount;
        v.AddVert(p+d*size,c,Vector2.zero);v.AddVert(p-d*size*.7f+n*size*.68f,c,Vector2.zero);
        v.AddVert(p-d*size*.35f,c,Vector2.zero);v.AddVert(p-d*size*.7f-n*size*.68f,c,Vector2.zero);
        v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
    }
    protected override void OnPopulateMesh(VertexHelper v){
        v.Clear();if(!car || !circuit)return;
        area=rectTransform.rect;var spans=worldMax-worldMin;
        bool local=path.Count==0;
        centre=local?XZ(car.transform.position):(worldMin+worldMax)*.5f;
        scale=local?area.width/padViewMetres:Mathf.Min((area.width-24)/Mathf.Max(1,spans.x),(area.height-20)/Mathf.Max(1,spans.y));
        if(local){
            for(int i=0;i+1<padLines.Count;i+=2){
                var a=Project(padLines[i]);var b=Project(padLines[i+1]);
                if(Mathf.Max(a.x,b.x)<area.xMin || Mathf.Min(a.x,b.x)>area.xMax || Mathf.Max(a.y,b.y)<area.yMin || Mathf.Min(a.y,b.y)>area.yMax)continue;
                Line(v,a,b,1.2f,route);
            }
            if(circuit.padBoundary){
                var box=circuit.padBoundary.bounds;
                Vector2 lo=Project(XZ(box.min)),hi=Project(XZ(box.max));
                var a=new Vector2(lo.x,hi.y);var b=new Vector2(hi.x,lo.y);
                Line(v,lo,a,3,target);Line(v,a,hi,3,target);Line(v,hi,b,3,target);Line(v,b,lo,3,target);
            }
        }else{
            for(int i=0;i<path.Count;i++)Line(v,Project(path[i]),Project(path[(i+1)%path.Count]),3,route);
        }
        Vector2 checkpoint=XZ(school?school.NextPosition:!local && circuit.gates.Length>0?circuit.gates[Mathf.Clamp(gate,0,circuit.gates.Length-1)].position:circuit.grid.position);
        Diamond(v,Project(checkpoint),5,target);
        if(school && school.Rival)Arrow(v,school.Rival.transform,7,rivalColor);
        if(Ghost)Arrow(v,Ghost.transform,7,Ghost.Recording.tint);
        Arrow(v,car.transform,8,new Color(.06f,.10f,.14f));Arrow(v,car.transform,6,Color.white);
    }
}
}
