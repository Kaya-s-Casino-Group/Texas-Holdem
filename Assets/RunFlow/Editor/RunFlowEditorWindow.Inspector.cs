using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.RunFlow.Editor
{
    public sealed partial class RunFlowEditorWindow
    {
        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox); inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            EditorGUI.BeginChangeCheck(); Undo.RecordObject(asset, "修改流程参数");
            if (floorIndex < 0) DrawRunSettings();
            else if (Floor != null)
            {
                if (Selected == null) DrawFloorSettings(); else DrawNodeSettings(Selected);
            }
            if (EditorGUI.EndChangeCheck()) { EditorUtility.SetDirty(asset); status = null; Repaint(); }
            EditorGUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void DrawRunSettings()
        {
            GUILayout.Label("整局规则", EditorStyles.boldLabel);
            asset.RulesVersion = EditorGUILayout.TextField(new GUIContent("规则版本", "运行存档必须匹配此版本。"), asset.RulesVersion);
            asset.StartingGold = EditorGUILayout.LongField("起始金币", asset.StartingGold);
            asset.JokerCapacity = EditorGUILayout.IntField("小丑槽位", asset.JokerCapacity);
            asset.RerollCost = EditorGUILayout.LongField("商店刷新价格", asset.RerollCost);
            asset.OfferCount = EditorGUILayout.IntField("每批报价数量", asset.OfferCount);
            DrawIds("初始小丑 ID", asset.StartingJokers);
            DrawIds("默认商品 ID", asset.ShopPool); DrawIds("默认奖励 ID", asset.RewardPool);
        }
        private void DrawFloorSettings()
        {
            GUILayout.Label("楼层设置", EditorStyles.boldLabel);
            Floor.DisplayName = EditorGUILayout.TextField("楼层名称", Floor.DisplayName);
            Floor.Kind = (RunFloorKind)(EditorGUILayout.Popup("楼层类型", (int)Floor.Kind - 1, new[] { "普通层", "Boss 层" }) + 1);
            Floor.TreeVersion = EditorGUILayout.TextField("节点树版本", Floor.TreeVersion);
            var choices = new[] { "未设置" }.Concat(Floor.Nodes.Select(n => n.DisplayName)).ToArray();
            int entry = Floor.Nodes.FindIndex(n => n.Id == Floor.EntryNodeId) + 1;
            int next = EditorGUILayout.Popup("入口节点", entry, choices);
            if (next != entry) Floor.EntryNodeId = next == 0 ? null : Floor.Nodes[next - 1].Id;
            EditorGUILayout.LabelField("节点数量", Floor.Nodes.Count.ToString());
            GUILayout.Space(10); EditorGUILayout.HelpBox("点击画布中的节点修改参数。奖励与商店可使用独立的内容池。", MessageType.Info);
        }
        private void DrawNodeSettings(RunFlowNodeData node)
        {
            if (GUILayout.Button("← 楼层设置")) { selectedNode = null; GUIUtility.ExitGUI(); }
            GUILayout.Space(8); GUILayout.Label("节点参数", EditorStyles.boldLabel);
            node.DisplayName = EditorGUILayout.TextField("节点名称", node.DisplayName);
            node.Kind = (RunNodeKind)(EditorGUILayout.Popup("节点类型", (int)node.Kind - 1,
                Enum.GetValues(typeof(RunNodeKind)).Cast<RunNodeKind>().Select(RunFlowEditing.KindName).ToArray()) + 1);
            if (node.Kind == RunNodeKind.Encounter || node.Kind == RunNodeKind.Elite || node.Kind == RunNodeKind.Boss)
            {
                node.TargetScore = EditorGUILayout.LongField("目标分数", node.TargetScore);
                node.RoundLimit = EditorGUILayout.IntField("回合上限", node.RoundLimit);
                node.ClearGold = EditorGUILayout.LongField("通关金币", node.ClearGold);
                node.Energy = EditorGUILayout.IntField("初始能量", node.Energy);
            }
            else
            {
                EditorGUILayout.HelpBox("留空则使用整局的默认内容池。", MessageType.Info);
                DrawIds(node.Kind == RunNodeKind.Shop ? "商品 ID" : "奖励 ID", node.OfferPool);
            }
            GUILayout.Space(8);
            if (Floor.EntryNodeId == node.Id) GUILayout.Label("✓ 本层入口", EditorStyles.boldLabel);
            else if (GUILayout.Button("设为本层入口")) RunFlowEditing.Change(asset, "修改入口节点", () => Floor.EntryNodeId = node.Id);
            GUILayout.Space(10); GUILayout.Label("后继节点", EditorStyles.boldLabel);
            for (int i = 0; i < node.NextNodeIds.Count; i++)
            {
                int index = i; var next = Floor.Nodes.FirstOrDefault(n => n.Id == node.NextNodeIds[i]);
                GUILayout.BeginHorizontal(); GUILayout.Label(next == null ? "缺失节点" : next.DisplayName, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("移除", GUILayout.Width(46)))
                { RunFlowEditing.Change(asset, "删除连接", () => node.NextNodeIds.RemoveAt(index)); GUIUtility.ExitGUI(); }
                GUILayout.EndHorizontal();
            }
            if (node.NextNodeIds.Count == 0) EditorGUILayout.HelpBox("本层路线终点：完成后进入下一层，最后一层则结束整局。", MessageType.Info);
            GUILayout.Space(12);
            if (GUILayout.Button("删除节点及其连线"))
            { RunFlowEditing.RemoveNode(asset, Floor, node); selectedNode = null; connectionSource = null; GUIUtility.ExitGUI(); }
        }
        private static void DrawIds(string label, List<string> values)
        {
            GUILayout.Space(8); GUILayout.Label(label, EditorStyles.boldLabel);
            for (int i = 0; i < values.Count; i++)
            {
                GUILayout.BeginHorizontal(); values[i] = EditorGUILayout.TextField(values[i]);
                if (GUILayout.Button("−", GUILayout.Width(25))) { values.RemoveAt(i); GUI.changed = true; GUILayout.EndHorizontal(); break; }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ 添加", EditorStyles.miniButton)) { values.Add(""); GUI.changed = true; }
        }
    }
}
