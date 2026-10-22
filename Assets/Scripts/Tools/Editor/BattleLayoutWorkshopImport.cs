#if UNITY_EDITOR
using System;
using System.Linq;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleLayoutWorkshopImport
{
    public const string ProfilePath = "Assets/GameAssets/SingleBattle/BattleLayoutWorkshop.asset";
    const string TargetPath = "Assets/Scenes/SingleChapterAdventure.unity";
    static readonly string[] Encounters = { "single-chapter-battle-1", "single-chapter-gatekeeper", "single-chapter-wrath" };

    [MenuItem("工具/战斗布局工作台/将调整接回主线战斗")]
    public static void Import()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play。");
        var workshop = SceneManager.GetSceneByPath(BattleLayoutWorkshopSetup.Path);
        bool opened = !workshop.isLoaded;
        if (opened) workshop = EditorSceneManager.OpenScene(BattleLayoutWorkshopSetup.Path, OpenSceneMode.Additive);
        else if (workshop.isDirty) EditorSceneManager.SaveScene(workshop);
        var originalActive = SceneManager.GetActiveScene();
        var roots = workshop.GetRootGameObjects().OrderBy(r => r.name).ToArray();
        var active = roots.Select(r => r.activeSelf).ToArray();
        var result = new System.Collections.Generic.List<BattleLayoutProfile.Entry>();
        try
        {
            if (roots.Length != 3) throw new InvalidOperationException("工作台必须包含普通、精英、Boss 三个顶层组。");
            for (int i = 0; i < roots.Length; i++)
            {
                foreach (var r in roots) r.SetActive(r == roots[i]);
                Canvas.ForceUpdateCanvases();
                var group = roots[i].transform;
                var camera = group.GetComponentsInChildren<Camera>(true).Single(c => c.CompareTag("MainCamera"));
                var actors = group.Cast<Transform>().Single(t => t.name.StartsWith("Enemies -"));
                var canvas = group.GetComponentInChildren<Canvas>(true);
                var canvasRect = (RectTransform)canvas.transform;
                if (canvas.scaleFactor <= 0 || canvasRect.rect.height <= 0) throw new InvalidOperationException("工作台 Canvas 尺寸无效。");
                foreach (Transform actor in actors)
                {
                    var id = actor.name.Split(new[] { " - " }, StringSplitOptions.None)[0];
                    var hud = canvas.transform.Find(id + " - HUD") as RectTransform;
                    if (hud == null) throw new InvalidOperationException("缺少 HUD: " + id);
                    var viewport = camera.WorldToViewportPoint(actor.position);
                    var art = actor.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Artwork").transform;
                    var hudScreen = RectTransformUtility.WorldToScreenPoint(null, hud.position);
                    result.Add(new BattleLayoutProfile.Entry {
                        encounter = Encounters[i], actorId = id, actorViewport = viewport, depth = viewport.z,
                        referenceFrustumHeight = camera.orthographic ? 2 * camera.orthographicSize : 2 * viewport.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f),
                        referenceAspect = camera.aspect, actorScale = actor.lossyScale,
                        cameraRelativeRotation = Quaternion.Inverse(camera.transform.rotation) * actor.rotation,
                        artworkPosition = art.localPosition, artworkRotation = art.localRotation, artworkScale = art.localScale,
                        hudOffset = (hudScreen - (Vector2)camera.WorldToScreenPoint(actor.position)) / canvas.scaleFactor,
                        hudViewport = new Vector2((hudScreen.x - camera.pixelRect.x) / camera.pixelRect.width, (hudScreen.y - camera.pixelRect.y) / camera.pixelRect.height),
                        fixedHud = i == 2 && id == "enemy", hudRoot = Capture(hud, ""),
                        nodes = hud.GetComponentsInChildren<RectTransform>(true).Where(t => t != hud)
                            .Select(t => Capture(t, AnimationUtility.CalculateTransformPath(t, hud)))
                            .Where(n => !n.path.StartsWith("Enemy Badge Tooltip") && n.path != "Health Bar/Health Fill").ToArray()
                    });
                }
            }
        }
        finally
        {
            for (int i = 0; i < roots.Length; i++) roots[i].SetActive(active[i]);
            Canvas.ForceUpdateCanvases();
            // Restore exactly the user's chosen group before saving the source workshop.
            EditorSceneManager.SaveScene(workshop);
            if (opened) EditorSceneManager.CloseScene(workshop, true);
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
        }
        var profile = AssetDatabase.LoadAssetAtPath<BattleLayoutProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<BattleLayoutProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        profile.entries = result.ToArray(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
        var target = SceneManager.GetSceneByPath(TargetPath);
        bool closeTarget = !target.isLoaded;
        if (closeTarget) target = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Additive);
        try
        {
            var host = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            var camera = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            var layout = host.GetComponent<BattleAuthoredLayout>() ?? host.gameObject.AddComponent<BattleAuthoredLayout>();
            layout.Configure(host, camera, profile); EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(target); EditorSceneManager.SaveScene(target);
        }
        finally { if (closeTarget) EditorSceneManager.CloseScene(target, true); }
        Debug.Log("已接回 " + result.Count + " 组敌人/HUD 参数；血量、意图与状态内容继续实时更新。配置：" + ProfilePath);
    }

    static BattleLayoutProfile.RectPose Capture(RectTransform rect, string path)
    {
        var pose = new BattleLayoutProfile.RectPose {
            path = path, anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
            sizeDelta = rect.sizeDelta, position = rect.anchoredPosition3D, scale = rect.localScale, rotation = rect.localRotation
        };
        var text = rect.GetComponent<TMP_Text>();
        if (text != null) {
            pose.hasText = true; pose.autoSize = text.enableAutoSizing; pose.fontSize = text.fontSize;
            pose.fontMin = text.fontSizeMin; pose.fontMax = text.fontSizeMax; pose.alignment = text.alignment;
            pose.fontStyle = text.fontStyle; pose.characterSpacing = text.characterSpacing; pose.lineSpacing = text.lineSpacing;
        }
        return pose;
    }
}
#endif
