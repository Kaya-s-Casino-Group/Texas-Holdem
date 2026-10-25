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
    public sealed class ReadableUiFeedbackTimelineActionTests
    {
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("Readable feedback", typeof(RectTransform));
            internal readonly List<IEnumerator> Routines = new List<IEnumerator>();
            internal readonly TMP_Text Template;
            internal readonly UiFeedbackTimelineAction Action;
            internal Fixture()
            {
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Template = NewTemplate("Status template");
                Action = NewAction(Template, UiFeedbackKind.Feedback);
            }
            internal TMP_Text NewTemplate(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(Root.transform, false); go.SetActive(false);
                var text = go.AddComponent<TextMeshProUGUI>(); text.fontSize = 35;
                text.rectTransform.sizeDelta = new Vector2(480, 70);
                return text;
            }
            internal UiFeedbackTimelineAction NewAction(TMP_Text template, UiFeedbackKind kind)
            {
                var action = Root.AddComponent<UiFeedbackTimelineAction>();
                action.Configure(template, kind, duration: .58f);
                action.DeltaTimeProvider = () => .1f;
                action.StartPresentationRoutine = routine => { Routines.Add(routine); return () => Routines.Remove(routine); };
                return action;
            }
            internal void Tick(int frames = 1)
            {
                for (int i = 0; i < frames; i++)
                    foreach (var routine in Routines.ToArray())
                        if (Routines.Contains(routine) && !routine.MoveNext()) Routines.Remove(routine);
            }
            internal TMP_Text[] Visible => Root.GetComponentsInChildren<TMP_Text>();
            public void Dispose() => Object.DestroyImmediate(Root);
        }

        private static BattleDisplayChange Status()
        {
            var unit = new UnitDisplayData("enemy", "敌人", "enemy", 100, 100, 0, 0, 0);
            var snapshot = new BattleDisplaySnapshot("readable", "player", 1, "player", 1, null, new[] { unit }, null);
            return new BattleDisplayChange(0, DisplayCursor.Empty, snapshot, "StatusChanged", "player", new[] { "enemy" },
                values: new Dictionary<string, string> { ["feedback"] = "敌人：伤害免疫（每层抵挡一段正数伤害）", ["floatingFeedback"] = "伤害免疫 ×2", ["statusTarget"] = "enemy" });
        }
        private static ActionContext Context(PresentationScope scope) => new ActionContext("readable-batch", 1, scope.Id, Status(), Status().After, null, scope);
        private static BattleDisplayChange StackedStatus(int stacks, string action = "straight", string definition = "bleed") =>
            new BattleDisplayChange(stacks, DisplayCursor.Empty, Status().After, "StatusChanged", "player", new[] { "enemy" },
                values: new Dictionary<string, string> { ["actionId"] = action, ["statusTarget"] = "enemy", ["statusDefinition"] = definition,
                    ["feedback"] = "完整中毒规则说明", ["floatingFeedback"] = "中毒 " + stacks + "层" });
        private static ActionContext Context(PresentationScope scope, BattleDisplayChange change) =>
            new ActionContext("readable-batch", 1, scope.Id + "/" + change.Sequence, change, change.After, null, scope);
        private sealed class Markers : IMarkerSink
        {
            internal int Reveals;
            public bool Emit(string marker) { if (marker == "Revealed") Reveals++; return true; }
        }

        [Test]
        public void RepeatedStackUpdatesShowOnlyTheLatestStatusWithoutCancellingTheirAuthoredOperations()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("one-poison-chain"))
            {
                var operations = new List<IPresentationOperation>();
                var markers = new List<Markers>();
                for (int stacks = 1; stacks <= 5; stacks++)
                {
                    var marker = new Markers(); markers.Add(marker);
                    operations.Add(f.Action.Play(Context(scope, StackedStatus(stacks)), marker)); f.Tick();
                }
                Assert.That(f.Visible.Select(text => text.text), Is.EqualTo(new[] { "中毒 5层" }),
                    "One action repeatedly adding the same status needs one current label, rather than five rows over the round HUD.");
                Assert.That(operations.All(operation => operation.Status == PresentationStatus.Pending), Is.True,
                    "Replacing a visual must not complete or cancel its authored .58 second operation early.");
                Assert.That(markers.All(marker => marker.Reveals == 1), Is.True, "Every committed status fact retains its reveal checkpoint.");
                f.Action.Play(Context(scope, StackedStatus(1, definition: "stun")), null);
                f.Action.Play(Context(scope, StackedStatus(2, action: "another-action")), null); f.Tick();
                Assert.That(f.Visible, Has.Length.EqualTo(3), "Different statuses and actions remain separate feedback.");
                f.Tick(20);
                Assert.That(operations.All(operation => operation.Status == PresentationStatus.Completed), Is.True);
                Assert.That(f.Visible, Is.Empty); Assert.That(f.Routines, Is.Empty);
            }
        }

        [Test]
        public void StatusUpdatesAcrossFactScopesReplaceTheOldVisualAndOldScopeCannotReleaseTheNewOne()
        {
            using (var f = new Fixture())
            using (var first = new PresentationScope("first-status-fact"))
            using (var second = new PresentationScope("second-status-fact"))
            {
                f.Action.Play(Context(first, StackedStatus(1)), null); f.Tick();
                f.Action.Play(Context(second, StackedStatus(2)), null); f.Tick();
                Assert.That(f.Visible.Select(text => text.text), Is.EqualTo(new[] { "中毒 2层" }),
                    "The real director gives every fact its own scope within the same action.");
                first.Dispose();
                Assert.That(f.Visible.Select(text => text.text), Is.EqualTo(new[] { "中毒 2层" }));
                second.Dispose();
                Assert.That(f.Visible, Is.Empty); Assert.That(f.Routines, Is.Empty);
            }
        }

        [Test]
        public void CrowdedTopBannerUsesLowerLanesBeforeAnyFloatingTextLeavesTheVisibleRoot()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("bounded-banner"))
            {
                f.Template.rectTransform.anchorMin = f.Template.rectTransform.anchorMax = new Vector2(.5f, .8f);
                f.Template.rectTransform.sizeDelta = new Vector2(700, 80);
                for (int i = 0; i < 12; i++) f.Action.Play(Context(scope), null);
                f.Tick(5);
                Assert.That(f.Visible, Has.Length.EqualTo(12));
                Assert.That(f.Visible.Any(text => text.rectTransform.anchoredPosition.y < 0), Is.True,
                    "When the authored upper anchor runs out of room, feedback can occupy a lower lane.");
                AssertInsideRoot(f);
                Assert.That(f.Template.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(f.Template.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .8f)));
            }
        }

        [Test]
        public void ComboSummaryRemainsInsideTheVisibleRootAfterManyIndividualDamageSegments()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("bounded-summary"))
            {
                f.Template.rectTransform.anchorMin = f.Template.rectTransform.anchorMax = new Vector2(.72f, .58f);
                var damage = f.NewAction(f.Template, UiFeedbackKind.Damage); damage.KeepVisualFor(1.2f);
                for (int i = 0; i < 12; i++) damage.Play(Context(scope), null);
                var summary = f.NewAction(f.Template, UiFeedbackKind.Custom);
                summary.Configure(f.Template, UiFeedbackKind.Custom, "12段连击·总伤害60", duration: .58f); summary.KeepVisualFor(1.6f);
                summary.Play(Context(scope), null); f.Tick(3);
                Assert.That(f.Visible, Has.Length.EqualTo(13), "Individual damage values must remain separate.");
                var text = f.Visible.Single(view => view.text == "12段连击·总伤害60");
                Assert.That(text.GetComponent<CanvasGroup>().alpha, Is.GreaterThanOrEqualTo(.75f));
                AssertInsideRoot(f);
            }
        }

        private static void AssertInsideRoot(Fixture f)
        {
            var root = f.Root.GetComponent<RectTransform>();
            foreach (var text in f.Visible)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, text.rectTransform);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(root.rect.yMin), text.text + " extends below the visible root.");
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(root.rect.yMax), text.text + " extends above the visible root.");
            }
        }

        [Test]
        public void FloatingStatusPrefersConciseCommittedTextWhileDetailedFeedbackRemainsAvailable()
        {
            Assert.That(UiFeedbackTimelineAction.Format(Status(), UiFeedbackKind.Feedback), Is.EqualTo("伤害免疫 ×2"));
            Assert.That(UiFeedbackTimelineAction.Format(Status(), UiFeedbackKind.Custom, "{value:feedback}"), Does.Contain("每层抵挡"));
        }

        [Test]
        public void SerializedShortStatusRemainsReadableForOneSecondAndExpiresWithoutScopeDisposal()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("readable-status"))
            {
                f.Action.Play(Context(scope), null);
                f.Tick(10);
                Assert.That(f.Visible, Has.Length.EqualTo(1), "A status must survive long enough to read despite old .58s scene settings.");
                Assert.That(f.Visible.Single().GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.5f));
                f.Tick(10);
                Assert.That(f.Visible, Is.Empty, "Transient text must retire without waiting for a scene change.");
            }
        }

        [Test]
        public void SeparateTemplatesAtOneTargetReserveEnoughVerticalRoomForBothLabels()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("two-statuses"))
            {
                var other = f.NewAction(f.NewTemplate("Different status template"), UiFeedbackKind.Feedback);
                f.Action.Play(Context(scope), null); other.Play(Context(scope), null); f.Tick();
                var ys = f.Visible.Select(text => text.rectTransform.anchoredPosition.y).OrderBy(y => y).ToArray();
                Assert.That(ys, Has.Length.EqualTo(2));
                Assert.That(ys[1] - ys[0], Is.GreaterThanOrEqualTo(40), "Different action templates must not produce overlapping status rows.");
                scope.Dispose();
                Assert.That(f.Visible, Is.Empty);
            }
        }

        [Test]
        public void ReadableVisualTailDoesNotExtendTheAuthoredSettlementGateAndNextCommandReleasesIt()
        {
            using (var f = new Fixture())
            {
                var store = new DisplayStore();
                var initial = Status().After;
                var director = new PresentationDirector(store, null, _ => new ActionPlan(new[] {
                    new ActionNode("status", f.Action)
                }, new[] { ActionDependency.Marker("status", "Revealed") }));
                director.Initialize(initial);
                PresentationBatch Batch(string id)
                {
                    var next = new BattleDisplaySnapshot("readable", "player", 2, "player", 1, null, initial.Units, null,
                        cursor: new DisplayCursor(id, 0));
                    var change = new BattleDisplayChange(0, store.Current.Cursor, next, "StatusChanged", values:
                        new Dictionary<string, string> { ["feedback"] = "伤害免疫 ×2" }, before: store.Current);
                    return new PresentationBatch(id, store.Current, new[] { change }, next);
                }
                var operation = director.Present(Batch("first"), PlaybackMode.Animated);
                for (int frame = 0; frame < 7; frame++) { f.Tick(); director.Tick(); }
                Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Completed), "Longer text must not keep combat input or final results locked.");
                Assert.That(f.Visible, Has.Length.EqualTo(1), "The readable visual remains after its authored blocking deadline.");
                director.Present(Batch("second"), PlaybackMode.Animated);
                Assert.That(f.Visible, Has.Length.EqualTo(1), "The next command owns only its new view; the preceding tail is released.");
                director.Synchronize(store.Current);
                Assert.That(f.Visible, Is.Empty);
                Assert.That(f.Routines, Is.Empty);
            }
        }

        [Test]
        public void AShortLabelCannotOverlapTheLargerLabelAlreadyReservedAtItsAnchor()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("different-font-sizes"))
            {
                f.Template.fontSize = 70;
                var small = f.NewTemplate("Smaller status"); small.fontSize = 20;
                var other = f.NewAction(small, UiFeedbackKind.Feedback);
                f.Action.Play(Context(scope), null); other.Play(Context(scope), null); f.Tick();
                var views = f.Visible.OrderBy(text => text.rectTransform.anchoredPosition.y).ToArray();
                Assert.That(views, Has.Length.EqualTo(2));
                Assert.That(views[1].rectTransform.anchoredPosition.y - views[0].rectTransform.anchoredPosition.y,
                    Is.GreaterThanOrEqualTo(60), "Lane allocation must account for both text heights.");
            }
        }

        [Test]
        public void ReconfiguringAnActionClearsThePreviousProfilesVisualLifetimeFloor()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("reconfigured-feedback"))
            {
                f.Action.KeepVisualFor(1.6f);
                f.Action.Configure(f.Template, UiFeedbackKind.Damage, duration: .58f);
                f.Action.Play(Context(scope), null); f.Tick(10);
                Assert.That(f.Visible, Is.Empty, "A previous profile must not keep an explicitly reconfigured action alive.");
            }
        }
    }
}
