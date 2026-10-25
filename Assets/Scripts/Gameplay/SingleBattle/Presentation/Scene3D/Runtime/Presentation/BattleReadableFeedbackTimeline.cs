using System;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using PokerEngine;
using TMPro;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Readable floating tails for the seven-station hand profile, using the scene's authored anchors.</summary>
    public static class BattleReadableFeedbackTimeline
    {
        private const string SummaryId = "ui-combo-summary", ImmuneId = "ui-immune-damage";

        public static void Upgrade(SingleBattleSceneAdapter host, ScenePresentationDirector director, CardDuelState state)
        {
            if (host == null || director == null || state == null) return;
            bool enabled = state.Duel.Players.Any(player => player.Rules.EffectRule is ConfiguredHandEffectRule rule &&
                rule.WheelAceScoresOne && rule.PureFlushScoresAllCards);
            var groups = director.Timeline.ToList();
            foreach (var ui in host.GetComponentsInChildren<UiFeedbackTimelineAction>(true))
                if ((ui.Kind == UiFeedbackKind.Damage || ui.Kind == UiFeedbackKind.Block || ui.Kind == UiFeedbackKind.Heal) &&
                    !groups.Any(group => group?.nodes?.Any(node => node.id == ImmuneId && node.action == ui) == true))
                    ui.KeepVisualFor(enabled ? 1.2f : 0);
            // Reattaching another encounter with this profile reuses the same authored recipes.
            // Reconfiguring here would publish the previous encounter's display during attachment.
            if (enabled && groups.Any(group => group?.nodes?.Any(node => node.id == SummaryId) == true) &&
                groups.Any(group => group?.nodes?.Any(node => node.id == ImmuneId) == true)) return;
            bool changed = groups.RemoveAll(group => group?.nodes != null && group.nodes.Any(node =>
                node.id == SummaryId || node.id == ImmuneId)) > 0;
            if (!enabled)
            {
                if (changed) director.ConfigureTimeline(groups.ToArray(), director.Targets.ToArray());
                return;
            }
            var templates = host.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)).ToArray();
            foreach (var team in new[] { "player", "enemy" })
            {
                var template = templates.FirstOrDefault(text => text.name == (team == "player" ? "Player Effect Template" : "Enemy Effect Template"));
                if (template == null) continue;
                var root = template.rectTransform.parent as RectTransform;
                if (root == null) continue;
                var immune = Action(host, "UI Immune Damage " + team);
                // Empty for ordinary zero requests; the committed immunity metadata overrides this token with “免疫”.
                immune.Configure(template, UiFeedbackKind.Damage, "{value:immuneFeedback}", new Color(.72f, .86f, 1), .58f, new Vector2(0, 24), root);
                immune.KeepVisualFor(1.4f);
                groups.Add(new SceneTimelineGroup {
                    semanticKey = "EffectSettled", filter = Filter(team, SceneAmountMatch.Zero, SceneAmountMatch.Zero),
                    nodes = new[] { new SceneActionNode { id = ImmuneId, action = immune, targetKey = "ui.feedback", blocking = false } },
                    explicitCheckpoint = true, checkpoint = new[] { Revealed(ImmuneId) }
                });
                var summary = Action(host, "UI Combo Summary " + team);
                summary.Configure(template, UiFeedbackKind.Custom, "{value:comboSummary}", new Color(1, .85f, .4f), .58f, new Vector2(0, 24), root);
                summary.KeepVisualFor(1.6f);
                AddSummary(groups, team, summary, SceneAmountMatch.Positive, SceneAmountMatch.Any, ExistingFloat(groups, team, "ui-damage"));
                AddSummary(groups, team, summary, SceneAmountMatch.Zero, SceneAmountMatch.Positive, ExistingFloat(groups, team, "ui-block-hit"));
                AddSummary(groups, team, summary, SceneAmountMatch.Zero, SceneAmountMatch.Zero, ImmuneId);
                changed = true;
            }
            if (changed) director.ConfigureTimeline(groups.ToArray(), director.Targets.ToArray());
        }

        private static string ExistingFloat(IEnumerable<SceneTimelineGroup> groups, string team, string id) =>
            groups.Any(group => group?.semanticKey == "EffectSettled" && group.filter?.targetTeam == team &&
                group.nodes != null && group.nodes.Any(node => node.id == id)) ? id : null;
        private static SceneTimelineFilter Filter(string team, SceneAmountMatch health, SceneAmountMatch block) =>
            new SceneTimelineFilter { targetTeam = team, effectKind = "Damage", healthLost = health, blockAbsorbed = block };
        private static void AddSummary(List<SceneTimelineGroup> groups, string team, UiFeedbackTimelineAction action,
            SceneAmountMatch health, SceneAmountMatch block, string after)
        {
            groups.Add(new SceneTimelineGroup {
                semanticKey = "EffectSettled", filter = Filter(team, health, block),
                nodes = new[] { new SceneActionNode { id = SummaryId, action = action, targetKey = "ui.feedback", blocking = false,
                    after = after == null ? Array.Empty<SceneActionDependency>() : new[] { Revealed(after) } } }
            });
        }
        private static SceneActionDependency Revealed(string id) => new SceneActionDependency { actionId = id, kind = DependencyKind.Marker, marker = "Revealed" };
        private static UiFeedbackTimelineAction Action(SingleBattleSceneAdapter host, string name)
        {
            var parent = host.transform.Find("Presentation Timeline") ?? host.transform;
            var child = parent.Find(name);
            if (child == null) { var go = new GameObject(name); go.transform.SetParent(parent, false); child = go.transform; }
            var action = child.GetComponent<UiFeedbackTimelineAction>() ?? child.gameObject.AddComponent<UiFeedbackTimelineAction>();
            action.StartPresentationRoutine = host.StartPresentationRoutine;
            return action;
        }
    }
}
