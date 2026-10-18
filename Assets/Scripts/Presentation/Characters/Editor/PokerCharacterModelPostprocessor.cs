using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Keep imported custom normals while deriving expression deltas from a consistent mesh basis.</summary>
public sealed class PokerCharacterModelPostprocessor : AssetPostprocessor
{
    const string Model="Assets/Art/Models/Characters/PokerCharacter/Models/PokerCharacter.fbx";
    sealed class Frame
    {
        public string name;
        public float weight;
        public Vector3[] positions;
    }
    public override uint GetVersion() => 1;
    void OnPostprocessModel(GameObject model)
    {
        if(assetPath!=Model)return;
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh=renderer.sharedMesh;
            if(!mesh || mesh.blendShapeCount==0)continue;
            Repair(mesh);
        }
    }
    static void Repair(Mesh mesh)
    {
        int count=mesh.vertexCount;
        var positions=mesh.vertices;
        var importedNormals=mesh.normals;
        var importedTangents=mesh.tangents;
        if(importedNormals.Length!=count || importedTangents.Length!=count)return;
        var frames=new List<Frame>();
        for(int s=0;s<mesh.blendShapeCount;s++)
            for(int f=0;f<mesh.GetBlendShapeFrameCount(s);f++)
            {
                var delta=new Vector3[count];
                mesh.GetBlendShapeFrameVertices(s,f,delta,null,null);
                frames.Add(new Frame{name=mesh.GetBlendShapeName(s),weight=mesh.GetBlendShapeFrameWeight(s,f),positions=delta});
            }
        var working=Object.Instantiate(mesh);
        try
        {
            working.ClearBlendShapes();
            working.RecalculateNormals();
            var calculatedBasis=working.normals;
            working.normals=importedNormals;
            working.RecalculateTangents();
            var tangentBasis=working.tangents;
            mesh.ClearBlendShapes();
            foreach(var frame in frames)
            {
                var shaped=new Vector3[count];
                for(int i=0;i<count;i++)shaped[i]=positions[i]+frame.positions[i];
                working.vertices=shaped;
                working.RecalculateNormals();
                var calculated=working.normals;
                var normals=new Vector3[count];
                var deltaNormals=new Vector3[count];
                for(int i=0;i<count;i++)
                {
                    // Subtract a matching calculated basis, not the imported custom normals.
                    // Unaffected triangles then have exactly zero delta.
                    Vector3 change=calculated[i]-calculatedBasis[i];
                    if(change.sqrMagnitude<1e-12f)normals[i]=importedNormals[i];
                    else
                    {
                        normals[i]=(importedNormals[i]+change).normalized;
                        deltaNormals[i]=normals[i]-importedNormals[i];
                    }
                }
                working.normals=normals;
                working.RecalculateTangents();
                var tangents=working.tangents;
                var deltaTangents=new Vector3[count];
                for(int i=0;i<count;i++)
                {
                    Vector3 change=(Vector3)tangents[i]-(Vector3)tangentBasis[i];
                    if(change.sqrMagnitude<1e-12f)continue;
                    deltaTangents[i]=((Vector3)importedTangents[i]+change).normalized-(Vector3)importedTangents[i];
                }
                mesh.AddBlendShapeFrame(frame.name,frame.weight,frame.positions,deltaNormals,deltaTangents);
            }
        }
        finally{Object.DestroyImmediate(working);}
    }
}
