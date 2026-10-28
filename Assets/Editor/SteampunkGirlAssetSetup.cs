using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class SteampunkGirlAssetSetup
{
    const string Root = "Assets/Art/Characters/SteampunkGirl";
    static readonly string[] Parts = { "Body", "Hair", "Head", "Legs", "Skirt" };

    [MenuItem("Tools/Characters/Setup SteampunkGirl Assets")]
    static void Setup()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Meshes");
        AssetDatabase.Refresh();
        var shader = Shader.Find("Rendering/Character Deferred Packed MR");
        if (!shader) throw new InvalidOperationException("Palamedes Packed MR shader missing");
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Palamedes/M_Palamedes_Body.mat");
        var materials = Parts.ToDictionary(p => p, p =>
        {
            string prefix = Root + "/Textures/T_SteampunkGirl_" + p;
            var color = Texture(prefix + "_BaseColor.jpg", true, false);
            var mr = Texture(prefix + "_MR.png", false, false);
            var normal = Texture(prefix + "_Normal.jpg", false, true, false);
            var path = Root + "/Materials/M_SteampunkGirl_" + p + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(template); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.name = "M_SteampunkGirl_" + p;
            mat.SetTexture("_BaseMap", color);
            mat.SetTexture("_PackedMRMap", mr);
            mat.SetTexture("_BumpMap", normal);
            mat.SetTexture("_OcclusionMap", null);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 1);
            mat.SetFloat("_Roughness", 1);
            mat.SetFloat("_BumpScale", 1);
            ApplyFill(mat, p);
            EditorUtility.SetDirty(mat);
            return mat;
        });
        string modelPath = Root + "/Models/SteampunkGirl.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var p in Parts)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_SteampunkGirl_" + p), materials[p]);
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = "SteampunkGirl";
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 5) throw new InvalidOperationException("Expected 5 skinned meshes, got " + renderers.Length);
            foreach (var renderer in renderers)
            {
                string part = Parts.Single(p => renderer.name == "SM_SteampunkGirl_" + p);
                var meshPath = Root + "/Meshes/Mesh_SteampunkGirl_" + part + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (!mesh)
                {
                    mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
                    mesh.name = "Mesh_SteampunkGirl_" + part;
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                else EditorUtility.CopySerialized(renderer.sharedMesh, mesh);
                mesh.name = "Mesh_SteampunkGirl_" + part;
                EditorUtility.SetDirty(mesh);
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = materials[part];
                if (mesh.bindposes.Length == 0 || renderer.bones.Length == 0 || mesh.uv.Length != mesh.vertexCount)
                    throw new InvalidOperationException(part + " skin/UV data missing");
            }
            PrefabUtility.SaveAsPrefabAsset(instance, Root + "/SteampunkGirl.prefab");
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/SteampunkGirl.prefab");
            Debug.Log("SteampunkGirl import verified: 5 skinned meshes, 5 Packed MR materials, preserved UVs and bindposes. Prefab: " + Root + "/SteampunkGirl.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    static Texture2D Texture(string path, bool srgb, bool normal, bool required = true)
    {
        if (!File.Exists(path))
        {
            if (required) throw new FileNotFoundException(path);
            return null;
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.maxTextureSize = path.Contains("_Body_") ? 4096 : 2048;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void ApplyFill(Material material, string part)
    {
        material.SetColor("_CharacterFillColor", new Color(1f, .95f, .88f, 1f));
        material.SetFloat("_CharacterFillWrap", .5f);
        material.SetFloat("_CharacterFillStrength", part == "Head" ? .13f : part == "Legs" ? .055f : .085f);
        material.SetFloat("_CharacterAmbientFloor", part == "Head" ? .035f : .02f);
    }

    [MenuItem("Tools/Characters/Apply SteampunkGirl Shader Fill")]
    static void SetupFill()
    {
        string shaderPath = "Assets/GameAssets/Rendering/Shaders/CharacterPackedMR.shader";
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Rendering/Character Deferred Packed MR");
        if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Character fill shader failed compilation");
        foreach (string part in Parts)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_SteampunkGirl_" + part + ".mat");
            if (!mat || !mat.HasProperty("_CharacterFillStrength")) throw new InvalidOperationException(part + " fill properties missing");
            ApplyFill(mat, part);
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Character shader fill verified: no shader compile errors; five SteampunkGirl materials configured. Existing materials default to zero fill.");
    }
}
