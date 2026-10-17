#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class PokerIOSSceneLifecycle
{
    [MenuItem("工具/构建发布/iPhone/应用导出工程生命周期配置")]
    public static void ApplyToCurrentExport() => Apply(BuildTarget.iOS, "Builds/iOS-CurrentScene");

    [PostProcessBuild(100)]
    public static void Apply(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        // Newer Unity versions ship their own delegate. Never replace it.
        if (File.Exists(Path.Combine(output, "Classes/UI/UnityScene.mm"))) return;
        var plistPath = Path.Combine(output, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        if (plist.root.values.ContainsKey("UIApplicationSceneManifest")) return;

        const string relativeSource = "Classes/PokerSceneLifecycle.mm";
        File.Copy("Tools/iOS/PokerSceneLifecycle.mm", Path.Combine(output, relativeSource), true);
        var projectPath = PBXProject.GetPBXProjectPath(output);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        var guid = project.AddFile(relativeSource, relativeSource, PBXSourceTree.Source);
        project.AddFileToBuild(project.GetUnityFrameworkTargetGuid(), guid);
        project.WriteToFile(projectPath);

        var manifest = plist.root.CreateDict("UIApplicationSceneManifest");
        manifest.SetBoolean("UIApplicationSupportsMultipleScenes", false);
        var configuration = manifest.CreateDict("UISceneConfigurations")
            .CreateArray("UIWindowSceneSessionRoleApplication").AddDict();
        configuration.SetString("UISceneConfigurationName", "Poker Game");
        configuration.SetString("UISceneClassName", "UIWindowScene");
        configuration.SetString("UISceneDelegateClassName", "PokerSceneDelegate");
        plist.WriteToFile(plistPath);
        Debug.Log("POKER_IOS_SCENE_LIFECYCLE_APPLIED: " + output);
    }
}
#endif
