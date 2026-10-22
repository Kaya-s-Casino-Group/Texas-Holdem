#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using Poker.Adventure;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConceptBattleSetup
{
    public const string ScenePath = "Assets/Scenes/SingleChapterAdventure.unity";
    const string PreviewPath = "Assets/Scenes/__ConceptUiPreview.unity";
    const string Art = "Assets/Art/UI/BattleConcept/";
    static T[] All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("工具/局内UI/应用已确认概念布局")]
    public static void Apply()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open SingleChapterAdventure in Edit mode.");
        foreach (string file in Directory.GetFiles(Art + "Resources", "*.png", SearchOption.AllDirectories))
        {
            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100; importer.spriteBorder = Vector4.zero;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // Select the visible pixels of newly generated sprites without resampling the PNG.
            // The health-bar layers retain their authored canvas and registered fill coordinates.
            if (new[]{"panel_button_play_compact","panel_button_discard_compact","panel_sort","panel_relic_rail",
                "icon_poison","icon_stun","icon_immunity","icon_reflect","icon_overheal","icon_heal","icon_buff","icon_cleanse","icon_unknown"}
                .Contains(Path.GetFileNameWithoutExtension(file)))
            {
                var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
                texture.LoadImage(File.ReadAllBytes(file)); var pixels = texture.GetPixels32();
                int left = texture.width, bottom = texture.height, right = -1, top = -1;
                for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                    if(pixels[y*texture.width+x].a>8) { left=Mathf.Min(left,x); right=Mathf.Max(right,x); bottom=Mathf.Min(bottom,y); top=Mathf.Max(top,y); }
                if(right>=left && top>=bottom)
                {
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritesheet = new[]{new SpriteMetaData {name=Path.GetFileNameWithoutExtension(file),
                        rect=new Rect(left,bottom,right-left+1,top-bottom+1), alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}};
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
            importer.SaveAndReimport();
        }
        SaveLayout();
    }

    [MenuItem("工具/局内UI/保存当前布局")]
    public static void SaveLayout()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open SingleChapterAdventure in Edit mode.");
        var host = All<AdventureSceneHost>().Single(); var hud = All<SingleBattleHud>().Single();
        var old = hud.GetComponent<RitualBattleLayout>(); if (old != null) { old.enabled = false; EditorUtility.SetDirty(old); }
        hud.GetComponent<DeskBattleLayout>().Configure(host.BattleView);
        host.BattleView.GetComponent<BattleDemoEnemyWorldHud>().ConfigureConceptLayout(true);
        EditorUtility.SetDirty(host.BattleView.GetComponent<BattleDemoEnemyWorldHud>());
        foreach (var layout in All<BattleDemoViewportLayout>())
        {
            var fields = new SerializedObject(layout); fields.FindProperty("handArea").rectValue = new Rect(.304f, .107f, .443f, .183f);
            fields.ApplyModifiedPropertiesWithoutUndo(); layout.Refresh(); EditorUtility.SetDirty(layout);
        }
        foreach (var renderer in All<MeshRenderer>().Where(r => r.name == "Battle Hand Backdrop")) { renderer.enabled = false; EditorUtility.SetDirty(renderer); }
        foreach (var scaler in All<CanvasScaler>().Where(s => s.GetComponent<Canvas>().renderMode != RenderMode.WorldSpace))
        { scaler.referenceResolution = new Vector2(1856,850); scaler.matchWidthOrHeight = 1; EditorUtility.SetDirty(scaler); }
        var scene = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Concept battle UI installed: native aspect sprites, layered clipped health, two-column relics and centered hand footer.");
    }

    [MenuItem("工具/局内UI/恢复原始背景材质")]
    public static void RestoreScenery()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open SingleChapterAdventure in Edit mode.");
        int restored = 0;
        foreach (var renderer in All<MeshRenderer>())
        {
            var current = renderer.sharedMaterial;
            if (current == null || !AssetDatabase.GetAssetPath(current).StartsWith(Art + "Materials/")) continue;
            var original = AssetDatabase.FindAssets(current.name + " t:Material")
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith(Art))
                .Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name == current.name);
            if (original == null) throw new InvalidOperationException("Cannot locate original background material: " + current.name);
            renderer.sharedMaterial = original; EditorUtility.SetDirty(renderer); restored++;
        }
        var scene = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.IsValidFolder(Art + "Materials")) AssetDatabase.DeleteAsset(Art + "Materials");
        Debug.Log("Restored " + restored + " original background material references; scenery grading remains unchanged.");
    }

    [MenuItem("工具/局内UI/整理对齐并清理旧元素")]
    public static void AlignAndClean()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open SingleChapterAdventure in Edit mode.");
        SaveLayout();
        var hud = All<SingleBattleHud>().Single();
        var bindings = ConceptBattleSkin.Node(hud.transform, "Data Bindings");
        bindings.gameObject.SetActive(false);
        // These still supply labels/templates to existing presenters; they have no visible UI role.
        foreach(var name in new[]{"Enemy","Enemy Intent","Battle Info"})
        {
            var legacy = hud.transform.Find(name) ?? hud.transform.Find("Safe Area/" + name);
            if(legacy != null) Undo.SetTransformParent(legacy,bindings,"Group nonvisual HUD bindings");
        }
        var candidate = hud.GetComponentInChildren<BattleDemoOutcomePreview>(true);
        if(candidate != null)
        {
            var oldPresenter = candidate.GetComponent<HandCandidatePresenter>();
            if(oldPresenter != null && !oldPresenter.enabled) Undo.DestroyObjectImmediate(oldPresenter);
            foreach(var name in new[]{"Heading","Hand","Details","Times","Chips Caption","Multiplier Caption","Chips Box","Multiplier Box"})
            { var child = candidate.transform.Find(name); if(child != null) Undo.DestroyObjectImmediate(child.gameObject); }
            foreach(var name in new[]{"Outcome Hand","Outcome Details"})
            { var child = candidate.transform.Find(name); if(child != null) { child.gameObject.SetActive(false); Undo.SetTransformParent(child,bindings,"Group nonvisual preview bindings"); } }
        }
        var oldLayout = hud.GetComponent<RitualBattleLayout>();
        if(oldLayout != null && !oldLayout.enabled) Undo.DestroyObjectImmediate(oldLayout);
        var roots = new[]{hud.transform, All<AdventureSceneHost>().Single().BattleView.Actions.Panel.transform,
            All<DrawPilePresenter>().First().View.transform};
        int removed = 0;
        foreach(var root in roots)
        foreach(var node in root.GetComponentsInChildren<RectTransform>(true).Reverse().ToArray())
        {
            if(node == null || node.name == "Result" || node.GetComponentsInParent<Transform>(true).Any(parent=>parent.name=="Result")) continue;
            if(new[]{"Painted Surface","Gold Fillet","Ritual Surface","Round Frame","Action Frame","Desk Deck Frame","Desk Health Frame","Count (1)"}.Contains(node.name) ||
                node.name == "Heading" && node.parent != null && new[]{"Player","Turn","Enemy"}.Contains(node.parent.name))
            { Undo.DestroyObjectImmediate(node.gameObject); removed++; }
        }
        foreach(var root in roots)
        foreach(var outline in root.GetComponentsInChildren<Outline>(true).Where(item=>!item.enabled).ToArray())
            Undo.DestroyObjectImmediate(outline);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Aligned battle UI; removed " + removed + " obsolete decorative objects and grouped nonvisual bindings.");
    }

    [InitializeOnLoadMethod]
    static void RegisterPreview()
    {
        EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ConceptUiPreviewStart", false)) return;
        SessionState.SetBool("ConceptUiPreviewStart", false); EditorApplication.delayCall += StartPreviewBattle;
    }
    [MenuItem("工具/局内UI/预览隔离对局")]
    public static void Preview()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop playback first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath) != null) AssetDatabase.DeleteAsset(PreviewPath);
        if (!AssetDatabase.CopyAsset(ScenePath, PreviewPath)) throw new InvalidOperationException("Cannot create the preview scene.");
        var scene = EditorSceneManager.OpenScene(PreviewPath);
        var owner = All<AdventureSceneHost>().Single(); var fields = new SerializedObject(owner);
        fields.FindProperty("saveAutomatically").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene); SessionState.SetBool("ConceptUiPreviewStart", true); EditorApplication.isPlaying = true;
    }
    static void StartPreviewBattle()
    {
        var host = All<AdventureSceneHost>().Single();
        if (!host.NewRun(host.AvailableTarots.First().Id, 42)) throw new InvalidOperationException(host.Message);
        if (!host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, host.Adventure.AvailableNodes.First().Id))) throw new InvalidOperationException(host.Message);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }
    [MenuItem("工具/局内UI/选牌预览")]
    public static void Select()
    {
        var view = All<AdventureSceneHost>().Single().BattleView; var hand = view.Actions.Hand;
        hand.GetComponent<HandZoneView>().CompleteTransitions();
        foreach (var card in hand.Cards) hand.SetInteraction(card.Id, default);
        foreach (var card in hand.Cards.Take(2)) hand.SetInteraction(card.Id, new CardInteractionState(isSelected:true));
        hand.Publish(); view.RefreshCandidate();
    }
    [MenuItem("工具/局内UI/截图")]
    public static void Capture()
    {
        Directory.CreateDirectory("Library/ConceptBattleUI");
        ScreenCapture.CaptureScreenshot("Library/ConceptBattleUI/game.png");
        Debug.Log("Battle UI screenshot saved to Library/ConceptBattleUI/game.png.");
    }
    [MenuItem("工具/局内UI/清理隔离预览")]
    public static void Cleanup()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop preview first.");
        EditorSceneManager.OpenScene(ScenePath); if(AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath)!=null)AssetDatabase.DeleteAsset(PreviewPath);
    }
}
#endif
