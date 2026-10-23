using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardArt;
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

namespace Poker.Adventure.Editor
{
    public static class UnderworldUISetup
    {
        private const string Art = "Assets/Art/UI/Underworld/";
        private static CardAtlasData atlas;
        private static CardArtProfile profile;
        private static Material canvasMaterial;
        private static readonly Dictionary<Object, Object> replacements = new Dictionary<Object, Object>();

        [MenuItem("工具/局外冒险/应用完整冥界UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出播放模式。");
            foreach (var file in Directory.GetFiles(Art + "Resources/RitualUI"))
            {
                if (!file.EndsWith(".png") && !file.EndsWith(".jpg")) continue;
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(file);
                importer.textureType = TextureImporterType.Default; importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = file.EndsWith("Nodes.png"); importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096; importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                Poker.EditorTools.PokerBuildAssetPolicy.ApplyStandalone(importer);
                importer.SaveAndReimport();
            }
            replacements.Clear(); Directory.CreateDirectory(Art + "Materials"); Directory.CreateDirectory(Art + "Prefabs"); AssetDatabase.Refresh();
            atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>("Assets/Art/CardAtlases/FourBeastsAtlas.asset");
            profile = AssetDatabase.LoadAssetAtPath<CardArtProfile>("Assets/GameAssets/CardArt/Profiles/StandardCardArt.asset");
            canvasMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Cards/FourBeastsCanvas.mat");
            var previous = SceneManager.GetActiveScene();
            foreach (var path in new[] { "Assets/Scenes/BattleDemo.unity", AdventureSceneSetup.ScenePath })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene); var roots = scene.GetRootGameObjects();
                    var view = roots.SelectMany(r => r.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                    var hud = roots.SelectMany(r => r.GetComponentsInChildren<SingleBattleHud>(true)).Single();
                    var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
                    foreach (var overlay in roots.SelectMany(r => r.GetComponentsInChildren<PlayerPortraitOverlay>(true))) Object.DestroyImmediate(overlay);
                    var playerArt = view.Player.Visual.Find("Artwork"); playerArt.localScale = Vector3.one;
                    var renderer = playerArt.GetComponent<MeshRenderer>(); renderer.forceRenderingOff = false;
                    var playerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/BattleBillboards/Player.png");
                    view.Player.GetComponent<BattleBillboardLayout>().Configure(camera, new Rect(-.012f, -.03f, .36f, .84f), 2.8f, (float)playerTexture.width / playerTexture.height);
                    renderer.sharedMaterial.SetTextureScale("_BaseMap", Vector2.one); renderer.sharedMaterial.SetTextureOffset("_BaseMap", Vector2.zero);
                    // Keep the portrait's screen footprint while opening a real 4.4-unit flight corridor.
                    foreach (var enemy in roots.SelectMany(r => r.GetComponentsInChildren<EnemyBattleActor>(true)))
                    {
                        var billboard = enemy.GetComponent<BattleBillboardLayout>();
                        if (billboard == null) continue;
                        var settings = new SerializedObject(billboard); settings.FindProperty("distance").floatValue = 6.4f;
                        settings.ApplyModifiedPropertiesWithoutUndo(); billboard.Refresh(); EditorUtility.SetDirty(billboard);
                    }
                    foreach (var flight in roots.SelectMany(r => r.GetComponentsInChildren<BattleCardProjectileAction>(true)))
                    {
                        var settings = new SerializedObject(flight);
                        settings.FindProperty("flightSeconds").floatValue = .64f;
                        settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(flight);
                    }
                    foreach (var root in roots) ReplaceReferences(root);
                    var theme = hud.GetComponent<BattleDemoUITheme>() ?? hud.gameObject.AddComponent<BattleDemoUITheme>();
                    var info = hud.transform.Find("Battle Info");
                    if (info != null) theme.StylePanel(info.GetComponent<Image>() ?? info.gameObject.AddComponent<Image>());
                    foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
                    {
                        if (canvas.renderMode == RenderMode.WorldSpace) continue;
                        theme.Apply(canvas.transform);
                        var scaler = canvas.GetComponent<CanvasScaler>();
                        if (scaler != null) { scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f; EditorUtility.SetDirty(scaler); }
                    }
                    var layout = hud.GetComponent<RitualBattleLayout>() ?? hud.gameObject.AddComponent<RitualBattleLayout>(); layout.Configure(view);
                    foreach (var item in roots.SelectMany(r => r.GetComponentsInChildren<BattleDemoViewportLayout>(true)))
                    {
                        var settings = new SerializedObject(item);
                        settings.FindProperty("distance").floatValue = 2;
                        settings.FindProperty("handArea").rectValue = new Rect(.425f, .02f, .40f, .18f);
                        settings.FindProperty("backdropArea").rectValue = new Rect(.37f, .005f, .62f, .31f);
                        settings.ApplyModifiedPropertiesWithoutUndo(); item.Refresh();
                    }
                    foreach (var pile in roots.SelectMany(r => r.GetComponentsInChildren<DrawPilePresenter>(true)))
                    {
                        var icon = pile.View.transform.Find("Icon"); if (icon != null) RitualSkin.Place(icon, new Rect(.375f, .025f, .045f, .15f));
                    }
                    foreach (var screen in roots.SelectMany(r => r.GetComponentsInChildren<AdventureScreen>(true)))
                    {
                        screen.ConfigureArt(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/BattleBackgrounds/Sources/Minion_Updated.jpg"), playerTexture, atlas);
                        EditorUtility.SetDirty(screen);
                    }
                    EditorUtility.SetDirty(layout); EditorUtility.SetDirty(playerArt); EditorUtility.SetDirty(view.Player.GetComponent<BattleBillboardLayout>());
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets(); Debug.Log("完整冥界UI、常规主角面片、FourBeasts图集已应用到两个Demo场景。");
        }
        private static Object Swap(Object source)
        {
            if (source == null) return null;
            if (replacements.TryGetValue(source, out var cached)) return cached;
            if (source is CardAtlasData) return atlas;
            if (source is CardArtProfile) return profile;
            string path = AssetDatabase.GetAssetPath(source);
            if (path == "Assets/GameAssets/CardArt/Prefabs/ClassicCard.prefab") return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/FourBeastsCard.prefab");
            if (source is Material material)
            {
                if (material.shader.name == "CardPresentation/Card Canvas") return canvasMaterial;
                if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap")?.name == "Classic")
                {
                    string target = Art + "Materials/" + material.name + "-FourBeasts.mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(target);
                    if (copy == null) { copy = new Material(material); AssetDatabase.CreateAsset(copy, target); }
                    CardShaderProperties.ConfigureSkin(copy, atlas); replacements[source] = copy; EditorUtility.SetDirty(copy); return copy;
                }
            }
            if (source is Component component && path.EndsWith(".prefab") &&
                (component is CardUIItemView || component is CardTooltipView || component is DeckSummaryView))
            {
                string target = Art + "Prefabs/" + Path.GetFileName(path);
                if (path == target) return source;
                if (!File.Exists(target)) AssetDatabase.CopyAsset(path, target);
                var contents = PrefabUtility.LoadPrefabContents(target);
                GameObject saved;
                try
                {
                    ReplaceReferences(contents);
                    // The local skin changes only these copied prefabs.
                    var theme = contents.AddComponent<BattleDemoUITheme>(); theme.Apply(contents.transform); Object.DestroyImmediate(theme);
                    saved = PrefabUtility.SaveAsPrefabAsset(contents, target);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                var replacement = saved.GetComponent(component.GetType()); replacements[source] = replacement; return replacement;
            }
            return source;
        }
        private static void ReplaceReferences(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;
                var settings = new SerializedObject(component); var property = settings.GetIterator(); bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                    var original = property.objectReferenceValue; var replacement = Swap(original);
                    if (replacement == original) continue; property.objectReferenceValue = replacement; changed = true;
                }
                if (changed) { settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(component); }
            }
        }
    }
}
