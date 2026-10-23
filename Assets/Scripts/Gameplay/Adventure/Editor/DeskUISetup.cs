using System;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using Poker.SingleBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.Adventure.Editor
{
    public static class DeskUISetup
    {
        public const string ArtRoot = "Assets/Art/UI/Desk/";
        public const string Sprites = ArtRoot + "Resources/DeskUI/";

        [MenuItem("工具/局外冒险/接入 desk_UI 与交付音频")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != AdventureSceneSetup.ScenePath) throw new InvalidOperationException("请打开 SingleChapterAdventure 场景。");
            foreach (var path in Directory.GetFiles(Sprites, "*.png"))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spritePixelsPerUnit = 100;
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "ContentButton") importer.spriteBorder = new Vector4(280, 300, 280, 300);
                if (name == "ContentPanel") importer.spriteBorder = new Vector4(180, 180, 180, 180);
                Poker.EditorTools.PokerBuildAssetPolicy.ApplyStandalone(importer);
                importer.SaveAndReimport();
            }
            var roots = scene.GetRootGameObjects();
            var host = roots.SelectMany(r => r.GetComponentsInChildren<AdventureSceneHost>(true)).Single();
            var view = host.BattleView;
            var hud = roots.SelectMany(r => r.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            var old = hud.GetComponent<RitualBattleLayout>(); if (old != null) { old.enabled = false; EditorUtility.SetDirty(old); }
            var layout = hud.GetComponent<DeskBattleLayout>();
            if (layout == null) layout = hud.gameObject.AddComponent<DeskBattleLayout>();
            layout.Configure(view); EditorUtility.SetDirty(layout);

            var handLayouts = roots.SelectMany(r => r.GetComponentsInChildren<BattleDemoViewportLayout>(true)).ToArray();
            foreach (var hand in handLayouts)
            {
                var fields = new SerializedObject(hand);
                fields.FindProperty("handArea").rectValue = new Rect(.285f, .025f, .42f, .17f);
                fields.FindProperty("backdropArea").rectValue = new Rect(.271f, -.005f, .453f, .25f);
                fields.FindProperty("backdropPreserveAspect").boolValue = true;
                fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(hand);
            }
            var backing = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Single(r => r.name == "Battle Hand Backdrop");
            Directory.CreateDirectory(ArtRoot + "Materials");
            string materialPath = ArtRoot + "Materials/DeskHandTray.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(backing.sharedMaterial); AssetDatabase.CreateAsset(material, materialPath); }
            var tray = AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites + "HandTray.png");
            if (tray == null) throw new InvalidOperationException("缺少 HandTray 美术。");
            material.SetTexture("_BaseMap", tray); material.SetTexture("_MainTex", tray);
            material.SetColor("_BaseColor", Color.white); material.SetColor("_Color", Color.white);
            backing.sharedMaterial = material; EditorUtility.SetDirty(backing); EditorUtility.SetDirty(material);
            foreach (var hand in handLayouts) hand.Refresh();
            foreach (var pile in roots.SelectMany(r => r.GetComponentsInChildren<DrawPilePresenter>(true)))
            {
                var icon = pile.View.transform.Find("Icon");
                if (icon != null) DeskUISkin.Place(icon, new Rect(.15f, .02f, .065f, .19f));
            }
            var relicArt = hud.GetComponent<BattleDemoRelicArt>();
            if (relicArt != null)
            {
                var fields = new SerializedObject(relicArt);
                fields.FindProperty("iconSize").floatValue = 70; fields.FindProperty("spacing").floatValue = 10;
                fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(relicArt);
            }
            AdventureAudioSetup.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("场景保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("desk_UI 布局、原始美术和交付音乐/音效已保存到 SingleChapterAdventure。");
        }
    }
}
