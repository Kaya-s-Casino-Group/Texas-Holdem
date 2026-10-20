using System;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using TMPro;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Adds missing recovery facts to older scene recipes without replacing their authored animation settings.</summary>
    public static class BattleRecoveryTimeline
    {
        public static void Upgrade(SingleBattleSceneAdapter host, ScenePresentationDirector director, bool includeFeedback = true)
        {
            if (host == null || director == null) throw new ArgumentNullException();
            var groups = director.Timeline.ToList();
            var bindings = director.Targets.ToList();
            var actions = Child(host.transform, "Presentation Timeline");
            if (!groups.Any(group => group?.semanticKey == "EffectSettled" && group.filter?.effectKind == "Revive" &&
                group.nodes != null && group.nodes.Any(node => node.action is BattleActorTimelineAction actor && actor.Descriptor.Key == "actor.Revive")))
            {
                var revive = Component<BattleActorTimelineAction>(actions, "Actor Revive");
                revive.Configure(null, BattlePresentationKind.Revive);
                groups.Add(new SceneTimelineGroup {
                    semanticKey = "EffectSettled",
                    filter = new SceneTimelineFilter { effectKind = "Revive", healthChange = SceneValueChange.Increased },
                    nodes = new[] { new SceneActionNode { id = "revive", action = revive, blocking = true } },
                    explicitCheckpoint = true, checkpoint = new[] { Marker("revive") }
                });
            }
            if (includeFeedback)
            {
                var sceneRoots = host.gameObject.scene.GetRootGameObjects();
                var hud = sceneRoots.SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).FirstOrDefault();
                var canvas = hud != null ? hud.GetComponentInParent<Canvas>() :
                    sceneRoots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
                var parent = hud != null ? hud.transform : canvas != null ? canvas.transform : null;
                if (parent != null)
                {
                    var feedback = parent.Find("Timeline Feedback") as RectTransform;
                    if (feedback == null)
                    {
                        var view = new GameObject("Timeline Feedback", typeof(RectTransform), typeof(CanvasGroup));
                        view.layer = parent.gameObject.layer; feedback = view.GetComponent<RectTransform>();
                        feedback.SetParent(parent, false); feedback.anchorMin = Vector2.zero; feedback.anchorMax = Vector2.one;
                        feedback.offsetMin = feedback.offsetMax = Vector2.zero;
                        var canvasGroup = view.GetComponent<CanvasGroup>(); canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false;
                    }
                    if (bindings.All(binding => binding.key != "ui.feedback"))
                        bindings.Add(new SceneTargetBinding { key = "ui.feedback", target = feedback });
                    var sample = parent.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.font != null);
                    foreach (var team in new[] { "player", "enemy" })
                    foreach (var kind in new[] { "Heal", "Revive" })
                    {
                        if (groups.Any(group => group?.semanticKey == "EffectSettled" && group.filter?.effectKind == kind &&
                            group.filter.targetTeam == team && group.nodes != null && group.nodes.Any(node => node.action is UiFeedbackTimelineAction))) continue;
                        var template = Template(feedback, team, sample);
                        var ui = Component<UiFeedbackTimelineAction>(actions, "UI " + kind + " " + team);
                        ui.Configure(template, UiFeedbackKind.Heal, kind == "Revive" ? "{target} 复活 +{heal}" : "{target} +{heal}",
                            new Color(.45f, 1, .65f), .58f, new Vector2(0, 24), feedback);
                        var id = kind == "Revive" ? "ui-revive" : "ui-heal";
                        groups.Add(new SceneTimelineGroup {
                            semanticKey = "EffectSettled",
                            filter = new SceneTimelineFilter { targetTeam = team, effectKind = kind, healthChange = SceneValueChange.Increased },
                            nodes = new[] { new SceneActionNode { id = id, action = ui, targetKey = "ui.feedback", blocking = true,
                                after = kind == "Revive" ? new[] { Marker("revive") } : Array.Empty<SceneActionDependency>() } },
                            explicitCheckpoint = true, checkpoint = new[] { Marker(id) }
                        });
                    }
                }
            }
            if (groups.Count == director.Timeline.Count && bindings.Count == director.Targets.Count) return;
            director.ConfigureTimeline(groups.ToArray(), bindings.ToArray());
            foreach (var action in actions.GetComponentsInChildren<PresentationActionBehaviour>(true))
                action.StartPresentationRoutine = host.StartPresentationRoutine;
        }
        private static SceneActionDependency Marker(string id) =>
            new SceneActionDependency { actionId = id, kind = DependencyKind.Marker, marker = "Revealed" };
        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        private static T Component<T>(Transform parent, string name) where T : Component
        {
            var child = Child(parent, name);
            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }
        private static TMP_Text Template(RectTransform root, string team, TMP_Text sample)
        {
            var name = team == "player" ? "Player Effect Template" : "Enemy Effect Template";
            var existing = root.Find(name);
            if (existing != null) return existing.GetComponent<TMP_Text>();
            var view = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); view.layer = root.gameObject.layer;
            var text = view.GetComponent<TextMeshProUGUI>(); var rect = text.rectTransform;
            rect.SetParent(root, false); rect.anchorMin = rect.anchorMax = new Vector2(team == "player" ? .28f : .72f, .58f);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(480, 70);
            if (sample != null) { text.font = sample.font; text.fontSharedMaterial = sample.fontSharedMaterial; }
            text.fontSize = 35; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.text = "";
            view.SetActive(false); return text;
        }
    }
}
