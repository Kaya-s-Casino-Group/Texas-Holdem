using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattlePresentation.Unity.Tests
{
    public sealed class UiFeedbackTimelineActionTests
    {
        private sealed class Markers : IMarkerSink
        {
            public readonly List<string> Received = new List<string>();
            public bool Emit(string marker) { Received.Add(marker); return true; }
        }
        private sealed class ManualRoutines
        {
            private readonly List<IEnumerator> active = new List<IEnumerator>();
            public Action Start(IEnumerator routine)
            {
                active.Add(routine);
                return () => active.Remove(routine);
            }
            public void Tick()
            { foreach (var routine in active.ToArray()) if (active.Contains(routine) && !routine.MoveNext()) active.Remove(routine); }
        }
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("UI feedback tests", typeof(RectTransform));
            public readonly TMP_Text Template;
            public readonly UiFeedbackTimelineAction Action;
            public readonly ManualRoutines Routines = new ManualRoutines();
            public Fixture()
            {
                var templateObject = new GameObject("Inactive template", typeof(RectTransform));
                templateObject.transform.SetParent(Root.transform, false);
                templateObject.SetActive(false);
                Template = templateObject.AddComponent<TextMeshProUGUI>();
                Template.text = "Original HUD template";
                Template.rectTransform.anchoredPosition = new Vector2(17, 22);
                Action = Root.AddComponent<UiFeedbackTimelineAction>();
                Action.Configure(Template, UiFeedbackKind.Damage, duration: .5f);
                Action.StartPresentationRoutine = Routines.Start;
                Action.DeltaTimeProvider = () => .1f;
            }
            public TMP_Text[] Visible => Root.GetComponentsInChildren<TMP_Text>().Where(x => x != Template).ToArray();
            public void Dispose() { Object.DestroyImmediate(Root); }
        }
        private static UnitDisplayData Unit(long hp, long block, long energy = 2) => new UnitDisplayData("enemy", "敌人", "enemy", hp, 100, block, energy, 3);
        private static BattleDisplayChange Damage()
        {
            var beforeUnit = Unit(80, 6); var afterUnit = Unit(73, 0);
            var before = new BattleDisplaySnapshot("battle", "player", 1, "player", 2, null, new[] { beforeUnit }, null);
            var after = new BattleDisplaySnapshot("battle", "player", 2, "player", 2, null, new[] { afterUnit }, null,
                labels: new Dictionary<string, string> { ["summary"] = "当前摘要" });
            return new BattleDisplayChange(0, DisplayCursor.Empty, after, "EffectSettled", "player", new[] { "enemy" },
                values: new Dictionary<string, string> { ["pattern"] = "对子", ["score"] = "13", ["cost"] = "1", ["feedback"] = "命中" },
                before: before, effect: new EffectDisplayData("Damage", "player", "enemy", 13, 7, 6, beforeUnit, afterUnit));
        }
        private static ActionContext Context(PresentationScope scope, BattleDisplayChange change = null)
        {
            var fact = change ?? Damage();
            return new ActionContext("batch", 1, scope.Id, fact, fact.Before, null, scope);
        }

        [Test]
        public void FormattingUsesActualEffectAndPerFactSnapshotsRatherThanRequestedDamage()
        {
            var fact = Damage();
            Assert.That(UiFeedbackTimelineAction.Format(fact, UiFeedbackKind.Damage), Is.EqualTo("-7"));
            Assert.That(UiFeedbackTimelineAction.Format(fact, UiFeedbackKind.Block), Is.EqualTo("格挡 6"));
            Assert.That(UiFeedbackTimelineAction.Format(fact, UiFeedbackKind.Hand), Is.EqualTo("对子  ·  13"));
            Assert.That(UiFeedbackTimelineAction.Format(fact, UiFeedbackKind.Custom,
                "{target} {before:target.health}→{after:target.health} / {amount} / {value:cost} / {after:summary}"),
                Is.EqualTo("敌人 80→73 / 13 / 1 / 当前摘要"));
            var healed = Unit(78, 0);
            var heal = new BattleDisplayChange(1, DisplayCursor.Empty, fact.After, "EffectSettled", before: fact.Before,
                effect: new EffectDisplayData("Heal", "player", "enemy", 20, 0, 0, Unit(73, 0), healed));
            Assert.That(UiFeedbackTimelineAction.Format(heal, UiFeedbackKind.Heal), Is.EqualTo("+5"));
            var ongoing = new BattleDisplaySnapshot("battle", "player", 2, "player", 5, null, null, null, completedRounds: 2);
            var phase = new BattleDisplayChange(2, DisplayCursor.Empty, ongoing, "PhaseStarted");
            Assert.That(UiFeedbackTimelineAction.Format(phase, UiFeedbackKind.Phase), Is.EqualTo("第 3 回合 · 玩家回合"));
            Assert.That(UiFeedbackTimelineAction.Format(phase, UiFeedbackKind.Custom, "{round}/{displayRound}"), Is.EqualTo("5/3"),
                "The existing round token retains its raw snapshot contract.");
            var ended = new BattleDisplaySnapshot("battle", "player", 3, "won", 5, "Won", null, null, completedRounds: 2);
            Assert.That(UiFeedbackTimelineAction.Format(new BattleDisplayChange(3, DisplayCursor.Empty, ended, "BattleEnded"),
                UiFeedbackKind.Custom, "{displayRound}"), Is.EqualTo("2"));
        }

        [Test]
        public void ConcurrentClonesRevealIndependentlyAndOldScopeCannotReleaseNewView()
        {
            using (var fixture = new Fixture())
            using (var firstScope = new PresentationScope("first"))
            using (var secondScope = new PresentationScope("second"))
            {
                var firstMarkers = new Markers(); var secondMarkers = new Markers();
                var first = fixture.Action.Play(Context(firstScope), firstMarkers);
                var second = fixture.Action.Play(Context(secondScope), secondMarkers);
                Assert.That(fixture.Action.ActiveViewCount, Is.EqualTo(2));
                Assert.That(firstMarkers.Received, Is.Empty, "No reveal before a visible animation frame.");
                fixture.Routines.Tick();
                var views = fixture.Visible;
                Assert.That(views.Length, Is.EqualTo(2));
                Assert.That(views[0].rectTransform.anchoredPosition.y, Is.Not.EqualTo(views[1].rectTransform.anchoredPosition.y));
                Assert.That(firstMarkers.Received, Is.EqualTo(new[] { "Revealed" }));
                Assert.That(secondMarkers.Received, Is.EqualTo(new[] { "Revealed" }));
                firstScope.Dispose();
                Assert.That(first.Status, Is.EqualTo(PresentationStatus.Cancelled));
                Assert.That(second.Status, Is.EqualTo(PresentationStatus.Pending));
                Assert.That(fixture.Action.ActiveViewCount, Is.EqualTo(1));
                for (var i = 0; i < 8; i++) fixture.Routines.Tick();
                Assert.That(second.Status, Is.EqualTo(PresentationStatus.Completed));
                Assert.That(fixture.Action.ActiveViewCount, Is.Zero);
                Assert.That(fixture.Template.text, Is.EqualTo("Original HUD template"));
                Assert.That(fixture.Template.gameObject.activeSelf, Is.False);
                Assert.That(fixture.Template.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(17, 22)));
                Assert.That(secondMarkers.Received, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void SeparateActionsSharingATemplateReserveIndependentLanesAndReturnTheirOwnLane()
        {
            using (var fixture = new Fixture())
            using (var firstScope = new PresentationScope("first-action"))
            using (var secondScope = new PresentationScope("second-action"))
            using (var reusedScope = new PresentationScope("reused-lane"))
            {
                var other = fixture.Root.AddComponent<UiFeedbackTimelineAction>();
                other.Configure(fixture.Template, UiFeedbackKind.Block, duration: .5f);
                other.StartPresentationRoutine = fixture.Routines.Start;
                other.DeltaTimeProvider = () => .1f;
                fixture.Action.Play(Context(firstScope), new Markers());
                var second = other.Play(Context(secondScope), new Markers());
                fixture.Routines.Tick();
                var views = fixture.Visible;
                var firstY = views.Single(x => x.text == "-7").rectTransform.anchoredPosition.y;
                var secondY = views.Single(x => x.text == "格挡 6").rectTransform.anchoredPosition.y;
                Assert.That(secondY, Is.GreaterThan(firstY));
                firstScope.Dispose();
                Assert.That(other.ActiveViewCount, Is.EqualTo(1));
                Assert.That(second.Status, Is.EqualTo(PresentationStatus.Pending));
                fixture.Action.Play(Context(reusedScope), new Markers());
                fixture.Routines.Tick();
                Assert.That(fixture.Visible.Single(x => x.text == "-7").rectTransform.anchoredPosition.y, Is.EqualTo(firstY),
                    "The released first lane is reusable without moving the other action's text.");
                secondScope.Dispose(); reusedScope.Dispose();
                Assert.That(other.ActiveViewCount + fixture.Action.ActiveViewCount, Is.Zero);
            }
        }

        [Test]
        public void SynchronizeCancelsUnrevealedViewsWithoutReplayingOrChangingHud()
        {
            using (var fixture = new Fixture())
            using (var scope = new PresentationScope("pending"))
            {
                var markers = new Markers();
                var operation = fixture.Action.Play(Context(scope), markers);
                fixture.Action.Synchronize(new ActionSyncContext(Damage().After, null, 2));
                for (var i = 0; i < 8; i++) fixture.Routines.Tick();
                Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Cancelled));
                Assert.That(fixture.Action.ActiveViewCount, Is.Zero);
                Assert.That(fixture.Visible, Is.Empty);
                Assert.That(markers.Received, Is.Empty);
                Assert.That(fixture.Template.gameObject.activeSelf, Is.False);
                Assert.That(fixture.Template.text, Is.EqualTo("Original HUD template"));
                fixture.Action.Configure(fixture.Template, UiFeedbackKind.Feedback);
                var empty = new BattleDisplayChange(1, DisplayCursor.Empty, Damage().After, "StatusChanged");
                var emptyMarkers = new Markers();
                var emptyOperation = fixture.Action.Play(Context(scope, empty), emptyMarkers);
                Assert.That(emptyOperation.Status, Is.EqualTo(PresentationStatus.Completed));
                Assert.That(emptyMarkers.Received, Is.EqualTo(new[] { "Revealed" }));
                Assert.That(fixture.Action.ActiveViewCount, Is.Zero);
                Assert.That(fixture.Visible, Is.Empty, "Absent optional feedback must not create an empty animated text.");
            }
        }
    }
}
