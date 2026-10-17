using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FoxStatueAssetImport
{
    public const string Folder="Assets/Art/Models/Creatures/FoxStatue";
    public const string MaterialPath=Folder+"/FoxStatue_Material.mat";
    public const string ManifestPath=Folder+"/VAT/FoxStatue_Collapse_vat.json";
    public const string Stem=Folder+"/VAT/FoxStatue_Collapse";

    [MenuItem("工具/场景美术/狐狸雕像/配置源模型")]
    public static void ConfigureSource()
    {
        var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        var roots=scene.GetRootGameObjects().Select(o=>o.GetInstanceID()).ToArray();
        foreach(var item in new[]{("BaseColor.jpg",true,false,false),("Normal.jpg",false,true,false),
            ("Metallic.jpg",false,false,false),("Roughness.jpg",false,false,false),("MetallicSmoothness.png",false,false,true)})
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(TexturePath(item.Item1));
            t.textureType=item.Item3?TextureImporterType.NormalMap:TextureImporterType.Default;
            t.convertToNormalmap=false;t.sRGBTexture=item.Item2;
            t.alphaSource=item.Item4?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            t.alphaIsTransparency=false;t.mipmapEnabled=true;t.maxTextureSize=1024;
            t.npotScale=TextureImporterNPOTScale.None;t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Repeat;
            t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
        }
        var shader=Shader.Find(RigidVATAssetImport.ShaderName);
        if(shader==null)throw new InvalidOperationException("Statue shader missing.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null)
        {
            material=new Material(shader){name="FoxStatue_Material"};
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);
            material.SetFloat("_VATEnabled",0);material.SetFloat("_UseSurfaceMask",0);material.SetFloat("_CrackProgress",0);
            AssetDatabase.CreateAsset(material,MaterialPath);
        }
        material.SetTexture("_BaseMap",Load("BaseColor.jpg"));material.SetTexture("_BumpMap",Load("Normal.jpg"));
        material.SetTexture("_MetallicGlossMap",Load("MetallicSmoothness.png"));
        material.SetTexture("_CrackMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Models/Creatures/DragonGargoyle/Textures/DragonGargoyle_Cracks.png"));
        material.shaderKeywords=Array.Empty<string>();EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/FoxStatue.fbx");
        importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
        importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.None;
        importer.importCameras=false;importer.importLights=false;importer.meshCompression=ModelImporterMeshCompression.Off;
        importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;
        importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"FoxStatue_Material"),material);
        importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/FoxStatue.fbx");
        var meshes=model.GetComponentsInChildren<MeshFilter>();
        if(meshes.Length!=1 || meshes[0].sharedMesh.triangles.Length/3!=2976)
            throw new InvalidOperationException("Fox source topology changed.");
        if(scene!=SceneManager.GetActiveScene() || dirty!=scene.isDirty || !roots.SequenceEqual(scene.GetRootGameObjects().Select(o=>o.GetInstanceID())))
            throw new InvalidOperationException("Source configuration changed scene state.");
        Debug.Log("Fox source configured: preserved 2976 triangles, original textures and deferred triplanar crack material.");
    }

    static string TexturePath(string suffix)=>Folder+"/Textures/FoxStatue_"+suffix;
    static Texture2D Load(string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(suffix));

    [MenuItem("工具/场景美术/狐狸雕像/配置顶点动画")]
    public static void ConfigureVAT()=>RigidVATAssetImport.Configure(ManifestPath,MaterialPath);

    [MenuItem("工具/场景美术/狐狸雕像/验证 GPU 解码")]
    public static void ValidateGPU()=>GargoyleVATValidation.Validate(ManifestPath,"Library/FoxStatueValidation/GPU");
}
