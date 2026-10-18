using System;
using RetroRendering;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class CharacterShaderSetup
{
    const string Folder = "Assets/Art/Models/Characters/PokerCharacter";
    const string Output = "Art/HandSkinReview/Unity/Shader";
    const string ShaderName = "Rendering/Character Deferred Outline";

    static Texture2D Texture(string suffix, bool color, bool normal = false)
    {
        var path = Directory.GetFiles(Folder + "/Textures")
            .First(p => p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).Replace('\\', '/');
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture != color)
        { importer.textureType = type; importer.sRGBTexture = color; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    [MenuItem("Tools/Hand Skin Validation/6 Apply Character Deferred Shader")]
    public static void Apply()
    {
        var shader = Shader.Find(ShaderName);
        if (!shader) throw new Exception("Character shader not imported");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/PokerCharacter.mat");
        Undo.RecordObject(mat, "Apply character deferred outline shader");
        mat.shader = shader; mat.shaderKeywords = Array.Empty<string>();
        mat.SetTexture("_BaseMap", Texture("_BaseColor.jpg", true));
        mat.SetTexture("_BumpMap", Texture("_Normal.jpg", false, true));
        mat.SetTexture("_MetallicMap", Texture("_Metallic.jpg", false));
        mat.SetTexture("_RoughnessMap", Texture("_Roughness.jpg", false));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_BumpScale", 1); mat.SetFloat("_Metallic", 1); mat.SetFloat("_Roughness", 1);
        mat.SetFloat("_OcclusionStrength", 1); mat.SetFloat("_OutlineWidth", .0008f);
        mat.SetColor("_OutlineColor", new Color(.025f,.018f,.015f,.7f));
        InstallOutlineFeature();
        mat.renderQueue = -1;
        foreach (var pass in new[] { "GBuffer", "ShadowCaster", "DepthOnly", "DepthNormals", "Outline" })
        { if (mat.FindPass(pass) < 0) throw new Exception("Missing pass " + pass); mat.SetShaderPassEnabled(pass, true); }
        EditorUtility.SetDirty(mat);
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (!renderer.sharedMesh || !renderer.sharedMesh.name.Contains("tripo_node_dc21eaf7")) continue;
            int oldLayer = renderer.gameObject.layer;
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>().Where(c => c.gameObject.scene == renderer.gameObject.scene))
                if ((camera.cullingMask & (1 << oldLayer)) != 0) { Undo.RecordObject(camera,"Keep character visible"); camera.cullingMask |= 1 << CharacterLayer(); }
            foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>().Where(l => l.gameObject.scene == renderer.gameObject.scene))
                if ((light.cullingMask & (1 << oldLayer)) != 0) { Undo.RecordObject(light,"Keep character lit"); light.cullingMask |= 1 << CharacterLayer(); }
            foreach (var t in renderer.transform.root.GetComponentsInChildren<Transform>(true))
            { Undo.RecordObject(t.gameObject,"Set character outline layer"); t.gameObject.layer = CharacterLayer(); PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject); }
            Undo.RecordObject(renderer, "Assign character deferred material");
            renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => mat).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var prefabPath = Folder + "/Models/PokerCharacter_Visual.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CharacterLayer();
            PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Debug.Log("Character Deferred Outline assigned: sRGB base color; tangent normal; linear metallic/roughness.");
        Validate();
    }

    static int CharacterLayer()
    {
        int existing = LayerMask.NameToLayer("Characters");
        if (existing >= 0) return existing;
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        for (int i=8;i<layers.arraySize;i++)
        {
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
            layers.GetArrayElementAtIndex(i).stringValue = "Characters";
            tags.ApplyModifiedProperties(); return i;
        }
        throw new Exception("No free layer for Characters");
    }

    static void InstallOutlineFeature()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
        var feature = data.rendererFeatures.OfType<DepthNormalOutlineFeature>().FirstOrDefault();
        if (!feature)
        {
            feature = ScriptableObject.CreateInstance<DepthNormalOutlineFeature>();
            feature.name = "Character Depth Normal Outline";
            AssetDatabase.AddObjectToAsset(feature, data);
            int index = data.rendererFeatures.FindIndex(f => f is BloomFeature || f is RetroPostProcessFeature);
            data.rendererFeatures.Insert(index < 0 ? data.rendererFeatures.Count : index, feature);
        }
        feature.OutlineShader = Shader.Find("Hidden/RetroRendering/DepthNormalOutline");
        feature.name = "Depth Normal Outline";
        feature.settings.excludedLayers |= 1 << CharacterLayer();
        feature.SetActive(true); feature.Create();
        EditorUtility.SetDirty(feature); data.SetDirty(); EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        var serialized = new SerializedObject(data);
        var map = serialized.FindProperty("m_RendererFeatureMap");
        map.arraySize = data.rendererFeatures.Count;
        for (int i=0; i<data.rendererFeatures.Count; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string guid, out long id);
            map.GetArrayElementAtIndex(i).longValue = id;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Hand Skin Validation/7 Validate Character Shader")]
    public static void Validate()
    {
        Directory.CreateDirectory(Output);
        var shader = Shader.Find(ShaderName);
        var source = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/PokerCharacter.mat");
        var material = new Material(source);
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
        var feature = rendererData.rendererFeatures.OfType<DepthNormalOutlineFeature>().Single();
        bool savedActive = feature.isActive;
        float savedWidth = feature.settings.width;
        int savedExcluded = feature.settings.excludedLayers;
        var scene = EditorSceneManager.NewPreviewScene();
        Camera camera = null;
        try
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/PokerCharacter_Visual.prefab"));
            SceneManager.MoveGameObjectToScene(model, scene);
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            { r.sharedMaterial = material; r.updateWhenOffscreen = true; }
            var bounds = model.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
            var light = new GameObject("Shader validation light").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << 30;
            light.transform.rotation = Quaternion.Euler(35,150,0); light.shadows = LightShadows.Soft;
            camera = new GameObject("Main pipeline validation camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene); camera.scene = scene;
            camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f,.25f,.29f); camera.allowHDR = true;
            camera.nearClipPlane = .01f; camera.farClipPlane = 20;
            camera.orthographic = true; camera.orthographicSize = bounds.extents.y * 1.22f;
            camera.transform.position = bounds.center + new Vector3(0,.05f,3);
            camera.transform.LookAt(bounds.center);
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(0); data.renderPostProcessing = true;
            var otherFeatures = rendererData.rendererFeatures.Where(f => f != feature).ToArray();
            var activeStates = otherFeatures.Select(f => f.isActive).ToArray();
            int geometryPixels=0, excludedPixels=0, includedPixels=0, litPixels=0, litExcludedPixels=0;
            try
            {
                feature.SetActive(true);
                foreach (var f in otherFeatures) f.SetActive(false);
                feature.settings.width = 0; material.SetFloat("_OutlineWidth",0);
                var baseline = Capture(camera,Output+"/outline_off.png");
                feature.settings.width = 1; feature.settings.excludedLayers = 1 << 30;
                var excluded = Capture(camera,Output+"/layer_excluded.png");
                excludedPixels = Difference(baseline,excluded);
                feature.settings.excludedLayers = 0;
                includedPixels = Difference(baseline,Capture(camera,Output+"/layer_included.png"));
                feature.settings.excludedLayers = 1 << 30;
                material.SetFloat("_OutlineWidth",.0008f);
                geometryPixels = Difference(baseline,Capture(camera,Output+"/deferred_clean.png"));
                // Verify ordinary URP Lit participates without any special mask pass.
                model.SetActive(false);
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(cube,scene); cube.layer=30;
                cube.transform.position=bounds.center; cube.transform.localScale=Vector3.one*.35f;
                var lit=new Material(Shader.Find("Universal Render Pipeline/Lit")); cube.GetComponent<Renderer>().sharedMaterial=lit;
                try
                {
                    feature.settings.width=0;
                    var litBaseline=Capture(camera,Output+"/lit_off.png");
                    feature.settings.width=1; feature.settings.excludedLayers=0;
                    litPixels=Difference(litBaseline,Capture(camera,Output+"/lit_included.png"));
                    feature.settings.excludedLayers=1<<30;
                    litExcludedPixels=Difference(litBaseline,Capture(camera,Output+"/lit_excluded.png"));
                }
                finally { UnityEngine.Object.DestroyImmediate(cube); UnityEngine.Object.DestroyImmediate(lit); model.SetActive(true); }
                if (excludedPixels!=0 || litExcludedPixels!=0 || includedPixels==0 || litPixels==0 || geometryPixels==0)
                    throw new Exception($"Outline checks failed: geometry={geometryPixels}, included={includedPixels}, excluded={excludedPixels}, lit={litPixels}, litExcluded={litExcludedPixels}");
            }
            finally { for(int i=0;i<otherFeatures.Length;i++) otherFeatures[i].SetActive(activeStates[i]); }
            Capture(camera,Output+"/outline_on.png");
            var messages = ShaderUtil.GetShaderMessages(shader).Concat(ShaderUtil.GetShaderMessages(Shader.Find("Hidden/RetroRendering/DepthNormalOutline"))).ToArray();
            if (messages.Any(m => m.severity.ToString() == "Error")) throw new Exception(string.Join("\n",messages.Select(m => m.message)));
            var renderer = data.scriptableRenderer;
            var mode = renderer.GetType().GetProperty("renderingModeActual",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.GetValue(renderer);
            File.WriteAllText(Output + "/validation.txt", "Shader=" + shader.name + "\nRenderer=" + renderer.GetType().Name +
                "\nActual rendering mode=" + mode + "\nPass count=" + material.passCount +
                "\nDepth-normal mask size=" + feature.LastMaskSize + "\nGeometry outline changed pixels=" + geometryPixels +
                "\nScreen outline included pixels=" + includedPixels + "\nExcluded layer changed pixels=" + excludedPixels +
                "\nStandard Lit included pixels=" + litPixels + "\nStandard Lit excluded pixels=" + litExcludedPixels +
                "\nShader errors=0\n" + string.Join("\n",messages.Select(m=>m.message)));
            Debug.Log("Character world-space outline and layer exclusion validated.");
        }
        finally { feature.SetActive(savedActive); feature.settings.excludedLayers = savedExcluded; feature.settings.width = savedWidth; EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(material); }
    }

    static int Difference(Color32[] a, Color32[] b) => Enumerable.Range(0,a.Length).Count(i=>!a[i].Equals(b[i]));

    static Color32[] Capture(Camera camera, string path)
    {
        var rt = RenderTexture.GetTemporary(960,960,24,RenderTextureFormat.ARGBHalf);
        var old = RenderTexture.active;
        var image = new Texture2D(960,960,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,960,960),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG()); return image.GetPixels32();
        }
        finally { camera.targetTexture=null; RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
}
