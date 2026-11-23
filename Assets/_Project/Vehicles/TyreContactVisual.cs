using UnityEngine;

namespace Apex {
// Only the rubber mesh yields against the ground plane. The rim and brake rotor remain rigid.
public sealed class TyreContactVisual {
    readonly Mesh mesh;
    readonly Transform rubber;
    readonly Vector3[] original, deformed;
    bool flattened;
    public TyreContactVisual(Transform wheelPose) {
        foreach(var filter in wheelPose.GetComponentsInChildren<MeshFilter>()){
            if(!filter.name.StartsWith("Tyre_",System.StringComparison.Ordinal) || !filter.sharedMesh.isReadable)continue;
            rubber=filter.transform;
            mesh=filter.mesh;
            mesh.MarkDynamic();
            original=mesh.vertices;
            deformed=new Vector3[original.Length];
            break;
        }
    }
    public void Apply(bool contact,Vector3 point,Vector3 normal) {
        if(!mesh)return;
        if(!contact){
            if(flattened){mesh.vertices=original;flattened=false;}
            return;
        }
        // Distances are in world metres even when the imported FBX uses a local scale.
        var matrix=rubber.localToWorldMatrix;
        var plane=matrix.transpose.MultiplyVector(normal);
        float offset=Vector3.Dot(normal,rubber.position-point);
        Vector3 correction=rubber.worldToLocalMatrix.MultiplyVector(normal);
        for(int i=0;i<original.Length;i++){
            float distance=Vector3.Dot(plane,original[i])+offset;
            deformed[i]=original[i]-Mathf.Min(0,distance)*correction;
        }
        mesh.vertices=deformed;
        flattened=true;
    }
    public void Dispose(){if(mesh)Object.Destroy(mesh);}
}
}
