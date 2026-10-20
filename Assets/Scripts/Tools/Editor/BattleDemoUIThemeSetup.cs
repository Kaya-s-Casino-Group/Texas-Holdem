#if UNITY_EDITOR
using System;
using System.Linq;
using CardInteraction;
using CardPresentation;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BattleDemoUIThemeSetup
{
    const string Art = "Assets/Art/UI/RitualBattle/";
    static T[] All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Tools/BattleDemo/UI/Apply Plain Theme")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/BattleDemo.unity")
            throw new InvalidOperationException("Open BattleDemo in Edit mode.");
        var hud = All<SingleBattleHud>().Single();
        var theme = hud.GetComponent<BattleDemoUITheme>();
        if (theme == null) theme = hud.gameObject.AddComponent<BattleDemoUITheme>();
        var host = All<SingleBattleSceneAdapter>().Single();
        var root = hud.transform;
        var controls = root.Find("Background Choices");
        if (controls != null) Object.DestroyImmediate(controls.gameObject);
        foreach (var switcher in All<BattleBackgroundSwitcher>())
        {
            var so = new SerializedObject(switcher);
            foreach (var property in new[] { "eliteButton", "minionButton", "bossButton" })
                so.FindProperty(property).objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var turn = root.Find("Turn");
        Region(turn, .012f, .897f, .278f, .091f);
        Label(turn.Find("Round").GetComponent<TMP_Text>(), 44, -12, 142, 52, 15);
        Label(turn.Find("Energy").GetComponent<TMP_Text>(), 192, -12, 144, 52, 14);
        Region(hud.RestartButton.transform, .84f, .25f, .13f, .5f);
        var emblem = turn.Find("Sun Emblem");
        if (emblem != null) Object.DestroyImmediate(emblem.gameObject);
        Label(turn.Find("Round").GetComponent<TMP_Text>(), 18, -12, 158, 52, 15);
        var hero = root.Find("Player");
        Region(hero, .012f, .815f, .255f, .071f);
        Label(hero.Find("Health").GetComponent<TMP_Text>(), 20, -8, 352, 27, 15);
        StretchBar(hero.Find("Health Bar"), new Vector2(20, 13), new Vector2(-20, -36), new Color32(77, 190, 153, 255));
        var heroStatuses = StatusLabel(hero, "Actor Status", hud);

        var monster = root.Find("Enemy");
        Region(monster, .50f, .91f, .20f, .074f);
        monster.localRotation = Quaternion.identity;
        Label(monster.Find("Health").GetComponent<TMP_Text>(), 22, -7, 270, 26, 16);
        StretchBar(monster.Find("Health Bar"), new Vector2(22, 14), new Vector2(-22, -36), new Color32(211, 49, 66, 255));
        var monsterStatuses = StatusLabel(monster, "Actor Status", hud);
        var intentRoot = Child(root, "Enemy Intent", true);
        Region(intentRoot, .722f, .655f, .16f, .11f);
        intentRoot.localRotation = Quaternion.Euler(0, 0, 14);
        var intent = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Intent");
        intent.transform.SetParent(intentRoot, false);
        Label(intent, 24, -37, 210, 42, 15);
        var intentTitle = Text(intentRoot, "Intent Heading", hud);
        intentTitle.text = "意图"; Label(intentTitle, 28, -9, 190, 30, 19);
        hud.ConfigureRitualLayout(heroStatuses, monsterStatuses, intentRoot.gameObject);
        Region(root.Find("Status Rail"), .895f, .31f, .096f, .665f);
        Region(root.Find("Draw Controls"), .838f, .076f, .153f, .192f);
        // A wider, calm readout above the original hand; scoring remains phase-driven.
        var candidate = root.Find("Hand Candidate");
        Region(candidate, .31f, .232f, .28f, .055f);
        Label(candidate.Find("Hand").GetComponent<TMP_Text>(), 0, 4, 144, 34, 19);
        Label(candidate.Find("Details").GetComponent<TMP_Text>(), 0, -28, 244, 24, 13);
        foreach (var c in All<Canvas>().Where(c => c.renderMode != RenderMode.WorldSpace))
        {
            // Include inactive overlay canvases, which are opened only when requested.
            theme.Apply(c.transform);
        }
        foreach (var label in new[] { heroStatuses, monsterStatuses })
            SingleBattleHud.PresentActorStatuses(label, "");
        StyleTooltip(theme);
        StyleDeckSummary(theme);
        var overview = All<DeckOverviewView>().Single();
        var returnButton = overview.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Return");
        if (returnButton != null)
        {
            var rect = (RectTransform)returnButton.transform;
            rect.sizeDelta = new Vector2(280, 42); rect.anchoredPosition = new Vector2(0, -283);
            var caption = returnButton.GetComponentInChildren<TMP_Text>();
            caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
        }
        // This is the existing UI backing plane, not either actor's material.
        var handBacking = GameObject.Find("Battle Hand Backdrop");
        if (handBacking != null)
        {
            var renderer = handBacking.GetComponent<MeshRenderer>();
            renderer.enabled = true;
            var handPanel = handBacking.transform.Find("Ritual Hand Panel");
            if (handPanel != null) Object.DestroyImmediate(handPanel.gameObject);
            var layout = new SerializedObject(All<BattleDemoViewportLayout>().Single());
            layout.FindProperty("backdropArea").rectValue = new Rect(.284f, .004f, .71f, .281f);
            layout.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(theme); EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("BattleDemo plain UI saved. Actors untouched; overhead vitals, hidden empty status, intent-only plate and relic-only rail.");
    }

    [MenuItem("Tools/BattleDemo/UI/Preview Action Phase")]
    public static void PreviewAction()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run BattleDemo first.");
        var host = All<SingleBattleSceneAdapter>().Single();
        host.SkipDraw(false);
        host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions();
        var card = host.Actions.Hand.Cards.First();
        host.Actions.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
        host.Actions.Hand.Publish();
        All<SingleBattleHud>().Single().Refresh();
    }

    [MenuItem("Tools/BattleDemo/UI/Preview Opening Phase")]
    public static void PreviewOpening()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run BattleDemo first.");
        var host = All<SingleBattleSceneAdapter>().Single();
        host.RestartBattle();
        host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions();
    }

    static void StyleTooltip(BattleDemoUITheme theme)
    {
        var presenters = All<CardHoverPresenter>();
        if (presenters.Length == 0) return;
        const string path = Art + "CardTooltip.prefab";
        var source = presenters[0].TooltipPrefab;
        var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        try
        {
            theme.Apply(contents.transform);
            foreach (var image in contents.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Border") theme.StylePanel(image);
                else if (image.name == "Background" || image.name.EndsWith("Background"))
                { image.sprite = null; image.color = BattleDemoUITheme.Ink; }
                else if (image.name == "Shadow") image.color = new Color(0, 0, 0, .5f);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(contents, path).GetComponent<CardTooltipView>();
            foreach (var presenter in presenters)
            {
                var so = new SerializedObject(presenter);
                so.FindProperty("tooltipPrefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void StyleDeckSummary(BattleDemoUITheme theme)
    {
        foreach (var pile in All<DrawPilePresenter>())
        {
            var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(pile.SummaryPrefab));
            try
            {
                theme.Apply(contents.transform);
                var prefab = PrefabUtility.SaveAsPrefabAsset(contents, Art + "DeckSummary.prefab").GetComponent<DeckSummaryView>();
                pile.BindSummary(prefab);
                EditorUtility.SetDirty(pile);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }

    static void Region(Transform item, float x, float y, float w, float h)
    {
        var rect = (RectTransform)item;
        rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(x + w, y + h);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    static void Label(TMP_Text text, float x, float y, float w, float h, float size)
    {
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
        text.fontSize = size; text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.TopLeft; text.color = BattleDemoUITheme.Ivory;
        text.raycastTarget = false;
    }
    static RectTransform Child(Transform parent, string name, bool image)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        }
        if (image && rect.GetComponent<Image>() == null) rect.gameObject.AddComponent<Image>();
        return rect;
    }
    static TMP_Text Text(Transform parent, string name, SingleBattleHud hud)
    {
        var rect = Child(parent, name, false);
        var text = rect.GetComponent<TMP_Text>();
        if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = hud.GetComponentsInChildren<TMP_Text>(true).First(t => t.font != null).font;
        text.color = BattleDemoUITheme.Ivory; text.raycastTarget = false;
        return text;
    }
    static TMP_Text StatusLabel(Transform parent, string name, SingleBattleHud hud)
    {
        var label = Text(parent, name, hud);
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, -4); rect.sizeDelta = new Vector2(-28, 65);
        label.fontSize = 13; label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis;
        label.color = BattleDemoUITheme.Ivory;
        var outline = label.GetComponent<Outline>();
        if (outline == null) outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, -1);
        return label;
    }
    static void StretchBar(Transform item, Vector2 min, Vector2 max, Color fill)
    {
        var rect = (RectTransform)item;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = max;
        item.GetComponent<Image>().color = new Color32(48, 29, 35, 255);
        item.Find("Fill").GetComponent<Image>().color = fill;
    }
}
#endif
