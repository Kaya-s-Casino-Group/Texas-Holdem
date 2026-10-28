using System;
using System.Linq;
using BattlePresentation.Unity;
using Poker.BattlePresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authors scene data and explicitly wires the Battle bridge. Runtime does not search the scene.</summary>
public static class CameraPresentationSetup
{
    public static BattleTimelineSceneAdapter Bridge(CameraPresentation executor)
    {
        var bridge = executor != null ? executor.GetComponentInParent<BattleTimelineSceneAdapter>(true) : null;
        if (bridge == null || bridge.CameraPresent != executor) throw new InvalidOperationException("Bind this executor on the Battle bridge.");
        return bridge;
    }
    public static BattleTimelineSceneAdapter Bridge(CameraSceneConfiguration configuration)
    {
        var bridges = configuration.gameObject.scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<BattleTimelineSceneAdapter>(true)).Where(b => b.CameraScene == configuration).ToArray();
        if (bridges.Length != 1) throw new InvalidOperationException("Expected one Battle bridge referencing this scene camera.");
        return bridges[0];
    }
    public static void EnsureBound(CameraPresentation executor) => Bridge(executor).BindCameraPresentation();
    private static void RequireEditMode()
    {
        if (Application.isPlaying || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Author scene camera configuration in scene Edit Mode.");
    }
    public static void CaptureOverview(CameraPresentation executor) => Capture(Bridge(executor).CameraScene, false);
    public static void CaptureOpening(CameraOpeningTimelineAction action) => Capture(Bridge(action.Source).CameraScene, true);
    public static void Capture(CameraSceneConfiguration configuration, bool opening)
    {
        RequireEditMode(); CameraActionPreview.Stop(); configuration.Validate();
        var shot = opening ? configuration.Opening?.shot : configuration.Overview;
        if (shot == null) throw new InvalidOperationException("Create the scene shot before capturing.");
        var camera = configuration.OutputCamera;
        Undo.RecordObjects(new UnityEngine.Object[] { shot, shot.transform, camera, camera.transform }, "Capture scene camera shot");
        shot.Capture(camera);
        if (opening) RestoreOverview(configuration);
        foreach (var value in new UnityEngine.Object[] { shot, shot.transform, camera, camera.transform })
        { EditorUtility.SetDirty(value); PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
        EditorSceneManager.MarkSceneDirty(configuration.gameObject.scene);
    }
    public static void RestoreOverview(CameraSceneConfiguration configuration)
    {
        RequireEditMode(); CameraActionPreview.Stop(); configuration.Validate();
        var camera = configuration.OutputCamera; var shot = configuration.Overview;
        Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform }, "Restore scene overview");
        camera.transform.SetPositionAndRotation(shot.transform.position, shot.transform.rotation);
        camera.fieldOfView = shot.FieldOfView; camera.orthographicSize = shot.OrthographicSize;
        EditorUtility.SetDirty(camera); EditorUtility.SetDirty(camera.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera); PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
        EditorSceneManager.MarkSceneDirty(configuration.gameObject.scene);
    }
    private static BattleTimelineSceneAdapter CurrentBridge() => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<BattleTimelineSceneAdapter>(true)).Single();

    [MenuItem("工具/卡牌战斗/Camera Present/将当前相机更新为开场")]
    public static void UpdateOpeningCurrent() => Capture(CurrentBridge().CameraScene, true);

    [MenuItem("工具/卡牌战斗/Camera Present/以当前相机配置开场")]
    public static void ConfigureOpeningCurrent()
    {
        RequireEditMode(); var bridge = CurrentBridge(); var config = bridge.CameraScene; var executor = bridge.CameraPresent;
        if (config == null || executor == null) throw new InvalidOperationException("Prepare the scene camera first.");
        if (executor.GetComponentsInChildren<CameraOpeningTimelineAction>(true).Length != 0)
        { Selection.activeGameObject = config.gameObject; Debug.Log("Opening already configured; scene settings preserved."); return; }
        var director = bridge.Director;
        if (director.Timeline.Any(g => g != null && g.semanticKey == "BattleStarted"))
            throw new InvalidOperationException("BattleStarted already has a recipe. Integrate the opening checkpoint explicitly.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Configure scene opening camera");
        try
        {
            Undo.RecordObject(config, "Add opening camera settings");
            var shot = config.Opening?.shot;
            if (shot == null) { shot = Undo.AddComponent<CameraShot>(Child("Opening", config.Overview.transform.parent)); shot.Capture(config.OutputCamera); }
            var opening = config.Opening?.shot != null ? config.Opening : new CameraOpeningSettings { shot = shot };
            config.Configure(config.OutputCamera, config.Overview, config.DefaultFocus, opening, config.Cues.ToArray());
            var action = Undo.AddComponent<CameraOpeningTimelineAction>(Child("Opening", executor.transform.Find("Actions") ?? executor.transform));
            action.Configure(executor, shot);
            Undo.RecordObject(director, "Add opening checkpoint");
            director.ConfigureTimeline(director.Timeline.Concat(new[] { new SceneTimelineGroup {
                semanticKey = "BattleStarted", explicitCheckpoint = true,
                nodes = new[] { new SceneActionNode { id = "opening-camera", action = action, targetKey = "camera", blocking = true } },
                checkpoint = new[] { new SceneActionDependency { actionId = "opening-camera", kind = BattlePresentation.DependencyKind.Marker, marker = "NearlyArrived" } }
            } }).ToArray(), director.Targets.ToArray());
            bridge.BindCameraPresentation();
            EditorUtility.SetDirty(config); EditorUtility.SetDirty(director); EditorUtility.SetDirty(action);
            PrefabUtility.RecordPrefabInstancePropertyModifications(director);
            EditorSceneManager.MarkSceneDirty(config.gameObject.scene); Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = config.gameObject;
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    [MenuItem("工具/卡牌战斗/Camera Present/准备当前场景")]
    public static void SetupCurrent()
    {
        RequireEditMode(); var scene = SceneManager.GetActiveScene(); var bridge = CurrentBridge();
        if (bridge.CameraScene != null || bridge.CameraPresent != null)
        { bridge.ValidateCameraConfiguration(); Selection.activeGameObject = bridge.CameraScene.gameObject; return; }
        if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CameraPresentation>(true)).Any())
            throw new InvalidOperationException("An executor already exists. Bind its scene configuration explicitly on the Battle bridge.");
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
            .Single(c => c.CompareTag("MainCamera") && c.isActiveAndEnabled);
        if (bridge.Director.Targets.Any(t => t != null && t.key == "camera")) throw new InvalidOperationException("camera target key is already in use.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Prepare scene camera and executor");
        try
        {
            var root = Child("BattleCamera", null); SceneManager.MoveGameObjectToScene(root, scene);
            var shots = Child("Shots", root.transform);
            var overview = Undo.AddComponent<CameraShot>(Child("Overview", shots.transform)); overview.Capture(camera);
            var closeup = Undo.AddComponent<CameraShot>(Child("Closeup", shots.transform)); closeup.Capture(camera); closeup.transform.position += camera.transform.forward * .5f;
            var focus = Child("FocusPoint", root.transform).transform; focus.position = camera.transform.position + camera.transform.forward * 5;
            Undo.SetTransformParent(camera.transform, root.transform, "Keep camera world pose in scene data");
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            var config = Undo.AddComponent<CameraSceneConfiguration>(root);
            var cues = Enum.GetValues(typeof(CameraActionKind)).Cast<CameraActionKind>().Select(k => new CameraCueSettings {
                key = k.ToString().ToLowerInvariant(), kind = k, shot = k == CameraActionKind.Shot ? closeup : null }).ToArray();
            config.Configure(camera, overview, focus, null, cues);
            var executor = Undo.AddComponent<CameraPresentation>(Child("CameraPresent", bridge.transform));
            var actions = Child("Actions", executor.transform);
            foreach (var cue in cues)
            {
                var action = Undo.AddComponent<CameraTimelineAction>(Child(cue.kind.ToString(), actions.transform));
                action.Configure(executor, cue.kind); action.SetSceneCueKey(cue.key);
            }
            Undo.RecordObject(bridge, "Bind camera scene and execution"); bridge.ConfigureCamera(config, executor); bridge.BindCameraPresentation();
            Undo.RecordObject(bridge.Director, "Bind camera executor");
            bridge.Director.ConfigureTimeline(bridge.Director.Timeline.ToArray(), bridge.Director.Targets.Concat(new[] {
                new SceneTargetBinding { key = "camera", target = executor } }).ToArray());
            foreach (var value in new UnityEngine.Object[] { config, bridge, bridge.Director })
            { EditorUtility.SetDirty(value); PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group); Selection.activeGameObject = root;
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    private static GameObject Child(string name, Transform parent)
    { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create camera authoring object"); if (parent != null) go.transform.SetParent(parent, false); return go; }

    [MenuItem("工具/卡牌战斗/Camera Present/校验当前场景")]
    public static void ValidateCurrent()
    { var bridge = CurrentBridge(); bridge.ValidateCameraConfiguration(); Debug.Log("Scene camera configuration and Battle execution bindings are valid.", bridge.CameraScene); }
}
