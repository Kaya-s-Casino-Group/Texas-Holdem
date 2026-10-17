using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using CardPresentation.Editor;
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
    public sealed class CardZoneDiscardTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly GameObject Root;
            public readonly HandZoneView View;
            public readonly CardZonePlayHost Host;
            public readonly CardZonePreviewModel Model = new CardZonePreviewModel();
            public Fixture(int count = 4)
            {
                Root = new GameObject("Discard Test") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(Root, Scene);
                View = Root.AddComponent<HandZoneView>();
                View.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var motion = View.MotionSettings; motion.Enabled = false; View.SetMotionSettings(motion);
                Model.Insert(0, "hearts_q", count);
                Host = Root.AddComponent<CardZonePlayHost>(); Capture();
            }
            public void Capture()
            {
                Host.Capture(Model.BuildZone(), Model.Cards.Select(card => new CardZoneCardState(
                    card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)));
                Host.BeginSession();
            }
            public void Select(params int[] indexes)
            {
                foreach (int index in indexes) Host.SetInteraction(Host.Cards[index].Id, new CardInteractionState(isSelected: true));
                Host.Publish();
            }
            public void Dispose()
            {
                Host.StopSession(); View.Clear(); Object.DestroyImmediate(Root); EditorSceneManager.ClosePreviewScene(Scene);
            }
        }
        private static void Near(Vector3 a, Vector3 b) => Assert.Less(Vector3.Distance(a, b), 0.00001f);

        [Test] public void DrawAndDiscardAudioKeepIndependentOverridesVolumeAndMuteAcrossSerialization()
        {
            var drawClip = AudioClip.Create("Draw Override", 256, 1, 48000, false);
            var discardClip = AudioClip.Create("Discard Override", 256, 1, 48000, false);
            try
            {
                using (var f = new Fixture(1))
                {
                    var placeholder = Resources.Load<AudioClip>(HandZoneView.DefaultDiscardSoundResource);
                    Assert.IsNotNull(placeholder); Assert.AreSame(placeholder, f.View.DiscardSound);
                    Assert.AreSame(f.View.DrawSound, f.View.DiscardSound); Assert.IsTrue(f.View.DiscardSoundEnabled);
                    Assert.AreEqual(1, f.View.DiscardSoundVolume);
                    f.View.SetDrawSound(drawClip, 0.3f, enabled: false);
                    Assert.AreSame(placeholder, f.View.DiscardSound); Assert.IsTrue(f.View.DiscardSoundEnabled);
                    f.View.SetDiscardSound(discardClip, 0.7f);
                    Assert.AreSame(drawClip, f.View.DrawSound); Assert.AreEqual(0.3f, f.View.DrawSoundVolume);
                    Assert.IsFalse(f.View.DrawSoundEnabled);
                    string saved = JsonUtility.ToJson(f.View);
                    f.View.SetDrawSound(null); f.View.SetDiscardSound(null, 0, enabled: false);
                    Assert.AreSame(placeholder, f.View.DiscardSound); Assert.IsTrue(f.View.DrawSoundEnabled);
                    Assert.AreEqual(1, f.View.DrawSoundVolume);
                    JsonUtility.FromJsonOverwrite(saved, f.View);
                    Assert.AreSame(drawClip, f.View.DrawSoundOverride); Assert.IsFalse(f.View.DrawSoundEnabled);
                    Assert.AreSame(discardClip, f.View.DiscardSoundOverride); Assert.IsTrue(f.View.DiscardSoundEnabled);
                    Assert.AreEqual(0.3f, f.View.DrawSoundVolume); Assert.AreEqual(0.7f, f.View.DiscardSoundVolume);
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f, 1.1f })
                        Assert.Throws<ArgumentOutOfRangeException>(() => f.View.SetDiscardSound(null, invalid, enabled: false));
                    Assert.AreSame(discardClip, f.View.DiscardSoundOverride); Assert.AreEqual(0.7f, f.View.DiscardSoundVolume);
                    Assert.IsTrue(f.View.DiscardSoundEnabled); Assert.IsFalse(f.View.DrawSoundEnabled);
                    f.View.SetDiscardSound(null);
                    Assert.AreSame(placeholder, f.View.DiscardSound); Assert.AreSame(drawClip, f.View.DrawSound);
                    Assert.AreEqual(0.3f, f.View.DrawSoundVolume); Assert.IsFalse(f.View.DrawSoundEnabled);
                }
            }
            finally { Object.DestroyImmediate(drawClip); Object.DestroyImmediate(discardClip); }
        }

        [Test] public void DiscardCuesFollowEachLaunchAfterListCommitAndLongFramesDoNotLoseOrRepeatThem()
        {
            using (var f = new Fixture(3))
            {
                f.Model.Cards[1].FaceUp = false; f.Capture(); f.Select(0, 1, 2);
                var ids = f.Host.Cards.Select(card => card.Id).ToArray();
                var cues = new List<string>(); int deals = 0;
                Assert.AreSame(Resources.Load<AudioClip>(HandZoneView.DefaultDrawSoundResource), f.View.DrawSound);
                f.View.CardEnteredZone += _ => deals++;
                f.View.CardDiscardStarted += card => {
                    Assert.IsNotNull(card); Assert.IsTrue(card.gameObject.activeSelf);
                    Assert.IsFalse(f.View.TryGetView(card.ViewId, out _));
                    Assert.IsFalse(f.View.Data.Cards.Any(current => current.ViewId == card.ViewId));
                    cues.Add(card.ViewId);
                };
                f.Host.DiscardSelected(); CollectionAssert.AreEqual(ids.Take(1), cues);
                f.View.AdvanceMotion(f.View.DiscardSettings.Interval / 2); Assert.AreEqual(1, cues.Count);
                f.Host.Publish(); f.View.RefreshLayout(); f.View.AdvanceMotion(0); Assert.AreEqual(1, cues.Count);
                f.View.AdvanceMotion(f.View.DiscardSettings.Interval / 2 + 0.001f);
                CollectionAssert.AreEqual(ids.Take(2), cues);
                f.View.AdvanceMotion(2); CollectionAssert.AreEqual(ids, cues);
                Assert.AreEqual(0, f.View.DepartingViews.Count); Assert.AreEqual(0, deals);
                f.Host.Publish(); f.View.AdvanceMotion(2); CollectionAssert.AreEqual(ids, cues);
                Assert.IsEmpty(f.Root.GetComponentsInChildren<AudioSource>()); // Editor previews emit cues without playing audio.
            }
        }

        [Test] public void CancelledSilentAndDisabledDiscardsDoNotCueButAnimationOffDiscardsStillDo()
        {
            using (var f = new Fixture(3))
            {
                int cues = 0; f.View.CardDiscardStarted += _ => cues++;
                Assert.AreEqual(0, f.Host.DiscardSelected()); Assert.AreEqual(0, cues);
                f.Select(0, 1, 2); f.Host.DiscardSelected(); Assert.AreEqual(1, cues);
                f.View.CompleteTransitions(); f.View.AdvanceMotion(2); Assert.AreEqual(1, cues);
                f.Host.Match.RestartMatch(); f.Host.BeginSession(); f.Select(0, 1, 2); f.Host.DiscardSelected(); Assert.AreEqual(2, cues);
                f.View.Present(f.View.Data, animate: false); f.View.AdvanceMotion(2); Assert.AreEqual(2, cues);
                f.Host.Match.RestartMatch(); f.Host.BeginSession(); f.Select(0, 1, 2); f.Host.DiscardSelected(animate: false);
                f.View.AdvanceMotion(2); Assert.AreEqual(2, cues);
                f.Host.Match.RestartMatch(); f.Host.BeginSession(); var tuning = f.View.TransitionSettings; tuning.Enabled = false; f.View.SetTransitionSettings(tuning);
                f.Select(0, 1, 2); var old = f.View.Views.ToArray(); f.Host.DiscardSelected();
                Assert.AreEqual(5, cues); Assert.IsTrue(old.All(card => card == null)); Assert.AreEqual(0, f.View.DepartingViews.Count);
                f.Host.DiscardSelected(); f.Host.Publish(); f.View.AdvanceMotion(2); Assert.AreEqual(5, cues);
                f.Model.Cards[0].IsDisabled = true; f.Model.Cards[0].IsSelected = true; f.Capture();
                Assert.Throws<InvalidOperationException>(() => f.Host.DiscardSelected()); Assert.AreEqual(5, cues);
                f.Model.Cards[0].IsDisabled = false; f.Capture(); f.View.enabled = false;
                f.Host.DiscardSelected(); Assert.AreEqual(5, cues);
            }
        }

        [Test] public void DiscardTransfersSelectedInHandOrderAndPreservesCardDataAndStartupSnapshot()
        {
            using (var f = new Fixture())
            {
                f.Model.Cards[1].Effects = CardEffects.Ineffective | CardEffects.Laminated;
                f.Model.Cards[1].OriginId = "reward/test"; f.Model.Cards[1].Tint = Color.cyan;
                f.Model.Cards[3].FaceUp = false; f.Capture();
                var ids = f.Host.Zone.Cards.ToArray(); var retained = f.View.Views[0];
                f.Select(3, 1); var selectedState = f.Host.Cards[1];
                int drawSounds = 0; f.View.CardEnteredZone += _ => drawSounds++;
                Assert.AreEqual(2, f.Host.DiscardSelected());
                CollectionAssert.AreEqual(new[] { ids[0], ids[2] }, f.Host.Zone.Cards);
                CollectionAssert.AreEqual(new[] { ids[1], ids[3] }, f.Host.DiscardZone.Cards);
                CollectionAssert.AreEqual(f.Host.Zone.Cards.Select(id => id.Value), f.View.Data.Cards.Select(card => card.ViewId));
                CollectionAssert.AreEqual(f.Host.DiscardZone.Cards.Select(id => id.Value), f.View.DepartingViews.Select(card => card.ViewId));
                Assert.AreEqual(CardZoneKinds.Discard, f.Host.DiscardZone.Kind);
                Assert.AreSame(retained, f.View.Views[0]); Assert.IsTrue(selectedState.Interaction.IsSelected);
                var discarded = f.Host.DiscardedCards[0];
                Assert.AreEqual(f.Model.Cards[1].Effects, discarded.Card.Effects); Assert.AreEqual("reward/test", discarded.Card.OriginId);
                Assert.AreEqual(Color.cyan, discarded.Tint); Assert.IsTrue(discarded.FaceUp); Assert.IsFalse(f.Host.DiscardedCards[1].FaceUp);
                Assert.IsTrue(f.Host.DiscardedCards.All(card => !card.Interaction.IsSelected && !card.Interaction.IsHovered));
                Assert.IsFalse(f.Host.TryGetInteraction(ids[1].Value, out _)); Assert.IsFalse(f.View.TryGetView(ids[1].Value, out _));
                CollectionAssert.AreEqual(ids, f.Host.InitialCards.Select(card => card.Card.Id));
                f.Host.Publish(); f.View.AdvanceMotion(2);
                Assert.AreEqual(0, drawSounds); Assert.AreEqual(0, f.View.DepartingViews.Count);
                Assert.AreEqual(2, f.Root.GetComponentsInChildren<CardInstanceView>().Length);
                f.Host.Match.RestartMatch(); f.Host.BeginSession(); Assert.AreEqual(4, f.Host.Cards.Count); Assert.AreEqual(0, f.Host.DiscardedCards.Count);
            }
        }

        [Test] public void EmptySelectionIsNoOpAndDisabledSelectionRejectsTheWholeBatch()
        {
            using (var f = new Fixture())
            {
                Assert.AreEqual(0, f.Host.DiscardSelected());
                f.Model.Cards[0].IsDisabled = true; f.Model.Cards[0].IsSelected = true;
                f.Model.Cards[2].IsSelected = true; f.Capture();
                var before = f.View.Data; var state = f.Host.Cards[2];
                Assert.Throws<InvalidOperationException>(() => f.Host.DiscardSelected());
                Assert.AreSame(before, f.View.Data); Assert.AreSame(state, f.Host.Cards[2]);
                Assert.AreEqual(4, f.Host.Cards.Count); Assert.AreEqual(0, f.Host.DiscardedCards.Count);
                Assert.AreEqual(0, f.View.DepartingViews.Count); Assert.IsTrue(state.Interaction.IsSelected);
                f.Host.SetInteraction(f.Host.Cards[0].Id, default(CardInteractionState));
                Assert.AreEqual(1, f.Host.DiscardSelected(false)); Assert.AreEqual(0, f.View.DepartingViews.Count);
            }
        }

        [Test] public void ExitStartsAtActualLiftAndSwayFlipsWithoutChangingDataAndDisappearsAtAnchor()
        {
            using (var f = new Fixture(1))
            {
                f.Root.transform.SetPositionAndRotation(Vector3.one, Quaternion.Euler(32, 17, -12));
                f.Root.transform.localScale = new Vector3(1.2f, 0.8f, 1.1f);
                var layout = f.View.Layout; layout.ArcHeight = 0.008f; f.View.SetLayout(layout);
                f.View.SetMotionSettings(CardZoneMotionSettings.Default); f.View.AdvanceMotion(0.31f);
                f.Select(0); f.View.AdvanceMotion(0.03f);
                var card = f.View.Views[0]; var position = card.FaceRoot.position; var rotation = card.SwayRoot.rotation;
                var data = card.Data; var tint = card.Surface.Tint;
                var anchor = new GameObject("Discard Anchor"); anchor.transform.SetParent(f.Root.transform, false);
                anchor.transform.localPosition = new Vector3(-0.4f, 0.03f, 0.05f);
                f.View.SetDiscardDestination(Vector3.zero, anchor.transform);
                Assert.AreEqual(1, f.Host.DiscardSelected());
                Assert.AreEqual(0, f.View.Views.Count); Assert.IsTrue(card.IsExiting); Assert.IsFalse(card.IsLifting);
                Near(position, card.FaceRoot.position); Assert.Less(Quaternion.Angle(rotation, card.SwayRoot.rotation), 0.001f);
                Assert.IsFalse(CardZoneRaycaster.TryHit(f.View, new Ray(position + f.Root.transform.up, -f.Root.transform.up), null, out _));
                f.View.AdvanceMotion(f.View.DiscardSettings.FlightSeconds / 2);
                Assert.AreEqual(90, card.DealFlipAngle, 0.001f); Assert.AreSame(data, card.Data); Assert.IsTrue(card.Data.FaceUp);
                Assert.AreEqual(tint, card.Surface.Tint);
                var tween = DOTween.TweensByTarget(card.TransitionRoot).Single();
                tween.ManualUpdate(f.View.DiscardSettings.FlightSeconds, f.View.DiscardSettings.FlightSeconds);
                Near(anchor.transform.position, card.FaceRoot.position); Assert.AreEqual(180, card.DealFlipAngle, 0.001f);
                Assert.Less(Vector3.Dot(card.FaceRoot.parent.TransformDirection(card.FaceRoot.localRotation * card.DealFlipRoot.localRotation * Vector3.up).normalized,
                    f.Root.transform.up), -0.999f);
                f.View.AdvanceMotion(0); Assert.IsTrue(card == null); Assert.IsFalse(f.View.IsTransitioning);
            }
        }

        [Test] public void HiddenCardsStayBackUpAndDepartureDelaysDoNotReplayOnPublishOrReorder()
        {
            using (var f = new Fixture(3))
            {
                f.Model.Cards[0].FaceUp = false; f.Capture(); f.Select(0, 2);
                var first = f.View.Views[0]; var second = f.View.Views[2]; var secondPosition = second.FaceRoot.position;
                f.Host.DiscardSelected(); var tween = DOTween.TweensByTarget(first.TransitionRoot).Single();
                f.View.AdvanceMotion(f.View.DiscardSettings.Interval / 2);
                Assert.AreEqual(0, first.DealFlipAngle); Assert.IsNull(first.Data.FaceArtId); Near(secondPosition, second.FaceRoot.position);
                f.Host.Publish(); f.View.RefreshLayout();
                Assert.AreSame(tween, DOTween.TweensByTarget(first.TransitionRoot).Single());
                f.View.AdvanceMotion(f.View.DiscardSettings.FlightSeconds - f.View.DiscardSettings.Interval / 2 + 0.0001f);
                Assert.IsTrue(first == null); Assert.IsTrue(second != null); Assert.AreEqual(1, f.View.DepartingViews.Count);
                f.View.AdvanceMotion(f.View.DiscardSettings.Interval + 0.001f); Assert.IsTrue(second == null);
                Assert.AreEqual(1, f.View.Views.Count);
            }
        }

        [Test] public void RapidBatchesAndDiscardingLastCardContinueAnimatingAnEmptyZone()
        {
            using (var f = new Fixture(3))
            {
                f.Select(1); f.Host.DiscardSelected(); var first = f.View.DepartingViews[0];
                f.View.AdvanceMotion(0.05f); var position = first.FaceRoot.position;
                f.Select(0, 1); f.Host.DiscardSelected();
                Assert.AreEqual(0, f.Host.Cards.Count); Assert.AreEqual(3, f.Host.DiscardedCards.Count);
                Assert.AreEqual(3, f.View.DepartingViews.Count); Near(position, first.FaceRoot.position);
                f.Host.Publish(); Assert.AreEqual(3, f.View.DepartingViews.Count);
                f.View.AdvanceMotion(2); Assert.IsFalse(f.View.IsTransitioning);
                Assert.AreEqual(0, f.Root.GetComponentsInChildren<CardInstanceView>().Length);
            }
        }

        [Test] public void DiscardingAnArrivalPreservesPartialFlipAndCancelsUnstartedCardsSilently()
        {
            using (var f = new Fixture(1))
            {
                f.Host.ReplaceCards(new CardZoneCardState[0]);
                f.Model.Insert(1, "spades_a"); f.Model.Insert(2, "clubs_k");
                var states = f.Model.Cards.Select(card => new CardZoneCardState(card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)).ToArray();
                int cues = 0, discardCues = 0; f.View.CardEnteredZone += _ => cues++;
                f.View.CardDiscardStarted += _ => discardCues++;
                f.Host.ReplaceCards(states); f.View.AdvanceMotion(0.04f);
                var visible = f.View.Views[0]; var queued = f.View.Views[1];
                var before = visible.FaceRoot.position; float flip = visible.DealFlipAngle;
                f.Select(0, 1, 2); f.Host.DiscardSelected();
                Near(before, visible.FaceRoot.position); Assert.AreEqual(flip, visible.DealFlipAngle);
                Assert.IsTrue(queued == null); Assert.AreEqual(1, f.View.DepartingViews.Count);
                Assert.AreEqual(3, f.Host.DiscardedCards.Count);
                f.View.AdvanceMotion(2); Assert.AreEqual(0, cues); Assert.AreEqual(1, discardCues); Assert.AreEqual(0, f.View.DepartingViews.Count);
            }
        }

        [Test] public void RestoreClearDisableAndImmediateCompletionRemoveGhostsAndOnlyOwnedTweens()
        {
            float value = 0; var foreignId = new object();
            var foreign = DOTween.To(() => value, next => value = next, 1, 5).SetUpdate(UpdateType.Manual).SetId(foreignId);
            try
            {
                using (var f = new Fixture(1))
                {
                    Action discard = () => { f.Host.Match.RestartMatch(); f.Host.BeginSession(); f.Select(0); f.Host.DiscardSelected(); };
                    discard(); var restored = f.Host.InitialCards[0].Project(); var old = f.View.DepartingViews[0];
                    f.View.Present(new CardZoneViewData(f.Host.Zone.Id, new[] { restored }));
                    Assert.IsTrue(old == null); Assert.AreEqual(0, f.View.DepartingViews.Count); Assert.AreEqual(1, f.View.Views.Count);
                    discard(); old = f.View.DepartingViews[0]; f.View.Present(f.View.Data, animate: false); Assert.IsTrue(old == null);
                    discard(); old = f.View.DepartingViews[0]; f.View.CompleteTransitions(); Assert.IsTrue(old == null);
                    discard(); old = f.View.DepartingViews[0]; f.View.enabled = false; Assert.IsTrue(old == null); f.View.enabled = true;
                    discard(); old = f.View.DepartingViews[0]; var owned = DOTween.TweensByTarget(old.TransitionRoot).Single();
                    f.View.Clear(); Assert.IsTrue(old == null); Assert.IsFalse(owned.IsActive());
                    Assert.IsTrue(foreign.IsActive()); Assert.AreEqual(0, value);
                }
            }
            finally { DOTween.Kill(foreignId); }
        }

        [Test] public void InvalidDiscardHintsAndSettingsLeaveExistingViewsAndDataUntouched()
        {
            using (var f = new Fixture(1))
            {
                var original = f.View.Data; string id = original.Cards[0].ViewId;
                Assert.Throws<ArgumentException>(() => f.View.Present(original, discardedViewIds: new[] { id }));
                var empty = new CardZoneViewData(original.ZoneId, new CardVisualData[0]);
                Assert.Throws<ArgumentException>(() => f.View.Present(empty, discardedViewIds: new[] { "unknown" }));
                Assert.Throws<ArgumentException>(() => f.View.Present(empty, discardedViewIds: new[] { id, id }));
                var bad = f.View.DiscardSettings; bad.FlightSeconds = float.NaN;
                Assert.Throws<ArgumentException>(() => f.View.SetDiscardSettings(bad));
                Assert.Throws<ArgumentException>(() => f.View.SetDiscardDestination(new Vector3(float.PositiveInfinity, 0, 0)));
                Assert.AreSame(original, f.View.Data); Assert.AreEqual(1, f.View.Views.Count); Assert.AreEqual(0, f.View.DepartingViews.Count);
                f.Select(0); f.Host.DiscardSelected();
                Assert.Throws<ArgumentException>(() => f.Host.ReplaceCards(f.Host.InitialCards));
                Assert.AreEqual(0, f.Host.Cards.Count); Assert.AreEqual(1, f.Host.DiscardedCards.Count);
            }
        }

        [Test] public void BakedDiscardPileSerializesAndRestartsWithNoLingeringSelectionOrVisuals()
        {
            using (var f = new Fixture(2))
            {
                f.Select(1); f.Host.DiscardSelected();
                f.Host.Capture(f.Host.Zone, f.Host.Cards, discarded: f.Host.DiscardedCards);
                string json = JsonUtility.ToJson(f.Host); JsonUtility.FromJsonOverwrite(json, f.Host); f.Host.Match.RestartMatch(); f.Host.BeginSession();
                Assert.AreEqual(1, f.Host.Cards.Count); Assert.AreEqual(1, f.Host.DiscardedCards.Count);
                Assert.AreEqual(1, f.Host.InitialDiscardedCards.Count); Assert.AreEqual(0, f.View.DepartingViews.Count);
                Assert.IsFalse(f.Host.DiscardedCards[0].Interaction.IsSelected);
                f.Select(0); f.Host.DiscardSelected(); f.Host.Match.RestartMatch(); f.Host.BeginSession();
                Assert.AreEqual(1, f.Host.Cards.Count); Assert.AreEqual(1, f.Host.DiscardedCards.Count);
                Assert.AreEqual(1, f.Root.GetComponentsInChildren<CardInstanceView>().Length);
            }
        }
    }
}
