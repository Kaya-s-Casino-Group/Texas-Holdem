#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CardArt;
using CardArt.Editor;
using CardInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public static class CardArtworkSetup
    {
        public const string ProfilePath = "Assets/CardArt/Profiles/StandardCardArt.asset";
        [MenuItem("工具/卡牌美术/接入当前玩法场景")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后配置卡牌美术资源。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
            if (prefab == null) throw new InvalidOperationException("缺少标准单牌 Prefab。");
            var skin = prefab.GetComponentInChildren<CardView>(true).Atlas;
            var profile = AssetDatabase.LoadAssetAtPath<CardArtProfile>(ProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/CardArt/Profiles")) AssetDatabase.CreateFolder("Assets/CardArt", "Profiles");
                profile = ScriptableObject.CreateInstance<CardArtProfile>();
                profile.Configure(skin, CardDeferredShaderCombinations.Create()); AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // Every world-card prefab on this atlas consumes the shared card artwork, never zone settings.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/CardArt/Prefabs", "Assets/CardPresentation/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path); bool changed = false;
                try
                {
                    foreach (var view in root.GetComponentsInChildren<CardView>(true))
                        if (view.Atlas == skin && view.ArtProfile == null) { view.SetArtProfile(profile); changed = true; }
                    foreach (var graphic in root.GetComponentsInChildren<CardUIGraphic>(true))
                        if (graphic.ArtProfile == null && (graphic.Atlas == null || graphic.Atlas == skin)) { graphic.SetArtProfile(profile); changed = true; }
                    foreach (var overview in root.GetComponentsInChildren<DeckOverviewView>(true))
                        if (overview.Atlas == skin && overview.ArtProfile == null) { overview.BindArtProfile(profile); changed = true; }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach(var match in root.GetComponentsInChildren<CardMatchHost>(true)) if(match.ArtBindings==null)
                { Undo.RecordObject(match,"绑定玩法卡牌状态美术映射"); match.ConfigureArtBindings(CardStateArtBindings.Standard); EditorUtility.SetDirty(match); }
                foreach (var view in root.GetComponentsInChildren<CardView>(true)) if (view.Atlas == skin && view.ArtProfile == null)
                { Undo.RecordObject(view, "绑定共享卡牌美术"); view.SetArtProfile(profile); EditorUtility.SetDirty(view); }
                foreach (var graphic in root.GetComponentsInChildren<CardUIGraphic>(true)) if (graphic.ArtProfile == null && (graphic.Atlas == null || graphic.Atlas == skin))
                { Undo.RecordObject(graphic, "绑定共享卡牌美术"); graphic.SetArtProfile(profile); EditorUtility.SetDirty(graphic); }
                foreach (var overview in root.GetComponentsInChildren<DeckOverviewView>(true)) if (overview.Atlas == skin && overview.ArtProfile == null)
                { Undo.RecordObject(overview, "绑定共享卡牌美术"); overview.BindArtProfile(profile); EditorUtility.SetDirty(overview); }
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeObject = profile;
            Debug.Log("卡牌美术配置已接入：牌区不再保存火漆/镭射样式，场景与 UI 共用单牌美术资源。");
        }
    }
}
#endif
