using System;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.RunFlow.Editor
{
    public sealed partial class RunFlowEditorWindow
    {
        private static readonly Vector2 NodeSize = new Vector2(190, 100);
        private static Color NodeColor(RunNodeKind kind)
        {
            switch (kind) {
                case RunNodeKind.Elite: return new Color(0.72f, 0.43f, 0.20f);
                case RunNodeKind.Boss: return new Color(0.68f, 0.25f, 0.28f);
                case RunNodeKind.Reward: return new Color(0.35f, 0.55f, 0.33f);
                case RunNodeKind.Shop: return new Color(0.37f, 0.44f, 0.67f);
                default: return new Color(0.28f, 0.49f, 0.57f);
            }
        }
        private Vector2 CanvasPoint(Vector2 world) => world * zoom + pan;
        private Vector2 WorldPoint(Vector2 canvas) => (canvas - pan) / zoom;
        private Rect NodeRect(RunFlowNodeData node) => new Rect(CanvasPoint(node.Position), NodeSize * zoom);
        private Vector2 InputPoint(RunFlowNodeData node) => NodeRect(node).position + new Vector2(0, 43 * zoom);
        private Vector2 OutputPoint(RunFlowNodeData node) => NodeRect(node).position + new Vector2(NodeSize.x * zoom, 43 * zoom);
        private void DrawGraph(Rect rect)
        {
            GUI.BeginGroup(rect); EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), new Color(0.12f, 0.13f, 0.15f));
            if (Floor == null)
            {
                GUI.Label(new Rect(24, 70, rect.width - 48, 80), "选择左侧楼层查看节点树。\n整局规则在右侧编辑。", EditorStyles.wordWrappedLabel);
                GUI.EndGroup(); return;
            }
            const float toolbarHeight = 30;
            GUILayout.BeginArea(new Rect(0, 0, rect.width, toolbarHeight)); GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("+ 节点", EditorStyles.toolbarDropDown, GUILayout.Width(80)))
            {
                var menu = new GenericMenu(); var center = WorldPoint(new Vector2(rect.width * 0.4f, rect.height * 0.4f));
                foreach (RunNodeKind kind in Enum.GetValues(typeof(RunNodeKind)))
                {
                    var selectedKind = kind;
                    menu.AddItem(new GUIContent(RunFlowEditing.KindName(kind)), false, () => {
                        var node = RunFlowEditing.AddNode(asset, Floor, selectedKind, center); selectedNode = node.Id; status = null; Repaint();
                    });
                }
                menu.ShowAsContext();
            }
            if (GUILayout.Button("自动排列", EditorStyles.toolbarButton, GUILayout.Width(70)))
            { RunFlowEditing.AutoLayout(asset, Floor); FitGraph(); status = null; }
            if (GUILayout.Button("适配", EditorStyles.toolbarButton, GUILayout.Width(45))) FitGraph();
            GUILayout.FlexibleSpace(); GUILayout.Label(Mathf.RoundToInt(zoom * 100) + "%", EditorStyles.miniLabel);
            GUILayout.EndHorizontal(); GUILayout.EndArea();

            if (Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                float grid = 30 * zoom; Handles.color = new Color(0.22f, 0.24f, 0.27f);
                for (float x = pan.x % grid; x < rect.width; x += grid) Handles.DrawLine(new Vector3(x, toolbarHeight), new Vector3(x, rect.height));
                for (float y = pan.y % grid; y < rect.height; y += grid) if (y >= toolbarHeight) Handles.DrawLine(new Vector3(0, y), new Vector3(rect.width, y));
                foreach (var source in Floor.Nodes)
                foreach (var next in source.NextNodeIds)
                {
                    var target = Floor.Nodes.FirstOrDefault(n => n.Id == next); if (target == null) continue;
                    DrawConnection(OutputPoint(source), InputPoint(target), new Color(0.62f, 0.68f, 0.74f));
                }
                if (connectionSource != null)
                {
                    var source = Floor.Nodes.FirstOrDefault(n => n.Id == connectionSource);
                    if (source != null) DrawConnection(OutputPoint(source), Event.current.mousePosition, new Color(1, 0.76f, 0.3f));
                }
                Handles.EndGUI();
            }
            foreach (var node in Floor.Nodes) DrawNode(node);
            HandleGraphInput(new Rect(0, toolbarHeight, rect.width, rect.height - toolbarHeight));
            GUI.EndGroup();
        }
        private static void DrawConnection(Vector2 start, Vector2 end, Color color)
        {
            float tangent = Mathf.Max(45, Mathf.Abs(end.x - start.x) * 0.45f);
            Handles.DrawBezier(start, end, start + Vector2.right * tangent, end - Vector2.right * tangent, color, null, 2);
            Handles.color = color;
            Handles.DrawAAConvexPolygon(end, end + new Vector2(-9, -4), end + new Vector2(-9, 4));
        }
        private void DrawNode(RunFlowNodeData node)
        {
            var rect = NodeRect(node); var color = NodeColor(node.Kind);
            if (node.Id == selectedNode) EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(1, 0.77f, 0.35f));
            EditorGUI.DrawRect(rect, new Color(0.21f, 0.23f, 0.26f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 28 * zoom), color);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = Mathf.Clamp(Mathf.RoundToInt(12 * zoom), 9, 13), clipping = TextClipping.Clip };
            var text = new GUIStyle(EditorStyles.label) { fontSize = Mathf.Clamp(Mathf.RoundToInt(11 * zoom), 9, 12), clipping = TextClipping.Clip };
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, 22 * zoom), node.DisplayName, title);
            GUI.Label(new Rect(rect.x + 8, rect.y + 31 * zoom, rect.width - 16, 22 * zoom), RunFlowEditing.KindName(node.Kind), text);
            string summary = node.Kind == RunNodeKind.Encounter || node.Kind == RunNodeKind.Elite || node.Kind == RunNodeKind.Boss
                ? "目标 " + node.TargetScore + "  /  " + node.RoundLimit + " 回合"
                : node.OfferPool.Count == 0 ? "默认内容池" : node.OfferPool.Count + " 项独立内容";
            GUI.Label(new Rect(rect.x + 8, rect.y + 55 * zoom, rect.width - 16, 21 * zoom), summary, text);
            GUI.Label(new Rect(rect.x + 8, rect.y + 78 * zoom, rect.width - 16, 20 * zoom),
                Floor.EntryNodeId == node.Id ? "▶ 本层入口" : node.NextNodeIds.Count == 0 ? "本层终点" : node.NextNodeIds.Count + " 个后继", text);
            if (Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI(); Handles.color = color * 1.4f;
                Handles.DrawSolidDisc(InputPoint(node), Vector3.forward, 6); Handles.DrawSolidDisc(OutputPoint(node), Vector3.forward, 6);
                Handles.EndGUI();
            }
        }
        private void HandleGraphInput(Rect area)
        {
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            { connectionSource = null; draggingNode = null; panning = false; evt.Use(); Repaint(); return; }
            if (evt.type == EventType.ScrollWheel && area.Contains(evt.mousePosition))
            {
                var world = WorldPoint(evt.mousePosition); zoom = Mathf.Clamp(zoom * Mathf.Pow(1.08f, -evt.delta.y), 0.25f, 1.5f);
                pan = evt.mousePosition - world * zoom; evt.Use(); Repaint(); return;
            }
            if (evt.type == EventType.MouseDown && area.Contains(evt.mousePosition))
            {
                GUIUtility.keyboardControl = 0;
                if (evt.button == 2) { panning = true; dragOrigin = evt.mousePosition; evt.Use(); return; }
                if (evt.button != 0) return;
                var output = Floor.Nodes.LastOrDefault(n => Vector2.Distance(OutputPoint(n), evt.mousePosition) < 12);
                if (output != null) { connectionSource = output.Id; selectedNode = output.Id; evt.Use(); Repaint(); return; }
                var input = Floor.Nodes.LastOrDefault(n => Vector2.Distance(InputPoint(n), evt.mousePosition) < 12);
                if (input != null && connectionSource != null) { TryConnect(input); evt.Use(); return; }
                var hit = Floor.Nodes.LastOrDefault(n => NodeRect(n).Contains(evt.mousePosition));
                if (hit != null)
                {
                    if (connectionSource != null) TryConnect(hit);
                    else
                    {
                        selectedNode = hit.Id; draggingNode = hit.Id; dragOrigin = evt.mousePosition; dragNodeOrigin = hit.Position;
                        Undo.IncrementCurrentGroup(); dragUndoGroup = Undo.GetCurrentGroup();
                    }
                }
                else { selectedNode = null; connectionSource = null; }
                evt.Use(); Repaint(); return;
            }
            if (evt.type == EventType.MouseDrag)
            {
                if (panning) { pan += evt.mousePosition - dragOrigin; dragOrigin = evt.mousePosition; evt.Use(); Repaint(); }
                else if (draggingNode != null)
                {
                    var node = Floor.Nodes.FirstOrDefault(n => n.Id == draggingNode);
                    if (node != null)
                    { Undo.RecordObject(asset, "移动节点"); node.Position = dragNodeOrigin + (evt.mousePosition - dragOrigin) / zoom; EditorUtility.SetDirty(asset); }
                    evt.Use(); Repaint();
                }
                else if (connectionSource != null) { evt.Use(); Repaint(); }
            }
            if (evt.type == EventType.MouseUp)
            {
                if (draggingNode != null) { Undo.CollapseUndoOperations(dragUndoGroup); draggingNode = null; evt.Use(); }
                if (panning) { panning = false; evt.Use(); }
                if (connectionSource != null)
                {
                    var target = Floor.Nodes.LastOrDefault(n => n.Id != connectionSource &&
                        (NodeRect(n).Contains(evt.mousePosition) || Vector2.Distance(InputPoint(n), evt.mousePosition) < 12));
                    if (target != null) { TryConnect(target); evt.Use(); }
                }
                Repaint();
            }
        }
        private void TryConnect(RunFlowNodeData target)
        {
            bool accepted = RunFlowEditing.Connect(asset, Floor, connectionSource, target.Id); connectionSource = null;
            status = accepted ? null : "无法连接：连线重复、目标无效或会形成循环。"; Repaint();
        }
        private void FitGraph()
        {
            if (Floor == null || Floor.Nodes.Count == 0) { pan = Vector2.zero; zoom = 1; return; }
            var min = new Vector2(Floor.Nodes.Min(n => n.Position.x), Floor.Nodes.Min(n => n.Position.y));
            var max = new Vector2(Floor.Nodes.Max(n => n.Position.x), Floor.Nodes.Max(n => n.Position.y)) + NodeSize;
            var size = max - min;
            float width = Mathf.Max(120, position.width - 540), height = Mathf.Max(120, position.height - 150);
            zoom = Mathf.Clamp(Mathf.Min(width / size.x, height / size.y), 0.25f, 1.15f);
            pan = new Vector2(20, 55) - min * zoom; Repaint();
        }
    }
}
