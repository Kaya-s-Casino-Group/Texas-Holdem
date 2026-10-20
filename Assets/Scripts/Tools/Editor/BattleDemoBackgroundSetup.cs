#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleDemoBackgroundSetup
{
    private const string Art = "Assets/Art/Environment/BattleBackgrounds";
    private const string ScenePath = "Assets/Scenes/BattleDemo.unity";

    [MenuItem("Tools/BattleDemo/Backgrounds/Set Up Background Choices")]
    public static void SetUp()
    {
        RequireScene();
        if (Application.isPlaying) throw new InvalidOperationException("Configure backgrounds in Edit mode.");
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Battle Backgrounds") ??
            new GameObject("Battle Backgrounds");
        var elite = Child(root.transform, "Elite - Existing Ruins");
        foreach (var item in scene.GetRootGameObjects().Where(go => go.name.StartsWith("StylizedRuins ", StringComparison.Ordinal)))
            item.transform.SetParent(elite.transform, true);
        var camera = Camera.main;
        var minion = Build(root.transform, "Minion - Red Wastes", "Minion", camera, .43f);
        var boss = Build(root.transform, "Boss - Castle Gate", "Boss", camera, .19f);
        var switcher = Component<BattleBackgroundSwitcher>(root);
        var hud = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<SingleBattleHud>(true)).Single();
        var controls = hud.transform.Find("Background Choices");
        if (controls == null)
            controls = new GameObject("Background Choices", typeof(RectTransform), typeof(Image)).transform;
        controls.SetParent(hud.transform, false);
        var rect = (RectTransform)controls;
        rect.anchorMin = new Vector2(.31f, .943f); rect.anchorMax = new Vector2(.56f, .993f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        controls.GetComponent<Image>().color = new Color32(24, 19, 27, 225);
        controls.GetComponent<Image>().raycastTarget = false;
        var eliteButton = Choice(controls, "Elite", "精英", 0);
        var minionButton = Choice(controls, "Minion", "小怪", 1);
        var bossButton = Choice(controls, "Boss", "Boss", 2);
        switcher.Configure(elite, minion, boss, eliteButton, minionButton, bossButton);
        switcher.SelectElite();
        EditorUtility.SetDirty(switcher);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BattleDemo: elite, minion and boss backgrounds configured in one scene; default = elite.");
    }

    private static GameObject Build(Transform parent, string name, string prefix, Camera camera, float floorTop)
    {
        var root = Child(parent, name);
        var shader = Shader.Find("StylizedRuins/Backdrop");
        if (shader == null) throw new InvalidOperationException("Missing painted backdrop shader.");
        Directory.CreateDirectory(Art + "/Materials");
        AssetDatabase.Refresh();
        var back = Layer(root.transform, "Rear", Material(prefix, "Back", shader, 2800));
        var mid = Layer(root.transform, "Middle", Material(prefix, "Mid", shader, 2820));
        var front = Layer(root.transform, "Foreground", Material(prefix, "Front", shader, 2830));
        var floorMaterial = Material(prefix, "Back", shader, 2810, "Floor");
        floorMaterial.SetFloat("_ProjectiveUV", 1);
        floorMaterial.SetFloat("_TopFadeStart", floorTop - .025f);
        floorMaterial.SetFloat("_TopFadeEnd", floorTop);
        EditorUtility.SetDirty(floorMaterial);
        var ground = Child(root.transform, "Floor - Camera Projection");
        var filter = Component<MeshFilter>(ground);
        var renderer = Component<MeshRenderer>(ground);
        renderer.sharedMaterial = floorMaterial; NoLighting(renderer);
        var backdrop = Component<PaintedBattleBackdrop>(root);
        backdrop.Configure(camera, back, mid, front, filter, floorTop);
        EditorUtility.SetDirty(backdrop);
        return root;
    }

    private static Material Material(string prefix, string layer, Shader shader, int queue, string materialName = null)
    {
        string texturePath = Art + "/Textures/" + prefix + "_" + layer + ".png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null) throw new InvalidOperationException("Missing layer: " + texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = layer != "Back"; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        string path = Art + "/Materials/" + prefix + "_" + (materialName ?? layer) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Exposure", 1);
        material.SetFloat("_PaintContrast", 1); material.SetFloat("_ShadowLift", 0);
        material.SetFloat("_ZWrite", 0); material.SetFloat("_ProjectiveUV", 0);
        material.SetFloat("_TopFadeStart", 1); material.SetFloat("_TopFadeEnd", 1.1f);
        material.renderQueue = queue;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform Layer(Transform parent, string name, Material material)
    {
        var go = parent.Find(name)?.gameObject;
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
        }
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material; NoLighting(renderer);
        return go.transform;
    }
    private static void NoLighting(MeshRenderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
    private static GameObject Child(Transform parent, string name)
    {
        var result = parent.Find(name)?.gameObject ?? new GameObject(name);
        result.transform.SetParent(parent, false); return result;
    }
    private static T Component<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
    private static Button Choice(Transform parent, string name, string caption, int index)
    {
        var existing = parent.Find(name);
        var root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(index / 3f, 0); rect.anchorMax = new Vector2((index + 1) / 3f, 1);
        rect.offsetMin = new Vector2(3, 3); rect.offsetMax = new Vector2(-3, -3);
        var label = root.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null)
            label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(root.transform, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset");
        label.text = caption; label.fontSize = 13; label.color = new Color32(239, 225, 199, 255);
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        var button = root.GetComponent<Button>(); button.targetGraphic = root.GetComponent<Image>();
        var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
        return button;
    }

    [MenuItem("Tools/BattleDemo/Backgrounds/Elite")]
    public static void Elite() => Select(BattleBackground.Elite);
    [MenuItem("Tools/BattleDemo/Backgrounds/Minion")]
    public static void Minion() => Select(BattleBackground.Minion);
    [MenuItem("Tools/BattleDemo/Backgrounds/Boss")]
    public static void Boss() => Select(BattleBackground.Boss);
    private static void Select(BattleBackground background)
    {
        RequireScene();
        var switcher = UnityEngine.Object.FindObjectOfType<BattleBackgroundSwitcher>();
        if (switcher == null) throw new InvalidOperationException("Set up background choices first.");
        Undo.RecordObject(switcher, "Switch BattleDemo background");
        switcher.Select(background);
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/BattleDemo/Backgrounds/Validate Runtime Switching")]
    public static void ValidateSwitching()
    {
        RequireScene();
        if (!Application.isPlaying) throw new InvalidOperationException("Run BattleDemo first.");
        var host = UnityEngine.Object.FindObjectOfType<SingleBattleSceneAdapter>();
        var switcher = UnityEngine.Object.FindObjectOfType<BattleBackgroundSwitcher>();
        var original = switcher.Current;
        var session = host.Session; var state = host.State;
        var selection = host.Actions.Hand.CaptureSelection().Cards.ToArray();
        try
        {
            foreach (BattleBackground background in Enum.GetValues(typeof(BattleBackground)))
            {
                switcher.Select(background);
                if (!ReferenceEquals(host.Session, session) || !ReferenceEquals(host.State, state) ||
                    !selection.SequenceEqual(host.Actions.Hand.CaptureSelection().Cards))
                    throw new InvalidOperationException("Changing backgrounds altered the active battle.");
            }
        }
        finally { switcher.Select(original); }
        Debug.Log("Background switching passed: all three environments preserve battle session, state and selection.");
    }

    [MenuItem("Tools/BattleDemo/Backgrounds/Capture Background Plates")]
    public static void CapturePlates()
    {
        RequireScene();
        var switcher = UnityEngine.Object.FindObjectOfType<BattleBackgroundSwitcher>();
        var camera = Camera.main;
        var selected = switcher.Current;
        float aspect = camera.aspect;
        var hidden = UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r =>
            !r.transform.IsChildOf(switcher.transform) && r.enabled).ToArray();
        var capture = new GameObject("Background Capture", typeof(Camera)).GetComponent<Camera>();
        capture.CopyFrom(camera); capture.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        capture.enabled = false;
        var target = new RenderTexture(2442, 1080, 24);
        var previousTarget = RenderTexture.active;
        var image = new Texture2D(2442, 1080, TextureFormat.RGB24, false);
        Directory.CreateDirectory("Library/BattleBackgrounds");
        try
        {
            foreach (var renderer in hidden) renderer.enabled = false;
            camera.aspect = capture.aspect = 2442f / 1080;
            capture.targetTexture = target;
            foreach (BattleBackground background in Enum.GetValues(typeof(BattleBackground)))
            {
                switcher.Select(background);
                foreach (var backdrop in switcher.GetComponentsInChildren<PaintedBattleBackdrop>()) backdrop.Refresh(true);
                capture.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 2442, 1080), 0, 0); image.Apply();
                File.WriteAllBytes("Library/BattleBackgrounds/" + background + ".png", image.EncodeToPNG());
            }
        }
        finally
        {
            foreach (var renderer in hidden) renderer.enabled = true;
            camera.aspect = aspect; switcher.Select(selected);
            foreach (var backdrop in switcher.GetComponentsInChildren<PaintedBattleBackdrop>()) backdrop.Refresh(true);
            RenderTexture.active = previousTarget; capture.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture.gameObject);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    private static void RequireScene()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open BattleDemo first.");
    }
}
#endif
