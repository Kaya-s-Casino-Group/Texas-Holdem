using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardZoneTransitionTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly HandZoneView Zone;
            public Fixture(bool previewScene = true)
            {
                Scene = previewScene ? EditorSceneManager.NewPreviewScene() : default(Scene);
                Root = new GameObject("Transition Test") { hideFlags = HideFlags.HideAndDontSave };
                if (previewScene) SceneManager.MoveGameObjectToScene(Root, Scene);
                Zone = Root.AddComponent<HandZoneView>();
                var lift = CardZoneLiftSettings.Default; lift.Enabled = false; Zone.SetLiftSettings(lift);
                Zone.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var motion = Zone.MotionSettings; motion.Enabled = false; Zone.SetMotionSettings(motion);
            }
            public void Dispose()
            {
                Zone.Clear(); Object.DestroyImmediate(Root);
                if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            }
        }
        private static CardVisualData Card(string id, bool visible = true, bool selected = false) =>
            new CardVisualData(id, visible ? "hearts_q" : null, interaction: new CardInteractionState(isSelected: selected));
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("deal"), cards);
        private static bool Visible(CardInstanceView card) => card.Surface.GetComponent<MeshRenderer>().enabled;
        private static void Near(Vector3 a, Vector3 b) => Assert.Less(Vector3.Distance(a, b), 0.00001f);
        private static void Settled(HandZoneView zone)
        {
            Assert.IsFalse(zone.IsTransitioning);
            foreach (var card in zone.Views)
            {
                Assert.IsFalse(card.IsEntering); Assert.IsTrue(Visible(card));
                Near(card.DisplayPose.Position, card.transform.localPosition);
                Assert.AreEqual(Vector3.zero, card.TransitionRoot.localPosition);
                Assert.AreEqual(Quaternion.identity, card.TransitionRoot.localRotation);
                Assert.AreEqual(0, card.DealFlipAngle);
                Assert.AreEqual(Quaternion.identity, card.DealFlipRoot.localRotation);
                Assert.IsFalse(DOTween.IsTweening(card.TransitionRoot));
            }
        }

        [Test] public void AcceptedSelectionEmitsRealIntermediateMarkerWithoutChangingHandData()
        {
            using (var f = new Fixture())
            {
                var data = Data(Card("a"), Card("b")); f.Zone.Present(data, false);
                int markers = 0, finished = 0;
                var action = f.Zone.PresentSelectionAction(new[] { "a" }, key => { Assert.AreEqual("ReadyToAttack", key); ++markers; });
                action.Finished += _ => ++finished;
                f.Zone.AdvanceMotion(.09f);
                Assert.AreEqual(1, markers); Assert.AreEqual(CardActionViewStatus.Pending, action.Status);
                Assert.AreSame(data, f.Zone.Data); Assert.AreEqual(2, f.Zone.Views.Count);
                Assert.Greater(f.Zone.Views[0].SelectionActionOffset.magnitude, 0);
                f.Zone.AdvanceMotion(.2f);
                Assert.AreEqual(CardActionViewStatus.Completed, action.Status);
                Assert.AreEqual(1, markers); Assert.AreEqual(1, finished);
                Assert.AreSame(data, f.Zone.Data);
            }
        }

        [Test] public void SelectionActionCancellationUsesLatestLayoutAndInvalidatesLateMarker()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"), Card("b")), false);
                var a = f.Zone.Views[0]; int markers = 0;
                var action = f.Zone.PresentSelectionAction(new[] { "a" }, _ => ++markers);
                f.Zone.AdvanceMotion(.01f);
                f.Zone.Present(Data(Card("b"), Card("a")), false);
                var latestPosition = a.transform.localPosition;
                Assert.IsTrue(action.Cancel()); f.Zone.AdvanceMotion(1);
                Assert.AreEqual(CardActionViewStatus.Cancelled, action.Status);
                Assert.AreEqual(0, markers); Assert.AreEqual(Vector3.zero, a.SelectionActionOffset);
                Near(latestPosition, a.DisplayPose.Position);
                Assert.AreEqual("a", f.Zone.Data.Cards[1].ViewId);
            }
        }

        [Test] public void TransferAfterReadyCompletesPreparationWithoutCancellingAcceptedAction()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"), Card("b")), false);
                int markers = 0;
                var action = f.Zone.PresentSelectionAction(new[] { "a" }, _ => ++markers);
                f.Zone.AdvanceMotion(.09f); Assert.AreEqual(1, markers);
                Assert.AreEqual(CardActionViewStatus.Pending, action.Status);
                f.Zone.PresentActionCommitted(CardViewActionKind.Play, Data(Card("b")), new[] { "a" }, () => { });
                Assert.AreEqual(CardActionViewStatus.Completed, action.Status);
                f.Zone.AdvanceMotion(1); Assert.AreEqual(1, markers);
                Assert.AreEqual(1, f.Zone.Data.Cards.Count);
            }
        }

        [Test] public void SelectionActionSyncClearsOnlyTemporaryOffsetAndCompletesOnlyOnce()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a", selected: true)), false);
                var a = f.Zone.Views[0]; var selectedOffset = a.InteractionOffset; int finished = 0;
                var action = f.Zone.PresentSelectionAction(new[] { "a" }, _ => { });
                action.Finished += _ => ++finished;
                f.Zone.AdvanceMotion(.01f); f.Zone.CompleteTransitions(); f.Zone.CompleteTransitions();
                Assert.AreEqual(CardActionViewStatus.Cancelled, action.Status);
                Assert.AreEqual(1, finished); Near(selectedOffset, a.InteractionOffset);
                Assert.IsTrue(a.Data.Interaction.IsSelected);
            }
        }

        [Test] public void DiscardActionCompletesAfterItsLastDepartureIndependentlyOfReflow()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"), Card("b"), Card("c")), false);
                var settings = f.Zone.TransitionSettings; settings.ReflowSeconds = 5; f.Zone.SetTransitionSettings(settings);
                f.Zone.SetDiscardSettings(new CardZoneDiscardSettings { FlightSeconds = .1f, Interval = .2f });
                int commits = 0;
                var operation = ((ICardActionView)f.Zone).PresentActionCommitted(CardViewActionKind.Discard,
                    Data(Card("c")), new[] { "a", "b" }, () => commits++, true);
                Assert.AreEqual(1, commits); Assert.IsTrue(operation.keepWaiting);
                f.Zone.AdvanceMotion(.15f); Assert.IsTrue(operation.keepWaiting);
                f.Zone.AdvanceMotion(.2f);
                Assert.AreEqual(CardActionViewStatus.Completed, operation.Status);
                Assert.IsEmpty(f.Zone.DepartingViews);
                Assert.IsTrue(f.Zone.IsTransitioning, "Retained-card reflow must not delay the action boundary.");
                Assert.IsFalse(operation.Cancel(), "Late completion changes must be ignored.");
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void InterruptedDiscardCannotReportSuccessfulCompletion(int interruption)
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a")), false);
                var operation = f.Zone.PresentActionCommitted(CardViewActionKind.Discard, Data(), new[] { "a" }, () => { });
                if (interruption == 0) f.Zone.Clear();
                else if (interruption == 1) f.Zone.enabled = false;
                else f.Zone.Present(Data(Card("a")), false);
                Assert.AreEqual(CardActionViewStatus.Cancelled, operation.Status);
                f.Zone.AdvanceMotion(5); Assert.IsFalse(operation.Complete());
            }
        }
        [TestCase(CardViewActionKind.Play)] [TestCase(CardViewActionKind.Discard)]
        public void ImmediateCardActionsUseTheSameCompletedContract(CardViewActionKind action)
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a")), false); int commits = 0;
                var operation = f.Zone.PresentActionCommitted(action, Data(), new[] { "a" }, () => commits++, false);
                Assert.AreEqual(1, commits); Assert.AreEqual(CardActionViewStatus.Completed, operation.Status);
                Assert.IsEmpty(f.Zone.Views); Assert.IsEmpty(f.Zone.DepartingViews);
            }
        }

        private static void SetAutoSize(HandZoneView zone, float height)
        {
            var layout = zone.Layout;
            layout.FitCardsToZone = true; layout.Size = new Vector2(1, height);
            layout.Padding = Vector2.zero; layout.ArcHeight = 0;
            zone.SetLayout(layout);
        }

        [Test] public void AutoSizeScalesEntireDealAndDiscardTrajectoriesWithoutChangingTiming()
        {
            using (var large = new Fixture())
            using (var small = new Fixture())
            {
                SetAutoSize(large.Zone, .1f); SetAutoSize(small.Zone, .05f);
                int largeCues = 0, smallCues = 0;
                large.Zone.CardEnteredZone += _ => largeCues++;
                small.Zone.CardEnteredZone += _ => smallCues++;
                large.Zone.Present(Data(Card("a", selected: true)));
                small.Zone.Present(Data(Card("a", selected: true)));
                var a = large.Zone.Views[0]; var b = small.Zone.Views[0];
                foreach (float step in new[] { 0, .1f, .125f, .225f, .2f, .5f })
                {
                    large.Zone.AdvanceMotion(step); small.Zone.AdvanceMotion(step);
                    Near(a.DisplayPose.Position * .5f, b.DisplayPose.Position);
                    Near(a.InteractionOffset * .5f, b.InteractionOffset);
                    Assert.AreEqual(a.DealFlipAngle, b.DealFlipAngle, .0001f);
                    Assert.Less(Quaternion.Angle(a.DisplayPose.Rotation, b.DisplayPose.Rotation), .01f);
                    Assert.AreEqual(a.IsEntering, b.IsEntering); Assert.AreEqual(largeCues, smallCues);
                }
                Assert.AreEqual(1, largeCues);
                large.Zone.Present(Data(), new[] { "a" }); small.Zone.Present(Data(), new[] { "a" });
                foreach (float step in new[] { 0, .1f, .125f, .2f })
                {
                    large.Zone.AdvanceMotion(step); small.Zone.AdvanceMotion(step);
                    Near(a.DisplayPose.Position * .5f, b.DisplayPose.Position);
                    Near(a.InteractionOffset * .5f, b.InteractionOffset);
                    Assert.AreEqual(a.DealFlipAngle, b.DealFlipAngle, .0001f);
                }
                large.Zone.AdvanceMotion(.1f); small.Zone.AdvanceMotion(.1f);
                Assert.IsEmpty(large.Zone.DepartingViews); Assert.IsEmpty(small.Zone.DepartingViews);
                Assert.AreEqual(CardZoneTransitionSettings.Default.FlightLift, small.Zone.TransitionSettings.FlightLift);
                Assert.AreEqual(CardZoneDiscardSettings.Default.FlightLift, small.Zone.DiscardSettings.FlightLift);
            }
        }

        [Test] public void AutoSizeScalesSelectionAndDeniedFeedbackAndResizesActiveTweenWithoutRestart()
        {
            using (var large = new Fixture())
            using (var small = new Fixture())
            {
                SetAutoSize(large.Zone, .1f); SetAutoSize(small.Zone, .05f);
                large.Zone.SetLiftSettings(CardZoneLiftSettings.Default); small.Zone.SetLiftSettings(CardZoneLiftSettings.Default);
                large.Zone.Present(Data(Card("a")), false); small.Zone.Present(Data(Card("a")), false);
                var a = large.Zone.Views[0]; var b = small.Zone.Views[0];
                Assert.IsTrue(large.Zone.PlaySelectionDenied("a")); Assert.IsTrue(small.Zone.PlaySelectionDenied("a"));
                foreach (float step in new[] { .04f, .04f, .06f, .15f })
                {
                    large.Zone.AdvanceMotion(step); small.Zone.AdvanceMotion(step);
                    Near(a.SelectionDeniedOffset * .5f, b.SelectionDeniedOffset);
                    Near(a.FaceRoot.localPosition * .5f, b.FaceRoot.localPosition);
                    Assert.AreEqual(a.IsSelectionDeniedAnimating, b.IsSelectionDeniedAnimating);
                }
                large.Zone.Present(Data(Card("a", selected: true))); small.Zone.Present(Data(Card("a", selected: true)));
                large.Zone.AdvanceMotion(.04f); small.Zone.AdvanceMotion(.04f);
                Near(a.InteractionOffset * .5f, b.InteractionOffset);
                var tween = DOTween.TweensByTarget(b.FaceRoot).Single();
                SetAutoSize(small.Zone, .1f);
                Assert.AreSame(tween, DOTween.TweensByTarget(b.FaceRoot).Single());
                Near(a.InteractionOffset, b.InteractionOffset);
                foreach (float step in new[] { .04f, .04f, .1f })
                {
                    large.Zone.AdvanceMotion(step); small.Zone.AdvanceMotion(step);
                    Near(a.InteractionOffset, b.InteractionOffset); Assert.AreEqual(a.IsLifting, b.IsLifting);
                }
                Near(small.Zone.SelectedOffset * small.Zone.CardScale, b.InteractionOffset);
            }
        }

        [Test] public void AutoSizeKeepsExplicitDeckAndDiscardAnchorsAtTheirWorldPositions()
        {
            using (var f = new Fixture())
            {
                var anchor = new GameObject("Fixed Anchor"); anchor.transform.SetParent(f.Root.transform, false);
                anchor.transform.localPosition = new Vector3(.6f, .2f, .1f);
                f.Zone.SetDeckSource(Vector3.zero, anchor.transform);
                f.Zone.SetDiscardDestination(Vector3.zero, anchor.transform);
                foreach (float height in new[] { .05f, .2f })
                {
                    SetAutoSize(f.Zone, height);
                    Near(anchor.transform.position, f.Zone.DeckWorldPosition);
                    Near(anchor.transform.position, f.Zone.DiscardWorldPosition);
                }
                f.Zone.Present(Data(Card("a")));
                Near(anchor.transform.position, f.Zone.transform.TransformPoint(f.Zone.Views[0].DisplayPose.Position));
            }
        }

        [Test] public void NewIdentitiesFlyFromDeckInOrderThenOvershootAndSettle()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(), animate: false);
                var data = Data(Card("a"), Card("b"), Card("c")); f.Zone.Present(data);
                Assert.AreSame(data, f.Zone.Data); Assert.IsTrue(f.Zone.IsTransitioning);
                foreach (var card in f.Zone.Views) Near(card.DisplayPose.Position, f.Zone.DeckLocalPosition);
                Assert.IsTrue(Visible(f.Zone.Views[0])); Assert.IsFalse(Visible(f.Zone.Views[1])); Assert.IsFalse(Visible(f.Zone.Views[2]));
                float interval = f.Zone.TransitionSettings.DealInterval;
                f.Zone.AdvanceMotion(interval * 0.5f);
                Assert.Greater(Vector3.Distance(f.Zone.Views[0].DisplayPose.Position, f.Zone.DeckLocalPosition), 0.001f);
                Assert.IsFalse(Visible(f.Zone.Views[1]));
                f.Zone.AdvanceMotion(interval * 0.6f); Assert.IsTrue(Visible(f.Zone.Views[1])); Assert.IsFalse(Visible(f.Zone.Views[2]));
                f.Zone.AdvanceMotion(f.Zone.TransitionSettings.FlightSeconds - interval * 1.1f + 0.0001f);
                var first = f.Zone.Views[0]; Assert.IsTrue(first.IsEntering);
                Assert.Less(first.DisplayPose.Position.x, first.transform.localPosition.x - 0.003f);
                f.Zone.AdvanceMotion(f.Zone.TransitionSettings.SettleSeconds / 3);
                Assert.Greater(first.DisplayPose.Position.x, first.transform.localPosition.x);
                f.Zone.AdvanceMotion(2); Settled(f.Zone);
                CollectionAssert.AreEqual(new[] { "a", "b", "c" }, f.Zone.Views.Select(v => v.ViewId));
            }
        }

        [Test] public void DealCuesFireOnceOnEntryIncludingHiddenCardsAcrossRefreshReorderAndAppend()
        {
            using (var f = new Fixture())
            {
                var cues = new List<string>();
                f.Zone.CardEnteredZone += card => {
                    Assert.Contains(card, f.Zone.Views.ToArray());
                    Assert.IsTrue(f.Zone.Data.Cards.Any(data => data.ViewId == card.ViewId));
                    Assert.IsTrue(Visible(card));
                    Assert.AreEqual(0, card.DealFlipAngle);
                    cues.Add(card.ViewId);
                };
                var a = Card("a"); var b = Card("b", visible: false); var c = Card("c"); var d = Card("d");
                var data = Data(a, b, c);
                f.Zone.Present(data);
                Assert.IsEmpty(cues);
                f.Zone.AdvanceMotion(0.04f);
                f.Zone.Present(data); f.Zone.Present(Data(c, b, a));
                f.Zone.Present(Data(c, b, a, d));
                Assert.IsEmpty(cues);
                f.Zone.AdvanceMotion(0.09f);
                Assert.IsEmpty(cues);
                f.Zone.RefreshLayout(); f.Zone.Present(Data(c, b, a, d));
                f.Zone.AdvanceMotion(0.12f);
                Assert.IsEmpty(cues);
                f.Zone.AdvanceMotion(f.Zone.TransitionSettings.FlightSeconds - 0.25f - 0.001f);
                Assert.IsEmpty(cues); // Even the first launched card is still flying.
                f.Zone.AdvanceMotion(0.002f);
                CollectionAssert.AreEqual(new[] { "a" }, cues);
                Assert.IsTrue(f.Zone.Views.Single(view => view.ViewId == "a").IsEntering); // Cue at entry, before settle completes.
                f.Zone.Present(Data(d, c, b, a)); f.Zone.RefreshLayout(); // Retarget in the settle phase without replay.
                f.Zone.AdvanceMotion(0.12f);
                CollectionAssert.AreEqual(new[] { "a", "b" }, cues);
                f.Zone.AdvanceMotion(0.12f);
                CollectionAssert.AreEqual(new[] { "a", "b", "c" }, cues);
                f.Zone.AdvanceMotion(2);
                CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, cues);
                f.Zone.Present(Data(d, c, b, a)); f.Zone.AdvanceMotion(2);
                Assert.AreEqual(4, cues.Count);
                Assert.AreSame(b, f.Zone.Views.Single(view => view.ViewId == "b").Data);
            }
        }

        [Test] public void RestoresAndCancelledQueuedCardsStaySilentWhileInstantNewArrivalsStillCue()
        {
            using (var f = new Fixture(previewScene: false))
            {
                var cues = new List<string>(); f.Zone.CardEnteredZone += card => cues.Add(card.ViewId);
                f.Zone.Present(Data(Card("restored")), animate: false);
                f.Zone.Present(Data(Card("restored"), Card("a"), Card("removed")));
                f.Zone.Present(Data(Card("restored"), Card("a"))); f.Zone.AdvanceMotion(2);
                CollectionAssert.AreEqual(new[] { "a" }, cues);
                f.Zone.Present(Data(Card("restored"), Card("a"), Card("b"), Card("skipped")));
                f.Zone.CompleteTransitions(); f.Zone.AdvanceMotion(2);
                CollectionAssert.AreEqual(new[] { "a" }, cues);
                f.Zone.Clear(); f.Zone.AdvanceMotion(2);
                f.Zone.Present(Data(Card("c"), Card("disabled")));
                f.Zone.AdvanceMotion(0.02f);
                f.Zone.enabled = false; f.Zone.enabled = true; f.Zone.AdvanceMotion(2);
                CollectionAssert.AreEqual(new[] { "a" }, cues);
                f.Zone.Present(Data(Card("loaded")), animate: false); f.Zone.AdvanceMotion(2);
                Assert.AreEqual(1, cues.Count);
                var settings = f.Zone.TransitionSettings; settings.Enabled = false; f.Zone.SetTransitionSettings(settings);
                f.Zone.Present(Data(Card("loaded"), Card("instant")));
                CollectionAssert.AreEqual(new[] { "a", "instant" }, cues);
                f.Zone.Present(Data(Card("instant"), Card("loaded"))); Assert.AreEqual(2, cues.Count);
            }
        }

        [Test] public void BundledDrawAudioLoadsAndConfigurationValidatesAndSurvivesSerialization()
        {
            using (var f = new Fixture())
            {
                var clip = Resources.Load<AudioClip>(HandZoneView.DefaultDrawSoundResource);
                Assert.IsNotNull(clip); Assert.AreEqual("CardDraw", clip.name);
                Assert.AreEqual(2, clip.channels); Assert.AreEqual(0.5333333f, clip.length, 0.001f);
                Assert.IsTrue(f.Zone.DrawSoundEnabled); Assert.AreSame(clip, f.Zone.DrawSound);
                f.Zone.SetDrawSound(clip, 0.4f, enabled: false);
                string serialized = JsonUtility.ToJson(f.Zone);
                f.Zone.SetDrawSound(null);
                JsonUtility.FromJsonOverwrite(serialized, f.Zone);
                Assert.AreSame(clip, f.Zone.DrawSoundOverride);
                Assert.AreEqual(0.4f, f.Zone.DrawSoundVolume); Assert.IsFalse(f.Zone.DrawSoundEnabled);
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -0.1f, 1.1f })
                    Assert.Throws<ArgumentOutOfRangeException>(() => f.Zone.SetDrawSound(null, invalid));
                Assert.AreEqual(0.4f, f.Zone.DrawSoundVolume); Assert.AreSame(clip, f.Zone.DrawSoundOverride);
                f.Zone.SetDrawSound(null, 0);
                Assert.AreSame(clip, f.Zone.DrawSound); Assert.AreEqual(0, f.Zone.DrawSoundVolume);
                Assert.IsEmpty(f.Root.GetComponentsInChildren<AudioSource>()); // Edit previews create no runtime emitter.
            }
        }

        [Test] public void DealStartsBackUpAndOnlyVisibleCardsFlipWithoutChangingDataOrShaderProperties()
        {
            using (var f = new Fixture())
            {
                var known = new CardInstance(new CardInstanceId("known"), new CardDefinitionId("queen"),
                    effects: CardEffects.Enhanced | CardEffects.Laminated, isDisabled: true);
                var secret = new CardInstance(new CardInstanceId("secret"), new CardDefinitionId("secret-face"), effects: CardEffects.Enhanced);
                var shown = CardVisualData.FromCard(known, "shown", true, _ => "hearts_q", tint: new Color(0.8f, 0.9f, 1));
                var hidden = CardVisualData.FromCard(secret, "hidden", false, _ => { Assert.Fail("Hidden face must not be resolved."); return null; });
                var data = Data(shown, hidden); f.Zone.Present(data);
                var a = f.Zone.Views[0]; var b = f.Zone.Views[1]; var appearance = a.Appearance;
                Assert.AreEqual(180, a.DealFlipAngle); Assert.AreEqual(0, b.DealFlipAngle);
                Assert.Less(Vector3.Dot(a.Surface.transform.up, f.Zone.transform.up), -0.99f);
                Assert.Less(Vector3.Dot(b.Surface.transform.up, f.Zone.transform.up), -0.99f);
                Assert.IsTrue(shown.FaceUp); Assert.IsFalse(hidden.FaceUp);
                Action unchanged = () => {
                    Assert.AreSame(data, f.Zone.Data); Assert.AreSame(shown, a.Data); Assert.AreSame(hidden, b.Data);
                    Assert.AreEqual(known.Id, a.Data.CardId); Assert.AreEqual(known.Effects, a.Data.Effects); Assert.IsTrue(a.Data.IsDisabled);
                    Assert.AreEqual("hearts_q", a.Surface.CardId); Assert.AreEqual(appearance, a.Appearance);
                    Assert.AreEqual(shown.Tint, a.Surface.Tint);
                    Assert.IsTrue(a.Surface.FoilEnabled); Assert.IsTrue(a.Surface.SealEnabled); Assert.IsTrue(a.Surface.RedCrossEnabled);
                    Assert.IsNull(b.Data.CardId); Assert.IsNull(b.Data.FaceArtId); Assert.IsFalse(b.Data.FaceUp);
                    Assert.AreEqual("card_back", b.Surface.CardId); Assert.AreEqual(0, b.DealFlipAngle);
                    Assert.IsFalse(b.Surface.FoilEnabled); Assert.IsFalse(b.Surface.SealEnabled);
                    Assert.Less(Vector3.Dot(b.Surface.transform.up, f.Zone.transform.up), -0.99f);
                };
                unchanged(); float flight = f.Zone.TransitionSettings.FlightSeconds;
                f.Zone.AdvanceMotion(flight * 0.5f); unchanged();
                Assert.AreEqual(90, a.DealFlipAngle, 0.001f);
                Assert.Less(Mathf.Abs(Vector3.Dot(a.Surface.transform.up, f.Zone.transform.up)), 0.001f);
                f.Zone.AdvanceMotion(flight * 0.5f + 0.001f); unchanged();
                Assert.AreEqual(0, a.DealFlipAngle); Assert.IsTrue(a.IsEntering);
                Assert.Greater(Vector3.Dot(a.Surface.transform.up, f.Zone.transform.up), 0.99f);
                f.Zone.AdvanceMotion(2); unchanged(); Settled(f.Zone);
                Assert.AreEqual(CardEffects.Enhanced | CardEffects.Laminated, known.Effects); Assert.IsTrue(known.IsDisabled);
                Assert.AreEqual(CardEffects.Enhanced, secret.Effects);
            }
        }

        [Test] public void DealFlipKeepsItsAngleAndRemainingFlightThroughRefreshReorderAndQueuedArrival()
        {
            using (var f = new Fixture())
            {
                var first = Card("a"); var second = Card("b"); var data = Data(first, second);
                f.Zone.Present(data); f.Zone.AdvanceMotion(0.09f);
                var a = f.Zone.Views[0]; var b = f.Zone.Views[1];
                float angle = a.DealFlipAngle; var normal = a.Surface.transform.up;
                Assert.Less(angle, 180); Assert.Greater(angle, 0); Assert.AreEqual(180, b.DealFlipAngle); Assert.IsFalse(Visible(b));
                var tween = DOTween.TweensByTarget(a.TransitionRoot).Single();
                f.Zone.Present(data); Assert.AreSame(tween, DOTween.TweensByTarget(a.TransitionRoot).Single());
                Assert.AreEqual(angle, a.DealFlipAngle);
                f.Zone.Present(Data(second, first, Card("c")));
                Assert.AreSame(a, f.Zone.Views[1]); Assert.AreSame(b, f.Zone.Views[0]);
                Assert.AreEqual(angle, a.DealFlipAngle); Near(normal, a.Surface.transform.up);
                var layout = f.Zone.Layout; layout.ArcHeight = -0.008f; f.Zone.SetLayout(layout);
                Assert.AreEqual(angle, a.DealFlipAngle); Near(normal, a.Surface.transform.up);
                f.Zone.AdvanceMotion(0.02f); Assert.IsFalse(Visible(b)); Assert.AreEqual(180, b.DealFlipAngle);
                f.Zone.AdvanceMotion(0.02f); Assert.IsTrue(Visible(b)); Assert.Less(b.DealFlipAngle, 180);
                Assert.Less(a.DealFlipAngle, angle); Assert.AreSame(first, a.Data);
                f.Zone.AdvanceMotion(f.Zone.TransitionSettings.FlightSeconds - 0.13f + 0.001f);
                Assert.AreEqual(0, a.DealFlipAngle); Assert.Greater(b.DealFlipAngle, 0);
                f.Zone.CompleteTransitions(); Settled(f.Zone);
                f.Zone.Present(Data(first, second)); Assert.AreEqual(0, a.DealFlipAngle); Assert.IsFalse(a.IsEntering);
                f.Zone.AdvanceMotion(1); Settled(f.Zone);
            }
        }

        [Test] public void LandingAndSettleCompletionReachZeroVelocityWithoutASnap()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a")));
                var card = f.Zone.Views[0]; var settings = f.Zone.TransitionSettings;
                const float step = 0.001f;
                f.Zone.AdvanceMotion(settings.FlightSeconds - step);
                var beforeLanding = card.DisplayPose;
                f.Zone.AdvanceMotion(step); var landing = card.DisplayPose;
                f.Zone.AdvanceMotion(step); var afterLanding = card.DisplayPose;
                AssertSlowEndpoint(beforeLanding, landing, step);
                AssertSlowEndpoint(landing, afterLanding, step);
                f.Zone.AdvanceMotion(settings.SettleSeconds - 2 * step);
                var beforeEnd = card.DisplayPose;
                f.Zone.AdvanceMotion(2 * step);
                AssertSlowEndpoint(beforeEnd, card.DisplayPose, step);
                Settled(f.Zone);
            }
        }

        private static void AssertSlowEndpoint(CardZoneCardPose from, CardZoneCardPose to, float step)
        {
            Assert.Less(Vector3.Distance(from.Position, to.Position) / step, 0.004f, "Position must slow to rest at the phase boundary.");
            // Comparing basis vectors avoids Quaternion.Angle's loss of precision near zero.
            Assert.Less(Vector3.Distance(from.Rotation * Vector3.forward, to.Rotation * Vector3.forward) / step,
                0.01f, "Yaw must slow to rest at the phase boundary.");
        }

        [Test] public void RepeatedSnapshotAndMidFlightReorderPreservePosePhaseAndQueue()
        {
            using (var f = new Fixture())
            {
                var motion = f.Zone.MotionSettings; motion.Enabled = true; f.Zone.SetMotionSettings(motion);
                var initial = Data(Card("a"), Card("b")); f.Zone.Present(initial); f.Zone.AdvanceMotion(0.06f);
                var a = f.Zone.Views[0]; var b = f.Zone.Views[1]; var pose = a.DisplayPose; var sway = a.SwayRoot.localRotation;
                var tween = DOTween.TweensByTarget(a.TransitionRoot).Single();
                f.Zone.Present(initial); f.Zone.RefreshLayout();
                Assert.AreSame(tween, DOTween.TweensByTarget(a.TransitionRoot).Single());
                Near(pose.Position, a.DisplayPose.Position); Assert.Less(Quaternion.Angle(sway, a.SwayRoot.localRotation), 0.001f);
                f.Zone.Present(Data(Card("b"), Card("a", selected: true), Card("c")));
                Assert.AreSame(a, f.Zone.Views[1]); Assert.AreSame(b, f.Zone.Views[0]);
                Near(pose.Position, a.DisplayPose.Position); Assert.Less(Quaternion.Angle(pose.Rotation, a.DisplayPose.Rotation), 0.02f);
                Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation);
                Near(f.Zone.transform.InverseTransformPoint(a.FaceRoot.position) - a.DisplayPose.Position, f.Zone.RaisedOffset);
                f.Zone.AdvanceMotion(0.065f); Assert.IsTrue(Visible(b)); Assert.IsFalse(Visible(f.Zone.Views[2]));
                f.Zone.AdvanceMotion(0.12f); Assert.IsTrue(Visible(f.Zone.Views[2]));
                f.Zone.AdvanceMotion(2); Settled(f.Zone);
            }
        }

        [Test] public void RemovingFlyingOrQueuedCardsCancelsOnlyThoseViewsAndReflowsSurvivors()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"), Card("b")), animate: false);
                var a = f.Zone.Views[0]; var oldPosition = a.DisplayPose.Position;
                f.Zone.Present(Data(Card("a"), Card("b"), Card("c"), Card("d")));
                Near(oldPosition, a.DisplayPose.Position); Assert.IsFalse(a.IsEntering); Assert.IsTrue(a.IsTransitioning);
                var flying = f.Zone.Views[2]; var queued = f.Zone.Views[3];
                var flyTween = DOTween.TweensByTarget(flying.TransitionRoot).Single();
                var queueTween = DOTween.TweensByTarget(queued.TransitionRoot).Single();
                f.Zone.AdvanceMotion(0.03f); var midway = a.DisplayPose.Position;
                f.Zone.Present(Data(Card("b"), Card("a")));
                Assert.IsTrue(flying == null); Assert.IsTrue(queued == null);
                Assert.IsFalse(flyTween.IsActive()); Assert.IsFalse(queueTween.IsActive());
                Near(midway, a.DisplayPose.Position); f.Zone.AdvanceMotion(1); Settled(f.Zone);
                f.Zone.Present(Data()); Assert.AreEqual(0, f.Zone.Views.Count); Assert.IsFalse(f.Zone.IsTransitioning);
                f.Zone.Present(Data(Card("fresh"))); Assert.IsTrue(Visible(f.Zone.Views[0]));
            }
        }

        [Test] public void ReflowOvershootsThenSettlesAndCanReverseFromItsDisplayedPose()
        {
            using (var f = new Fixture())
            {
                var layout = f.Zone.Layout; layout.ArcHeight = 0; f.Zone.SetLayout(layout);
                var tuning = f.Zone.TransitionSettings; tuning.ReflowSeconds = .4f; tuning.ReflowOvershoot = 2;
                f.Zone.SetTransitionSettings(tuning);
                f.Zone.Present(Data(Card("a"), Card("b")), false);
                var card = f.Zone.Views[0]; var start = card.DisplayPose.Position;
                int deals = 0; f.Zone.CardDealStarted += _ => deals++; f.Zone.CardEnteredZone += _ => deals++;
                f.Zone.Present(Data(Card("b"), Card("a")));
                var destination = card.transform.localPosition;
                f.Zone.AdvanceMotion(tuning.ReflowSeconds * .7f);
                var beyond = card.DisplayPose.Position;
                Assert.Greater(Vector3.Dot(beyond - destination, destination - start), 0);
                Assert.IsFalse(card.IsEntering); Assert.AreEqual(0, card.DealFlipAngle);
                var tween = DOTween.TweensByTarget(card.TransitionRoot).Single();
                f.Zone.Present(Data(Card("b"), Card("a")));
                Assert.AreSame(tween, DOTween.TweensByTarget(card.TransitionRoot).Single()); Near(beyond, card.DisplayPose.Position);
                f.Zone.Present(Data(Card("a"), Card("b")));
                Assert.AreSame(card, f.Zone.Views[0]); Near(beyond, card.DisplayPose.Position);
                f.Zone.AdvanceMotion(tuning.ReflowSeconds + .001f);
                Settled(f.Zone); Near(start, card.DisplayPose.Position); Assert.AreEqual(0, deals);
                f.Zone.Present(Data(Card("b"), Card("a")), false);
                Settled(f.Zone);
            }
        }

        [Test] public void ReflowInertiaScalesWithCardSizeAndRetainsItsTiming()
        {
            using (var large = new Fixture())
            using (var small = new Fixture())
            {
                foreach (var zone in new[] { large.Zone, small.Zone })
                {
                    var layout = zone.Layout;
                    layout.FitCardsToZone = true; layout.ArcHeight = 0; layout.Padding = Vector2.zero; layout.PreferredGap = 0;
                    layout.Size = zone == large.Zone ? new Vector2(1, .1f) : new Vector2(.5f, .05f);
                    zone.SetLayout(layout);
                    var tuning = zone.TransitionSettings; tuning.ReflowOvershoot = 2; tuning.ReflowSeconds = .4f;
                    zone.SetTransitionSettings(tuning);
                    zone.Present(Data(Card("a"), Card("b")), false);
                    zone.Present(Data(Card("b"), Card("a")));
                }
                foreach (float step in new[] { .1f, .18f, .08f, .05f })
                {
                    large.Zone.AdvanceMotion(step); small.Zone.AdvanceMotion(step);
                    for (int i = 0; i < 2; i++)
                    {
                        Near(large.Zone.Views[i].DisplayPose.Position * .5f, small.Zone.Views[i].DisplayPose.Position);
                        Assert.AreEqual(large.Zone.Views[i].IsTransitioning, small.Zone.Views[i].IsTransitioning);
                    }
                }
                Settled(large.Zone); Settled(small.Zone);
            }
        }

        [Test] public void ZeroReflowInertiaKeepsLegacyEaseAndInvalidStrengthDoesNotReplaceTuning()
        {
            using (var f = new Fixture())
            {
                var tuning = f.Zone.TransitionSettings; tuning.ReflowOvershoot = 0; tuning.ReflowSeconds = .4f;
                f.Zone.SetTransitionSettings(tuning);
                f.Zone.Present(Data(Card("a"), Card("b")), false);
                var card = f.Zone.Views[0]; var start = card.DisplayPose.Position;
                f.Zone.Present(Data(Card("b"), Card("a")));
                f.Zone.AdvanceMotion(.2f);
                Near(Vector3.Lerp(start, card.transform.localPosition, .875f), card.DisplayPose.Position);
                foreach (float value in new[] { -.1f, 3.1f, float.NaN, float.PositiveInfinity })
                {
                    var invalid = tuning; invalid.ReflowOvershoot = value;
                    Assert.Throws<ArgumentException>(() => f.Zone.SetTransitionSettings(invalid));
                    Assert.AreEqual(0, f.Zone.TransitionSettings.ReflowOvershoot);
                }
                tuning.ReflowOvershoot = 2.1f; f.Zone.SetTransitionSettings(tuning);
                string saved = JsonUtility.ToJson(f.Zone);
                f.Zone.SetTransitionSettings(CardZoneTransitionSettings.Default);
                JsonUtility.FromJsonOverwrite(saved, f.Zone);
                Assert.AreEqual(2.1f, f.Zone.TransitionSettings.ReflowOvershoot);
                Assert.AreEqual(.4f, f.Zone.TransitionSettings.ReflowSeconds);
            }
        }

        [Test] public void ReflowCuesOncePerCommittedBatchAndSkipsRepeatedSnapshots()
        {
            using (var f = new Fixture())
            {
                int cues = 0;
                CardZoneViewData committed = null;
                f.Zone.ReflowStarted += () => { cues++; committed = f.Zone.Data; };
                f.Zone.Present(Data(Card("a"), Card("b"), Card("c")), false);
                var sorted = Data(Card("c"), Card("b"), Card("a"));
                f.Zone.Present(sorted);
                Assert.AreEqual(1, cues); Assert.AreSame(sorted, committed);
                f.Zone.AdvanceMotion(.05f);
                f.Zone.Present(Data(Card("c"), Card("b"), Card("a")));
                Assert.AreEqual(1, cues, "An unfinished reflow must not make repeated snapshots cue again.");
                f.Zone.AdvanceMotion(2);
                f.Zone.Present(Data(Card("c", selected: true), Card("b"), Card("a")));
                Assert.AreEqual(1, cues, "Selection does not change the layout target.");
                f.Zone.Present(Data(Card("c"), Card("b"), Card("a"), Card("d")));
                Assert.AreEqual(2, cues, "Settled cards making room cue once, independently of the incoming card.");
                f.Zone.AdvanceMotion(2);
                f.Zone.Present(Data(Card("c"), Card("a")));
                Assert.AreEqual(3, cues, "Survivors closing a gap cue once.");
                f.Zone.Present(Data());
                Assert.AreEqual(3, cues, "Removing every card leaves no reflow.");
            }
        }

        [Test] public void ReflowCuesSkipRestoresInvalidUpdatesAndDisabledAnimations()
        {
            using (var f = new Fixture())
            {
                int cues = 0; f.Zone.ReflowStarted += () => cues++;
                f.Zone.Present(Data(Card("a"), Card("b")), false);
                f.Zone.Present(Data(Card("b"), Card("a")), false);
                Assert.Throws<ArgumentException>(() => f.Zone.Present(Data(Card("a"), Card("a"))));
                f.Root.SetActive(false);
                f.Zone.Present(Data(Card("a"), Card("b")));
                f.Root.SetActive(true);
                var tuning = f.Zone.TransitionSettings; tuning.Enabled = false;
                f.Zone.SetTransitionSettings(tuning);
                f.Zone.Present(Data(Card("b"), Card("a")));
                Assert.AreEqual(0, cues);
            }
        }

        [Test] public void NewCardFlightsAndTheirRetargetingDoNotCueReflow()
        {
            using (var f = new Fixture())
            {
                int cues = 0; f.Zone.ReflowStarted += () => cues++;
                f.Zone.Present(Data(Card("a"), Card("b")));
                f.Zone.Present(Data(Card("b"), Card("a"), Card("c")));
                Assert.AreEqual(0, cues);
                f.Zone.AdvanceMotion(3);
                f.Zone.Present(Data(Card("a"), Card("b"), Card("c")));
                Assert.AreEqual(1, cues);
            }
        }

        [Test] public void SortingSnapshotsReuseViewsAndRetargetReflowWithoutDealingCardsAgain()
        {
            using (var f = new Fixture())
            {
                var model = new CardPresentation.Editor.CardZonePreviewModel();
                foreach (var art in new[] { "diamonds_a", "hearts_q", "spades_02", "clubs_k" }) model.Add(art);
                f.Zone.Present(model.BuildZone(), model.Project, animate: false);
                var views = f.Zone.Views.ToDictionary(v => v.ViewId);
                var poses = views.ToDictionary(pair => pair.Key, pair => pair.Value.DisplayPose);
                model.SortBySuit(); f.Zone.Present(model.BuildZone(), model.Project);
                Assert.IsTrue(f.Zone.IsTransitioning);
                foreach (var card in f.Zone.Views)
                {
                    Assert.AreSame(views[card.ViewId], card); Assert.IsFalse(card.IsEntering);
                    Near(poses[card.ViewId].Position, card.DisplayPose.Position);
                }
                f.Zone.AdvanceMotion(0.05f);
                poses = views.ToDictionary(pair => pair.Key, pair => pair.Value.DisplayPose);
                model.SortByRank(); f.Zone.Present(model.BuildZone(), model.Project);
                foreach (var card in f.Zone.Views)
                {
                    Assert.AreSame(views[card.ViewId], card); Assert.IsFalse(card.IsEntering);
                    Near(poses[card.ViewId].Position, card.DisplayPose.Position);
                }
                f.Zone.AdvanceMotion(1); Settled(f.Zone);
                CollectionAssert.AreEqual(model.Cards.Select(c => c.Id), f.Zone.Views.Select(v => v.ViewId));
                CollectionAssert.AreEqual(new[] { "diamonds_a", "clubs_k", "hearts_q", "spades_02" }, f.Zone.Views.Select(v => v.Data.FaceArtId));
            }
        }

        [Test] public void SceneDeckAnchorIsSampledInZoneSpaceAndLocalFallbackCanBeChanged()
        {
            using (var f = new Fixture())
            {
                var anchor = new GameObject("Deck") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(anchor, f.Scene);
                try
                {
                    f.Zone.transform.SetPositionAndRotation(new Vector3(2, 3, -1), Quaternion.Euler(15, 45, 13));
                    f.Zone.transform.localScale = Vector3.one * 1.2f;
                    anchor.transform.position = new Vector3(4, 2, -1);
                    f.Zone.SetDeckSource(new Vector3(0.4f, 0.03f, 0), anchor.transform);
                    f.Zone.Present(Data(Card("a")));
                    Near(anchor.transform.position, f.Zone.transform.TransformPoint(f.Zone.Views[0].DisplayPose.Position));
                    Near(anchor.transform.position, f.Zone.DeckWorldPosition);
                    f.Zone.CompleteTransitions(); anchor.transform.position += Vector3.right;
                    f.Zone.Present(Data(Card("a"), Card("b")));
                    Near(anchor.transform.position, f.Zone.transform.TransformPoint(f.Zone.Views[1].DisplayPose.Position));
                    f.Zone.CompleteTransitions(); f.Zone.SetDeckSource(new Vector3(0.5f, 0.02f, 0));
                    f.Zone.Present(Data(Card("a"), Card("b"), Card("c")));
                    Near(new Vector3(0.5f, 0.02f, 0), f.Zone.Views[2].DisplayPose.Position);
                }
                finally { Object.DestroyImmediate(anchor); }
            }
        }

        [Test] public void SkipDisableDestroyAndClearCleanOwnedTweensWithoutAdvancingForeignTween()
        {
            var foreignId = new object(); float value = 0;
            var foreign = DOTween.To(() => value, x => value = x, 1, 5).SetUpdate(UpdateType.Manual).SetId(foreignId);
            try
            {
                using (var f = new Fixture(previewScene: false))
                {
                    var data = Data(Card("a"), Card("b"), Card("c")); f.Zone.Present(data); f.Zone.AdvanceMotion(0.06f);
                    var owned = f.Zone.Views.Select(v => DOTween.TweensByTarget(v.TransitionRoot).Single()).ToArray();
                    f.Zone.CompleteTransitions(); Settled(f.Zone); Assert.IsTrue(owned.All(t => !t.IsActive()));
                    f.Zone.Clear(); f.Zone.Present(data); owned = f.Zone.Views.Select(v => DOTween.TweensByTarget(v.TransitionRoot).Single()).ToArray();
                    f.Zone.enabled = false; Settled(f.Zone); Assert.IsTrue(owned.All(t => !t.IsActive()));
                    f.Zone.enabled = true; f.Zone.AdvanceMotion(0.1f); Settled(f.Zone);
                    f.Zone.Clear(); f.Zone.Present(data); var removed = f.Zone.Views[0];
                    var removedTween = DOTween.TweensByTarget(removed.TransitionRoot).Single(); Object.DestroyImmediate(removed.gameObject);
                    Assert.IsFalse(removedTween.IsActive()); f.Zone.Present(data); Assert.IsFalse(f.Zone.Views[0].IsEntering);
                    owned = f.Zone.Views.Where(v => v.IsTransitioning).Select(v => DOTween.TweensByTarget(v.TransitionRoot).Single()).ToArray();
                    f.Zone.Clear(); Assert.IsTrue(owned.All(t => !t.IsActive()));
                    Assert.IsTrue(foreign.IsActive()); Assert.AreEqual(0, value);
                }
            }
            finally { DOTween.Kill(foreignId); }
        }

        [Test] public void InvalidInputDoesNotChangeLiveTransitionOrConsumeADealSlot()
        {
            using (var f = new Fixture())
            {
                var initial = Data(Card("a"), Card("b")); f.Zone.Present(initial); f.Zone.AdvanceMotion(0.06f);
                var a = f.Zone.Views[0]; var pose = a.DisplayPose; var tween = DOTween.TweensByTarget(a.TransitionRoot).Single();
                var originalSettings = f.Zone.TransitionSettings; var bad = originalSettings; bad.DealInterval = float.NaN;
                Assert.Throws<ArgumentException>(() => f.Zone.SetTransitionSettings(bad));
                bad = originalSettings; bad.FlightSeconds = 0; Assert.Throws<ArgumentException>(() => f.Zone.SetTransitionSettings(bad));
                Assert.Throws<ArgumentException>(() => f.Zone.SetDeckSource(new Vector3(float.NaN, 0, 0)));
                Assert.Throws<ArgumentException>(() => f.Zone.Present(Data(Card("a"), Card("b"), new CardVisualData("bad", "missing-face"))));
                Assert.AreEqual(originalSettings, f.Zone.TransitionSettings); Assert.AreSame(initial, f.Zone.Data);
                Assert.AreSame(tween, DOTween.TweensByTarget(a.TransitionRoot).Single()); Near(pose.Position, a.DisplayPose.Position);
                f.Zone.Present(Data(Card("a"), Card("b"), Card("c"))); f.Zone.AdvanceMotion(0.1f);
                Assert.IsFalse(Visible(f.Zone.Views[2])); f.Zone.AdvanceMotion(0.09f); Assert.IsTrue(Visible(f.Zone.Views[2]));
            }
        }

        [Test] public void CoreListChangesDriveAnimationsWithoutMutatingCardsOrDisclosingHiddenFaces()
        {
            using (var f = new Fixture())
            {
                var original = new CardZone(new ZoneId("hand"), CardZoneKinds.Hand, null, new[] { new CardInstanceId("a") });
                Func<CardInstanceId, CardVisualData> project = id => Card(id.Value, visible: id.Value != "secret");
                f.Zone.Present(original, project, animate: false);
                var next = original.InsertRange(1, new[] { new CardInstanceId("b"), new CardInstanceId("secret") });
                f.Zone.Present(next, project);
                Assert.AreEqual(1, original.Cards.Count); Assert.AreEqual(3, next.Cards.Count);
                var secret = f.Zone.Views[2]; Assert.AreEqual("card_back", secret.Surface.CardId); Assert.IsNull(secret.Data.FaceArtId);
                f.Zone.AdvanceMotion(0.2f); Assert.AreEqual("card_back", secret.Surface.CardId);
                var removed = next.Remove(new CardInstanceId("b")); f.Zone.Present(removed, project);
                CollectionAssert.AreEqual(new[] { "a", "secret" }, f.Zone.Views.Select(v => v.ViewId));
                f.Zone.AdvanceMotion(2); Settled(f.Zone); Assert.AreEqual("card_back", secret.Surface.CardId);
            }
        }

        [Test] public void ImmediateRestoreAndDisabledTransitionsSnapWithoutReplayOrResidualOffsets()
        {
            using (var f = new Fixture())
            {
                var data = Data(Card("a"), Card("b")); f.Zone.Present(data); f.Zone.AdvanceMotion(0.1f);
                f.Zone.Present(data, animate: false); Settled(f.Zone);
                f.Zone.Present(Data(Card("a"), Card("b"), Card("c")));
                var settings = f.Zone.TransitionSettings; settings.Enabled = false; f.Zone.SetTransitionSettings(settings); Settled(f.Zone);
                f.Zone.Present(Data(Card("c"), Card("b"), Card("a"), Card("d"))); Settled(f.Zone);
                settings.Enabled = true; f.Zone.SetTransitionSettings(settings);
                f.Zone.Present(Data(Card("c"), Card("b"), Card("a"), Card("d"), Card("e")));
                Assert.IsFalse(f.Zone.Views[0].IsEntering); Assert.IsTrue(f.Zone.Views[4].IsEntering); Assert.IsTrue(Visible(f.Zone.Views[4]));
                f.Zone.Present(new CardZoneViewData(new ZoneId("replacement"), new[] { Card("fresh") }));
                Assert.IsTrue(Visible(f.Zone.Views[0])); f.Zone.AdvanceMotion(2); Settled(f.Zone);
            }
        }
    }
}
