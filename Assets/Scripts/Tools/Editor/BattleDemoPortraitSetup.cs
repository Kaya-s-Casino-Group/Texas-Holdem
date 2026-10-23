#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Poker.SingleBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BattleDemoPortraitSetup
{
    const string Folder = "Assets/Art/Characters/BattleBillboards/";
    static T One<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

    [MenuItem("Tools/BattleDemo/Portraits/Set Up Phase Portraits")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/BattleDemo.unity")
            throw new InvalidOperationException("Open BattleDemo in Edit mode.");
        var neutral = ImportPortrait("Player.png");
        var enemy = ImportPortrait("Enemy.png");
        var textures = new[] { "Player-Discard", "Player-Action", "Player-Ending" }.Select(name =>
            ImportPortrait("PlayerPoses/" + name + ".png")).ToArray();
        var host = One<SingleBattleSceneAdapter>();
        var actor = host.Player;
        var visual = actor.Visual;
        var previous = visual.GetComponent<BattleActorPresenter>();
        var portrait = previous as PortraitBattleActorPresenter;
        if (portrait == null)
        {
            // Preserve all existing body timings, offsets, model and audio bindings.
            var settings = previous != null ? JsonUtility.ToJson(previous) : null;
            if (previous != null) { previous.ResetPresentation(); Object.DestroyImmediate(previous); }
            portrait = visual.gameObject.AddComponent<PortraitBattleActorPresenter>();
            if (settings != null) JsonUtility.FromJsonOverwrite(settings, portrait);
            portrait.Configure(visual);
        }
        portrait.ConfigurePortrait(visual.Find("Artwork").GetComponent<MeshRenderer>(),
            neutral, textures[0], textures[1], textures[2]);
        RefreshAspect(actor.GetComponent<BattleBillboardLayout>(), neutral);
        RefreshAspect(host.Enemy.GetComponent<BattleBillboardLayout>(), enemy);
        var coordinator = visual.GetComponent<BattleActorPresentationCoordinator>();
        if (coordinator != null)
        {
            coordinator.Configure(portrait, coordinator.Shields);
            actor.ConfigurePresentation(coordinator);
            EditorUtility.SetDirty(coordinator);
        }
        else actor.ConfigurePresentation(portrait);
        EditorUtility.SetDirty(portrait); EditorUtility.SetDirty(actor);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Phase portraits bound through actor display presentation; existing combat motion retained.");
    }

    static Texture2D ImportPortrait(string relativePath)
    {
        var path = Folder + relativePath;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing portrait: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        Poker.EditorTools.PokerBuildAssetPolicy.ApplyStandalone(importer);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void RefreshAspect(BattleBillboardLayout layout, Texture2D texture)
    {
        if (layout == null) throw new InvalidOperationException("Actor is missing billboard placement.");
        // Keep the authored viewport, camera and depth; only fit the replacement artwork.
        var settings = new SerializedObject(layout);
        settings.FindProperty("artworkAspect").floatValue = (float)texture.width / texture.height;
        settings.ApplyModifiedProperties();
        layout.Refresh();
        EditorUtility.SetDirty(layout);
    }

    static EditorApplication.CallbackFunction verificationTick;
    [MenuItem("Tools/BattleDemo/Portraits/Verify Phase Flow")]
    public static void VerifyFlow()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run BattleDemo first.");
        if (verificationTick != null) throw new InvalidOperationException("A portrait verification is already running.");
        var host = One<SingleBattleSceneAdapter>();
        var view = One<PortraitBattleActorPresenter>();
        const string output = "Library/PlayerPortraits";
        Directory.CreateDirectory(output);
        host.RestartBattle();
        host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
        int stage = 0; bool faded = false, movedRight = false;
        var observed = new HashSet<string>();
        double started = EditorApplication.timeSinceStartup;
        verificationTick = () =>
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup - started > 30)
                    throw new InvalidOperationException("Portrait flow verification interrupted or timed out.");
                observed.Add(view.Pose);
                faded |= view.Opacity > .01f && view.Opacity < .99f;
                movedRight |= view.transform.Find("Artwork").localPosition.x > .001f;
                if (!view.IsChangingPose && !File.Exists(output + "/" + view.Pose + ".png"))
                    ScreenCapture.CaptureScreenshot(output + "/" + view.Pose + ".png");
                if (host.IsPresenting || view.IsChangingPose) return;
                if (stage == 0 && view.Pose == "discard")
                { if (!host.SkipDraw(true)) throw new InvalidOperationException("Skip draw rejected."); stage = 1; }
                else if (stage == 1 && view.Pose == "action")
                { if (!host.EndTurn(true)) throw new InvalidOperationException("End turn rejected."); stage = 2; }
                else if (stage == 2 && view.Pose == "discard")
                {
                    if (!observed.Contains("guard") || !faded || !movedRight)
                        throw new InvalidOperationException("Expected discard → action → guard → discard with fade and right shift.");
                    File.WriteAllText(output + "/verification.txt",
                        "PASS: discard → action → guard → discard; fractional alpha and rightward offset observed through live actor playback.");
                    Debug.Log("Portrait live phase flow passed.");
                    EditorApplication.update -= verificationTick; verificationTick = null;
                }
            }
            catch (Exception error)
            {
                File.WriteAllText(output + "/verification.txt", "FAIL: " + error);
                EditorApplication.update -= verificationTick; verificationTick = null;
                Debug.LogException(error);
            }
        };
        EditorApplication.update += verificationTick;
    }
}
#endif
