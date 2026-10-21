using System.Linq;
using CardInteraction;
using CardPresentation;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace Poker.SingleBattle.Editor
{
    public static class BattleCardFlightSceneSetup
    {
        private const string TailPath = "Assets/GameAssets/SingleBattle/Presentation/Scene3D/CardProjectileTail.mat";
        [MenuItem("工具/卡牌战斗/表现时间线/接入卡牌弹道与弃牌")]
        public static void ConnectCurrentScene()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before authoring card flights.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            Configure(host, host.GetComponent<ScenePresentationDirector>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Card projectiles, bound tails and lower-left discard connected: " + scene.path, host);
        }

        public static void Configure(SingleBattleSceneAdapter host, ScenePresentationDirector director)
        {
            if (host.gameObject.scene.path != "Assets/Scenes/BattleDemo.unity" && host.gameObject.scene.path != "Assets/Scenes/SingleChapterAdventure.unity") return;
            var hand = host.Actions.Hand.GetComponent<HandZoneView>();
            var frontend = hand.GetComponent<WorldCardZonePresentation>();
            if (frontend == null) return;
            var root = host.transform.Find("Presentation Timeline");
            if (root == null) return;
            var child = root.Find("Card Projectiles");
            if (child == null) { var go = new GameObject("Card Projectiles"); child = go.transform; child.SetParent(root, false); }
            var flight = child.GetComponent<BattleCardProjectileAction>() ?? Undo.AddComponent<BattleCardProjectileAction>(child.gameObject);
            var tail = AssetDatabase.LoadAssetAtPath<Material>(TailPath);
            if (tail == null)
            {
                var shader = Shader.Find("Poker/Card Projectile Tail");
                if (shader == null) throw new System.InvalidOperationException("Import the card projectile tail shader first.");
                tail = new Material(shader); AssetDatabase.CreateAsset(tail, TailPath);
            }
            flight.Configure(frontend, tail); flight.StartPresentationRoutine = host.StartPresentationRoutine;
            if (hand.DiscardAnchor == null) hand.ConfigureScreenDiscard(true);
            var pile = host.gameObject.scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<DrawPilePresenter>(true))
                .FirstOrDefault(item => item.Source == host.Actions.Hand);
            if (pile != null && pile.View != null && pile.View.DealAnchor != null)
                hand.SetDeckSource(hand.DeckLocalPosition, pile.View.DealAnchor);
            bool battleDemo = host.gameObject.scene.path == "Assets/Scenes/BattleDemo.unity" || host.gameObject.scene.path == "Assets/Scenes/SingleChapterAdventure.unity";
            if (battleDemo && host.GetComponent<BattleCardEffectBridge>() == null) Undo.AddComponent<BattleCardEffectBridge>(host.gameObject);
            var groups = director.Timeline.ToList();
            foreach (var group in groups.Where(group => group?.semanticKey == "EffectSettled"))
            {
                var reactions = group.nodes.Where(node => node.action is BattleActorTimelineAction &&
                    (((BattleActorTimelineAction)node.action).Descriptor.Key == "actor.Hit" ||
                     ((BattleActorTimelineAction)node.action).Descriptor.Key == "actor.BlockHit")).ToArray();
                if (reactions.Length == 0) continue;
                if (group.nodes.All(node => node.id != "card-projectile"))
                    group.nodes = new[] { new SceneActionNode { id = "card-projectile", action = flight, blocking = true } }.Concat(group.nodes).ToArray();
                foreach (var reaction in reactions)
                    if (reaction.after.All(dependency => dependency.actionId != "card-projectile"))
                        reaction.after = reaction.after.Concat(new[] { new SceneActionDependency {
                            actionId = "card-projectile", kind = DependencyKind.Marker, marker = "Impact" } }).ToArray();
            }
            if (battleDemo)
            {
                // Elimination has its own label; do not display the target's entire HP as ordinary damage.
                foreach (var node in groups.SelectMany(group => group.nodes))
                    if (node.action is UiFeedbackTimelineAction ui && ui.Kind == UiFeedbackKind.Damage)
                        node.filter.effectKind = "Damage";
                if (!groups.Any(group => group.nodes.Any(node => node.id == "ui-royal")))
                {
                    var template = host.gameObject.scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<TMP_Text>(true))
                        .FirstOrDefault(text => text.name == "Enemy Effect Template");
                    if (template != null)
                    {
                        var royal = root.Find("Royal Flush Feedback");
                        if (royal == null) { var go = new GameObject("Royal Flush Feedback"); royal = go.transform; royal.SetParent(root, false); }
                        var ui = royal.GetComponent<UiFeedbackTimelineAction>() ?? Undo.AddComponent<UiFeedbackTimelineAction>(royal.gameObject);
                        ui.Configure(template, UiFeedbackKind.Custom, "皇家同花顺 · 消灭", new Color(1, .9f, .35f), .75f,
                            new Vector2(0, 32), template.transform.parent as RectTransform);
                        groups.Add(new SceneTimelineGroup { semanticKey = "EffectSettled", ruleAction = "Play",
                            filter = new SceneTimelineFilter { effectKind = "Eliminate", actorRelation = SceneTargetRelation.Observer, targetTeam = "enemy" },
                            nodes = new[] { new SceneActionNode { id = "ui-royal", action = ui, blocking = true,
                                after = new[] { new SceneActionDependency { actionId = "effect", kind = DependencyKind.Marker, marker = "Revealed" } } } } });
                        EditorUtility.SetDirty(ui);
                    }
                }
            }
            director.ConfigureTimeline(groups.ToArray(), director.Targets.ToArray());
            EditorUtility.SetDirty(hand); EditorUtility.SetDirty(frontend); EditorUtility.SetDirty(flight); EditorUtility.SetDirty(director);
            AssetDatabase.SaveAssets();
        }
    }
}
