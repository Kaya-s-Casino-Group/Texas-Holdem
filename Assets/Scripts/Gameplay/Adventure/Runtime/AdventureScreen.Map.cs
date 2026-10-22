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
            RitualSkin.Place(page, new Rect(0, 0, 1, 1));
            var side = DeskPanel(page, "Route Legend", DeskPixels(47, 149, 263, 367));
            Label(side, "七站旅程", new Rect(.07f, .84f, .86f, .11f), 27, Gold, TextAlignmentOptions.Center);
            var kinds = new[] { AdventureNodeKind.Battle, AdventureNodeKind.Elite, AdventureNodeKind.Chest, AdventureNodeKind.Shop, AdventureNodeKind.Rest, AdventureNodeKind.Boss };
            var captions = new[] { "普通战斗", "精英战斗", "宝箱", "商店", "休息", "首领战斗" };
            for (int i = 0; i < kinds.Length; i++)
            {
                NodeIcon(side, captions[i], new Rect(.10f, .69f - i * .115f, .22f, .10f), kinds[i]);
                Label(side, captions[i], new Rect(.37f, .70f - i * .115f, .55f, .08f), 22, Paper);
            }
            DeskButton(page, "持有道具", DeskPixels(47, 555, 263, 70), () =>
            {
                var modal = Modal("持有道具");
                Inventory(modal, new Rect(.06f, .36f, .88f, .25f), false);
            }, true, "Button", 22);
            var board = DeskPanel(page, "Parchment Route", DeskPixels(317, 79, 1422, 928), "MapPanel");
            var graph = Rect(board, "Route Map", new Rect(.08f, .035f, .84f, .94f));
            var positions = host.Definition.Nodes.ToDictionary(n => n.Id, n => new Vector2(.5f + (n.Column - 1) * .12f, .085f + n.Row * .135f));
            foreach (var node in host.Definition.Nodes)
            foreach (string nextId in node.NextIds)
            {
                if (!positions.TryGetValue(nextId, out var next)) continue;
                bool traveled = run.VisitedNodeIds.Contains(node.Id) && run.VisitedNodeIds.Contains(nextId);
                bool available = run.VisitedNodeIds.Contains(node.Id) && run.AvailableNodes.Any(n => n.Id == nextId);
                DottedPath(graph, positions[node.Id], next, traveled || available ? new Color32(234, 178, 246, 255) : new Color32(114, 73, 106, 255));
            }
            foreach (var node in host.Definition.Nodes)
            {
                var at = positions[node.Id]; bool reachable = run.AvailableNodes.Any(n => n.Id == node.Id);
                bool visited = run.VisitedNodeIds.Contains(node.Id);
                var root = Rect(graph, NodeLabel(node), new Rect(at, Vector2.zero));
                root.sizeDelta = new Vector2(88, 72);
                var hitArea = root.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
                var button = root.gameObject.AddComponent<Button>();
                var image = NodeIcon(root, "Node Emblem", new Rect(.18f, .10f, .64f, .80f), node.Kind);
                image.color = reachable || visited ? Color.white : new Color(.58f, .58f, .58f);
                button.targetGraphic = image;
                var colors = button.colors; colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.3f, 1.18f, .8f); colors.pressedColor = new Color(.75f, .65f, .45f);
                colors.disabledColor = Color.white; button.colors = colors;
                button.interactable = reachable;
                AdventureAudioController.BindButton(button);
                button.onClick.AddListener(() => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, node.Id)));
                if (reachable)
                {
                    var outline = image.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1, .82f, .35f, .85f); outline.effectDistance = new Vector2(2, -2);
                }
                MapCaption(root, NodeLabel(node), new Rect(.95f, .30f, 1.42f, .46f), 19, Paper);
                if (reachable || visited) MapCaption(root, reachable ? "可前往" : "已完成", new Rect(.95f, -.01f, 1.42f, .38f), 16, Gold);
            }
            Label(footer, "已完成 " + run.VisitedNodeIds.Count + " / 7 站 · 点击高亮地点前进", new Rect(.20f, .28f, .60f, .50f), 23, Paper, TextAlignmentOptions.Center);
        }
        private Image NodeIcon(Transform parent, string name, Rect area, AdventureNodeKind kind)
        {
            string sprite = "Node" + kind;
            return DeskUISkin.Get(sprite) != null ? DeskArt(parent, name, area, sprite, true) :
                RitualSkin.Icon(parent, name, area, RitualSkin.Symbol(SymbolIndex(kind)));
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
