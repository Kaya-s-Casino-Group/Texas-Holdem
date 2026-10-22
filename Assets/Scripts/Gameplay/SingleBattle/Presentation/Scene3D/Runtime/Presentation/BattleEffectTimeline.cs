using System;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.SingleBattle
{
    public static class BattleEffectTimeline
    {
        /// <summary>Append effects to existing scene recipes without changing their checkpoints or impact dependencies.</summary>
        public static void Upgrade(SingleBattleSceneAdapter host, ScenePresentationDirector director)
        {
            var groups = director.Timeline.ToList();
            var action = host.GetComponentInChildren<BattleEffectTimelineAction>(true);
            if (action == null)
            {
                var child = new GameObject("Battle Effects Timeline"); child.transform.SetParent(host.transform, false);
                action = child.AddComponent<BattleEffectTimelineAction>();
            }
            bool changed = false;
            foreach (var group in groups.Where(group => group?.semanticKey == "EffectSettled"))
            {
                if (group.nodes.Any(node => node.action is BattleEffectTimelineAction)) continue;
                var reaction = group.nodes.FirstOrDefault(node => node.action is BattleActorTimelineAction actor &&
                    (actor.Descriptor.Key == "actor.Hit" || actor.Descriptor.Key == "actor.BlockHit" || actor.Descriptor.Key == "actor.GainBlock"));
                if (reaction == null) continue;
                group.nodes = group.nodes.Concat(new[] { new SceneActionNode { id = "battle-vfx", action = action, blocking = true,
                    after = new[] { new SceneActionDependency { actionId = reaction.id, kind = DependencyKind.Marker, marker = "Revealed" } } } }).ToArray();
                changed = true;
            }
            foreach (var semantic in new[] { "EffectSettled", "StatusChanged" })
            {
                string id = semantic == "EffectSettled" ? "battle-vfx-heal" : "battle-vfx-status";
                if (groups.Any(group => group?.nodes != null && group.nodes.Any(node => node.id == id))) continue;
                groups.Add(new SceneTimelineGroup {
                    semanticKey = semantic,
                    filter = semantic == "EffectSettled" ? new SceneTimelineFilter { effectKind = "Heal", healthChange = SceneValueChange.Increased } : new SceneTimelineFilter(),
                    nodes = new[] { new SceneActionNode { id = id, action = action, blocking = true } },
                    explicitCheckpoint = false
                });
                changed = true;
            }
            var recovery = action.GetComponent<BattleAttackRecoveryAction>() ?? action.gameObject.AddComponent<BattleAttackRecoveryAction>();
            // PhaseEnding can precede ActionResolved on the last enemy's rule timeline.
            // Hold both boundaries so neither another monster nor the next turn overtakes recovery.
            foreach (var semantic in new[] { "PhaseEnding", "ActionResolved" })
            {
                if (groups.Any(group => group?.semanticKey == semantic && group.nodes.Any(node => node.action is BattleAttackRecoveryAction))) continue;
                groups.Add(new SceneTimelineGroup {
                    semanticKey = semantic, ruleAction = "EnemyAction",
                    nodes = new[] { new SceneActionNode { id = "enemy-recovery", action = recovery, blocking = true } },
                    explicitCheckpoint = true,
                    checkpoint = new[] { new SceneActionDependency { actionId = "enemy-recovery", kind = DependencyKind.Marker, marker = "Recovered" } }
                });
                changed = true;
            }
            var recoveryAction = host.GetComponentInChildren<BattleAttackRecoveryAction>(true);
            if (recoveryAction != null) recoveryAction.StartPresentationRoutine = host.StartPresentationRoutine;
            if (changed) director.ConfigureTimeline(groups.ToArray(), director.Targets.ToArray());
            action.StartPresentationRoutine = host.StartPresentationRoutine;
        }
    }
}
