using System.Collections.Generic;
using System.Linq;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.Adventure
{
    public sealed partial class AdventureScreen
    {
        private static int SymbolIndex(AdventureNodeKind kind) => kind == AdventureNodeKind.Battle ? 0 :
            kind == AdventureNodeKind.Elite ? 1 : kind == AdventureNodeKind.Chest ? 2 : kind == AdventureNodeKind.Shop ? 3 : kind == AdventureNodeKind.Rest ? 4 : 5;
        private void Map()
        {
            var run = host.Adventure;
            var side = Box(page, "Route Legend", new Rect(0, .02f, .27f, .96f), Panel);
            Label(side, "七站旅程", new Rect(.09f, .83f, .82f, .12f), 37, Gold);
            Label(side, "由下向上，前往高亮地点。\n两处补给可选商店或休息。", new Rect(.09f, .67f, .82f, .13f), 23, Paper);
            Label(side, "已完成 " + run.VisitedNodeIds.Count + " / 7 站", new Rect(.09f, .57f, .82f, .07f), 26, Gold);
            var kinds = new[] { AdventureNodeKind.Battle, AdventureNodeKind.Elite, AdventureNodeKind.Chest, AdventureNodeKind.Shop, AdventureNodeKind.Rest, AdventureNodeKind.Boss };
            var captions = new[] { "普通战斗", "精英战斗", "宝箱", "商店", "休息", "首领战斗" };
            for (int i = 0; i < kinds.Length; i++)
            {
                int column = i % 2, row = i / 2;
                RitualSkin.Icon(side, captions[i], new Rect(.09f + column * .47f, .41f - row * .13f, .13f, .10f), RitualSkin.Symbol(SymbolIndex(kinds[i])));
                Label(side, captions[i], new Rect(.24f + column * .47f, .42f - row * .13f, .24f, .07f), 20, Paper);
            }
            Label(side, "金色：可前往    灰色：未解锁\n已完成路线会保留金色连线", new Rect(.09f, .025f, .82f, .09f), 18, Muted);
            var graph = Rect(page, "Route Map", new Rect(.29f, .015f, .70f, .97f));
            var positions = host.Definition.Nodes.ToDictionary(n => n.Id, n => new Vector2(.5f + (n.Column - 1) * .27f, .08f + n.Row * .137f));
            foreach (var node in host.Definition.Nodes)
            foreach (string nextId in node.NextIds)
            {
                if (!positions.TryGetValue(nextId, out var next)) continue;
                bool traveled = run.VisitedNodeIds.Contains(node.Id) && run.VisitedNodeIds.Contains(nextId);
                bool available = run.VisitedNodeIds.Contains(node.Id) && run.AvailableNodes.Any(n => n.Id == nextId);
                DottedPath(graph, positions[node.Id], next, traveled || available ? Gold : new Color32(67, 50, 38, 255));
            }
            foreach (var node in host.Definition.Nodes)
            {
                var at = positions[node.Id]; bool reachable = run.AvailableNodes.Any(n => n.Id == node.Id);
                bool visited = run.VisitedNodeIds.Contains(node.Id);
                var root = Rect(graph, NodeLabel(node), new Rect(at, Vector2.zero));
                root.sizeDelta = new Vector2(116, 84);
                var hitArea = root.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
                var button = root.gameObject.AddComponent<Button>();
                var image = RitualSkin.Icon(root, "Node Emblem", new Rect(.18f, .10f, .64f, .80f), RitualSkin.Symbol(SymbolIndex(node.Kind)));
                image.color = reachable || visited ? Color.white : new Color(.42f, .42f, .42f);
                button.targetGraphic = image;
                var colors = button.colors; colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.3f, 1.18f, .8f); colors.pressedColor = new Color(.75f, .65f, .45f);
                colors.disabledColor = Color.white; button.colors = colors;
                button.interactable = reachable;
                button.onClick.AddListener(() => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, node.Id)));
                if (reachable)
                {
                    var outline = image.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1, .82f, .35f, .85f); outline.effectDistance = new Vector2(2, -2);
                }
                MapCaption(root, NodeLabel(node), new Rect(.92f, .30f, 1.12f, .46f), 20, Paper);
                if (reachable || visited) MapCaption(root, reachable ? "可前往" : "已完成", new Rect(.92f, -.01f, 1.12f, .38f), 17, Gold);
            }
            Inventory(footer, new Rect(0, .12f, .64f, .80f), false);
            Label(footer, "选定地点后立即进入 · 每一步自动保存", new Rect(.66f, .30f, .34f, .45f), 20, Paper, TextAlignmentOptions.Right);
        }
        private void MapCaption(Transform parent, string text, Rect area, int size, Color color)
        {
            var label = Label(parent, text, area, size, color);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(1, -1);
        }
        private void DottedPath(RectTransform parent, Vector2 from, Vector2 to, Color color)
        {
            // Fixed square dots are resolution-independent native UI primitives, not stretched art.
            for (int i = 2; i < 15; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / 16f);
                var dot = Rect(parent, "Route Dot", new Rect(point, Vector2.zero));
                dot.sizeDelta = new Vector2(5, 5);
                var image = dot.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            }
        }
    }
}
