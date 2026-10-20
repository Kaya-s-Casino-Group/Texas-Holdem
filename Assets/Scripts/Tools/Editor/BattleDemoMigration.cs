#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CardPresentation;
using Poker.SingleBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleDemoMigration
{
    const string Destination = "Assets/Scenes/BattleDemo.unity";
    [MenuItem("Tools/BattleDemo/Configure Forward")]
    public static void ConfigurePipeline()
    {
        const string rp = "Assets/Settings/BattleForwardRenderer.asset", pp = "Assets/Settings/BattleURP.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rp);
        if (renderer == null) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer, rp); }
        renderer.renderingMode = RenderingMode.Forward; renderer.rendererFeatures.Clear();
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pp);
        if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, pp); }
        var settings = new SerializedObject(pipeline);
        var renderers = settings.FindProperty("m_RendererDataList"); renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        settings.FindProperty("m_DefaultRendererIndex").intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo();
        pipeline.renderScale = 1; pipeline.msaaSampleCount = 1; pipeline.supportsHDR = false;
        GraphicsSettings.defaultRenderPipeline = pipeline;
        var quality = QualitySettings.GetQualityLevel();
        for (int i=0;i<QualitySettings.names.Length;i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
        QualitySettings.SetQualityLevel(quality, false);
        foreach (var camera in All<Camera>(SceneManager.GetActiveScene()))
        { var d=camera.GetUniversalAdditionalCameraData(); d.SetRenderer(0); d.renderPostProcessing=false; camera.cullingMask=-1; camera.allowHDR=false; }
        EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssetIfDirty(renderer); AssetDatabase.SaveAssetIfDirty(pipeline);
    }
    static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<T>(true)).ToArray();
    [MenuItem("Tools/BattleDemo/Finalize Current Rules")]
    public static void FinalizeRules()
    {
        var scene=SceneManager.GetActiveScene();var host=All<SingleBattleSceneAdapter>(scene).Single();
        var xml=AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml");
        host.ConfigureContent(xml,"standard-single-battle");
        foreach(var source in All<Poker.BattleHosting.XmlBattleSessionSource>(scene))source.Configure(xml,"standard-single-battle");
        BattleDemoReferenceLayout.ApplyHud(All<SingleBattleHud>(scene).Single(),host);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Audit();
    }
    [MenuItem("Tools/BattleDemo/Audit")]
    public static void Audit()
    {
        Directory.CreateDirectory("Library/BattleDemoMigration");var scene=SceneManager.GetActiveScene();
        var host=All<SingleBattleSceneAdapter>(scene).Single();
        var session=host.BuildConfiguredSession();
        File.WriteAllText("Library/BattleDemoMigration/audit.txt", "scene="+scene.path+"\nplayers="+host.Players.Count+"\nenemies="+host.Enemies.Count+"\nxml="+AssetDatabase.GetAssetPath(host.ContentXml)+"\nencounter="+host.ContentEncounterId+"\n"+string.Join("\n",All<Component>(scene).Where(c=>c!=null).Select(c=>c.gameObject.name+" : "+c.GetType().FullName)));
        File.WriteAllLines("Library/BattleDemoMigration/dependencies.txt",AssetDatabase.GetDependencies(scene.path,true));
        Debug.Log("BattleDemo audit passed: session assembled, scene and prefab dependencies recorded.");
    }
    [MenuItem("Tools/BattleDemo/Capture")]
    public static void Capture(){Directory.CreateDirectory("Library/BattleDemoMigration");ScreenCapture.CaptureScreenshot("Library/BattleDemoMigration/game.png");}
}
#endif
