using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Configures only this imported asset folder, never the user's scene.
public static class DragonGargoyleAssetImport
{
    const string Folder = "Assets/Art/Models/Creatures/DragonGargoyle";
    const string ModelPath = Folder + "/DragonGargoyle.fbx";
    const string MaterialPath = Folder + "/DragonGargoyle_Material.mat";
    const string Output = "Blender/DragonGargoyle";

    [MenuItem("Tools/Art Assets/Configure Dragon Gargoyle")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects().Select(o => o.GetInstanceID()).ToArray();
        bool wasDirty = scene.isDirty;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Texture("BaseColor.jpg", true, false, false);
        Texture("Normal.jpg", false, true, false);
        Texture("MetallicRoughness.png", false, false, false);
        Texture("MetallicSmoothness.png", false, false, true);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Project URP Lit shader is missing.");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) {name = "DragonGargoyle_Material"};
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BaseMap", Load("BaseColor.jpg"));
        mat.SetTexture("_BumpMap", Load("Normal.jpg"));
        mat.SetFloat("_BumpScale", 1);
        mat.SetTexture("_MetallicGlossMap", Load("MetallicSmoothness.png"));
        mat.SetFloat("_Metallic", 1);
        mat.SetFloat("_Smoothness", 1);
        mat.SetFloat("_SmoothnessTextureChannel", 0);
        mat.SetFloat("_WorkflowMode", 1);
        mat.SetFloat("_Surface", 0);
        mat.SetFloat("_Cull", (float)CullMode.Off); // Preserve glTF doubleSided=true.
        mat.doubleSidedGI = true;
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        mat.DisableKeyword("_SPECULAR_SETUP");
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);

        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("FBX ModelImporter was not created.");
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.Import;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "DragonGargoyle_Material"), mat);
        importer.SaveAndReimport();

        Validate();
        if (SceneManager.GetActiveScene().handle != scene.handle || scene.isDirty != wasDirty ||
            !scene.GetRootGameObjects().Select(o => o.GetInstanceID()).SequenceEqual(roots))
            throw new InvalidOperationException("Scene state changed during asset import; inspect before continuing.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Selection.activeObject = model;
        EditorGUIUtility.PingObject(model);
        Debug.Log("Dragon Gargoyle imported and validated; current scene was not modified.");
    }

    static string TexturePath(string suffix) => Folder + "/Textures/DragonGargoyle_" + suffix;
    static Texture2D Load(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(suffix));

    static void Texture(string suffix, bool srgb, bool normal, bool alpha)
    {
        var importer = AssetImporter.GetAtPath(TexturePath(suffix)) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture: " + suffix);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.convertToNormalmap = false;
        importer.sRGBTexture = srgb;
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 4;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    [MenuItem("Tools/Art Assets/Validate Dragon Gargoyle")]
    public static void Validate()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (model == null || mat == null || mat.shader.name != "Universal Render Pipeline/Lit")
            throw new InvalidOperationException("Model or URP material is missing.");
        var filters = model.GetComponentsInChildren<MeshFilter>(true);
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (filters.Length != 1 || renderers.Length != 1 || renderers[0].sharedMaterials.Any(m => m != mat))
            throw new InvalidOperationException("Expected one mesh using the external URP material.");
        var mesh = filters[0].sharedMesh;
        int triangles = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i) / 3);
        if (triangles != 2807 || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) ||
            !mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.Tangent))
            throw new InvalidOperationException("Mesh topology, UVs, normals or tangents failed validation.");
        if (model.transform.localScale != Vector3.one || Quaternion.Angle(model.transform.localRotation, Quaternion.identity) > .01f)
            throw new InvalidOperationException("Unexpected imported model root transform.");
        var transform = model.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;
        var b = mesh.bounds;
        var bounds = new Bounds(transform.MultiplyPoint3x4(b.min), Vector3.zero);
        for (int i = 0; i < 8; ++i)
            bounds.Encapsulate(transform.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
        if ((bounds.size - new Vector3(.701172f,.998047f,.634766f)).sqrMagnitude > .00001f || Mathf.Abs(bounds.min.y) > .0001f)
            throw new InvalidOperationException("Scale, axis conversion or ground pivot failed: " + bounds);
        var report = new StringBuilder();
        report.AppendLine("Model: " + ModelPath);
        report.AppendLine("Triangles: " + triangles + "; vertices: " + mesh.vertexCount);
        report.AppendLine("Unity dimensions (m): " + bounds.size.ToString("F6"));
        report.AppendLine("Minimum Y: " + bounds.min.y + "; root scale 1; root rotation identity");
        report.AppendLine("Material: URP/Lit; glTF double-sided preserved; all renderer slots linked");
        foreach (var tuple in new[]{("BaseColor.jpg", "_BaseMap"), ("Normal.jpg", "_BumpMap"), ("MetallicSmoothness.png", "_MetallicGlossMap")})
        {
            var texture = Load(tuple.Item1);
            if (texture == null || texture.width != 1024 || texture.height != 1024 || mat.GetTexture(tuple.Item2) != texture)
                throw new InvalidOperationException("Texture binding failed: " + tuple.Item1);
            var settings = (TextureImporter)AssetImporter.GetAtPath(TexturePath(tuple.Item1));
            report.AppendLine(tuple.Item1 + ": 1024x1024; " + settings.textureType + "; sRGB=" + settings.sRGBTexture);
        }
        report.AppendLine("MetallicSmoothness: R=source B, A=1-source G; linear sampling; smoothness multiplier=1");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/unity_import_report.txt", report.ToString());
        Debug.Log(report.ToString());
    }

}
