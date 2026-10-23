using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PokerPerformanceValidationBuild
{
    [MenuItem("Tools/Performance/Build Isolated Adventure Validation")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before building.");
        const string source = "Assets/Scenes/SingleChapterAdventure.unity";
        string temporary = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__PerformanceValidation.unity");
        string output = "Builds/macOS-PerformanceValidation-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(output);
        try
        {
            if (!AssetDatabase.CopyAsset(source, temporary)) throw new IOException("Could not copy adventure scene.");
            string scene = File.ReadAllText(temporary);
            if (!scene.Contains("saveAutomatically: 1")) throw new InvalidOperationException("Unexpected adventure save configuration.");
            File.WriteAllText(temporary, scene.Replace("saveAutomatically: 1", "saveAutomatically: 0"));
            AssetDatabase.ImportAsset(temporary, ImportAssetOptions.ForceSynchronousImport);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { temporary }, locationPathName = output + "/Pocket Poker Validation.app",
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None,
                extraScriptingDefines = new[] { "POKER_PERFORMANCE_VALIDATION" }
            });
            File.WriteAllText(output + "/build-status.txt", report.summary.result + "; source=" + source + "; isolatedSave=true; errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Validation build failed.");
            File.WriteAllText("output/performance-validation-build.txt", Path.GetFullPath(output));
            Debug.Log("POKER_PERFORMANCE_BUILD_OK " + output);
        }
        finally { AssetDatabase.DeleteAsset(temporary); }
    }
}
