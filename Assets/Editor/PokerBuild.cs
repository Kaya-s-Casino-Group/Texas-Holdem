using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PokerBuild
{
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
