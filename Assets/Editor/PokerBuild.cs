using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class PokerBuild
{
    private const string PendingIOSScene = "PokerBuild.PendingIOSScene";
    private const string IOSOutput = "Builds/iOS-CurrentScene";

    [MenuItem("Tools/Poker/Build Current Scene for iPhone")]
    public static void BuildCurrentSceneForIPhone()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            throw new System.InvalidOperationException("Install iOS Build Support for this Unity version.");
        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
            throw new System.InvalidOperationException("Save the active scene before building.");
        if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            throw new System.InvalidOperationException("Could not save the active scene.");

        ConfigureIPhone();
        Directory.CreateDirectory(IOSOutput);
        File.WriteAllText(IOSOutput + "/build-status.txt", "Queued: " + scene.path);
        SessionState.SetString(PendingIOSScene, scene.path);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            SessionState.EraseString(PendingIOSScene);
            throw new System.InvalidOperationException("Could not switch to iOS.");
        }
        EditorApplication.delayCall += ContinueIOSBuild;
    }

    [MenuItem("Tools/Poker/Configure iPhone Build")]
    public static void ConfigureIPhone()
    {
        PokerAppIcon.Apply();
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.linxi.texasholdem");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.appleDeveloperTeamID = "XFL668K9G3";
        AssetDatabase.SaveAssets();
    }

    [InitializeOnLoadMethod]
    private static void ResumeIOSBuild()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(PendingIOSScene, "")))
            EditorApplication.delayCall += ContinueIOSBuild;
    }

    private static void ContinueIOSBuild()
    {
        var scene = SessionState.GetString(PendingIOSScene, "");
        if (string.IsNullOrEmpty(scene)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ContinueIOSBuild;
            return;
        }
        SessionState.EraseString(PendingIOSScene);
        try
        {
            RigidVATAssetImport.ConfigureAndValidateDataTextures();
            File.WriteAllText(IOSOutput + "/build-status.txt", "Building: " + scene);
            var report = BuildPipeline.BuildPlayer(new[] { scene }, IOSOutput,
                BuildTarget.iOS, BuildOptions.Development);
            var messages = new System.Text.StringBuilder();
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    messages.AppendLine(message.type + ": " + message.content);
            File.WriteAllText(IOSOutput + "/build-messages.txt", messages.ToString());
            File.WriteAllText(IOSOutput + "/build-status.txt",
                report.summary.result + ": " + scene + "; errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("iOS export failed: " + report.summary.result);
            Debug.Log("POKER_IOS_BUILD_OK: " + scene + " -> " + IOSOutput);
        }
        catch (System.Exception exception)
        {
            File.WriteAllText(IOSOutput + "/build-status.txt", "Failed: " + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Poker/Build macOS Game")]
    public static void BuildMac()
    {
        PlayerSettings.productName = "Pocket Poker";
        PlayerSettings.companyName = "Pocket Games";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/MainScene.unity" }, "Builds/Pocket Poker.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Build failed: " + report.summary.result);
        Debug.Log("POKER_BUILD_OK");
    }
}
