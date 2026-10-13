using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetroRendering;

// Import is deliberately separate from applying the prepared mesh to the scene.
public static class GargoyleVATImport
{
    public const string Folder = "Assets/Art/Models/Creatures/DragonGargoyle/VAT";
    public const string Stem = Folder + "/DragonGargoyle_Collapse";
    public const string MeshPath = Stem + "_Unity.asset";
    public const string SurfacePath = Stem + "_Surface.mat";
    const string LegacyExteriorPath = Stem + "_Exterior.mat";
    const string LegacyInteriorPath = Stem + "_Interior.mat";
    public const string OriginalMaterial = "Assets/Art/Models/Creatures/DragonGargoyle/DragonGargoyle_Material.mat";
    public const string ShaderName = "Rendering/Gargoyle Cracks Deferred";
    public static JObject Metadata => JObject.Parse(File.ReadAllText(Stem + "_vat.json"));
    public static Vector3 Vector(JToken a) => new Vector3((float)a[0], (float)a[1], (float)a[2]);
    public static Vector3 ToUnity(Vector3 v) => new Vector3(v.x, v.z, v.y);

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Configure Assets")]
    public static void Configure()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(SurfacePath)==null && AssetDatabase.LoadAssetAtPath<Material>(LegacyExteriorPath)!=null)
        {
            string error=AssetDatabase.MoveAsset(LegacyExteriorPath,SurfacePath);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }
        RigidVATAssetImport.Configure(Stem+"_vat.json",OriginalMaterial);
        var legacy=AssetDatabase.LoadAssetAtPath<Material>(LegacyInteriorPath);
        if (legacy!=null)
        {
            var surface=AssetDatabase.LoadAssetAtPath<Material>(SurfacePath);
            surface.SetColor("_InteriorColor",legacy.GetColor("_BaseColor"));
            surface.SetFloat("_InteriorMetallic",legacy.GetFloat("_Metallic"));
            surface.SetFloat("_InteriorSmoothness",legacy.GetFloat("_Smoothness"));
            EditorUtility.SetDirty(surface);AssetDatabase.SaveAssetIfDirty(surface);
        }
    }

    public static void Bind(Material mat,JObject data) => RigidVATAssetImport.Bind(mat,data,Stem);
    public static void ValidateChannels(Mesh mesh,JObject data) => RigidVATAssetImport.ValidateChannels(mesh,data);

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Apply To Current Scene")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply in edit mode.");
        var targets = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true).Where(x => x.name == "DragonGargoyle" && x.gameObject.scene.IsValid()).ToArray();
        if (targets.Length != 1) throw new InvalidOperationException("Expected exactly one DragonGargoyle in the loaded scenes.");
        Configure(); GargoyleVATValidation.Validate();
        var mf = targets[0]; var renderer = mf.GetComponent<MeshRenderer>();
        if (renderer == null) throw new InvalidOperationException("Statue MeshRenderer is missing.");
        Undo.RecordObjects(new UnityEngine.Object[] {mf,renderer}, "Replace Gargoyle with VAT bind mesh");
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        renderer.sharedMaterials = new[] {AssetDatabase.LoadAssetAtPath<Material>(SurfacePath)};
        renderer.ResetLocalBounds();
        var player = mf.GetComponent<RigidVATPlayer>();
        if (player == null) player = Undo.AddComponent<RigidVATPlayer>(mf.gameObject);
        Undo.RecordObject(player, "Configure Gargoyle Collapse");
        player.Configure(((int)Metadata["frame_count"] - 1) / (float)Metadata["fps"]);
        player.ResetPose();
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject);
        if (prefabRoot != null)
        {
            var sourceRenderer = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            var mods = PrefabUtility.GetPropertyModifications(prefabRoot);
            if (mods != null)
                PrefabUtility.SetPropertyModifications(prefabRoot,mods.Where(m => !(m.target==sourceRenderer &&
                    m.propertyPath.StartsWith("m_Materials.Array.data[") && m.propertyPath!="m_Materials.Array.data[0]")).ToArray());
        }
        EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);
        if (!EditorSceneManager.SaveScene(mf.gameObject.scene)) throw new IOException("Could not save the updated scene.");
        // Remove the old generated cap material only when no saved asset uses it.
        if (AssetDatabase.LoadAssetAtPath<Material>(LegacyInteriorPath) != null &&
            !AssetDatabase.GetAllAssetPaths().Where(p => p != LegacyInteriorPath &&
                new[]{".unity",".prefab",".asset",".fbx",".mat"}.Contains(Path.GetExtension(p)))
                .Any(p=>AssetDatabase.GetDependencies(p,false).Contains(LegacyInteriorPath)))
            AssetDatabase.DeleteAsset(LegacyInteriorPath);
        Selection.activeGameObject = mf.gameObject; SceneView.RepaintAll();
        Debug.Log("DragonGargoyle scene mesh replaced and saved; existing transform/components retained.");
    }

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Preview/Rest")]
    static void Rest() => Preview(0);
    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Preview/Mid Collapse")]
    static void Middle() => Preview(50.25f/109);
    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Preview/Collapsed")]
    static void End() => Preview(1);
    public static void Preview(float progress)
    {
        foreach (string path in new[] {SurfacePath})
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) throw new InvalidOperationException("Configure the VAT assets first.");
            Undo.RecordObject(mat,"Preview Gargoyle Collapse"); mat.SetFloat("_VATProgress",Mathf.Clamp01(progress));
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
        }
        foreach (var player in UnityEngine.Object.FindObjectsOfType<RigidVATPlayer>(true))
            if (player.name == "DragonGargoyle")
            {
                Undo.RecordObject(player, "Preview Gargoyle Collapse"); player.SetProgress(progress);
                EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            }
        SceneView.RepaintAll();
    }
}
