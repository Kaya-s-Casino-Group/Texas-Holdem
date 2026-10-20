#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authored layout matching the supplied 1377 × 757 battle composition.</summary>
public static class BattleDemoReferenceLayout
{
    private const string ArtFolder = "Assets/Art/Characters/BattleBillboards/";
    private static readonly Color PanelColor = new Color32(31, 26, 39, 232);
    private static readonly Color TextColor = new Color32(235, 224, 205, 255);
    private static T[] All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Tools/BattleDemo/Apply Reference Layout")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Apply the layout in Edit mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/BattleDemo.unity") throw new InvalidOperationException("Open BattleDemo first.");
        var host = All<SingleBattleSceneAdapter>().Single();
        var camera = Camera.main;
        ApplyHud(All<SingleBattleHud>().Single(), host);
        Billboard(host.Player, "Player", camera, new Rect(-.012f, -.03f, .36f, .84f), 2.8f, .035f);
        Billboard(host.Enemy, "Enemy", camera, new Rect(.30f, -.12f, .63f, 1.18f), 3.2f, -.035f);
        var hand = host.Actions.Hand.GetComponent<HandZoneView>();
        var layout = hand.GetComponent<BattleDemoViewportLayout>();
        var backdrop = GameObject.Find("Battle Hand Backdrop");
        if (backdrop == null) backdrop = Quad("Battle Hand Backdrop");
        backdrop.GetComponent<MeshRenderer>().sharedMaterial = Material("HandBackdrop", null, new Color(.075f, .055f, .09f, .92f));
        layout.Configure(camera, hand);
        layout.ConfigureBackdrop(backdrop.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("BattleDemo reference layout saved: left portrait, enemy plane, corner HUD and bottom card strip.");
    }

    public static void ApplyHud(SingleBattleHud hud, SingleBattleSceneAdapter host)
    {
        var root = hud.transform;
        foreach (var scaler in All<CanvasScaler>())
        {
            if (scaler.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace) continue;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1377, 757);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
        }
        hud.GetComponent<Canvas>().sortingOrder = 30;
        var turn = root.Find("Turn");
        Region(turn, .006f, .908f, .258f, .082f);
        Label(turn.Find("Round"), 12, -8, 140, 48, 15);
        var energy = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Energy");
        energy.transform.SetParent(turn, false);
        Label(energy.transform, 158, -8, 140, 48, 14);
        var restart = hud.RestartButton;
        restart.transform.SetParent(turn, false);
        Region(restart.transform, .86f, .22f, .12f, .56f);
        restart.GetComponentInChildren<TMP_Text>().text = "重开";
        StyleButton(restart, 12);

        var player = root.Find("Player");
        Region(player, .006f, .844f, .185f, .041f);
        Label(player.Find("Health"), 7, -2, 244, 21, 13);
        Fixed(player.Find("Health Bar"), 6, -24, 242, 4);
        Region(root.Find("Enemy"), .70f, .635f, .18f, .086f);
        root.Find("Enemy").localRotation = Quaternion.Euler(0, 0, 18);
        Label(root.Find("Enemy/Health"), 12, -6, 226, 23, 17);
        Fixed(root.Find("Enemy/Health Bar"), 12, -33, 222, 5);
        var intent = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Intent");
        intent.transform.SetParent(root.Find("Enemy"), false);
        Label(intent.transform, 12, -42, 222, 18, 11);

        var info = root.Find("Battle Info");
        Region(info, .58f, .212f, .25f, .05f, false);
        info.GetComponent<Image>().color = Color.clear;
        info.Find("Message").gameObject.SetActive(false);
        Label(info.Find("Counts"), 0, 0, 340, 19, 11);
        Label(info.Find("Score"), 0, -19, 340, 18, 11);
        info.Find("Score").GetComponent<TMP_Text>().enableWordWrapping = false;

        var candidate = root.Find("Hand Candidate");
        Region(candidate, .30f, .212f, .28f, .05f, false);
        candidate.GetComponent<Image>().color = Color.clear;
        candidate.Find("Heading").gameObject.SetActive(false);
        candidate.Find("Chips Caption").gameObject.SetActive(false);
        candidate.Find("Multiplier Caption").gameObject.SetActive(false);
        Label(candidate.Find("Hand"), 0, 0, 132, 23, 19);
        Label(candidate.Find("Details"), 0, -24, 138, 14, 10);
        Fixed(candidate.Find("Chips Box"), 146, -4, 60, 31);
        Fixed(candidate.Find("Multiplier Box"), 239, -4, 60, 31);
        Label(candidate.Find("Times"), 214, -6, 20, 28, 18);
        foreach (var box in new[] { candidate.Find("Chips Box"), candidate.Find("Multiplier Box") })
        {
            box.GetComponent<Image>().color = new Color32(56, 46, 64, 245);
            var text = box.GetComponentInChildren<TMP_Text>();
            text.fontSize = 22; text.color = TextColor;
        }

        var actions = host.Actions.Panel;
        var actionCanvas = actions.GetComponentInParent<Canvas>();
        actionCanvas.renderMode = RenderMode.ScreenSpaceOverlay; actionCanvas.worldCamera = null; actionCanvas.sortingOrder = 20;
        Region(actions.transform.Find("Actions"), .85f, .08f, .14f, .174f);
        Region(actions.PlayButton.transform, 0, .53f, .48f, .47f, false);
        Region(actions.DiscardButton.transform, .52f, .53f, .48f, .47f, false);
        Region(actions.RankSortButton.transform, 0, .05f, .48f, .35f, false);
        Region(actions.SuitSortButton.transform, .52f, .05f, .48f, .35f, false);
        foreach (var button in new[] { actions.PlayButton, actions.DiscardButton, actions.RankSortButton, actions.SuitSortButton })
            StyleButton(button, button == actions.PlayButton || button == actions.DiscardButton ? 20 : 13);
        Region(hud.EndTurnButton.transform, .85f, .012f, .14f, .052f);
        StyleButton(hud.EndTurnButton, 13);

        var pile = All<DrawPilePresenter>().Single();
        pile.GetComponent<Canvas>().sortingOrder = 25;
        var icon = pile.View.transform.Find("Icon");
        Region(icon, .29f, .025f, .047f, .15f, false);
        icon.GetComponent<Image>().preserveAspect = true;
        var count = icon.GetComponentInChildren<TMP_Text>();
        count.fontSize = 16; count.alignment = TextAlignmentOptions.Bottom;
        foreach (var canvas in All<Canvas>().Where(c => c.name == "DeckOverview")) canvas.sortingOrder = 100;

        var flow = Slot(root, "Draw Controls", .84f, .076f, .15f, .18f);
        var feedback = Slot(root, "Status Rail", .927f, .31f, .066f, .66f);
        hud.ConfigureCompactLayout(flow, feedback);
        foreach (var hover in All<CardHoverPresenter>())
        {
            var serialized = new SerializedObject(hover);
            serialized.FindProperty("gap").floatValue = .06f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        { text.raycastTarget = false; text.color = TextColor; }
        EditorUtility.SetDirty(hud);
    }

    private static void Billboard(BattleActor actor, string name, Camera camera, Rect area, float depth, float attack)
    {
        var texturePath = ArtFolder + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var visual = actor.Visual;
        var presenter = visual.GetComponent<BattleActorPresenter>();
        presenter.ResetPresentation();
        foreach (var collider in visual.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var renderer in visual.GetComponents<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer);
        foreach (var filter in visual.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter);
        visual.name = "Portrait Presentation";
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
        var artwork = visual.Find("Artwork");
        if (artwork == null)
        {
            artwork = Quad("Artwork").transform;
            artwork.SetParent(visual, false);
        }
        artwork.localPosition = Vector3.zero; artwork.localRotation = Quaternion.identity; artwork.localScale = Vector3.one;
        artwork.GetComponent<MeshRenderer>().sharedMaterial = Material(name, texture, Color.white);
        presenter.Configure(visual);
        presenter.ConfigureAttackOffset(new Vector3(attack, 0, 0));
        var placement = actor.GetComponent<BattleBillboardLayout>() ?? actor.gameObject.AddComponent<BattleBillboardLayout>();
        placement.Configure(camera, area, depth, (float)texture.width / texture.height);
        EditorUtility.SetDirty(presenter);
    }

    private static GameObject Quad(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go;
    }

    private static Material Material(string name, Texture2D texture, Color color)
    {
        var path = ArtFolder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static RectTransform Slot(Transform root, string name, float x, float y, float w, float h)
    {
        var value = root.Find(name) as RectTransform;
        if (value == null)
        {
            value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(root, false); value.gameObject.layer = root.gameObject.layer;
        }
        Region(value, x, y, w, h, false);
        return value;
    }

    private static void Region(Transform value, float x, float y, float w, float h, bool style = true)
    {
        var rect = (RectTransform)value;
        rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(x + w, y + h);
        rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
        var image = rect.GetComponent<Image>();
        if (!style || image == null) return;
        image.color = PanelColor;
        var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(163, 139, 108, 160);
        outline.effectDistance = new Vector2(1, -1);
        image.raycastTarget = rect.GetComponent<Button>() != null;
    }

    private static void Fixed(Transform value, float left, float top, float width, float height)
    {
        var rect = (RectTransform)value;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(left, top); rect.sizeDelta = new Vector2(width, height);
    }

    private static void Label(Transform value, float left, float top, float width, float height, int font)
    {
        Fixed(value, left, top, width, height);
        var label = value.GetComponent<TMP_Text>(); label.fontSize = font;
        label.enableAutoSizing = false; label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.TopLeft; label.color = TextColor;
        label.raycastTarget = false;
    }

    private static void StyleButton(Button button, int font)
    {
        button.GetComponent<Image>().color = PanelColor;
        var text = button.GetComponentInChildren<TMP_Text>(); text.fontSize = font;
        text.alignment = TextAlignmentOptions.Center; text.color = TextColor;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(3, 2); text.rectTransform.offsetMax = new Vector2(-3, -2);
        text.enableAutoSizing = true; text.fontSizeMax = font; text.fontSizeMin = font - 2;
    }
}
#endif
