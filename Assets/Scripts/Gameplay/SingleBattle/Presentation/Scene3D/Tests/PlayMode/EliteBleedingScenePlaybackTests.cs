#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using global::BattlePresentation;

namespace Poker.SingleBattle.Tests
{
    public sealed class EliteBleedingScenePlaybackTests
    {
        private const string Bleeding = "elite-bleeding";
        private const string Evidence = "Library/EliteValidation/Unity";
        private SingleBattleSceneAdapter host;

        [TearDown]
        public void StopOwnedPlayback()
        {
            if (host != null) host.SnapToState();
        }

        [UnityTest]
        public IEnumerator CastStrikeCastPreviewsMatchRealImpactsAndVisibleStatusCheckpoints()
        {
            yield return LoadElite();
            var layoutFailures = new List<string>();
            MeasureHud("opening-cast", false, layoutFailures);
            Preview(4, "施法", 8);
            var frames = new List<long>();
            host.PresentationDirector.Display.Changed += update => frames.Add(DisplayStacks(update.Current));
            long initialHealth = host.Player.Unit.Health;
            Assert.AreEqual(800, initialHealth);

            Assert.IsTrue(host.EndTurn(true));
            Assert.IsTrue(host.IsPresenting, "Use the saved scene's asynchronous timeline, not a synchronous display snap.");
            Assert.AreEqual(initialHealth, host.Display.Unit(host.Player.ParticipantId.Value).Health,
                "Authority commits now; the displayed damage must wait for its impact checkpoint.");
            Assert.AreEqual(796, host.AuthorityState.Duel.Combat.Get(host.Player.ParticipantId).Health);
            yield return FinishRound();
            CollectionAssert.Contains(frames, 3L, "The actual timeline must show application before decay.");
            CollectionAssert.Contains(frames, 2L);
            Assert.Less(frames.IndexOf(3), frames.LastIndexOf(2));
            Assert.AreEqual(2, Stacks());
            Assert.AreEqual(796, host.Player.DisplayUnit.Health);
            AssertSynchronized();
            Preview(20, "普攻", 13);
            MeasureHud("two-stacks-strike", true, layoutFailures);
            yield return Ready();

            frames.Clear();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            CollectionAssert.Contains(frames, 1L);
            Assert.AreEqual(1, Stacks());
            Assert.AreEqual(776, host.Player.DisplayUnit.Health);
            AssertSynchronized();
            Preview(11, "施法", 18);

            frames.Clear();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            CollectionAssert.Contains(frames, 4L, "Existing 1 + cast 3 layers are displayed before decay.");
            CollectionAssert.Contains(frames, 3L);
            Assert.AreEqual(3, Stacks());
            Assert.AreEqual(765, host.Player.DisplayUnit.Health);
            Assert.AreEqual(3, host.State.Duel.CompletedRounds);
            AssertSynchronized();
            MeasureHud("three-stacks-strike", true, layoutFailures);
            yield return null;
            Assert.IsEmpty(layoutFailures, string.Join("\n", layoutFailures));
        }

