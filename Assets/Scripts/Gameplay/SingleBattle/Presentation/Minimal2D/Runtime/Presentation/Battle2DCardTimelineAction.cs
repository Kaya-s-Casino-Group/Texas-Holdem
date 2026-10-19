using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.Battle2DValidation
{
    /// <summary>Transient card offset, independent of membership, selection and current layout slots.</summary>
    public sealed class Battle2DCardTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private Vector3 offset = Vector3.up * .7f;
        [SerializeField, Min(0)] private float seconds = .18f;
        [SerializeField, Range(0, 1)] private float signalProgress = .5f;
        [SerializeField] private string signal = "ReadyToAttack";
        private sealed class Run
        {
            internal Battle2DCard[] Cards;
            internal PresentationOperation Operation;
            internal Action CancelRoutine;
        }
        private readonly HashSet<Run> runs = new HashSet<Run>();
        public override ActionDescriptor Descriptor => new ActionDescriptor(name, new[] { signal }, new[] { "card-offset" });
        public void Configure(Vector3 movement, float duration, string marker, float progress = .5f)
        { offset = movement; seconds = duration; signal = marker; signalProgress = progress; }
        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            if (!(context.Target is Battle2DView view)) return PresentationOperation.Failed("MissingCardView", name);
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || signalProgress < 0 || signalProgress > 1 ||
                float.IsNaN(signalProgress) || string.IsNullOrEmpty(signal))
                return PresentationOperation.Failed("InvalidCardMotion", name);
            var run = new Run { Cards = view.Cards.Where(card => context.Data.CardIds.Contains(card.Id)).ToArray() };
            foreach (var card in run.Cards)
                if (!card.TryOwnAction(run)) { Release(run); return PresentationOperation.Failed("TargetBusy", card.Id); }
            run.Operation = new PresentationOperation(() => Release(run)); runs.Add(run);
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            var cancel = StartRoutine(Animate(run, markers));
            if (run.Operation.Status == PresentationStatus.Pending) run.CancelRoutine = cancel; else cancel();
            return run.Operation;
        }
        private IEnumerator Animate(Run run, IMarkerSink markers)
        {
            float elapsed = 0; bool emitted = false;
            do
            {
                elapsed += DeltaTime;
                float progress = seconds == 0 ? 1 : Mathf.Clamp01(elapsed / seconds);
                foreach (var card in run.Cards) if (card != null) card.SetActionOffset(run, offset * progress);
                if (!emitted && progress >= signalProgress) { emitted = true; markers.Emit(signal); }
                if (progress >= 1) break;
                yield return null;
            } while (run.Operation.Status == PresentationStatus.Pending);
            if (run.Operation.Status != PresentationStatus.Pending) yield break;
            run.CancelRoutine = null; run.Operation.TryComplete();
        }
        private void Release(Run run)
        {
            var cancel = run.CancelRoutine; run.CancelRoutine = null; cancel?.Invoke();
            foreach (var card in run.Cards) if (card != null) card.ReleaseAction(run);
            runs.Remove(run);
        }
        private void ReleaseAll()
        {
            foreach (var run in runs.ToArray())
                if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run);
        }
        public override void Synchronize(ActionSyncContext context) => ReleaseAll();
        private void OnDisable() => ReleaseAll();
    }
}
