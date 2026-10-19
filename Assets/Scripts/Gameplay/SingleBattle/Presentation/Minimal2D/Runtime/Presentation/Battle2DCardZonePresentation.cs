using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.Battle2DValidation
{
    /// <summary>Flat-scene capability implementation. Timeline order lives in CardZoneTimelineAction.</summary>
    public sealed class Battle2DCardZonePresentation : CardZonePresentationBehaviour
    {
        public const string PrepareDiscard = "cards.prepare-discard";
        [SerializeField] private Vector3 prepareOffset = Vector3.up * .7f;
        [SerializeField] private Vector3 discardOffset = Vector3.down * .7f;
        [SerializeField, Min(0)] private float seconds = .18f;
        [SerializeField, Range(0, 1)] private float signalProgress = .5f;
        private Battle2DView View => this == null ? null : GetComponentInParent<Battle2DView>();
        protected override IEnumerable<ActionDescriptor> BuiltInCapabilities => new[] {
            new ActionDescriptor(CardZoneActions.Prepare, new[] { "Prepared" }, new[] { "cards.preparation" }),
            new ActionDescriptor(PrepareDiscard, new[] { "Released" }, new[] { "cards.preparation" })
        };
        public override CardZoneDisplayData Current => View == null ? null : View.PresentedState?.Zones.FirstOrDefault(x => x.Kind == "hand" && x.OwnerId == View.PresentedState.ObserverId);
        public override bool IsPresenting => runs.Any(run => run.Operation.Status == PresentationStatus.Pending);
        private sealed class Run
        {
            internal Battle2DCard[] Cards;
            internal PresentationOperation Operation;
            internal Coroutine Routine;
        }
        private readonly HashSet<Run> runs = new HashSet<Run>();
        protected override IPresentationOperation PlayBuiltIn(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || signalProgress < 0 || signalProgress > 1 || float.IsNaN(signalProgress))
                return PresentationOperation.Failed("InvalidCardMotion", name);
            var ids = request.Before.Cards.Where(card => request.ViewIds.Contains(card.ViewId)).Select(card => card.CardId).ToArray();
            var run = new Run { Cards = View.Cards.Where(card => ids.Contains(card.Id)).ToArray() };
            foreach (var card in run.Cards)
                if (!card.TryOwnAction(run)) { Release(run); return PresentationOperation.Failed("TargetBusy", card.Id); }
            run.Operation = new PresentationOperation(() => Release(run)); runs.Add(run);
            request.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            var routine = Animate(run, request, markers);
            if (request.Mode == PlaybackMode.Instant || !Application.isPlaying) { while (routine.MoveNext()) { } }
            else { var handle = StartCoroutine(routine); if (run.Operation.Status == PresentationStatus.Pending) run.Routine = handle; }
            return run.Operation;
        }
        private IEnumerator Animate(Run run, CardZonePresentationRequest request, IMarkerSink markers)
        {
            float elapsed = 0; bool emitted = false;
            var offset = request.ActionKey == PrepareDiscard ? discardOffset : prepareOffset;
            string signal = request.ActionKey == PrepareDiscard ? "Released" : "Prepared";
            do
            {
                elapsed += request.Mode == PlaybackMode.Instant || !Application.isPlaying ? seconds : Time.unscaledDeltaTime;
                float progress = seconds == 0 ? 1 : Mathf.Clamp01(elapsed / seconds);
                foreach (var card in run.Cards) if (card != null) card.SetActionOffset(run, offset * progress);
                if (!emitted && progress >= signalProgress) { emitted = true; markers.Emit(signal); }
                if (progress >= 1) break;
                yield return null;
            } while (run.Operation.Status == PresentationStatus.Pending);
            if (run.Operation.Status != PresentationStatus.Pending) yield break;
            run.Routine = null; run.Operation.TryComplete();
        }
        private void Release(Run run)
        {
            var routine = run.Routine; run.Routine = null; if (routine != null) StopCoroutine(routine);
            foreach (var card in run.Cards) if (card != null) card.ReleaseAction(run);
            runs.Remove(run);
        }
        private void ReleaseAll()
        {
            foreach (var run in runs.ToArray()) if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run);
            CancelPresentationActions();
        }
        public override void Synchronize(CardZoneDisplayData snapshot) { ReleaseAll(); if (snapshot != null && View != null) View.PresentCardZone(snapshot); }
        private void OnDisable() => ReleaseAll();
    }
}
