using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Publishes no state. After the card fact checkpoint, waits for the real projected zone motion.</summary>
    [AddComponentMenu("Battle Presentation/Hand Settle Timeline Action")]
    public sealed class HandSettleTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private HandZoneView source;
        private sealed class Run
        {
            internal HandZoneView Hand;
            internal ActionContext Context;
            internal PresentationOperation Operation;
            internal Action Stop;
        }
        private readonly List<Run> pending = new List<Run>();
        public override ActionDescriptor Descriptor => new ActionDescriptor("cards.settle");
        public void Configure(HandZoneView hand) { CancelAll(); source = hand; }
        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            var hand = source != null ? source : HandPreparationTimelineAction.ResolveObject(context.Target) ??
                HandPreparationTimelineAction.ResolveObject(context.Targets?.Resolve("cards", context));
            if (hand == null) return PresentationOperation.Failed("MissingHand", context.TargetKey ?? "cards");
            var run = new Run { Hand = hand, Context = context };
            run.Operation = new PresentationOperation(() => Release(run)); pending.Add(run);
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            if (Application.isPlaying || StartPresentationRoutine != null) run.Stop = StartRoutine(WaitForProjection(run));
            return run.Operation;
        }
        private IEnumerator WaitForProjection(Run run)
        {
            // The action starts before the empty checkpoint publishes; wait until the projection has happened.
            yield return null;
            while (run.Operation.Status == PresentationStatus.Pending) { Tick(run); if (run.Operation.Status == PresentationStatus.Pending) yield return null; }
            run.Stop = null;
        }
        public void Tick() { foreach (var run in pending.ToArray()) Tick(run); }
        private void Tick(Run run)
        {
            if (run.Operation.Status != PresentationStatus.Pending) return;
            if (!run.Context.Scope.IsActive) { run.Operation.Cancel(); return; }
            if (run.Hand == null || !run.Hand.isActiveAndEnabled) { run.Operation.TryFail("HandDisabled", "The hand was disabled or removed."); return; }
            if (!run.Hand.IsTransitioning) run.Operation.TryComplete();
        }
        private void Release(Run run) { var stop = run.Stop; run.Stop = null; stop?.Invoke(); pending.Remove(run); }
        private void CancelAll() { foreach (var run in pending.ToArray()) { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); } }
        public override void Synchronize(ActionSyncContext context) => CancelAll();
        private void Update() { if (StartPresentationRoutine == null && !Application.isPlaying) Tick(); }
        private void OnDisable() => CancelAll();
    }
}
