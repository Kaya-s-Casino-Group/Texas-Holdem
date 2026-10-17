using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.RunFlow.Editor
{
    /// <summary>Undoable draft editing. Invalid intermediate graphs may be saved; compilation rejects them.</summary>
    public static class RunFlowEditing
    {
        public static void Change(RunFlowAsset asset, string label, Action action)
        { Undo.RecordObject(asset, label); action(); EditorUtility.SetDirty(asset); }
        public static RunFlowFloorData AddFloor(RunFlowAsset asset)
        {
            var floor = new RunFlowFloorData { Id = "floor-" + Guid.NewGuid().ToString("N"), DisplayName = "第 " + (asset.Floors.Count + 1) + " 层" };
            Change(asset, "添加楼层", () => asset.Floors.Add(floor)); return floor;
        }
        public static void MoveFloor(RunFlowAsset asset, int index, int offset)
        {
            int next = index + offset;
            if (index < 0 || index >= asset.Floors.Count || next < 0 || next >= asset.Floors.Count) return;
            Change(asset, "调整楼层顺序", () => { var floor = asset.Floors[index]; asset.Floors.RemoveAt(index); asset.Floors.Insert(next, floor); });
        }
        public static RunFlowNodeData AddNode(RunFlowAsset asset, RunFlowFloorData floor, RunNodeKind kind, Vector2 position)
        {
            var node = new RunFlowNodeData { Id = "node-" + Guid.NewGuid().ToString("N"), DisplayName = KindName(kind), Kind = kind, Position = position };
            Change(asset, "添加节点", () => { floor.Nodes.Add(node); if (floor.Nodes.Count == 1) floor.EntryNodeId = node.Id; }); return node;
        }
        public static void RemoveNode(RunFlowAsset asset, RunFlowFloorData floor, RunFlowNodeData node)
        {
            Change(asset, "删除节点", () => {
                floor.Nodes.Remove(node); foreach (var n in floor.Nodes) n.NextNodeIds.RemoveAll(id => id == node.Id);
                if (floor.EntryNodeId == node.Id) floor.EntryNodeId = null;
            });
        }
        public static bool CanConnect(RunFlowFloorData floor, string sourceId, string targetId)
        {
            var source = floor.Nodes.FirstOrDefault(n => n.Id == sourceId); var target = floor.Nodes.FirstOrDefault(n => n.Id == targetId);
            if (source == null || target == null || source == target || source.NextNodeIds.Contains(targetId)) return false;
            var visited = new HashSet<string>(); var pending = new Stack<string>(); pending.Push(targetId);
            while (pending.Count > 0)
            {
                string id = pending.Pop(); if (id == sourceId) return false;
                if (!visited.Add(id)) continue;
                var node = floor.Nodes.FirstOrDefault(n => n.Id == id);
                if (node != null) foreach (var next in node.NextNodeIds) pending.Push(next);
            }
            return true;
        }
        public static bool Connect(RunFlowAsset asset, RunFlowFloorData floor, string source, string target)
        {
            if (!CanConnect(floor, source, target)) return false;
            Change(asset, "连接节点", () => floor.Nodes.First(n => n.Id == source).NextNodeIds.Add(target)); return true;
        }
        public static void AutoLayout(RunFlowAsset asset, RunFlowFloorData floor)
        {
            // Kahn layering also lays out disconnected drafts, without changing logical node order.
            var depth = floor.Nodes.ToDictionary(n => n.Id, n => 0);
            var incoming = floor.Nodes.ToDictionary(n => n.Id, n => 0);
            foreach (var node in floor.Nodes) foreach (var id in node.NextNodeIds) if (incoming.ContainsKey(id)) incoming[id]++;
            var queue = new Queue<string>(floor.Nodes.Where(n => incoming[n.Id] == 0).Select(n => n.Id));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); var source = floor.Nodes.First(n => n.Id == current);
                foreach (var id in source.NextNodeIds.Where(incoming.ContainsKey))
                { depth[id] = Math.Max(depth[id], depth[source.Id] + 1); if (--incoming[id] == 0) queue.Enqueue(id); }
            }
            var lanes = floor.Nodes.ToDictionary(n => n.Id, n => int.MaxValue);
            var occupied = new Dictionary<int, HashSet<int>>();
            foreach (var node in floor.Nodes) incoming[node.Id] = 0;
            foreach (var node in floor.Nodes) foreach (var id in node.NextNodeIds) if (incoming.ContainsKey(id)) incoming[id]++;
            queue = new Queue<string>(); int rootRow = 0;
            foreach (var node in floor.Nodes.Where(n => incoming[n.Id] == 0)) { lanes[node.Id] = rootRow++; queue.Enqueue(node.Id); }
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); var node = floor.Nodes.First(n => n.Id == current);
                if (!occupied.ContainsKey(depth[current])) occupied[depth[current]] = new HashSet<int>();
                while (!occupied[depth[current]].Add(lanes[current])) lanes[current]++;
                for (int i = 0; i < node.NextNodeIds.Count; i++)
                {
                    string id = node.NextNodeIds[i]; if (!incoming.ContainsKey(id)) continue;
                    lanes[id] = Math.Min(lanes[id], lanes[current] + i);
                    if (--incoming[id] == 0) queue.Enqueue(id);
                }
            }
            Change(asset, "自动排列节点", () => {
                foreach (var column in floor.Nodes.GroupBy(n => depth[n.Id]))
                {
                    if (!occupied.ContainsKey(column.Key)) occupied[column.Key] = new HashSet<int>();
                    foreach (var node in column)
                    {
                        int row = lanes[node.Id];
                        if (row == int.MaxValue) { row = 0; while (!occupied[column.Key].Add(row)) row++; }
                        node.Position = new Vector2(50 + column.Key * 260, 60 + row * 140);
                    }
                }
            });
        }
        public static string KindName(RunNodeKind kind)
        {
            switch (kind) {
                case RunNodeKind.Encounter: return "普通战斗"; case RunNodeKind.Elite: return "精英战斗";
                case RunNodeKind.Boss: return "Boss 战斗"; case RunNodeKind.Reward: return "奖励"; case RunNodeKind.Shop: return "商店";
                default: return "未知节点";
            }
        }
        public static string Validate(RunFlowAsset asset)
        {
            try { asset.BuildDefinition(ScoreProfile.Default); return null; }
            catch (ArgumentException error) { return Friendly(error.Message); }
        }
        private static string Friendly(string message) => message
            .Replace("Unreachable node in tree.", "有节点无法从入口到达，请补充连接或删除。")
            .Replace("Unresolved node tree reference.", "缺少有效入口，或连线指向不存在的节点。")
            .Replace("Node trees cannot contain cycles.", "节点之间存在循环，请移除循环连线。")
            .Replace("Invalid node tree size or duplicate identities.", "每层需要 1–500 个节点，且节点 ID 不能重复。")
            .Replace("Invalid encounter stages.", "每层最多 100 个战斗，且战斗 ID 不能重复。")
            .Replace("Invalid stage.", "检查目标分数、回合上限、通关金币和能量。")
            .Replace("Invalid floors or duplicate floor identities.", "需要 1–32 层，且楼层 ID 不能重复。")
            .Replace("Invalid run definition.", "检查整局数值、默认商品池和奖励池。")
            .Replace("Invalid node offer pool.", "商品/奖励池不能包含空白或重复 ID。");
    }
}
