using System;
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
    public sealed class CardZonePlayTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly HandZoneView View;
            public readonly CardZonePlayHost Host;
            public readonly CardZonePreviewModel Model = new CardZonePreviewModel();
            public Fixture()
            {
                Scene = EditorSceneManager.NewPreviewScene(); Root = new GameObject("Baked Zone") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(Root, Scene);
                View = Root.AddComponent<HandZoneView>();
                View.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var motion = View.MotionSettings; motion.Enabled = false; View.SetMotionSettings(motion);
                var transitions = View.TransitionSettings; transitions.Enabled = false; View.SetTransitionSettings(transitions);
                Model.Add("hearts_q"); Model.Add("spades_a");
                Host = Root.AddComponent<CardZonePlayHost>(); Capture(); Host.BeginSession();
            }
            public void Capture() => Host.Capture(Model.BuildZone(), Model.Cards.Select(c =>
                new CardZoneCardState(c.ToCard(), c.ArtId, c.FaceUp, c.Tint, c.Interaction)));
            public void Set(bool hovered, bool selected)
            { Host.SetInteraction(Host.Cards[0].Id, new CardInteractionState(hovered, selected)); Host.Publish(); }
            public void Dispose()
            { Host.StopSession(); View.Clear(); Object.DestroyImmediate(Root); EditorSceneManager.ClosePreviewScene(Scene); }
        }

        [Test] public void SelectionCuesFollowCardStateEdgesAndSkipHoverDeselectRefreshAndRestores()
        {
            using (var f = new Fixture())
            {
                int cues = 0; string id = f.Host.Cards[0].Id;
                f.View.CardSelected += card => {
                    Assert.IsTrue(card.Data.Interaction.IsSelected);
                    Assert.IsTrue(f.View.TryGetView(card.ViewId, out var current)); Assert.AreSame(card, current);
                    Assert.IsTrue(f.View.Data.Cards.Single(data => data.ViewId == card.ViewId).Interaction.IsSelected);
                    cues++;
                };
                f.Set(true, false); Assert.AreEqual(0, cues);
                f.Set(true, true); Assert.AreEqual(1, cues); // Also works with list transitions disabled.
                f.Set(false, true); f.Set(true, true); f.Host.Publish(); f.View.RefreshLayout(); f.View.AdvanceMotion(1);
                f.Host.ReplaceCards(f.Host.Cards.Reverse()); Assert.AreEqual(1, cues);
                f.Host.SetInteraction(id, default(CardInteractionState)); f.Host.Publish(); Assert.AreEqual(1, cues);
                f.Host.SetInteraction(id, new CardInteractionState(isSelected: true)); f.Host.Publish(); Assert.AreEqual(2, cues);

                f.Host.BeginSession(); Assert.AreEqual(2, cues);
                f.Host.SetInteraction(id, new CardInteractionState(isSelected: true));
                f.View.Present(f.Host.Zone, cardId => f.Host.Cards.Single(card => card.Id == cardId.Value).Project(), animate: false);
                f.Host.Publish(); Assert.AreEqual(2, cues);
                var selected = f.View.Views.Single(card => card.ViewId == id); Object.DestroyImmediate(selected.gameObject);
                f.Host.Publish(); Assert.AreEqual(2, cues); // Repairing a lost visual isn't another selection.
                f.View.Clear(); f.Host.Publish(); Assert.AreEqual(2, cues);
                f.View.Present(new CardZoneViewData(new ZoneId("different-zone"), f.View.Data.Cards)); Assert.AreEqual(2, cues);
                f.Host.Publish(); Assert.AreEqual(2, cues);
                f.View.enabled = false; f.Set(false, false); f.Set(false, true);
                f.View.enabled = true; f.Host.Publish(); Assert.AreEqual(2, cues);

                f.Model.Add("clubs_k"); var newCard = f.Model.Cards.Last();
                f.Host.ReplaceCards(f.Host.Cards.Concat(new[] {
                    new CardZoneCardState(newCard.ToCard(), newCard.ArtId, true, Color.white, new CardInteractionState(isSelected: true)) }));
                Assert.AreEqual(2, cues); // A new card already selected in a snapshot is not an input edge.
                var before = f.View.Data;
                Assert.Throws<ArgumentException>(() => f.View.Present(new CardZoneViewData(f.Host.Zone.Id, new[] {
                    new CardVisualData(id, "missing-card-art", interaction: new CardInteractionState(isSelected: true)) })));
                Assert.AreSame(before, f.View.Data); Assert.AreEqual(2, cues);
                Assert.IsEmpty(f.Root.GetComponentsInChildren<AudioSource>());
            }
        }

        [Test] public void SelectionAudioConfigurationIsIndependentAndSurvivesSerialization()
        {
            var clip = AudioClip.Create("Selection Override", 256, 1, 48000, false);
            try
            {
                using (var f = new Fixture())
                {
                    var placeholder = Resources.Load<AudioClip>(HandZoneView.DefaultSelectionSoundResource);
                    Assert.IsNotNull(placeholder); Assert.AreSame(placeholder, f.View.SelectionSound);
                    Assert.IsTrue(f.View.SelectionSoundEnabled); Assert.AreEqual(1, f.View.SelectionSoundVolume);
                    f.View.SetDrawSound(null, 0.2f, enabled: false); f.View.SetDiscardSound(null, 0.3f);
                    Assert.IsTrue(f.View.SelectionSoundEnabled); Assert.AreEqual(1, f.View.SelectionSoundVolume);
                    f.View.SetSelectionSound(clip, 0.4f, enabled: false);
                    string saved = JsonUtility.ToJson(f.View);
                    f.View.SetSelectionSound(null); Assert.AreSame(placeholder, f.View.SelectionSound);
                    Assert.IsFalse(f.View.DrawSoundEnabled); Assert.AreEqual(0.2f, f.View.DrawSoundVolume);
                    Assert.IsTrue(f.View.DiscardSoundEnabled); Assert.AreEqual(0.3f, f.View.DiscardSoundVolume);
                    JsonUtility.FromJsonOverwrite(saved, f.View);
                    Assert.AreSame(clip, f.View.SelectionSoundOverride); Assert.AreEqual(0.4f, f.View.SelectionSoundVolume);
                    Assert.IsFalse(f.View.SelectionSoundEnabled);
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f, 1.1f })
                        Assert.Throws<ArgumentOutOfRangeException>(() => f.View.SetSelectionSound(null, invalid));
                    Assert.AreSame(clip, f.View.SelectionSoundOverride); Assert.AreEqual(0.4f, f.View.SelectionSoundVolume);
                    Assert.IsFalse(f.View.SelectionSoundEnabled);
                    f.View.SetDrawSound(placeholder); f.View.SetDiscardSound(null, 0);
                    Assert.AreSame(clip, f.View.SelectionSound); Assert.IsFalse(f.View.SelectionSoundEnabled);
                    Assert.AreEqual(0.4f, f.View.SelectionSoundVolume);
                }
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test] public void RuntimeDrawKeepsInteractionAndStartupSnapshotWhileCueingEachNewCardOnce()
        {
            using (var f = new Fixture())
            {
                f.View.SetTransitionSettings(CardZoneTransitionSettings.Default);
                var original = f.Host.InitialCards.Select(card => card.Id).ToArray();
                var firstView = f.View.Views[0]; int cues = 0;
                f.View.CardEnteredZone += _ => cues++;
                var live = new CardZonePreviewModel(); live.Load(f.Host.Cards);
                var deck = new CardZoneDrawExperiment(); deck.PrepareDeck(new[] { "hearts_q", "spades_a" }); deck.Draw(live, 3);
                var drawn = live.Cards.Select(c => new CardZoneCardState(c.ToCard(), c.ArtId, c.FaceUp, c.Tint, c.Interaction)).ToArray();
                f.Host.ReplaceCards(drawn);
                Assert.AreEqual(5, f.Host.Cards.Count); Assert.AreEqual(0, cues); Assert.AreSame(firstView, f.View.Views[0]);
                f.Host.SetInteraction(drawn[4].Id, new CardInteractionState(isSelected: true)); f.Host.Publish();
                f.View.AdvanceMotion(2);
                Assert.AreEqual(3, cues); Assert.AreEqual(5, f.View.Views.Count);
                Assert.IsTrue(f.View.Views[4].Data.Interaction.IsSelected);
                Assert.IsFalse(drawn[4].Interaction.IsSelected);
                CollectionAssert.AreEqual(original, f.Host.InitialCards.Select(card => card.Id));
                Assert.Throws<ArgumentException>(() => f.Host.ReplaceCards(new[] { drawn[0], drawn[0] }));
                Assert.AreEqual(5, f.Host.Cards.Count); Assert.AreEqual(5, f.View.Views.Count);
                f.Host.BeginSession(); f.View.AdvanceMotion(2);
                Assert.AreEqual(3, cues); CollectionAssert.AreEqual(original, f.Host.Cards.Select(card => card.Id));
            }
        }

        [Test] public void HoverIsLowerThanSelectionAndStateChangesRetargetContinuously()
        {
            using (var f = new Fixture())
            {
                var card = f.View.Views[0]; f.Set(true, false);
                Assert.AreEqual(0.01f, f.View.HoveredOffset.z); Assert.AreEqual(0.02f, f.View.SelectedOffset.z);
                Assert.IsTrue(card.Data.Interaction.IsHovered); Assert.IsTrue(card.IsLifting); Assert.AreEqual(Vector3.zero, card.InteractionOffset);
                f.View.AdvanceMotion(1); Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);
                f.Set(true, true); Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);
                f.View.AdvanceMotion(f.View.LiftSettings.RaiseSeconds * 0.25f); float raised = card.InteractionOffset.z;
                Assert.Greater(raised, f.View.HoveredOffset.z); Assert.Less(raised, f.View.SelectedOffset.z);
                var tween = DOTween.TweensByTarget(card.FaceRoot).Single();
                f.Set(false, true); f.Set(true, true); f.Host.Publish();
                Assert.AreSame(tween, DOTween.TweensByTarget(card.FaceRoot).Single()); Assert.AreEqual(raised, card.InteractionOffset.z);
                f.View.AdvanceMotion(1); Assert.AreEqual(f.View.SelectedOffset, card.InteractionOffset); Assert.IsFalse(card.IsLifting);
                f.Set(true, false); Assert.AreEqual(f.View.SelectedOffset, card.InteractionOffset);
                f.View.AdvanceMotion(f.View.LiftSettings.LowerSeconds * 0.25f);
                Assert.Greater(card.InteractionOffset.z, f.View.HoveredOffset.z); Assert.Less(card.InteractionOffset.z, f.View.SelectedOffset.z);
                f.View.AdvanceMotion(1); Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);
                f.Set(false, false); Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);
                f.View.AdvanceMotion(f.View.LiftSettings.LowerSeconds * 0.25f); Assert.Greater(card.InteractionOffset.z, 0); Assert.Less(card.InteractionOffset.z, f.View.HoveredOffset.z);
                f.View.AdvanceMotion(1); Assert.AreEqual(Vector3.zero, card.InteractionOffset);
            }
        }

        [Test] public void HoverAndLowerOvershootThenSettleQuicklyAndCanReverseDuringRebound()
        {
            using (var f = new Fixture())
            {
                var card = f.View.Views[0]; var tuning = f.View.LiftSettings;
                Assert.AreEqual(0.12f, tuning.RaiseSeconds); Assert.AreEqual(0.14f, tuning.LowerSeconds);
                f.Set(true, false); f.View.AdvanceMotion(tuning.RaiseSeconds * 0.65f);
                Assert.Greater(card.InteractionOffset.z, f.View.HoveredOffset.z * 1.1f);
                Assert.Less(card.InteractionOffset.z, f.View.HoveredOffset.z * 1.18f);
                var tween = DOTween.TweensByTarget(card.FaceRoot).Single(); var over = card.InteractionOffset;
                f.Host.Publish(); Assert.AreSame(tween, DOTween.TweensByTarget(card.FaceRoot).Single());
                Assert.AreEqual(over, card.InteractionOffset);
                f.View.AdvanceMotion(tuning.RaiseSeconds * 0.35f + 0.001f);
                Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset); Assert.IsFalse(card.IsLifting);

                f.Set(false, false); f.View.AdvanceMotion(tuning.LowerSeconds * 0.65f);
                Assert.Less(card.InteractionOffset.z, -0.001f); Assert.Greater(card.InteractionOffset.z, -0.0018f);
                f.View.AdvanceMotion(tuning.LowerSeconds * 0.35f + 0.001f);
                Assert.AreEqual(Vector3.zero, card.InteractionOffset); Assert.IsFalse(card.IsLifting);

                f.Set(true, false); f.View.AdvanceMotion(tuning.RaiseSeconds * 0.65f);
                var midway = card.InteractionOffset; f.Set(false, false); Assert.AreEqual(midway, card.InteractionOffset);
                f.View.AdvanceMotion(tuning.LowerSeconds * 0.65f); midway = card.InteractionOffset;
                f.Set(true, false); Assert.AreEqual(midway, card.InteractionOffset);
                f.View.AdvanceMotion(tuning.RaiseSeconds + 0.001f); Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);

                f.Set(true, true); f.View.AdvanceMotion(tuning.RaiseSeconds + 0.001f);
                Assert.AreEqual(f.View.SelectedOffset, card.InteractionOffset);
                f.Set(true, false); f.View.AdvanceMotion(tuning.LowerSeconds * 0.65f);
                Assert.Less(card.InteractionOffset.z, f.View.HoveredOffset.z);
                f.View.AdvanceMotion(tuning.LowerSeconds * 0.35f + 0.001f);
                Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset); Assert.IsFalse(card.IsLifting);
            }
        }

        [Test] public void SelectionCentersSwayDuringSecondLiftAndCancellationKeepsActualPose()
        {
            using (var f = new Fixture())
            {
                f.View.SetMotionSettings(CardZoneMotionSettings.Default);
                f.Set(true, false); f.View.AdvanceMotion(1);
                var card = f.View.Views[0];
                var idle = DOTween.TweensByTarget(card.SwayRoot).Single();
                idle.Goto(idle.Duration(false) * 0.25f, true);
                float startAngle = Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y);
                Assert.Greater(Mathf.Abs(startAngle), 0.1f);
                f.Set(true, true);
                Assert.IsFalse(idle.IsActive());
                Assert.AreEqual(startAngle, Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y), 0.0001f);
                Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);
                float step = f.View.LiftSettings.RaiseSeconds * 0.25f;
                f.View.AdvanceMotion(step);
                float angle = Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y);
                float progress = (card.InteractionOffset.z - f.View.HoveredOffset.z) / (f.View.SelectedOffset.z - f.View.HoveredOffset.z);
                Assert.Greater(progress, 0); Assert.Less(progress, 1);
                Assert.AreEqual(startAngle * (1 - progress), angle, 0.0001f);
                var lift = DOTween.TweensByTarget(card.FaceRoot).Single();
                f.Host.Publish(); f.Set(false, true); f.View.RefreshLayout();
                Assert.AreSame(lift, DOTween.TweensByTarget(card.FaceRoot).Single());
                Assert.AreEqual(angle, Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y), 0.0001f);
                f.View.AdvanceMotion(f.View.LiftSettings.RaiseSeconds * 0.4f);
                progress = (card.InteractionOffset.z - f.View.HoveredOffset.z) / (f.View.SelectedOffset.z - f.View.HoveredOffset.z);
                Assert.Greater(progress, 1.18f); // Selection's stronger rebound carries height and angle past their targets together.
                Assert.AreEqual(startAngle * (1 - progress), Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y), 0.0001f);
                f.View.AdvanceMotion(f.View.LiftSettings.RaiseSeconds * 0.35f + 0.001f);
                Assert.AreEqual(f.View.SelectedOffset, card.InteractionOffset);
                Assert.AreEqual(Quaternion.identity, card.SwayRoot.localRotation); Assert.IsFalse(card.IsLifting);
                f.View.AdvanceMotion(1); Assert.AreEqual(Quaternion.identity, card.SwayRoot.localRotation);

                f.Set(true, false); f.View.AdvanceMotion(0);
                Assert.AreEqual(Quaternion.identity, card.SwayRoot.localRotation);
                f.View.AdvanceMotion(0.3f); f.Set(true, true); f.View.AdvanceMotion(0.05f);
                float partialAngle = Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y);
                var partialOffset = card.InteractionOffset;
                Assert.Greater(Mathf.Abs(partialAngle), 0.0001f);
                f.Set(true, false); f.View.AdvanceMotion(0);
                Assert.AreEqual(partialOffset, card.InteractionOffset);
                Assert.AreEqual(partialAngle, Mathf.DeltaAngle(0, card.SwayRoot.localEulerAngles.y), 0.0001f);
                f.Set(true, true); f.View.AdvanceMotion(1);
                Assert.AreEqual(Quaternion.identity, card.SwayRoot.localRotation);
                Assert.AreEqual(f.View.SelectedOffset, card.InteractionOffset);
            }
        }

        [Test] public void LiftRetargetsFromActualOffsetAndRemainsZoneLocalThroughFlipAndReflow()
        {
            using (var f = new Fixture())
            {
                var card = f.View.Views[0]; f.Set(true, false); f.View.AdvanceMotion(0.04f);
                var current = card.InteractionOffset; f.Set(false, false); Assert.AreEqual(current, card.InteractionOffset);
                f.View.AdvanceMotion(0.03f); current = card.InteractionOffset; f.Set(true, false); Assert.AreEqual(current, card.InteractionOffset);
                f.Root.transform.SetPositionAndRotation(Vector3.one, Quaternion.Euler(40, 35, 12));
                f.Root.transform.localScale = new Vector3(1.3f, 0.7f, 1.2f);
                var layout = f.View.Layout; layout.ArcHeight = -0.012f; f.View.SetLayout(layout);
                var data = new CardZoneViewData(f.Host.Zone.Id, new[] {
                    f.Host.Cards[1].Project(), new CardVisualData(card.ViewId, interaction: new CardInteractionState(isHovered: true)) });
                f.View.Present(data); Assert.AreSame(card, f.View.Views[1]);
                f.View.AdvanceMotion(0.02f);
                var actual = f.View.transform.InverseTransformVector(card.FaceRoot.position - f.View.transform.TransformPoint(card.DisplayPose.Position));
                Assert.Less(Vector3.Distance(actual, card.InteractionOffset), 0.00001f);
                Assert.AreEqual(0, actual.x, 0.00001f); Assert.AreEqual(0, actual.y, 0.00001f); Assert.Greater(actual.z, 0);
            }
        }

        [Test] public void RestoreDisableAndDestroyCleanLiftWithoutAffectingForeignTween()
        {
            float value = 0; var foreignId = new object();
            var foreign = DOTween.To(() => value, next => value = next, 1, 5).SetUpdate(UpdateType.Manual).SetId(foreignId);
            try
            {
                using (var f = new Fixture())
                {
                    var card = f.View.Views[0]; f.Set(true, false); f.View.AdvanceMotion(0.03f);
                    f.View.Present(f.Host.Zone, id => f.Host.Cards.Single(c => c.Id == id.Value).Project(), animate: false);
                    Assert.IsFalse(card.IsLifting); Assert.AreEqual(f.View.HoveredOffset, card.InteractionOffset);
                    f.Set(false, false); Assert.IsTrue(card.IsLifting); f.View.enabled = false;
                    Assert.IsFalse(card.IsLifting); Assert.AreEqual(Vector3.zero, card.InteractionOffset);
                    f.View.enabled = true; f.Set(true, false); var owned = DOTween.TweensByTarget(card.FaceRoot).Single();
                    f.View.Clear(); Assert.IsFalse(owned.IsActive()); Assert.IsTrue(foreign.IsActive()); Assert.AreEqual(0, value);
                }
            }
            finally { DOTween.Kill(foreignId); }
        }

        [Test] public void BakedCardsSerializeAndRestartIndependentlyOfEditorAndRuntimeSelection()
        {
            using (var f = new Fixture())
            {
                var first = f.Model.Cards[0]; first.IsHovered = true; first.IsSelected = true; first.IsDisabled = true;
                first.Effects = CardEffects.Laminated | CardEffects.Ineffective; first.FaceUp = false; first.Tint = Color.red;
                f.Capture(); var ids = f.Model.BuildZone().Cards.ToArray(); string json = JsonUtility.ToJson(f.Host);
                f.Model.Clear(); JsonUtility.FromJsonOverwrite(json, f.Host); f.Host.BeginSession();
                CollectionAssert.AreEqual(ids, f.Host.Zone.Cards); Assert.AreEqual(2, f.View.Views.Count);
                Assert.IsNotNull(f.Host.MouseInput.Controller); Assert.IsFalse(f.Root.CompareTag("EditorOnly"));
                var restored = f.Host.Cards[0]; Assert.IsFalse(restored.Interaction.IsHovered); Assert.IsTrue(restored.Interaction.IsSelected);
                Assert.IsTrue(restored.Card.IsDisabled); Assert.IsFalse(restored.FaceUp); Assert.AreEqual(Color.red, restored.Tint);
                Assert.AreEqual(first.Effects, restored.Card.Effects); Assert.IsNull(f.View.Views[0].Data.FaceArtId);
                f.Set(false, false); Assert.IsFalse(restored.Interaction.IsSelected); Assert.IsTrue(f.Host.InitialCards[0].Interaction.IsSelected);
                f.Host.BeginSession(); Assert.IsTrue(f.Host.Cards[0].Interaction.IsSelected);
                Assert.AreEqual(2, f.Root.GetComponentsInChildren<CardInstanceView>().Length);
                CollectionAssert.DoesNotContain(typeof(CardZonePlayHost).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray(), "CardPresentation.Editor");
            }
        }

        [Test] public void InvalidCaptureAndLiftSettingsLeaveRunningSessionIntact()
        {
            using (var f = new Fixture())
            {
                var original = f.View.Views[0]; var source = f.Host.Cards[0]; string json = JsonUtility.ToJson(f.Host);
                Assert.Throws<ArgumentException>(() => f.Host.Capture(f.Host.Zone, f.Host.Cards.Reverse()));
                Assert.Throws<ArgumentException>(() => f.Host.Capture(f.Host.Zone, new CardZoneCardState[] { null }));
                Assert.IsTrue(f.Host.IsRunning); Assert.AreSame(original, f.View.Views[0]); Assert.AreSame(source, f.Host.Cards[0]);
                Assert.AreEqual(json, JsonUtility.ToJson(f.Host));
                var bad = f.View.LiftSettings; bad.RaiseSeconds = float.NaN;
                Assert.Throws<ArgumentException>(() => f.View.SetLiftSettings(bad)); Assert.AreEqual(0.12f, f.View.LiftSettings.RaiseSeconds);
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -0.1f, 3.1f })
                {
                    bad = f.View.LiftSettings; bad.Overshoot = invalid;
                    Assert.Throws<ArgumentException>(() => f.View.SetLiftSettings(bad));
                }
                Assert.AreEqual(2.2f, f.View.LiftSettings.Overshoot);
            }
        }

        [Test] public void ReopeningEditorModelRecoversBakedIdentityOrderAndProperties()
        {
            using (var f = new Fixture())
            {
                f.Model.Cards[0].IsSelected = true; f.Model.Cards[1].Effects = CardEffects.Enhanced; f.Model.Reverse(); f.Capture();
                var reopened = new CardZonePreviewModel(); reopened.Load(f.Host.InitialCards);
                CollectionAssert.AreEqual(f.Model.BuildZone().Cards, reopened.BuildZone().Cards);
                Assert.AreEqual(CardEffects.Enhanced, reopened.Cards[0].Effects); Assert.IsTrue(reopened.Cards[1].IsSelected);
                Assert.IsFalse(reopened.Cards.Any(c => c.IsHovered));
            }
        }
    }
}
