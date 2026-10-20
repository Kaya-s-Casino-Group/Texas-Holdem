using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using global::BattlePresentation;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleActorDisplayTests
    {
        private sealed class MarkerRecorder : IMarkerSink
        {
            internal readonly List<string> Values = new List<string>();
            public bool Emit(string marker) { Values.Add(marker); return true; }
        }
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public readonly PlayerBattleActor Actor;
            public readonly BattleActorPresenter Presenter;
            public readonly Transform Model;
            public Fixture()
            {
                var go = new GameObject("Neutral actor"); SceneManager.MoveGameObjectToScene(go, scene);
                Actor = go.AddComponent<PlayerBattleActor>();
                Model = new GameObject("Model").transform; Model.SetParent(go.transform, false);
                Presenter = Model.gameObject.AddComponent<BattleActorPresenter>(); Presenter.Configure(Model);
                Actor.ConfigurePresentation(Presenter); Actor.SynchronizeDisplay(Unit(100), true);
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
        }
        private static UnitDisplayData Unit(long health, long block = 0) => new UnitDisplayData("unit", "Unit", "player", health, 100, block, 3, 3);

        [Test] public void NeutralCueAndActorPlayNeedNoRuleSessionOrCombatEvent()
        {
            using (var f = new Fixture())
            {
                Assert.IsNull(f.Actor.Battle); Assert.IsFalse(f.Actor.IsBound);
                var marker = new MarkerRecorder();
                var operation = f.Actor.PresentDisplay(BattlePresentationKind.Attack, null, marker);
                Assert.AreEqual(BattlePresentationStatus.Completed, operation.Status);
                CollectionAssert.AreEqual(new[] { "Impact" }, marker.Values);
                var effect = new EffectDisplayData("Damage", "opponent", "unit", 20, 20, 0, Unit(100), Unit(80));
                var cue = new BattlePresentationCue(BattlePresentationKind.Hit, "unit", effect, marker);
                Assert.IsNull(cue.Effect); Assert.IsFalse(cue.ActorId.IsValid);
                Assert.AreSame(effect, cue.DisplayEffect); Assert.AreEqual("unit", cue.DisplayActorId);
                Assert.AreSame(marker, cue.WithKind(BattlePresentationKind.BlockHit).Markers);
                f.Actor.PresentDisplay(BattlePresentationKind.Hit, effect, null);
                Assert.AreEqual(80, f.Actor.DisplayUnit.Health);
            }
        }

        [Test] public void NeutralShieldFactsAreIdempotentAndRecoveryReconcilesLatestValues()
        {
            using (var f = new Fixture())
            {
                var gain = new EffectDisplayData("GainBlock", "unit", "unit", 24, 0, 0, Unit(100), Unit(100, 24));
                var cards = new[] { new ShieldCard("one", "spades_a"), new ShieldCard("two", "hearts_a") };
                var shields = f.Actor.RecordDisplayBlock("batch/3", gain, cards);
                Assert.AreEqual(24, shields.TotalBlock); Assert.AreEqual(2, shields.Batches[0].Cards.Count);
                Assert.AreEqual(24, f.Actor.RecordDisplayBlock("batch/3", gain, cards).TotalBlock);
                f.Actor.PresentDisplay(BattlePresentationKind.GainBlock, gain, null, shields);
                var hit = new EffectDisplayData("Damage", "enemy", "unit", 30, 6, 24, Unit(100, 24), Unit(94));
                Assert.AreEqual(0, f.Actor.RecordDisplayBlock("batch/4", hit, null).TotalBlock);
                f.Actor.SynchronizeDisplay(Unit(94), true);
                Assert.AreEqual(0, f.Actor.Shields.TotalBlock); Assert.IsFalse(f.Actor.IsDisplayBusy);
                Assert.AreEqual(94, f.Actor.DisplayUnit.Health);
            }
        }

        [Test] public void NeutralActorSynchronizesInt64HealthAndShieldWithoutNarrowing()
        {
            using (var f = new Fixture())
            {
                long health = (long)int.MaxValue + 100;
                var before = new UnitDisplayData("unit", "Unit", "player", health, health, 0, 3, 3);
                var after = new UnitDisplayData("unit", "Unit", "player", health, health, long.MaxValue, 3, 3);
                f.Actor.SynchronizeDisplay(before, true);
                var gain = new EffectDisplayData("GainBlock", "unit", "unit", long.MaxValue, 0, 0, before, after);
                var shields = f.Actor.RecordDisplayBlock("large/gain", gain, new[] { new ShieldCard("one", "spades_a") });
                Assert.AreEqual(long.MaxValue, shields.TotalBlock);
                f.Actor.SynchronizeDisplay(after, true);
                Assert.AreEqual(health, f.Actor.DisplayUnit.Health);
                Assert.AreEqual(long.MaxValue, f.Actor.Shields.TotalBlock);
                var replacement = new UnitDisplayData("other", "Other", "player", health, health, health, 3, 3);
                f.Actor.SynchronizeDisplay(replacement, true);
                Assert.AreEqual(health, f.Actor.Shields.TotalBlock);
            }
        }

        [Test] public void ActualAttackTweenEmitsImpactAtForwardEndpointBeforeCompletionAndNeverAfterCancel()
        {
            using (var f = new Fixture())
            {
                var markers = new MarkerRecorder(); var operation = new BattlePresentationOperation();
                var cue = new BattlePresentationCue(BattlePresentationKind.Attack, "unit", markers: markers);
                var method = typeof(BattleActorPresenter).GetMethod("Pulse", BindingFlags.Instance | BindingFlags.NonPublic);
                var pulse = (IEnumerator)method.Invoke(f.Presenter, new object[] { cue, operation });
                Assert.IsTrue(pulse.MoveNext());
                var motionField = typeof(BattleActorPresenter).GetField("motion", BindingFlags.Instance | BindingFlags.NonPublic);
                var motion = (Tween)motionField.GetValue(f.Presenter);
                motion.GotoWithCallbacks(.081f, false);
                CollectionAssert.AreEqual(new[] { "Impact" }, markers.Values);
                Assert.IsTrue(operation.keepWaiting, "Impact must not complete the actor operation.");
                Assert.Greater(f.Model.localPosition.x, .05f, "The body is still in its attack pose when dependents start.");
                motion.Complete(); Assert.IsFalse(pulse.MoveNext());
                (pulse as IDisposable)?.Dispose();

                markers = new MarkerRecorder(); operation = new BattlePresentationOperation();
                cue = new BattlePresentationCue(BattlePresentationKind.Attack, "unit", markers: markers);
                pulse = (IEnumerator)method.Invoke(f.Presenter, new object[] { cue, operation });
                Assert.IsTrue(pulse.MoveNext()); motion = (Tween)motionField.GetValue(f.Presenter);
                operation.Cancel(); motion.GotoWithCallbacks(.081f, false);
                Assert.IsEmpty(markers.Values);
                f.Presenter.CancelPresentation(); (pulse as IDisposable)?.Dispose();
            }
        }
    }
}
