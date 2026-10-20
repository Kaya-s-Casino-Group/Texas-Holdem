using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using global::BattlePresentation;
using global::BattlePresentation.Unity;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Editor authoring only. Runtime order lives entirely in the scene's serialized timeline.</summary>
    public static class BattlePresentationSceneSetup
    {
        private const string ActionRootName = "Presentation Timeline";
        [MenuItem("工具/卡牌战斗/表现时间线/接入当前场景")]
        public static void ConnectCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before saving scene bindings.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var host = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            ConfigureHost(host);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Scene-authored presentation timeline connected: " + scene.path, host);
        }

        /// <summary>Authors a real scene recipe; fixtures may omit decorative feedback when they have no Canvas.</summary>
        public static ScenePresentationDirector ConfigureHost(SingleBattleSceneAdapter host, bool replaceExisting = false, bool includeFeedback = true)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var battleHost = host.EnsureSceneHost();
            EditorUtility.SetDirty(battleHost);
            if (host.Actions == null || host.Actions.Hand == null) throw new InvalidOperationException("Bind the host's hand before authoring its timeline.");
            var director = host.GetComponent<ScenePresentationDirector>();
            if (director == null) director = Undo.AddComponent<ScenePresentationDirector>(host.gameObject);
            if (host.GetComponent<SceneCandidateFeedback>() == null) Undo.AddComponent<SceneCandidateFeedback>(host.gameObject);
            var serialized = new SerializedObject(host); serialized.FindProperty("presentationDirector").objectReferenceValue = director;
            serialized.ApplyModifiedProperties();
            // A second menu invocation never replaces an authored/customized recipe or its action settings.
            var existingRoot = host.transform.Find(ActionRootName);
            if (!replaceExisting && existingRoot != null && director.HasConfiguredTimeline)
            {
                BattleRecoveryTimeline.Upgrade(host, director, includeFeedback);
                EditorUtility.SetDirty(director);
                return director;
            }
            var preserved = replaceExisting ? Array.Empty<SceneTimelineGroup>() : director.Timeline
                .Where(group => group != null && group.nodes != null && group.nodes.Length > 0).ToArray();
            var root = existingRoot != null ? existingRoot : Child(host.transform, ActionRootName);
            var hand = host.Actions.Hand.GetComponent<HandZoneView>();
            var frontend = hand.GetComponent<WorldCardZonePresentation>() ?? Undo.AddComponent<WorldCardZonePresentation>(hand.gameObject);
            var prepareCards = Action<CardZoneTimelineAction>(root, "Cards Prepare"); prepareCards.Configure(frontend, CardZoneActions.Prepare);
            var settleCards = Action<CardZoneTimelineAction>(root, "Cards Settle"); settleCards.Configure(frontend, CardZoneActions.Settle, observe: true);
            var prepare = Actor(root, "Actor Prepare", BattlePresentationKind.PrepareAttack);
            var attack = Actor(root, "Actor Attack", BattlePresentationKind.Attack);
            var hit = Actor(root, "Actor Hit", BattlePresentationKind.Hit);
            var block = Actor(root, "Actor Block Hit", BattlePresentationKind.BlockHit);
            var gain = Actor(root, "Actor Gain Block", BattlePresentationKind.GainBlock);
            var clear = Actor(root, "Actor Clear Block", BattlePresentationKind.ClearBlock);
            var death = Actor(root, "Actor Death", BattlePresentationKind.Death);
            var revive = Actor(root, "Actor Revive", BattlePresentationKind.Revive);
            var groups = new List<SceneTimelineGroup> {
                Group("ActionStarted", "Play", NotSkipped(), new[] {
                    Node("cards", prepareCards, "cards"),
                    Node("attack", attack, null, Marker("cards", "Prepared")) }, Marker("attack", "Impact")),
                Group("ActionStarted", "Discard", NotSkipped(), new[] { Node("cards", prepareCards, "cards") }, Completed("cards")),
                Group("ActionStarted", "EnemyAction", NotSkipped(), new[] {
                    Node("prepare", prepare), Node("attack", attack, null, Completed("prepare")) }, Marker("attack", "Impact"))
            };
            var hitFilter = new SceneTimelineFilter { effectPresence = SceneMatchBoolean.True, healthLost = SceneAmountMatch.Positive, blockAbsorbed = SceneAmountMatch.Zero };
            var blockFilter = new SceneTimelineFilter { effectPresence = SceneMatchBoolean.True, blockAbsorbed = SceneAmountMatch.Positive };
            var gainFilter = new SceneTimelineFilter { effectKind = "GainBlock", blockChange = SceneValueChange.Increased };
            var clearFilter = new SceneTimelineFilter { effectKind = "ClearBlock", blockChange = SceneValueChange.Decreased };
            groups.Add(Reaction(hitFilter, hit, death));
            groups.Add(Reaction(blockFilter, block, death));
            groups.Add(Reaction(gainFilter, gain, death));
            groups.Add(Reaction(clearFilter, clear, death));
            groups.Add(Group("EffectSettled", null, new SceneTimelineFilter {
                effectKind = "Revive", healthChange = SceneValueChange.Increased
            }, new[] { Node("revive", revive) }, Marker("revive", "Revealed")));
            groups.Add(Group("EffectSettled", null, new SceneTimelineFilter {
                isDeath = SceneMatchBoolean.True, healthLost = SceneAmountMatch.Zero,
                blockAbsorbed = SceneAmountMatch.Zero, blockChange = SceneValueChange.Unchanged
            }, new[] { Node("death", death) }, Marker("death", "Revealed")));
            foreach (var semantic in new[] { "CardsMoved", "CardsDealt", "DeckRecycled" })
                groups.Add(Group(semantic, null, null, new[] { Node("cards-settle", settleCards, "cards") }));
            foreach (var semantic in new[] { "BattleStarted", "PhaseStarted", "HandEvaluated", "ActionCostSettled", "ActionResolved", "PhaseEnding", "PhaseEnded", "PhaseClosed", "BattleEnded" })
                groups.Add(Group(semantic, null, null, Array.Empty<SceneActionNode>()));

            var camera = host.gameObject.scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(item => item.CompareTag("MainCamera"));
            var bindings = new List<SceneTargetBinding> {
                new SceneTargetBinding { key = "cards", target = frontend },
                new SceneTargetBinding { key = "player", target = host.Players.FirstOrDefault() },
                new SceneTargetBinding { key = "enemy", target = host.Enemies.FirstOrDefault() },
                new SceneTargetBinding { key = "camera", target = camera != null ? camera.transform : null },
                new SceneTargetBinding { key = "scene", target = host.transform.root }
            };
            if (includeFeedback) AddFeedback(host, root, camera, groups, bindings);
            // Previously populated custom groups retain their own replacement/append semantics after the standard recipe.
            groups.AddRange(preserved);
            foreach (var binding in director.Targets.Where(binding => binding != null))
                if (bindings.All(current => current.key != binding.key)) bindings.Add(binding);
            Undo.RecordObject(director, "Author battle timeline recipe");
            director.ConfigureTimeline(groups.ToArray(), bindings.ToArray());
            foreach (var action in root.GetComponentsInChildren<PresentationActionBehaviour>(true))
            { action.StartPresentationRoutine = host.StartPresentationRoutine; EditorUtility.SetDirty(action); }
            EditorUtility.SetDirty(host); EditorUtility.SetDirty(director);
            return director;
        }
        private static SceneTimelineFilter NotSkipped() => new SceneTimelineFilter { skipped = SceneMatchBoolean.False };
        private static SceneTimelineGroup Reaction(SceneTimelineFilter filter, BattleActorTimelineAction reaction, BattleActorTimelineAction death)
        {
            var deathNode = Node("death", death, null, Completed("effect")); deathNode.filter.isDeath = SceneMatchBoolean.True;
            return Group("EffectSettled", null, filter, new[] { Node("effect", reaction), deathNode }, Marker("effect", "Revealed"));
        }
        private static void AddFeedback(SingleBattleSceneAdapter host, Transform actions, Camera camera, List<SceneTimelineGroup> groups, List<SceneTargetBinding> bindings)
        {
            var roots = host.gameObject.scene.GetRootGameObjects();
            var hud = roots.SelectMany(o => o.GetComponentsInChildren<SingleBattleHud>(true)).FirstOrDefault();
            var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : roots.SelectMany(o => o.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
            if (canvas != null)
            {
                var parent = hud != null ? hud.transform : canvas.transform;
                var feedbackRoot = parent.Find("Timeline Feedback") as RectTransform;
                if (feedbackRoot == null)
                {
                    var view = new GameObject("Timeline Feedback", typeof(RectTransform), typeof(CanvasGroup)); view.layer = parent.gameObject.layer; Undo.RegisterCreatedObjectUndo(view, "Create timeline feedback");
                    feedbackRoot = view.GetComponent<RectTransform>(); feedbackRoot.SetParent(parent, false);
                    feedbackRoot.anchorMin = Vector2.zero; feedbackRoot.anchorMax = Vector2.one;
                    feedbackRoot.offsetMin = feedbackRoot.offsetMax = Vector2.zero;
                    var group = view.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                }
                var sample = parent.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.font != null);
                var banner = Template(feedbackRoot, "Banner Template", sample, new Vector2(.5f, .8f), new Vector2(700, 80), 31);
                var playerEffect = Template(feedbackRoot, "Player Effect Template", sample, new Vector2(.28f, .58f), new Vector2(480, 70), 35);
                var enemyEffect = Template(feedbackRoot, "Enemy Effect Template", sample, new Vector2(.72f, .58f), new Vector2(480, 70), 35);
                bindings.Add(new SceneTargetBinding { key = "ui.feedback", target = feedbackRoot });
                UiFeedbackTimelineAction Ui(string name, UiFeedbackKind kind, TMP_Text template, string text, Color color, float duration = .58f)
                {
                    var action = Action<UiFeedbackTimelineAction>(actions, name);
                    action.Configure(template, kind, text, color, duration, new Vector2(0, 24), feedbackRoot); return action;
                }
                var white = new Color(.95f, .95f, 1); var gold = new Color(1, .82f, .35f);
                AddUi(groups, "BattleStarted", null, null, Ui("UI Opening", UiFeedbackKind.Custom, banner, "战斗开始", gold), "ui-opening", true);
                AddUi(groups, "PhaseStarted", null, null, Ui("UI Phase", UiFeedbackKind.Phase, banner, null, white), "ui-phase", true);
                AddUi(groups, "HandEvaluated", "Play", null, Ui("UI Hand", UiFeedbackKind.Hand, banner, null, gold), "ui-hand", true);
                AddUi(groups, "ActionCostSettled", "Play", null, Ui("UI Cost", UiFeedbackKind.Cost, banner, null, new Color(.5f, .8f, 1)), "ui-cost", true);
                AddUi(groups, "PhaseEnding", null, null, Ui("UI Phase Ending", UiFeedbackKind.Custom, banner, "回合结束", white), "ui-ending", true);
                AddUi(groups, "BattleEnded", null, null, Ui("UI Result", UiFeedbackKind.Result, banner, null, gold, .9f), "ui-result", true);
                AddUi(groups, "StatusChanged", null, null, Ui("UI Status", UiFeedbackKind.Feedback, banner, null, new Color(.8f, .7f, 1)), "ui-status", true);
                AddUi(groups, "RelicActivated", null, null, Ui("UI Relic", UiFeedbackKind.Feedback, banner, null, gold), "ui-relic", true);
                AddUi(groups, "ActionStarted", "Play", NotSkipped(), Ui("UI Play", UiFeedbackKind.Action, banner, "出牌", gold, .4f),
                    "ui-play", false, Marker("cards", "Prepared"));
                foreach (var team in new[] { "player", "enemy" })
                {
                    var floatText = team == "player" ? playerEffect : enemyEffect;
                    var damage = Ui("UI Damage " + team, UiFeedbackKind.Damage, floatText, "{target} -{damage}", new Color(1, .4f, .32f));
                    AddUi(groups, "EffectSettled", null, new SceneTimelineFilter { targetTeam = team, healthLost = SceneAmountMatch.Positive }, damage, "ui-damage", false, Marker("effect", "Revealed"));
                    var blockHit = Ui("UI Block Hit " + team, UiFeedbackKind.Block, floatText, "格挡抵消 {block}", new Color(.55f, .85f, 1));
                    AddUi(groups, "EffectSettled", null, new SceneTimelineFilter { targetTeam = team, blockAbsorbed = SceneAmountMatch.Positive }, blockHit, "ui-block-hit", false, Marker("effect", "Revealed"));
                    var gained = Ui("UI Gain Block " + team, UiFeedbackKind.Block, floatText, "格挡 +{amount}", new Color(.5f, .85f, 1));
                    AddUi(groups, "EffectSettled", null, new SceneTimelineFilter { targetTeam = team, effectKind = "GainBlock", blockChange = SceneValueChange.Increased }, gained, "ui-gain", false, Marker("effect", "Revealed"));
                    var clear = Ui("UI Clear Block " + team, UiFeedbackKind.Block, floatText, "格挡消退", new Color(.65f, .75f, .85f));
                    AddUi(groups, "EffectSettled", null, new SceneTimelineFilter { targetTeam = team, effectKind = "ClearBlock", blockChange = SceneValueChange.Decreased }, clear, "ui-clear", false, Marker("effect", "Revealed"));
                    var heal = Ui("UI Heal " + team, UiFeedbackKind.Heal, floatText, "{target} +{heal}", new Color(.45f, 1, .65f));
                    AddUi(groups, "EffectSettled", null, new SceneTimelineFilter { targetTeam = team, effectKind = "Heal", healthChange = SceneValueChange.Increased }, heal, "ui-heal", true);
                    var revive = Ui("UI Revive " + team, UiFeedbackKind.Heal, floatText, "{target} 复活 +{heal}", new Color(.45f, 1, .65f));
                    AddUi(groups, "EffectSettled", null, new SceneTimelineFilter { targetTeam = team, effectKind = "Revive", healthChange = SceneValueChange.Increased },
                        revive, "ui-revive", true, Marker("revive", "Revealed"));
                }
            }
            if (camera != null)
            {
                var shake = Action<TransformTimelineAction>(actions, "Camera Impact");
                shake.Configure(null, new[] {
                    new TransformTimelineStep { localEuler = new Vector3(.3f, 0, .32f), duration = .045f, marker = "Peak" },
                    new TransformTimelineStep { duration = .1f, marker = "Settled" }
                });
                // These mutually exclusive effect filters also ensure `effect` exists in the assembled plan.
                AddCamera(groups, shake, new SceneTimelineFilter { targetTeam = "enemy", healthLost = SceneAmountMatch.Positive, blockAbsorbed = SceneAmountMatch.Zero });
                AddCamera(groups, shake, new SceneTimelineFilter { targetTeam = "enemy", blockAbsorbed = SceneAmountMatch.Positive });
            }
        }
        private static void AddCamera(List<SceneTimelineGroup> groups, TransformTimelineAction action, SceneTimelineFilter filter) =>
            groups.Add(Group("EffectSettled", null, filter, new[] { Node("camera-impact", action, "camera", Marker("effect", "Revealed")) },
                Marker("effect", "Revealed"), Completed("camera-impact")));
        private static void AddUi(List<SceneTimelineGroup> groups, string semantic, string action, SceneTimelineFilter filter,
            UiFeedbackTimelineAction feedback, string id, bool revealCheckpoint, params SceneActionDependency[] after)
        {
            var group = Group(semantic, action, filter, new[] { Node(id, feedback, "ui.feedback", after) });
            group.explicitCheckpoint = revealCheckpoint;
            if (revealCheckpoint) group.checkpoint = new[] { Marker(id, "Revealed") };
            groups.Add(group);
        }
        private static TMP_Text Template(RectTransform root, string name, TMP_Text sample, Vector2 anchor, Vector2 size, float fontSize)
        {
            var existing = root.Find(name); if (existing != null) return existing.GetComponent<TMP_Text>();
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = root.gameObject.layer; Undo.RegisterCreatedObjectUndo(go, "Create UI feedback template");
            var text = go.GetComponent<TextMeshProUGUI>(); var rect = text.rectTransform;
            rect.SetParent(root, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size;
            if (sample != null) { text.font = sample.font; text.fontSharedMaterial = sample.fontSharedMaterial; }
            text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.text = "";
            go.SetActive(false); return text;
        }
        private static BattleActorTimelineAction Actor(Transform parent, string name, BattlePresentationKind kind)
        { var action = Action<BattleActorTimelineAction>(parent, name); action.Configure(null, kind); return action; }
        private static T Action<T>(Transform parent, string name) where T : Component
        {
            var child = parent.Find(name);
            if (child == null) child = Child(parent, name);
            var action = child.GetComponent<T>();
            return action != null ? action : Undo.AddComponent<T>(child.gameObject);
        }
        private static Transform Child(Transform parent, string name)
        { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Author presentation action"); go.transform.SetParent(parent, false); return go.transform; }
        private static SceneTimelineGroup Group(string semantic, string action, SceneTimelineFilter filter, SceneActionNode[] nodes, params SceneActionDependency[] checkpoint) =>
            new SceneTimelineGroup { semanticKey = semantic, ruleAction = action, filter = filter ?? new SceneTimelineFilter(), nodes = nodes,
                explicitCheckpoint = true, checkpoint = checkpoint ?? Array.Empty<SceneActionDependency>() };
        private static SceneActionNode Node(string id, MonoBehaviour action, string target = null, params SceneActionDependency[] after) =>
            new SceneActionNode { id = id, action = action, targetKey = target, after = after ?? Array.Empty<SceneActionDependency>(), blocking = true };
        private static SceneActionDependency Marker(string id, string marker) => new SceneActionDependency { actionId = id, kind = DependencyKind.Marker, marker = marker };
        private static SceneActionDependency Completed(string id) => new SceneActionDependency { actionId = id, kind = DependencyKind.Completed };
    }
}
