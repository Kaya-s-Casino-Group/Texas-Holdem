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
    [AddComponentMenu("Battle Presentation/Hand Preparation Timeline Action")]
    public sealed class HandPreparationTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private HandZoneView source;
        private sealed class Run
        {
            internal HandZoneView Hand;
            internal ActionContext Context;
            internal IMarkerSink Markers;
            internal CardActionViewOperation Source;
            internal PresentationOperation Operation;
            internal Action<CardActionViewStatus> Finished;
            internal Action Stop;
        }
        private readonly List<Run> pending = new List<Run>();
        public override ActionDescriptor Descriptor => new ActionDescriptor("cards.prepare", new[] { "ReadyToAttack" }, new[] { "hand.preparation" });
        public void Configure(HandZoneView hand) { CancelAll(); source = hand; }
        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            var hand = source != null ? source : ResolveObject(context.Target) ?? ResolveObject(context.Targets?.Resolve("cards", context));
            if (hand == null) return PresentationOperation.Failed("MissingHand", context.TargetKey ?? "cards");
            var run = new Run { Hand = hand, Context = context, Markers = markers };
            run.Operation = new PresentationOperation(() => Release(run)); pending.Add(run);
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            Tick(run);
            if (run.Operation.Status == PresentationStatus.Pending && (Application.isPlaying || StartPresentationRoutine != null))
                run.Stop = StartRoutine(ObserveRoutine(run));
            return run.Operation;
        }
        private IEnumerator ObserveRoutine(Run run)
        {
            while (run.Operation.Status == PresentationStatus.Pending) { yield return null; Tick(run); }
            run.Stop = null;
        }
        public void Tick() { foreach (var run in pending.ToArray()) Tick(run); }
        private void Tick(Run run)
        {
            if (run.Operation.Status != PresentationStatus.Pending) return;
            if (!run.Context.Scope.IsActive) { run.Operation.Cancel(); return; }
            if (run.Hand == null || !run.Hand.isActiveAndEnabled) { Fail(run, "HandDisabled", "The hand was disabled or removed."); return; }
            try
            {
                if (run.Source == null)
                {
                    if (run.Hand.IsTransitioning) return;
                    var visible = run.Context.Before.Zones.SelectMany(zone => zone.Cards).ToArray();
                    var ids = run.Context.Data.CardIds.Select(id => visible.FirstOrDefault(card => card.CardId == id || card.ViewId == id)?.ViewId ?? id).ToArray();
                    run.Source = run.Hand.PresentSelectionAction(ids, marker => {
                        if (run.Context.Scope.IsActive && run.Operation.Status == PresentationStatus.Pending) run.Markers.Emit(marker);
                    });
                    run.Finished = _ => Observe(run); run.Source.Finished += run.Finished;
                }
                Observe(run);
            }
            catch (Exception error) { Fail(run, "CardPreparationFailed", error.Message, error); }
        }
        private void Observe(Run run)
        {
            if (run.Operation.Status != PresentationStatus.Pending || run.Source == null || run.Source.keepWaiting) return;
            if (run.Source.Status == CardActionViewStatus.Completed)
            {
                if (run.Finished != null) run.Source.Finished -= run.Finished;
                run.Operation.TryComplete();
            }
            else if (run.Source.Status == CardActionViewStatus.Cancelled) run.Operation.Cancel();
            else Fail(run, "CardPreparationFailed", run.Source.Error?.Message ?? "Card preparation failed.", run.Source.Error);
        }
        internal static HandZoneView ResolveObject(object value)
        {
            if (value is HandZoneView hand) return hand;
            if (value is GameObject gameObject) return gameObject.GetComponentInParent<HandZoneView>();
            return value is Component component ? component.GetComponentInParent<HandZoneView>() : null;
        }
        private void Fail(Run run, string code, string message, Exception error = null)
        { Release(run); run.Operation.TryFail(code, message, error); }
        private void Release(Run run)
        {
            var stop = run.Stop; run.Stop = null; stop?.Invoke();
            if (run.Source != null && run.Finished != null) run.Source.Finished -= run.Finished;
            if (run.Hand != null && run.Source != null) run.Hand.CancelSelectionAction(run.Source);
            pending.Remove(run);
        }
        private void CancelAll() { foreach (var run in pending.ToArray()) { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); } }
        public override void Synchronize(ActionSyncContext context) => CancelAll();
        private void Update() { if (StartPresentationRoutine == null && !Application.isPlaying) Tick(); }
        private void OnDisable() => CancelAll();
    }
}
