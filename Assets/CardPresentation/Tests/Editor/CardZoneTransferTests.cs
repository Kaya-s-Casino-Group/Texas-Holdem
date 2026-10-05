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
    public sealed class CardZoneTransferTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene;
            public readonly HandZoneView Hand;
            public readonly PlayedZoneView Played;
            public readonly CardZonePlayHost From, To;
            public readonly CardZoneTransferPresenter Route;
            public Fixture(bool running = true, int occupied = 1, int handCount = 4)
            {
                scene = EditorSceneManager.NewPreviewScene();
                var a = new GameObject("Transfer Hand"); var b = new GameObject("Transfer Played");
                SceneManager.MoveGameObjectToScene(a, scene); SceneManager.MoveGameObjectToScene(b, scene);
                Hand = a.AddComponent<HandZoneView>(); Played = b.AddComponent<PlayedZoneView>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                Hand.Configure(prefab, CardZoneLayoutSettings.Default); Played.Configure(prefab, CardZoneLayoutSettings.Default);
                Hand.transform.SetPositionAndRotation(new Vector3(.2f, .1f, -.3f), Quaternion.Euler(15, 22, 5));
                Played.transform.SetPositionAndRotation(new Vector3(-.2f, .3f, .3f), Quaternion.Euler(-10, -18, 8));
                Hand.transform.localScale = Vector3.one * .9f; Played.transform.localScale = Vector3.one * 1.2f;
                From = a.AddComponent<CardZonePlayHost>(); To = b.AddComponent<CardZonePlayHost>();
                var hand = new[] { Card("a"), Card("b", false), Card("c"), Card("d"), Card("e") }.Take(handCount).ToArray();
                var played = Enumerable.Range(0, occupied).Select(i => Card("old" + i)).ToArray();
                From.Capture(Zone("hand", hand), hand); To.Capture(Zone("played", played), played);
                if (running) { From.BeginSession(); To.BeginSession(); }
                else { Hand.Present(Projection(From), false); Played.Present(Projection(To), false); }
                Route = a.AddComponent<CardZoneTransferPresenter>(); Route.Configure(Hand, Played, CardZoneTransferSettings.Default);
            }
            public void Select(params string[] ids)
            {
                if (From.IsRunning)
                {
                    foreach (var card in From.Cards) From.SetInteraction(card.Id, new CardInteractionState(true, ids.Contains(card.Id)));
                    From.Publish();
                }
                else
                {
                    var copy = From.InitialCards.Select(card => card.Copy()).ToArray();
                    foreach (var card in copy) card.SetInteraction(new CardInteractionState(true, ids.Contains(card.Id)));
                    From.Capture(From.Zone, copy); Hand.Present(Projection(From), false);
                }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Hand.gameObject); Object.DestroyImmediate(Played.gameObject);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static CardZoneCardState Card(string id, bool visible = true, bool disabled = false, CardEffects effects = CardEffects.None) =>
            new CardZoneCardState(new CardInstance(new CardInstanceId(id), new CardDefinitionId("hearts_q"), "origin", effects, disabled),
                "hearts_q", visible, new Color(.9f, .8f, .7f), default(CardInteractionState));
        private static CardZone Zone(string id, CardZoneCardState[] cards) => new CardZone(new ZoneId(id), id, null, cards.Select(card => card.Card.Id));
        private static CardZoneViewData Projection(CardZonePlayHost host) => new CardZoneViewData(host.Zone.Id,
            (host.IsRunning ? host.Cards : host.InitialCards).Select(card => card.Project()));
        private static void Close(Vector3 expected, Vector3 actual) => Assert.Less(Vector3.Distance(expected, actual), .00001f);

        [Test] public void OneThroughFiveCardsCrossInReverseThenReorderTogetherWithoutChangingDataOrder()
        {
            for (int count = 1; count <= 5; count++) using (var f = new Fixture(occupied: 0, handCount: 5))
            {
                var ids = f.From.Cards.Take(count).Select(card => card.Id).ToArray(); f.Select(ids);
                var originals = f.Hand.Views.Take(count).ToArray(); var settings = f.Route.Settings;
                var slots = f.Played.GetSlotPoses();
                Assert.AreEqual(count, f.From.PlaySelected(f.To, f.Route));
                CollectionAssert.AreEqual(ids, f.To.Cards.Select(card => card.Id));
                CollectionAssert.AreEqual(originals, f.Played.Views);
                Assert.AreEqual(count, f.Played.transform.childCount);
                if (count == 1)
                {
                    f.Played.AdvanceMotion(settings.FlightSeconds + .001f);
                    Assert.IsFalse(f.Route.IsTransferring); Close(f.Played.transform.TransformPoint(slots[0].Position), originals[0].SwayRoot.position);
                    continue;
                }
                // The leftmost card reaches the rightmost new slot while later cards are still flying.
                f.Played.AdvanceMotion(settings.FlightSeconds + settings.Interval * .5f);
                Close(f.Played.transform.TransformPoint(slots[count - 1].Position), originals[0].SwayRoot.position);
                Assert.IsTrue(originals[0].IsTransferring);
                float allArrived = settings.FlightSeconds + (count - 1) * settings.Interval;
                float heldTime = allArrived + settings.LandingPause * .5f;
                f.Played.AdvanceMotion(heldTime - settings.FlightSeconds - settings.Interval * .5f);
                for (int i = 0; i < count; i++)
                {
                    Close(f.Played.transform.TransformPoint(slots[count - 1 - i].Position), originals[i].SwayRoot.position);
                    Assert.IsTrue(originals[i].IsEntering);
                }
                // A data refresh in the reversed visual phase must not snap cards into their logical slots.
                f.To.Publish();
                Close(f.Played.transform.TransformPoint(slots[count - 1].Position), originals[0].SwayRoot.position);
                CollectionAssert.AreEqual(ids, f.To.Zone.Cards.Select(card => card.Value));
                f.Played.AdvanceMotion(settings.LandingPause * .5f + settings.ResolvedReorderSeconds * .5f);
                for (int i = 0; i < count; i++)
                {
                    var actual = f.Played.transform.InverseTransformPoint(originals[i].SwayRoot.position);
                    Assert.AreEqual((slots[i].Position.x + slots[count - 1 - i].Position.x) * .5f, actual.x, .00001f);
                }
                Assert.AreNotEqual(originals[0].SwayRoot.position, originals[count - 1].SwayRoot.position);
                f.Played.AdvanceMotion(settings.ResolvedReorderSeconds);
                Assert.IsFalse(f.Route.IsTransferring);
                for (int i = 0; i < count; i++) Close(f.Played.transform.TransformPoint(slots[i].Position), originals[i].SwayRoot.position);
                CollectionAssert.AreEqual(ids, f.To.Cards.Select(card => card.Id));
            }
        }

        [Test] public void CrossedBatchesUseOnlyTheirNewSlotsAndKeepTheirOwnArrivalBarrier()
        {
            using (var f = new Fixture(occupied: 1))
            {
                f.Select("a", "b"); f.From.PlaySelected(f.To, f.Route);
                var old = f.Played.Views[0]; var oldPosition = old.SwayRoot.position;
                var first = f.Played.Views[1]; var settings = f.Route.Settings;
                f.Played.AdvanceMotion(.2f); var firstTween = DOTween.TweensByTarget(first.TransferRoot).Single();
                f.Select("c", "d"); f.From.PlaySelected(f.To, f.Route);
                Assert.AreSame(firstTween, DOTween.TweensByTarget(first.TransferRoot).Single());
                var second = f.Played.Views[3];
                f.Played.AdvanceMotion(.29f); // Batch one has landed; batch two has not.
                Close(f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[2].Position), first.SwayRoot.position);
                Assert.Greater(Vector3.Distance(second.SwayRoot.position, f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[4].Position)), .001f);
                Close(oldPosition, old.SwayRoot.position);
                f.Played.AdvanceMotion(2); Assert.IsFalse(f.Route.IsTransferring);
                CollectionAssert.AreEqual(new[] { "old0", "a", "b", "c", "d" }, f.To.Cards.Select(card => card.Id));
                for (int i = 0; i < 5; i++) Close(f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[i].Position), f.Played.Views[i].SwayRoot.position);
            }
        }

        [Test] public void CrossedReorderTracksLayoutAndCanCompleteOrCancelDuringEitherPhase()
        {
            using (var f = new Fixture())
            {
                f.Select("a", "b"); f.From.PlaySelected(f.To, f.Route);
                var card = f.Played.Views[1];
                f.Played.AdvanceMotion(.49f); // Landed in reverse, awaiting reorder.
                var layout = f.Played.Layout; layout.PreferredGap = .005f; f.Played.SetLayout(layout);
                Close(f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[2].Position), card.SwayRoot.position);
                f.Played.transform.position += new Vector3(.1f, .05f, 0);
                f.Played.AdvanceMotion(.15f); Assert.IsTrue(card.IsTransferring);
                f.Route.CompleteTransfers(); Assert.IsFalse(f.Route.IsTransferring);
                Close(f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[1].Position), card.SwayRoot.position);
                f.Select("c", "d"); f.From.PlaySelected(f.To, f.Route);
                f.Played.AdvanceMotion(.6f); var tweenTarget = f.Played.Views[3].TransferRoot;
                f.Played.Clear(); Assert.IsFalse(f.Route.IsTransferring); Assert.IsNull(DOTween.TweensByTarget(tweenTarget));
                Assert.AreEqual(5, f.To.Cards.Count);
            }
        }

        [Test] public void LegacyRouteTimingGetsReorderDurationAndInvalidNewTimingRejectsBeforeMutation()
        {
            using (var f = new Fixture(occupied: 0))
            {
                var settings = f.Route.Settings; settings.ReorderSeconds = 0; settings.LandingPause = 0;
                f.Route.Configure(f.Hand, f.Played, settings); Assert.AreEqual(.3f, f.Route.Settings.ResolvedReorderSeconds);
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, -1 })
                {
                    var bad = settings; bad.ReorderSeconds = value;
                    Assert.Throws<ArgumentOutOfRangeException>(() => f.Route.Configure(f.Hand, f.Played, bad));
                    bad = settings; bad.LandingPause = value;
                    Assert.Throws<ArgumentOutOfRangeException>(() => f.Route.Configure(f.Hand, f.Played, bad));
                }
                f.Select("a", "b"); f.From.PlaySelected(f.To, f.Route);
                f.Played.AdvanceMotion(.6f); Assert.IsTrue(f.Route.IsTransferring);
                f.Played.AdvanceMotion(1); Assert.IsFalse(f.Route.IsTransferring);
            }
        }

        [Test] public void TransferReusesVisualsAndPreservesDisplayedStartIncludingLiftAndSway()
        {
            using (var f = new Fixture())
            {
                f.Hand.AdvanceMotion(.13f); f.Select("c", "a"); f.Hand.AdvanceMotion(.03f);
                var a = f.Hand.Views[0]; var c = f.Hand.Views[2]; var remaining = f.Hand.Views[3];
                var position = a.SwayRoot.position; var rotation = a.SwayRoot.rotation; var scale = a.SwayRoot.lossyScale;
                var cPosition = c.SwayRoot.position;
                int draws = 0, discards = 0; f.Hand.CardDealStarted += _ => draws++; f.Hand.CardEnteredZone += _ => draws++;
                f.Hand.CardDiscardStarted += _ => discards++;
                Assert.AreEqual(2, f.From.PlaySelected(f.To, f.Route));
                CollectionAssert.AreEqual(new[] { "b", "d" }, f.From.Cards.Select(card => card.Id));
                CollectionAssert.AreEqual(new[] { "old0", "a", "c" }, f.To.Cards.Select(card => card.Id));
                Assert.AreSame(a, f.Played.Views[1]); Assert.AreSame(c, f.Played.Views[2]); Assert.AreSame(remaining, f.Hand.Views[1]);
                Close(position, a.SwayRoot.position); Close(scale, a.SwayRoot.lossyScale);
                Assert.Less(Quaternion.Angle(rotation, a.SwayRoot.rotation), .02f); Assert.IsTrue(a.IsEntering);
                Assert.IsFalse(a.Data.Interaction.IsSelected); Assert.IsFalse(a.Data.Interaction.IsHovered);
                Assert.AreEqual(4, f.From.InitialCards.Count); Assert.AreEqual(1, f.To.InitialCards.Count);
                f.Played.AdvanceMotion(.025f); Assert.Greater(Vector3.Distance(position, a.SwayRoot.position), .0001f);
                Close(cPosition, c.SwayRoot.position); // Second card retains its actual source pose during the delay.
                f.Played.AdvanceMotion(2); f.Hand.AdvanceMotion(2);
                Assert.IsFalse(f.Route.IsTransferring); Assert.IsEmpty(f.Hand.DepartingViews); Assert.AreEqual(0, draws + discards);
                Close(f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[1].Position), a.SwayRoot.position);
                Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation); Assert.AreEqual(Vector3.zero, a.TransferRoot.localPosition);
            }
        }

        [Test] public void BatchClearsOnlyInteractionAndRejectsDisabledOrCapacityWithoutChangingInputs()
        {
            var a = Card("a", false, effects: CardEffects.Ineffective); var b = Card("b", effects: CardEffects.Ineffective);
            a.SetInteraction(new CardInteractionState(true, true)); b.SetInteraction(new CardInteractionState(false, true));
            var cards = new[] { a, b }; var empty = new CardZoneCardState[0];
            var batch = new CardZonePlayBatch(Zone("hand", cards), cards, Zone("played", empty), empty, 5);
            Assert.AreEqual(2, batch.Played.Count); Assert.IsEmpty(batch.Remaining);
            Assert.IsTrue(a.Interaction.IsSelected); Assert.IsTrue(b.Interaction.IsSelected);
            foreach (var card in batch.Played)
            {
                Assert.IsFalse(card.Interaction.IsSelected); Assert.AreEqual(CardEffects.Ineffective, card.Card.Effects);
                Assert.AreEqual("origin", card.Card.OriginId); Assert.AreEqual(a.Tint, card.Tint);
            }
            Assert.IsFalse(batch.Played[0].FaceUp);
            Assert.Throws<InvalidOperationException>(() => new CardZonePlayBatch(Zone("hand", cards), cards, Zone("played", empty), empty, 1));
            var disabled = Card("disabled", disabled: true); disabled.SetInteraction(new CardInteractionState(isSelected: true));
            var blocked = new[] { a, disabled };
            Assert.Throws<InvalidOperationException>(() => new CardZonePlayBatch(Zone("hand", blocked), blocked, Zone("played", empty), empty, 5));
        }

        [Test] public void HiddenCardStaysBackUpAndModelEffectsSurviveTransfer()
        {
            using (var f = new Fixture())
            {
                f.Select("b"); var card = f.Hand.Views[1]; var rotation = card.SwayRoot.rotation;
                f.From.PlaySelected(f.To, f.Route);
                Assert.AreSame(card, f.Played.Views[1]); Assert.IsFalse(card.Data.FaceUp); Assert.IsNull(card.Data.CardId);
                Assert.AreEqual("card_back", card.Surface.CardId); Assert.Less(Quaternion.Angle(rotation, card.SwayRoot.rotation), .02f);
                Assert.IsFalse(f.To.Cards.Last().FaceUp);
                f.Played.AdvanceMotion(1);
                Assert.AreEqual(180, card.FaceRoot.localEulerAngles.z, .001f); Assert.AreEqual(0, card.DealFlipAngle);
            }
        }

        [Test] public void InvalidDestinationPresentationAndFullTargetLeaveBothModelsAndViewsUntouched()
        {
            using (var f = new Fixture())
            {
                f.Select("a"); var before = f.Hand.Data; var targetBefore = f.Played.Data; var card = f.Hand.Views[0];
                var serialized = new SerializedObject(f.Played); var prefab = serialized.FindProperty("cardPrefab");
                var original = prefab.objectReferenceValue; prefab.objectReferenceValue = null; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => f.From.PlaySelected(f.To, f.Route));
                prefab.objectReferenceValue = original; serialized.ApplyModifiedPropertiesWithoutUndo();
                var invalid = new CardZoneViewData(f.To.Zone.Id, new[] { new CardVisualData("old0", "missing-art"), f.From.Cards[0].Project() });
                bool committed = false;
                Assert.Throws<ArgumentException>(() => f.Route.PresentTransfer(new CardZoneViewData(f.From.Zone.Id, f.From.Cards.Skip(1).Select(c => c.Project())),
                    invalid, new[] { "a" }, commitData: () => committed = true));
                Assert.IsFalse(committed); Assert.AreSame(before, f.Hand.Data); Assert.AreSame(targetBefore, f.Played.Data);
                Assert.AreSame(card, f.Hand.Views[0]); Assert.AreEqual(4, f.From.Cards.Count); Assert.AreEqual(1, f.To.Cards.Count);
                f.Played.transform.localScale = new Vector3(1, 2, 1);
                Assert.Throws<InvalidOperationException>(() => f.From.PlaySelected(f.To, f.Route)); Assert.AreSame(before, f.Hand.Data);
            }
            using (var f = new Fixture(occupied: 5))
            {
                f.Select("a"); Assert.Throws<InvalidOperationException>(() => f.From.PlaySelected(f.To, f.Route));
                Assert.AreEqual(4, f.From.Cards.Count); Assert.AreEqual(5, f.To.Cards.Count); Assert.IsTrue(f.From.Cards[0].Interaction.IsSelected);
            }
        }

        [Test] public void RepeatedSnapshotsAndSecondBatchDoNotRestartInFlightCards()
        {
            using (var f = new Fixture())
            {
                f.Select("a"); f.From.PlaySelected(f.To, f.Route); var a = f.Played.Views[1];
                f.Played.AdvanceMotion(.15f); var position = a.SwayRoot.position;
                var tween = DOTween.TweensByTarget(a.TransferRoot).Single();
                f.To.Publish(); f.From.Publish(); Close(position, a.SwayRoot.position);
                Assert.AreSame(tween, DOTween.TweensByTarget(a.TransferRoot).Single());
                Assert.AreEqual(0, f.From.PlaySelected(f.To, f.Route));
                f.Select("c"); f.From.PlaySelected(f.To, f.Route);
                Assert.AreSame(a, f.Played.Views[1]); Assert.AreSame(tween, DOTween.TweensByTarget(a.TransferRoot).Single());
                Close(position, a.SwayRoot.position);
                f.Played.transform.position += new Vector3(.1f, 0, 0);
                var layout = f.Played.Layout; layout.PreferredGap = .005f; f.Played.SetLayout(layout);
                f.Played.AdvanceMotion(2);
                Close(f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[1].Position), a.SwayRoot.position);
                Assert.IsFalse(f.Route.IsTransferring); Assert.AreEqual(3, f.To.Cards.Count);
            }
        }

        [Test] public void CustomControlPointControlsMidFlightAndSilentTransferHasNoTween()
        {
            using (var f = new Fixture())
            {
                var anchor = new GameObject("Control"); SceneManager.MoveGameObjectToScene(anchor, f.Hand.gameObject.scene);
                anchor.transform.position = new Vector3(.4f, .8f, 0);
                f.Route.Configure(f.Hand, f.Played, CardZoneTransferSettings.Default, anchor.transform);
                f.Select("a"); var a = f.Hand.Views[0]; var start = a.SwayRoot.position;
                var end = f.Played.transform.TransformPoint(f.Played.GetSlotPoses()[1].Position);
                f.From.PlaySelected(f.To, f.Route); f.Played.AdvanceMotion(.2f);
                Close(.25f * start + .5f * anchor.transform.position + .25f * end, a.SwayRoot.position);
                f.Select("b"); f.From.PlaySelected(f.To, f.Route, false);
                Assert.IsFalse(f.Played.Views[2].IsTransferring); Assert.IsTrue(a.IsTransferring);
            }
        }

        [Test] public void DisableClearAndDestroyCancelFlightsWithoutChangingCommittedOwnership()
        {
            using (var f = new Fixture())
            {
                f.Select("a"); f.From.PlaySelected(f.To, f.Route); var a = f.Played.Views[1];
                f.Route.enabled = false;
                Assert.IsFalse(a.IsTransferring); Assert.AreEqual(3, f.From.Cards.Count); Assert.AreEqual(2, f.To.Cards.Count);
                Assert.IsNull(DOTween.TweensByTarget(a.TransferRoot));
                f.Route.enabled = true; f.Select("b"); f.From.PlaySelected(f.To, f.Route);
                var b = f.Played.Views[2]; var target = b.TransferRoot;
                f.Played.Clear(); Assert.IsTrue(b == null); Assert.IsNull(DOTween.TweensByTarget(target));
                Assert.AreEqual(3, f.To.Cards.Count); Assert.IsFalse(f.Route.IsTransferring);
                f.To.Publish(); Assert.AreEqual(3, f.Played.Views.Count);
                f.Select("c"); f.From.PlaySelected(f.To, f.Route);
                Object.DestroyImmediate(f.Route); Assert.IsFalse(f.Played.Views.Last().IsTransferring);
            }
        }

        [Test] public void PreviewEditsBothStartupListsAndUndoRestoresBothDataSources()
        {
            using (var f = new Fixture(running: false))
            {
                f.Select("a", "b");
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                Undo.RecordObjects(new Object[] { f.From, f.To }, "Test play transfer");
                Assert.AreEqual(2, f.From.PlaySelectedPreview(f.To, f.Route));
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
                Assert.AreEqual(2, f.From.InitialCards.Count); Assert.AreEqual(3, f.To.InitialCards.Count);
                Assert.IsFalse(f.From.IsRunning); Assert.IsNull(f.From.GetComponent<CardZoneMouseInput>());
                int errors = 0;
                Application.LogCallback log = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception) errors++; };
                Application.logMessageReceived += log;
                try { Undo.PerformUndo(); } finally { Application.logMessageReceived -= log; }
                Assert.AreEqual(0, errors, "Undo must not write transforms during Unity's restoration pass.");
                f.Route.CompleteTransfers();
                Assert.AreEqual(4, f.From.InitialCards.Count); Assert.AreEqual(1, f.To.InitialCards.Count);
                f.Hand.Present(Projection(f.From), false); f.Played.Present(Projection(f.To), false);
                Assert.AreEqual(4, f.Hand.Views.Count); Assert.AreEqual(1, f.Played.Views.Count);
                f.From.BeginSession(); f.To.BeginSession();
                Assert.AreEqual(2, f.From.PlaySelected(f.To, f.Route, false));
                Assert.AreEqual(4, f.From.InitialCards.Count); Assert.AreEqual(1, f.To.InitialCards.Count);
                Undo.ClearUndo(f.From); Undo.ClearUndo(f.To);
            }
        }
    }
}
