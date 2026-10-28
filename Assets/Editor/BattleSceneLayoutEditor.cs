using System;
using System.Linq;
using Poker.BattleHosting;
using Poker.BattlePresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleSceneLayoutSetup
{
    [MenuItem("工具/卡牌战斗/Scene Layout/配置当前场景站位")]
    public static void ConfigureCurrent()
    {
        if (Application.isPlaying || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Configure Layout in scene Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var adapters = roots.SelectMany(r => r.GetComponentsInChildren<BattleTimelineSceneAdapter>(true)).ToArray();
        if (adapters.Length != 1) throw new InvalidOperationException("Expected exactly one Battle bridge in the active scene.");
        var adapter = adapters[0];
        var layouts = roots.SelectMany(r => r.GetComponentsInChildren<BattleSceneLayout>(true)).ToArray();
        if (adapter.SceneLayout == null && layouts.Length > 1) throw new InvalidOperationException("Choose a Layout explicitly on the Battle bridge.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Configure Battle Scene Layout");
        try
        {
            var layout = adapter.SceneLayout != null ? adapter.SceneLayout : layouts.SingleOrDefault();
            if (layout == null)
            {
                var root = New("BattleLayout", null);
                SceneManager.MoveGameObjectToScene(root.gameObject, scene);
                layout = Undo.AddComponent<BattleSceneLayout>(root.gameObject);
                var players = New("Players", root); var enemies = New("Enemies", root);
                var hero = roots.FirstOrDefault(r => r.name == "Palamedes_Visual");
                var p1 = hero != null ? hero.transform.position : new Vector3(-.8f, 0, 0);
                var pRotation = hero != null ? hero.transform.rotation : Quaternion.identity;
                var playerSlots = new[] { Slot("P1", players, p1, pRotation), Slot("P2", players, p1 + Vector3.right * 1.5f, pRotation) };
                var enemySlots = new BattleLayoutSlot[5];
                string[] examples = { "BarrelCrab_Visual", "BarrelCrab_Visual (1)", "BarrelCrab_Visual (2)" };
                Vector3[] defaults = { new Vector3(.67f, 0, 4.54f), new Vector3(2.12f, 0, 4.55f), new Vector3(-.87f, 0, 4.35f),
                    new Vector3(3.5f, 0, 4.6f), new Vector3(-2.4f, 0, 4.2f) };
                for (int i = 0; i < enemySlots.Length; i++)
                {
                    var example = i < examples.Length ? roots.FirstOrDefault(r => r.name == examples[i]) : null;
                    enemySlots[i] = Slot("E" + (i + 1), enemies, example != null ? example.transform.position : defaults[i],
                        example != null ? example.transform.rotation : Quaternion.Euler(0, 210, 0));
                }
                layout.Configure(playerSlots, enemySlots); EditorUtility.SetDirty(layout);
            }
            layout.Validate();
            Undo.RecordObject(adapter, "Bind scene layout");
            adapter.ConfigureLayout(layout, adapter.LayoutOverrides.ToArray());
            Preview(adapter); // Validate the currently selected XML without starting the battle.
            EditorUtility.SetDirty(adapter); PrefabUtility.RecordPrefabInstancePropertyModifications(adapter);
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = layout.gameObject;
            Debug.Log("Scene Layout ready: P1-P2 / E1-E5. Read by Battle bridge; no actor parenting or animation writes.", layout);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    private static Transform New(string name, Transform parent)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create layout anchor");
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }
    private static BattleLayoutSlot Slot(string id, Transform parent, Vector3 position, Quaternion rotation)
    {
        var anchor = New(id, parent); anchor.SetPositionAndRotation(position, rotation);
        return new BattleLayoutSlot(id, anchor);
    }
    public static BattleLayoutBindings Preview(BattleTimelineSceneAdapter adapter)
    {
        var host = adapter.GetComponentInParent<BattleSceneHost>(true);
        if (host == null || !(host.SessionSource is IBattleSessionSource source)) throw new InvalidOperationException("Bind a Battle session source first.");
        var session = Application.isPlaying && host.Session != null ? host.Session : source.CreateSession();
        return BattleLayoutBindings.Resolve(adapter.SceneLayout,
            BattleDisplayProjector.Build(session.State, session.Engine, session.State.Duel.PlayerId), adapter.LayoutOverrides,
            Application.isPlaying ? adapter.LayoutBindings : null);
    }
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawLayout(BattleSceneLayout layout, GizmoType type)
    {
        foreach (var slot in layout.Players.Concat(layout.Enemies))
        {
            if (slot?.anchor == null) continue;
            Handles.color = layout.Players.Contains(slot) ? new Color(.25f, .8f, 1) : new Color(1, .45f, .25f);
            var t = slot.anchor;
            Handles.DrawWireDisc(t.position, Vector3.up, .35f);
            Handles.ArrowHandleCap(0, t.position, t.rotation, .65f, EventType.Repaint);
            Handles.Label(t.position + Vector3.up * .15f, slot.id);
        }
    }
}

[CustomEditor(typeof(BattleSceneLayout))]
public sealed class BattleSceneLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("场景静态站位数据：2 个玩家槽 / 5 个独立敌人槽。移动锚点调整基础位置，蓝色为玩家，橙色为敌人。角色无需挂在锚点下。", MessageType.Info);
        DrawDefaultInspector();
        try { ((BattleSceneLayout)target).Validate(); EditorGUILayout.HelpBox("Layout 配置有效", MessageType.Info); }
        catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
    }
}

[CustomEditor(typeof(BattleTimelineSceneAdapter))]
public sealed class BattleTimelineSceneAdapterEditor : Editor
{
    private BattleLayoutBindings preview;
    private string error;
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) { preview = null; error = null; }
        var adapter = (BattleTimelineSceneAdapter)target;
        EditorGUILayout.HelpBox("Scene Layout 由桥接读取。Layout Overrides 可指定 UnitId 的槽位，或 Owner Unit Id + Part Key 的部位归属。不会将静态锚点注册为动画目标。", MessageType.Info);
        using (new EditorGUI.DisabledScope(adapter.SceneLayout == null))
            if (GUILayout.Button("校验并预览当前对局站位"))
            {
                try { preview = BattleSceneLayoutSetup.Preview(adapter); error = null; }
                catch (Exception exception) { preview = null; error = exception.Message; }
            }
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
        var bindings = Application.isPlaying ? adapter.LayoutBindings : preview;
        if (bindings == null) return;
        foreach (var unit in bindings.Units.Values)
            EditorGUILayout.LabelField(unit.UnitId, (unit.SlotId ?? "无占位") + (unit.IsPart ? " / " + unit.RootUnitId + ":" + unit.PartKey : "") +
                (unit.OccupiesSlot ? "" : "（已释放）"));
    }
}
