using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Apex {
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RaceMap : MaskableGraphic {
    [SerializeField, Min(20)] float padViewMetres=245;
    [SerializeField] Color route=new Color(.70f,.80f,.81f), target=new Color(1,.70f,.23f);
    static readonly Color outlineDark=new Color32(16,25,32,255), outlineLight=new Color32(246,249,247,255);
    readonly List<Vector2> path=new();
    readonly List<Vector2> padLines=new();
    RaceCar car;
    public RaceGhost Ghost {get;set;}
    public GridRace Field {get;set;}
    Circuit circuit;
    RoadLayout layout;
    Vector2 centre,worldMin,worldMax,anchor,up=Vector2.up,right=Vector2.right;
    Rect area;
    float scale,nextDraw;
    int gate;
    Color playerPaint;
    public bool FollowCar {get;private set;}=true;
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);

    public void SetFollow(bool follow){
        FollowCar=follow;
        SetVerticesDirty();
    }
    public void Bind(RaceCar player,Circuit track,RoadLayout road,Color paint){
        car=player;circuit=track;layout=road;Ghost=null;Field=null;
        playerPaint=paint;
        gate=0;padLines.Clear();
        if(track.padMarkings){
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
        if(layout && layout.knots.Length>1){
            for(int i=0;i<layout.knots.Length;i++)
                for(int j=0;j<layout.samplesPerSpan;j++)path.Add(XZ(circuit.transform.TransformPoint(layout.Sample(i,(float)j/layout.samplesPerSpan))));
        }else if(circuit.mapCenterline!=null && circuit.mapCenterline.Length>1){
            foreach(var p in circuit.mapCenterline)path.Add(XZ(circuit.transform.TransformPoint(p)));
        }else if(circuit.gates!=null){
            foreach(var g in circuit.gates)if(g)path.Add(XZ(g.position));
        }
        worldMin=worldMax=XZ(circuit.grid.position);
        foreach(var p in path){worldMin=Vector2.Min(worldMin,p);worldMax=Vector2.Max(worldMax,p);}
        if(path.Count==0 && circuit.padBoundary){
            var bounds=circuit.padBoundary.bounds;
            worldMin=XZ(bounds.min);worldMax=XZ(bounds.max);
        }
        SetVerticesDirty();
    }
    public void SetGate(int index){gate=index;}
    void LateUpdate(){
        if(!car || Time.unscaledTime<nextDraw)return;
        nextDraw=Time.unscaledTime+.04f;
        SetVerticesDirty();
    }
    void SetView(){
        area=rectTransform.rect;
        var spans=worldMax-worldMin;
        scale=Mathf.Min((area.width-24)/Mathf.Max(1,spans.x),(area.height-24)/Mathf.Max(1,spans.y));
        if(!FollowCar){
            centre=(worldMin+worldMax)*.5f;anchor=area.center;
            up=Vector2.up;right=Vector2.right;
            return;
        }
        // Once the player finishes, the race camera and map follow the same spectator target.
        var subject=Field!=null?Field.Watching.car:car;
        centre=XZ(subject.transform.position);
        up=XZ(subject.transform.forward).normalized;
        if(up.sqrMagnitude<.5f)up=Vector2.up;
        right=new Vector2(up.y,-up.x);
        anchor=new Vector2(area.center.x,area.yMin+area.height*.34f);
        scale=path.Count==0?area.width/padViewMetres:Mathf.Max(scale*1.7f,area.width/460f);
    }
    Vector2 Project(Vector2 p){
        var delta=p-centre;
        return anchor+new Vector2(Vector2.Dot(delta,right),Vector2.Dot(delta,up))*scale;
    }
    void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color col){
        if(Mathf.Max(a.x,b.x)<area.xMin-width || Mathf.Min(a.x,b.x)>area.xMax+width ||
           Mathf.Max(a.y,b.y)<area.yMin-width || Mathf.Min(a.y,b.y)>area.yMax+width)return;
        Vector2 d=(b-a).normalized;
        Vector2 n=new Vector2(-d.y,d.x)*(width*.5f);
        int i=vh.currentVertCount;vh.AddVert(a+n,col,Vector2.zero);vh.AddVert(b+n,col,Vector2.zero);
        vh.AddVert(b-n,col,Vector2.zero);vh.AddVert(a-n,col,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    void Diamond(VertexHelper v,Vector2 p,float size,Color color){
        Line(v,p+Vector2.up*size,p+Vector2.right*size,2,color);Line(v,p+Vector2.right*size,p-Vector2.up*size,2,color);
        Line(v,p-Vector2.up*size,p-Vector2.right*size,2,color);Line(v,p-Vector2.right*size,p+Vector2.up*size,2,color);
    }
    static void Arrow(VertexHelper v,Vector2 p,Vector2 d,float size,Color c){
        var n=new Vector2(d.y,-d.x);
        int i=v.currentVertCount;
        v.AddVert(p+d*size,c,Vector2.zero);v.AddVert(p-d*size*.7f+n*size*.68f,c,Vector2.zero);
        v.AddVert(p-d*size*.35f,c,Vector2.zero);v.AddVert(p-d*size*.7f-n*size*.68f,c,Vector2.zero);
        v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
    }
    static void Circle(VertexHelper v,Vector2 p,float radius,Color color){
        const int sides=12;
        int center=v.currentVertCount;
        v.AddVert(p,color,Vector2.zero);
        for(int i=0;i<sides;i++){
            float angle=i*(2*Mathf.PI/sides);
            v.AddVert(p+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
        }
        for(int i=0;i<sides;i++)v.AddTriangle(center,center+1+(i+1)%sides,center+1+i);
    }
    void Opponent(VertexHelper v,Transform vehicle,Color paint){
        var point=Project(XZ(vehicle.position));
        if(!area.Contains(point))return;
        Circle(v,point,4.8f,outlineLight);
        Circle(v,point,3.8f,paint);
    }
    protected override void OnPopulateMesh(VertexHelper v){
        v.Clear();if(!car || !circuit)return;
        SetView();
        bool local=path.Count==0;
        if(local){
            for(int i=0;i+1<padLines.Count;i+=2){
                Line(v,Project(padLines[i]),Project(padLines[i+1]),FollowCar?1.2f:.65f,route);
            }
            if(circuit.padBoundary){
                var box=circuit.padBoundary.bounds;
                var min=XZ(box.min);var max=XZ(box.max);
                var lo=Project(min);var hi=Project(max);
                var a=Project(new Vector2(min.x,max.y));var b=Project(new Vector2(max.x,min.y));
                Line(v,lo,a,3,target);Line(v,a,hi,3,target);Line(v,hi,b,3,target);Line(v,b,lo,3,target);
            }
        }else{
            for(int i=0;i<path.Count;i++)Line(v,Project(path[i]),Project(path[(i+1)%path.Count]),3,route);
        }
        Vector2 checkpoint=XZ(!local && circuit.gates.Length>0?circuit.gates[Mathf.Clamp(gate,0,circuit.gates.Length-1)].position:circuit.grid.position);
        Diamond(v,Project(checkpoint),5,target);
        if(Ghost)Opponent(v,Ghost.transform,Ghost.Recording.tint);
        if(Field!=null){
            foreach(var e in Field.entries)if(e!=Field.Player && !e.Finished)
                Opponent(v,e.car.transform,e.livery.paint);
        }
        var point=Project(XZ(car.transform.position));
        if(!FollowCar){
            point.x=Mathf.Clamp(point.x,area.xMin+12,area.xMax-12);
            point.y=Mathf.Clamp(point.y,area.yMin+12,area.yMax-12);
        }
        var heading=XZ(car.transform.forward).normalized;
        var direction=new Vector2(Vector2.Dot(heading,right),Vector2.Dot(heading,up));
        if(direction.sqrMagnitude<.5f)direction=Vector2.up;
        Arrow(v,point,direction,12,outlineLight);
        Arrow(v,point,direction,9.6f,outlineDark);
        Arrow(v,point,direction,7.1f,playerPaint);
    }
}
}