        [UnityTest]
        public IEnumerator RealFullHouseCleansesBleedingAndStunPreservesIntentWhileAttackGrows()
        {
            yield return LoadElite();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            Assert.AreEqual(2, Stacks());
            var player = host.Player.ParticipantId;
            Apply(new BattleAdminPatch(zones: new[] { new BattleAdminZone(player, CardZoneKinds.Hand,
                new[] { "standard:1:2", "standard:2:2", "standard:3:2", "standard:1:3", "standard:2:3" }
                    .Select(id => new BattleAdminCard(definitionId: id))) }, reason: "Real FullHouse scene fixture"));
            yield return Idle();
            SelectAll();
            var before = host.AuthorityState;
            var preview = host.PreviewSelectedPlay();
            Assert.IsTrue(preview.Accepted, preview.Error.ToString());
            Assert.AreEqual(HandCategory.FullHouse, preview.Score.Category);
            Assert.AreSame(before, host.AuthorityState, "Card preview is read-only.");
            Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle();
            Assert.AreEqual(HandCategory.FullHouse, host.LastPlayResolution.Score.Category);
            Assert.AreEqual(0, Stacks(), "Real FullHouse Cleanse removes the new generic Negative status.");
            Assert.IsTrue(host.State.Duel.Combat.Statuses.Any(s => s.DefinitionId.Value == "stun" && s.Target.Id == host.Enemy.ParticipantId.Value));
            StringAssert.Contains("眩晕", host.Display.Labels["intent/" + host.Enemy.ParticipantId.Value]);
            var intent = host.State.Duel.Enemies.Single().BehaviorState;
            Assert.AreEqual("strike", intent.Intent.Id);
            long health = host.Player.Unit.Health;
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            Assert.IsTrue(Facts().OfType<CombatActionWindowSkippedEvent>().Any(e => e.Actor == host.Enemy.ParticipantId));
            Assert.IsEmpty(EnemyHits(), "A skipped enemy action must not generate a damage impact.");
            Assert.AreEqual(health, host.Player.DisplayUnit.Health);
            Assert.AreEqual(intent.SequenceCursor, host.State.Duel.Enemies.Single().BehaviorState.SequenceCursor);
            Assert.AreEqual("strike", host.State.Duel.Enemies.Single().BehaviorState.Intent.Id);
            Assert.AreEqual(2, host.State.Duel.CompletedRounds);
            Preview(18, "普攻", 18);
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator RealReviverCancelsLaterCastBleedingAndCannotBeOverwrittenByLateDeathPlayback()
        {
            yield return LoadElite();
            var player = host.Player.ParticipantId;
            Apply(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(player, health: 1) },
                relics: new[] { new BattleAdminRelics(player, new[] { new BattleAdminRelic("13004", charges: 1) }) },
                reason: "Real reviver scene fixture"));
            yield return Idle();
            string reviverId = host.State.Duel.Player(player).Relics.Relics.Single(r => r.DefinitionId == "13004").InstanceId;
            Assert.NotNull(host.Player.Visual);
            var visual = host.Player.Visual;
            Vector3 restScale = visual.localScale;
            Quaternion restRotation = visual.localRotation;
            var healthFrames = new List<long>();
            host.PresentationDirector.Display.Changed += update => healthFrames.Add(update.Current.Unit(player.Value).Health);
            long life = host.Player.DisplayLifeVersion;
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            CollectionAssert.Contains(healthFrames, 0L, "The real lethal impact checkpoint must play.");
            CollectionAssert.Contains(healthFrames, 480L, "The real revival checkpoint must play.");
            Assert.Less(healthFrames.IndexOf(0), healthFrames.IndexOf(480));
            Assert.AreEqual(480, host.Player.DisplayUnit.Health);
            Assert.Greater(host.Player.DisplayLifeVersion, life);
            Assert.AreEqual(1, EnemyHits().Length);
            Assert.IsEmpty(Facts().OfType<CombatStatusEvent>().Where(e => e.Status.DefinitionId.Value == Bleeding),
                "Revival aborts the enemy phase before the cast's subsequent +3 application.");
            Assert.AreEqual(0, Stacks());
            Assert.IsFalse(host.State.Duel.Players.Single().Relics.Relics.Any(item => item.InstanceId == reviverId), "The real depleted reviver is removed.");
            AssertSynchronized();
            yield return new WaitForSecondsRealtime(.45f);
            Assert.AreEqual(480, host.Player.DisplayUnit.Health, "An old death coroutine must not overwrite the revival checkpoint.");
            Assert.IsFalse(host.Player.IsDisplayBusy);
            Assert.Less(Vector3.Distance(restScale, visual.localScale), .00001f, "A completed death coroutine must not write its collapsed scale after revival.");
            Assert.Less(Quaternion.Angle(restRotation, visual.localRotation), .001f, "Revival restores the authored actor pose.");
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator RealReviverClearsExistingBleedingBeforeNextIntent()
        {
            yield return LoadElite();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            Assert.AreEqual(2, Stacks(), "Create existing bleeding through the real first cast.");
            var player = host.Player.ParticipantId;
            Apply(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(player, health: 1) },
                relics: new[] { new BattleAdminRelics(player, new[] { new BattleAdminRelic("13004", charges: 1) }) },
                reason: "Existing bleeding revival scene fixture"));
            yield return Idle();
            Assert.AreEqual(2, Stacks(), "Admin changes only HP and inventory; existing bleeding must remain.");
            Assert.AreEqual(2, DisplayStacks(host.Display));
            Preview(20, "普攻", 13);
            var strikeIntent = host.State.Duel.Enemies.Single().BehaviorState;
            var healthFrames = new List<long>();
            long? layersAtFirstRevival = null;
            host.PresentationDirector.Display.Changed += update =>
            {
                long health = update.Current.Unit(player.Value).Health;
                healthFrames.Add(health);
                if (health == 480 && !layersAtFirstRevival.HasValue)
                    layersAtFirstRevival = DisplayStacks(update.Current);
            };
            yield return Ready();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            CollectionAssert.Contains(healthFrames, 0L, "The amplified normal attack must play its lethal checkpoint.");
            CollectionAssert.Contains(healthFrames, 480L, "The real reviver must restore 60% of 800 HP.");
            Assert.Less(healthFrames.IndexOf(0), healthFrames.IndexOf(480));
            Assert.AreEqual(0L, layersAtFirstRevival, "The first revived display checkpoint must already have cleared existing bleeding.");
            Assert.AreEqual(20, EnemyHits().Single().SettledDamage);
            Assert.AreEqual(480, host.Player.DisplayUnit.Health);
            Assert.AreEqual(0, Stacks());
            Assert.AreEqual(0, DisplayStacks(host.Display));
            var hud = Object.FindObjectsOfType<SingleBattleHud>().Single();
            hud.Refresh();
            var status = new SerializedObject(hud).FindProperty("playerStatuses").objectReferenceValue as TMP_Text;
            Assert.NotNull(status);
            StringAssert.DoesNotContain("流血", status.text);
            Assert.IsFalse(status.gameObject.activeInHierarchy, "An empty status label must disappear after revival.");
            Assert.AreEqual(strikeIntent.SequenceCursor, host.State.Duel.Enemies.Single().BehaviorState.SequenceCursor,
                "Revival aborts completion, so the interrupted enemy intent is retained.");
            Assert.AreEqual("strike", host.State.Duel.Enemies.Single().BehaviorState.Intent.Id);
            Preview(18, "普攻", 18);
            AssertSynchronized();
            yield return Ready();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            Assert.AreEqual(18, EnemyHits().Single().SettledDamage, "The retained strike must no longer receive the old bleeding bonus.");
            Assert.AreEqual(462, host.Player.DisplayUnit.Health);
            Assert.AreEqual(0, Stacks());
            Assert.AreEqual(0, DisplayStacks(host.Display));
            Preview(11, "施法", 23);
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator BleedingMergesBeforeShieldIntoOneAuthoritativeDamageFact()
        {
            yield return LoadElite();
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            Apply(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(host.Player.ParticipantId, block: 10) }, reason: "Shield scene fixture"));
            yield return Idle();
            var preview = Preview(20, "普攻", 13);
            Assert.AreEqual(10, preview.BlockAbsorbed);
            Assert.AreEqual(10, preview.DamageToHealth);
            Assert.IsTrue(host.EndTurn(true));
            yield return FinishRound();
            var hits = EnemyHits();
            Assert.AreEqual(1, hits.Length, "Bleeding amplifies the primary hit instead of issuing a second damage event.");
            Assert.AreEqual(20, hits[0].SettledDamage);
            Assert.AreEqual(10, hits[0].BlockAbsorbed);
            Assert.AreEqual(10, hits[0].DamageToHealth);
            var contribution = hits[0].IncomingModification.Triggers.Single(t => t.SourceId == "incoming.attack-per-stack");
            StringAssert.Contains("13→20", contribution.Reason);
            StringAssert.Contains("+7", contribution.Reason);
            Assert.AreEqual(786, host.Player.DisplayUnit.Health);
            Assert.AreEqual(0, host.Player.DisplayUnit.Block);
            Assert.AreEqual(1, Stacks());
            AssertSynchronized();
        }

        private IEnumerator LoadElite()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BleedingEliteDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            yield return Idle();
            var xml = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml");
            Assert.NotNull(xml);
            host.ConfigureContent(xml, "standard-armored-summoner");
            host.RestartBattle();
            yield return Idle();
            Assert.IsTrue(host.PresentationDirector.HasConfiguredTimeline);
            Assert.AreEqual(500, host.Enemy.Unit.Health);
            Assert.AreEqual(0, host.State.Duel.CompletedRounds);
            AssertSynchronized();
        }
        private IEnumerator Idle()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 20;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsPresenting, "Saved scene timeline did not finish within 20 seconds.");
            Assert.IsNull(host.PresentationDirector.LastError);
        }
        private IEnumerator Ready()
        {
            // Hand view landing/rebinding may start on the frame after a rule timeline finishes.
            // Await the same complete interaction gate as the real End Turn button; do not snap it.
            yield return null;
            float deadline = Time.realtimeSinceStartup + 20;
            while (!host.CanEndTurn && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(host.CanEndTurn, "End Turn never became ready: presenting=" + host.IsPresenting +
                ", handPresenting=" + host.Actions.Hand.Presentation.IsPresenting +
                ", handRunning=" + host.Actions.Hand.IsRunning + ", matchRunning=" + host.Match.IsRunning +
                ", displayedAuthority=" + ReferenceEquals(host.State, host.AuthorityState) +
                ", phase=" + host.State.Duel.Phase + ", window=" + host.State.FlowFor(host.Player.ParticipantId).Window);
        }
        private IEnumerator FinishRound()
        {
            yield return Idle();
            if (host.State.FlowFor(host.Player.ParticipantId).Window == BattlePlayerWindow.Ending)
            {
                Assert.IsTrue(host.EndTurn(true));
                yield return Idle();
            }
        }
        private void Apply(BattleAdminPatch patch)
        {
            var result = host.Playback.ApplyAdmin(host.AuthorityState.Revision, patch, animate: false);
            Assert.IsTrue(result.Accepted, result.Error.ToString());
            host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions();
        }
        private void SelectAll()
        {
            var hand = host.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); host.RefreshCandidate();
        }
        private CombatEvent Preview(long damage, string skill, long attack)
        {
            var before = host.AuthorityState;
            var preview = host.Engine.Duel.PreviewEnemyAction(before.Duel, host.Enemy.ParticipantId);
            Assert.IsTrue(preview.Accepted, preview.Error.ToString());
            Assert.IsFalse(preview.IsSkipped);
            Assert.AreSame(before, host.AuthorityState);
            var hit = preview.Hits.Single(e => e.Effect.Source == host.Enemy.ParticipantId && e.Effect.Target == host.Player.ParticipantId);
            Assert.AreEqual(damage, hit.SettledDamage);
            string text = host.Display.Labels["intent/" + host.Enemy.ParticipantId.Value];
            StringAssert.Contains(skill, text);
            StringAssert.Contains("攻击力 " + attack, text);
            StringAssert.Contains("每回合结束 +5", text);
            StringAssert.Contains("伤害 " + damage, text);
            return hit;
        }
        private long Stacks() => host.AuthorityState.Duel.Combat.Statuses.Where(s => s.DefinitionId.Value == Bleeding && s.Target.Id == host.Player.ParticipantId.Value).Sum(s => s.Stacks);
        private long DisplayStacks(BattleDisplaySnapshot display) => display.Unit(host.Player.ParticipantId.Value).Statuses.Where(s => s.Id == Bleeding).Sum(s => s.Stacks);
        private CombatRuleEvent[] Facts() => host.LastResolution.Timeline.Select(t => t.RuleEvent).Where(e => e != null).ToArray();
        private CombatEvent[] EnemyHits() => Facts().OfType<CombatEvent>().Where(e => e.Effect.Kind == CombatEffectKind.Damage && e.Effect.Source == host.Enemy.ParticipantId && e.Effect.Target == host.Player.ParticipantId).ToArray();
        private void AssertSynchronized()
        {
            Assert.AreSame(host.AuthorityState, host.State);
            Assert.AreEqual(host.AuthorityState.Revision, host.Display.RuleRevision);
            foreach (var unit in host.AuthorityState.Duel.Combat.Combatants)
            {
                var shown = host.Display.Unit(unit.Id.Value);
                Assert.AreEqual(unit.Health, shown.Health, unit.Id.Value + " health");
                Assert.AreEqual(unit.Block, shown.Block, unit.Id.Value + " block");
                CollectionAssert.AreEquivalent(host.AuthorityState.Duel.Combat.Statuses.Where(s => s.Target.Id == unit.Id.Value)
                    .Select(s => s.DefinitionId.Value + ":" + s.Stacks), shown.Statuses.Select(s => s.Id + ":" + s.Stacks));
            }
            Assert.AreEqual(host.Player.Unit.Health, host.Player.DisplayUnit.Health);
            Assert.AreEqual(host.Player.Unit.Block, host.Player.Shields.TotalBlock);
            if (host.LastResolution != null && host.LastResolution.Timeline.Count > 0)
                Assert.AreEqual(host.LastResolution.Timeline.Last().Sequence, host.Display.Cursor.Sequence);
            Assert.IsNull(host.PresentationDirector.LastError);
        }
        private void MeasureHud(string stage, bool requireStatus, List<string> failures)
        {
            var hud = Object.FindObjectsOfType<SingleBattleHud>().Single();
            hud.Refresh(); Canvas.ForceUpdateCanvases();
            var serialized = new SerializedObject(hud);
            var panelObject = serialized.FindProperty("intentPanel").objectReferenceValue as GameObject;
            Assert.NotNull(panelObject, "Saved scene must bind the intent background.");
            var panel = panelObject.transform as RectTransform;
            var rail = serialized.FindProperty("feedbackContainer").objectReferenceValue as RectTransform;
            var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
            var canvasRect = (RectTransform)canvas.transform;
            Assert.NotNull(rail, "Saved scene must bind the relic rail.");
            Vector2 position = panel.anchoredPosition, size = panel.sizeDelta;
            for (int refresh = 0; refresh < 3; refresh++)
            {
                hud.Refresh(); Canvas.ForceUpdateCanvases();
                Assert.Less(Vector2.Distance(position, panel.anchoredPosition), .001f, stage + ": repeated Refresh moves the panel.");
                Assert.Less(Vector2.Distance(size, panel.sizeDelta), .001f, stage + ": repeated Refresh grows the panel.");
            }
            float epsilon = 2f / Mathf.Max(.0001f, canvas.scaleFactor);
            Rect panelBounds = BoundsIn(canvasRect, panel), railBounds = BoundsIn(canvasRect, rail);
            CheckContained(canvasRect.rect, panelBounds, epsilon, stage + ": intent background leaves canvas", failures);
            if (panelBounds.xMax > railBounds.xMin + epsilon)
                failures.Add(stage + ": intent background overlaps relic rail horizontally: " + panelBounds + " / " + railBounds);
            foreach (string field in requireStatus ? new[] { "intent", "playerStatuses" } : new[] { "intent" })
            {
                var label = serialized.FindProperty(field).objectReferenceValue as TMP_Text;
                Assert.NotNull(label, "Saved scene must bind actual " + field + " TMP label.");
                Assert.IsTrue(label.gameObject.activeInHierarchy, field + " must be visible.");
                if (field == "intent")
                {
                    bool opening = stage == "opening-cast";
                    long damage = opening ? 4 : stage == "two-stacks-strike" ? 20 : 43;
                    StringAssert.Contains(opening ? "施法" : "普攻", label.text);
                    StringAssert.Contains("伤害 " + damage, label.text);
                    StringAssert.Contains("流血", label.text);
                    if (!opening) StringAssert.Contains(stage == "two-stacks-strike" ? "+7" : "+20", label.text);
                }
                else
                {
                    Assert.AreEqual(host.Display.Labels["status/" + host.Player.ParticipantId.Value], label.text);
                    StringAssert.Contains(stage == "two-stacks-strike" ? "×2" : "×3", label.text);
                }
                if (field == "intent")
                {
                    CheckContained(panel.rect, BoundsIn(panel, label.rectTransform), epsilon,
                        stage + ": intent label corners leave background", failures);
                    var corners = new Vector3[4];
                    panel.GetWorldCorners(corners);
                    var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    foreach (var corner in corners)
                    {
                        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                        if (screen.x < -2 || screen.y < -2 || screen.x > Screen.width + 2 || screen.y > Screen.height + 2)
                            failures.Add(stage + ": intent background corner leaves screen: " + screen + " / " + Screen.width + "x" + Screen.height);
                    }
                }
                label.ForceMeshUpdate(true, true);
                float required = label.preferredHeight, available = label.rectTransform.rect.height;
                Directory.CreateDirectory(Evidence);
                File.AppendAllText(Path.Combine(Evidence, "elite-hud-layout.txt"), stage + "/" + field + ": preferred=" + required + ", height=" + available + ", width=" + label.rectTransform.rect.width + "\n" + label.text + "\n");
                if (required > available + 1) failures.Add(stage + "/" + field + " requires " + required + "px but rect has " + available + "px: " + label.text);
            }
        }
        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var points = corners.Select(corner => (Vector2)parent.InverseTransformPoint(corner)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        private static void CheckContained(Rect outer, Rect inner, float epsilon, string message, List<string> failures)
        {
            if (inner.xMin < outer.xMin - epsilon || inner.xMax > outer.xMax + epsilon ||
                inner.yMin < outer.yMin - epsilon || inner.yMax > outer.yMax + epsilon)
                failures.Add(message + ": " + inner + " inside " + outer);
        }
    }
}
#endif
