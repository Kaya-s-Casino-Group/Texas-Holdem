using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CharacterBlinkValidation
{
    const string Model="Assets/Art/Models/Characters/PokerCharacter/Models/PokerCharacter.fbx";
    const string Report="Art/HandSkinReview/Unity/Idle/blink-normals.txt";
    [MenuItem("Tools/Hand Skin Validation/14 Reimport and Validate Blink Normals")]
    public static void Reimport()
    {
        AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        Diagnose();
        ValidateUnchangedBody();
        ValidateBakedBody();
    }
    static void ValidateUnchangedBody()
    {
        var mesh=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Mesh>().First();
        var vertices=mesh.vertices;var normals=mesh.normals;
        var dv=new Vector3[mesh.vertexCount];var dn=new Vector3[mesh.vertexCount];var dt=new Vector3[mesh.vertexCount];
        foreach(string name in new[]{"Blink_L","Blink_R","Smile_Subtle"})
        {
            int shape=mesh.GetBlendShapeIndex(name);if(shape<0)throw new Exception("Missing expression "+name);
            mesh.GetBlendShapeFrameVertices(shape,0,dv,dn,dt);
            var moved=Enumerable.Range(0,mesh.vertexCount).Where(i=>dv[i].sqrMagnitude>1e-16f).ToArray();
            var allowed=new HashSet<int>(moved);
            var triangles=mesh.triangles;
            // Normal updates touch adjacent faces; tangent updates touch their neighboring vertices.
            for(int ring=0;ring<2;ring++)
            {
                var next=new HashSet<int>(allowed);
                for(int i=0;i<triangles.Length;i+=3)
                    if(allowed.Contains(triangles[i])||allowed.Contains(triangles[i+1])||allowed.Contains(triangles[i+2]))
                    {next.Add(triangles[i]);next.Add(triangles[i+1]);next.Add(triangles[i+2]);}
                allowed=next;
            }
            int outside=0;
            for(int i=0;i<vertices.Length;i++)
                if(!allowed.Contains(i) && (dn[i].sqrMagnitude>1e-10f || dt[i].sqrMagnitude>1e-10f))outside++;
            if(outside!=0)throw new Exception(name+": shading deltas outside facial neighborhood="+outside);
            File.AppendAllText(Report,name+": shading changes outside facial region=0\n");
        }
    }
    static void ValidateBakedBody()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var baseline=new Mesh();var sample=new Mesh();
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),scene);
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
            var original=skin.sharedMesh;
            var dv=new Vector3[original.vertexCount];var dn=new Vector3[original.vertexCount];var dt=new Vector3[original.vertexCount];
            var affected=new HashSet<int>();
            foreach(string name in new[]{"Blink_L","Blink_R"})
            {
                original.GetBlendShapeFrameVertices(original.GetBlendShapeIndex(name),0,dv,dn,dt);
                for(int i=0;i<original.vertexCount;i++)
                    if(dv[i].sqrMagnitude>1e-16f || dn[i].sqrMagnitude>1e-10f || dt[i].sqrMagnitude>1e-10f)affected.Add(i);
            }
            skin.BakeMesh(baseline,true);var normals=baseline.normals;var tangents=baseline.tangents;
            foreach(float weight in new[]{12f,50f,100f})
            {
                skin.SetBlendShapeWeight(original.GetBlendShapeIndex("Blink_L"),weight);
                skin.SetBlendShapeWeight(original.GetBlendShapeIndex("Blink_R"),weight);
                skin.BakeMesh(sample,true);var ns=sample.normals;var ts=sample.tangents;
                int changed=0;
                for(int i=0;i<original.vertexCount;i++)
                    if(!affected.Contains(i)&&((ns[i]-normals[i]).sqrMagnitude>1e-10f || ((Vector3)ts[i]-(Vector3)tangents[i]).sqrMagnitude>1e-10f))changed++;
                if(changed!=0)throw new Exception("Baked non-eye shading changed: "+changed);
                File.AppendAllText(Report,"Baked blink weight="+weight+": non-eye normal/tangent changes=0\n");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(baseline);UnityEngine.Object.DestroyImmediate(sample);EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Hand Skin Validation/13 Diagnose Blink Normals")]
    public static void Diagnose()
    {
        var mesh=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Mesh>().First();
        var report=new StringBuilder();report.AppendLine("Vertices="+mesh.vertexCount);
        var positions=new Vector3[mesh.vertexCount];var normals=new Vector3[mesh.vertexCount];var tangents=new Vector3[mesh.vertexCount];
        for(int s=0;s<mesh.blendShapeCount;s++)
        {
            mesh.GetBlendShapeFrameVertices(s,0,positions,normals,tangents);
            int moved=0,unchangedNormals=0,unchangedTangents=0;
            float maxNormal=0,maxTangent=0;
            for(int i=0;i<mesh.vertexCount;i++)
            {
                if(positions[i].sqrMagnitude>1e-16f){moved++;continue;}
                if(normals[i].sqrMagnitude>1e-10f)unchangedNormals++;
                if(tangents[i].sqrMagnitude>1e-10f)unchangedTangents++;
                maxNormal=Mathf.Max(maxNormal,normals[i].magnitude);maxTangent=Mathf.Max(maxTangent,tangents[i].magnitude);
            }
            report.AppendLine(mesh.GetBlendShapeName(s)+": moved="+moved+", unchanged vertices with normal delta="+unchangedNormals+", tangent delta="+unchangedTangents+", maxNormal="+maxNormal+", maxTangent="+maxTangent);
        }
        File.WriteAllText(Report,report.ToString());Debug.Log(report.ToString());
    }
}
