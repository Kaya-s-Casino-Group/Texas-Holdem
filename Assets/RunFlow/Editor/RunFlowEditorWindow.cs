using System;
using System.IO;
using System.Linq;
using Poker.EditorTools;
using PokerEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Poker.RunFlow.Editor
{
    public sealed partial class RunFlowEditorWindow : EditorWindow
    {
        [SerializeField] private RunFlowAsset asset;
        [SerializeField] private int floorIndex;
        [SerializeField] private string selectedNode;
        [SerializeField] private Vector2 pan;
        [SerializeField] private float zoom = 1;
        private Vector2 floorScroll, inspectorScroll;
        private string status, connectionSource, draggingNode;
        private Vector2 dragOrigin, dragNodeOrigin;
        private int dragUndoGroup;
        private bool panning;
        private Rect graphRect;
        private readonly ToolWindowChrome chrome = new ToolWindowChrome();
        private RunFlowFloorData Floor => asset != null && floorIndex >= 0 && floorIndex < asset.Floors.Count ? asset.Floors[floorIndex] : null;
        private RunFlowNodeData Selected => Floor == null ? null : Floor.Nodes.FirstOrDefault(n => n.Id == selectedNode);

        [MenuItem("Poker/Run Flow Editor")]
        public static void ShowWindow() => Open(Selection.activeObject as RunFlowAsset);
        public static RunFlowEditorWindow Open(RunFlowAsset target)
        {
            var window = GetWindow<RunFlowEditorWindow>(); window.titleContent = new GUIContent("Run 流程编辑器");
            window.minSize = new Vector2(900, 500);
            if (target != null) window.SetAsset(target);
            window.Show(); window.Focus(); return window;
        }
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var target = EditorUtility.InstanceIDToObject(instanceId) as RunFlowAsset;
            if (target == null) return false; Open(target); return true;
        }
        [MenuItem("Poker/Run Flow/Create Four Floor Example")]
        public static void CreateExample()
        {
            var target = CreateInstance<RunFlowAsset>(); target.CopyFrom(ExampleRunContent.CreateFourFloors().Definition);
            foreach (var floor in target.Floors) RunFlowEditing.AutoLayout(target, floor);
            SaveNew(target, "RunFlow_ThreeFloorsAndBoss.asset");
        }
        private static void CreateEmpty()
        {
            var target = CreateInstance<RunFlowAsset>(); var floor = RunFlowEditing.AddFloor(target);
            RunFlowEditing.AddNode(target, floor, RunNodeKind.Encounter, new Vector2(60, 80)); SaveNew(target, "RunFlow.asset");
        }
        private static void SaveNew(RunFlowAsset target, string fileName)
        {
            const string directory = "Assets/RunFlow/Configs";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            var path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + fileName);
            AssetDatabase.CreateAsset(target, path); AssetDatabase.SaveAssetIfDirty(target);
            Undo.ClearUndo(target); Selection.activeObject = target; EditorGUIUtility.PingObject(target);
            var window = Open(target); window.position = new Rect(window.position.x, window.position.y, 1320, 720);
            window.FitGraph();
        }
        private void OnEnable() { Undo.undoRedoPerformed += OnUndo; }
        private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; chrome.Release(); }
        private void OnUndo() { status = null; connectionSource = null; draggingNode = null; Repaint(); }
        private void SetAsset(RunFlowAsset target)
        { asset = target; floorIndex = 0; selectedNode = null; status = null; connectionSource = null; pan = Vector2.zero; zoom = 1; }
        private void OnGUI()
        {
            DrawToolbar();
            var body = new Rect(0, 25, position.width, position.height - 25 - ToolWindowChrome.FooterHeight - 25);
            if (asset == null)
            {
                GUILayout.Space(40); GUILayout.Label("先选择或新建一份 Run 流程配置。", EditorStyles.boldLabel);
                GUILayout.Label("每层编辑一棵节点树；整局规则与各层流程一起保存为 .asset。", EditorStyles.wordWrappedLabel);
            }
            else
            {
                var left = new Rect(body.x, body.y, 200, body.height);
                var right = new Rect(body.xMax - 300, body.y, 300, body.height);
                graphRect = new Rect(left.xMax + 1, body.y, Mathf.Max(1, right.x - left.xMax - 2), body.height);
                DrawFloors(left); DrawInspector(right); DrawGraph(graphRect);
            }
            string message = status;
            var footer = new Rect(8, position.height - ToolWindowChrome.FooterHeight - 23, position.width - 16, 23);
            GUI.Label(footer, message ?? "楼层按列表顺序推进 · 节点右侧圆点连到目标左侧 · 中键平移 / 滚轮缩放 · Ctrl/Cmd+Z 撤销", EditorStyles.miniLabel);
            chrome.Draw(this);
        }
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(25));
            EditorGUI.BeginChangeCheck();
            var next = (RunFlowAsset)EditorGUILayout.ObjectField(asset, typeof(RunFlowAsset), false, GUILayout.Width(260));
            if (EditorGUI.EndChangeCheck()) SetAsset(next);
            {
                if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(48))) { CreateEmpty(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("三层 + Boss 示例", EditorStyles.toolbarButton, GUILayout.Width(125))) { CreateExample(); GUIUtility.ExitGUI(); }
                using (new EditorGUI.DisabledScope(asset == null))
                {
                    if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(48))) ValidateAsset();
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    {
                        AssetDatabase.SaveAssetIfDirty(asset); ValidateAsset();
                        status = "已保存 " + AssetDatabase.GetAssetPath(asset) + (status == null ? "" : " · " + status);
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (asset != null) GUILayout.Label(asset.Floors.Count + " 层" + (EditorUtility.IsDirty(asset) ? " · 未保存" : ""), EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }
        private void ValidateAsset()
        { status = RunFlowEditing.Validate(asset) ?? "流程校验通过；商品和小丑 ID 将在绑定内容目录时校验。"; }
        private void DrawFloors(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox); floorScroll = EditorGUILayout.BeginScrollView(floorScroll);
            if (GUILayout.Button("整局规则", floorIndex < 0 ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
            { floorIndex = -1; selectedNode = null; }
            GUILayout.Space(8); GUILayout.Label("楼层顺序", EditorStyles.boldLabel);
            for (int i = 0; i < asset.Floors.Count; i++)
            {
                var floor = asset.Floors[i]; var previous = GUI.backgroundColor;
                if (floorIndex == i) GUI.backgroundColor = new Color(0.45f, 0.72f, 1);
                if (GUILayout.Button((i + 1) + ". " + floor.DisplayName + (floor.Kind == RunFloorKind.Boss ? " [Boss]" : ""), GUILayout.Height(30)))
                { floorIndex = i; selectedNode = null; connectionSource = null; FitGraph(); }
                GUI.backgroundColor = previous;
            }
            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(asset.Floors.Count >= 32))
                if (GUILayout.Button("+ 添加一层")) { RunFlowEditing.AddFloor(asset); floorIndex = asset.Floors.Count - 1; selectedNode = null; }
            if (Floor != null)
            {
                GUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(floorIndex == 0))
                    if (GUILayout.Button("上移")) { RunFlowEditing.MoveFloor(asset, floorIndex, -1); floorIndex--; }
                using (new EditorGUI.DisabledScope(floorIndex == asset.Floors.Count - 1))
                    if (GUILayout.Button("下移")) { RunFlowEditing.MoveFloor(asset, floorIndex, 1); floorIndex++; }
                GUILayout.EndHorizontal();
                if (GUILayout.Button("删除此层"))
                { RunFlowEditing.Change(asset, "删除楼层", () => asset.Floors.RemoveAt(floorIndex)); floorIndex = Mathf.Min(floorIndex, asset.Floors.Count - 1); selectedNode = null; GUIUtility.ExitGUI(); }
            }
            GUILayout.Space(14); GUILayout.Label("走完本层路线终点后\n进入下一层。\n最后一层完成即通关。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }

    [CustomEditor(typeof(RunFlowAsset))]
    public sealed class RunFlowAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var flow = (RunFlowAsset)target;
            GUILayout.Label(flow.Floors.Count + " 层 · " + flow.Floors.Sum(f => f.Nodes.Count) + " 个节点", EditorStyles.boldLabel);
            if (GUILayout.Button("打开 Run 流程编辑器", GUILayout.Height(30))) RunFlowEditorWindow.Open(flow);
            var error = RunFlowEditing.Validate(flow);
            EditorGUILayout.HelpBox(error ?? "流程结构有效。开局时通过 BuildDefinition / BuildContent 转换为 GameCore 配置。",
                error == null ? MessageType.Info : MessageType.Error);
        }
    }
}
