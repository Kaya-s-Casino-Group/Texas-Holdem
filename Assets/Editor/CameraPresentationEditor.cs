using System;
using System.Collections;
using BattlePresentation;
using BattlePresentation.Unity;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraSceneConfiguration))]
public sealed class CameraSceneConfigurationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var configuration = (CameraSceneConfiguration)target;
        try { configuration.Validate(); }
        catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        EditorGUILayout.HelpBox("场景相机配置：输出相机、Overview、Opening、默认焦点及各 Cue 参数。此组件不播放动作。Opening 的 Deal Start Progress 按时长比例放行发牌。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("将当前 Main Camera 机位保存为基础构图")) CameraPresentationSetup.Capture(configuration, false);
            if (GUILayout.Button("将当前 Main Camera 机位保存为开场")) CameraPresentationSetup.Capture(configuration, true);
            if (GUILayout.Button("回到基础构图")) CameraPresentationSetup.RestoreOverview(configuration);
            if (GUILayout.Button("预览开场镜头"))
                CameraActionPreview.Start(CameraPresentationSetup.Bridge(configuration).CameraPresent.GetComponentInChildren<CameraOpeningTimelineAction>(true));
            if (GUILayout.Button("停止预览并回位")) CameraActionPreview.Stop();
        }
    }
    private void OnDisable() => CameraActionPreview.Stop();
}

[CustomEditor(typeof(CameraPresentation))]
public sealed class CameraPresentationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var rig = (CameraPresentation)target;
        EditorGUILayout.HelpBox("Camera Present 只执行动作和合成输出。相机、机位及参数由 Battle 桥接从场景配置绑定；编辑模式尚未绑定是正常状态。", MessageType.Info);
        if (GUILayout.Button("查看场景相机配置")) Selection.activeObject = CameraPresentationSetup.Bridge(rig).CameraScene;
    }
}

[CustomEditor(typeof(CameraTimelineAction))]
public sealed class CameraTimelineActionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Cue Key 选择场景配置中的参数。Shot / Focus 发 Arrived，回位后发 Settled。Focus 优先使用配置目标，其次时间线目标，最后场景默认焦点。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("预览镜头动作（自动回位）")) CameraActionPreview.Start((CameraTimelineAction)target);
            if (GUILayout.Button("停止预览并回位")) CameraActionPreview.Stop();
        }
    }
    private void OnDisable() => CameraActionPreview.Stop();
}

[CustomEditor(typeof(CameraOpeningTimelineAction))]
public sealed class CameraOpeningTimelineActionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var action = (CameraOpeningTimelineAction)target;
        EditorGUILayout.HelpBox("开场执行器：机位、时长、曲线和放行比例来自 BattleCamera 场景配置。NearlyArrived 继续释放 BattleStarted 检查点。", MessageType.Info);
        if (GUILayout.Button("查看场景相机配置")) Selection.activeObject = CameraPresentationSetup.Bridge(action.Source).CameraScene;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("将当前 Main Camera 机位保存为开场")) CameraPresentationSetup.CaptureOpening(action);
            if (GUILayout.Button("预览开场镜头")) CameraActionPreview.Start(action);
            if (GUILayout.Button("停止预览并回位")) CameraActionPreview.Stop();
        }
    }
    private void OnDisable() => CameraActionPreview.Stop();
}

// Edit-mode preview uses the same action/operation code and a private clock; no rules or global tween clocks run.
[InitializeOnLoad]
internal static class CameraActionPreview
{
    private static PresentationActionBehaviour action;
    private static PresentationScope scope;
    private static IEnumerator routine;
    private static IPresentationOperation operation;
    private static double lastTime;
    private static float delta;
    private sealed class Markers : IMarkerSink { public bool Emit(string marker) => true; }
    static CameraActionPreview()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.playModeStateChanged += _ => Stop();
        EditorApplication.quitting += Stop;
    }
    internal static void Start(CameraTimelineAction value) => Start(value, value != null ? value.Source : null);
    internal static void Start(CameraOpeningTimelineAction value) => Start(value, value != null ? value.Source : null);
    private static void Start(PresentationActionBehaviour value, CameraPresentation rig)
    {
        Stop();
        if (value == null || rig == null) return;
        CameraPresentationSetup.EnsureBound(rig);
        action = value; scope = new PresentationScope("camera-editor-preview");
        var resolver = new DictionaryTargetResolver();
        if (rig.Director != null)
            foreach (var binding in rig.Director.Targets)
                if (binding != null && !string.IsNullOrEmpty(binding.key)) resolver.Bind(binding.key, binding.target);
        var state = new BattleDisplaySnapshot("preview", "", 0, "", 0, null, null, null);
        var data = new BattleDisplayChange(0, DisplayCursor.Empty, state, "CameraPreview");
        value.DeltaTimeProvider = () => delta;
        value.StartPresentationRoutine = body => { routine = body; return () => routine = null; };
        lastTime = EditorApplication.timeSinceStartup;
        operation = value.Play(new ActionContext("preview", 0, "camera-preview", data, state, resolver, scope, "camera.focus"), new Markers());
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        delta = Mathf.Clamp((float)(EditorApplication.timeSinceStartup - lastTime), 0, .05f);
        lastTime = EditorApplication.timeSinceStartup;
        try
        {
            if (routine == null || !routine.MoveNext() || operation.Status != PresentationStatus.Pending)
            {
                if (operation?.Error != null) Debug.LogError(operation.Error.ToString());
                Stop();
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Stop(); }
        EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
    }
    internal static void Stop()
    {
        EditorApplication.update -= Tick;
        scope?.Dispose(); scope = null; routine = null; operation = null;
        if (action != null) { action.StartPresentationRoutine = null; action.DeltaTimeProvider = null; }
        action = null;
    }
}
