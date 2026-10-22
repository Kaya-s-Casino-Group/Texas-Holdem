using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using TMPro;
using UnityEngine;

namespace Poker.SingleBattle
{
    public static class BattleTurnBannerTimeline
    {
        public static void Upgrade(SingleBattleSceneAdapter host, ScenePresentationDirector director)
        {
            var groups = director.Timeline.ToList();
            var banner = host.GetComponentInChildren<BattleTurnBannerAction>(true);
            if (banner == null)
            {
                var root = new GameObject("Turn Banner Timeline"); root.transform.SetParent(host.transform, false);
                banner = root.AddComponent<BattleTurnBannerAction>();
                var sample = host.gameObject.scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(text => text.font != null);
                banner.Configure(sample != null ? sample.font : null);
            }
            bool changed = false;
            foreach (var group in groups.Where(group => group?.semanticKey == "PhaseStarted"))
            {
                foreach (var node in group.nodes.Where(node => node.action is UiFeedbackTimelineAction ui && ui.Kind == UiFeedbackKind.Phase))
                {
                    node.action = banner;
                    group.explicitCheckpoint = true;
                    group.checkpoint = new[] { new SceneActionDependency { actionId = node.id, kind = DependencyKind.Completed } };
                    changed = true;
                }
            }
            // The banner replaces the old floating "回合结束" line as well, so the two phases cannot overlap as text.
            foreach (var group in groups.Where(group => group?.semanticKey == "PhaseEnding"))
            {
                var old = group.nodes.Where(node => node.action is UiFeedbackTimelineAction && node.action.name == "UI Phase Ending").ToArray();
                if (old.Length == 0) continue;
                group.nodes = group.nodes.Except(old).ToArray();
                group.checkpoint = group.checkpoint.Where(dependency => old.All(node => node.id != dependency.actionId)).ToArray();
                changed = true;
            }
            if (!groups.Any(group => group.nodes.Any(node => node.action == banner)))
            {
                groups.Add(new SceneTimelineGroup {
                    semanticKey = "PhaseStarted",
                    nodes = new[] { new SceneActionNode { id = "turn-banner", action = banner, blocking = true } },
                    explicitCheckpoint = true,
                    checkpoint = new[] { new SceneActionDependency { actionId = "turn-banner", kind = DependencyKind.Completed } }
                });
                changed = true;
            }
            if (changed) director.ConfigureTimeline(groups.ToArray(), director.Targets.ToArray());
            banner.StartPresentationRoutine = host.StartPresentationRoutine;
        }
    }
}
