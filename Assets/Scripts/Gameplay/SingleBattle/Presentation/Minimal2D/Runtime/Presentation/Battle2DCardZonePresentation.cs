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
    public sealed class Battle2DCardZonePresentation : CardZonePresentationBehaviour, ICardZonePresentationPreview
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
        public override CardZoneDisplayData Current => View == null ? null : View.PresentedCardZone;
        public override bool IsPresenting => runs.Any(run => run.Operation.Status == PresentationStatus.Pending);
        private sealed class Run
        {
            internal Battle2DCard[] Cards;
            internal PresentationOperation Operation;
            internal float Elapsed;
            internal bool Emitted;
            internal CardZonePresentationRequest Request;
            internal IMarkerSink Markers;
        }
        private readonly HashSet<Run> runs = new HashSet<Run>();
        protected override IPresentationOperation PlayBuiltIn(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || signalProgress < 0 || signalProgress > 1 || float.IsNaN(signalProgress))
                return PresentationOperation.Failed("InvalidCardMotion", name);
            var ids = request.Before.Cards.Where(card => request.ViewIds.Contains(card.ViewId)).Select(card => card.CardId).ToArray();
            var run = new Run { Cards = View.Cards.Where(card => ids.Contains(card.Id)).ToArray(), Request = request, Markers = markers };
            foreach (var card in run.Cards)
                if (!card.TryOwnAction(run)) { Release(run); return PresentationOperation.Failed("TargetBusy", card.Id); }
            run.Operation = new PresentationOperation(() => Release(run)); runs.Add(run);
            request.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            if (request.Mode == PlaybackMode.Instant || !Application.isPlaying && !previewClock) Advance(run, seconds);
            return run.Operation;
        }
        private bool previewClock;
        public IDisposable PreservePreviewState()
        {
            var snapshot = Current;
            var restoration = new PresentationScope("card-preview-restore");
            restoration.Own(() => Synchronize(snapshot));
            return restoration;
        }
        public void AdvancePreview(float deltaTime)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Preview clock is EditMode only.");
            AdvanceCustomPreview(deltaTime);
            previewClock = true; AdvanceAll(deltaTime);
        }
        private void AdvanceAll(float deltaTime)
        {
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            foreach (var run in runs.ToArray()) Advance(run, deltaTime);
        }
        private void Advance(Run run, float deltaTime)
        {
            if (run.Operation.Status != PresentationStatus.Pending) return;
            run.Elapsed += deltaTime;
            float progress = seconds == 0 ? 1 : Mathf.Clamp01(run.Elapsed / seconds);
            var offset = run.Request.ActionKey == PrepareDiscard ? discardOffset : prepareOffset;
            foreach (var card in run.Cards) if (card != null) card.SetActionOffset(run, offset * progress);
            if (!run.Emitted && progress >= signalProgress)
            { run.Emitted = true; run.Markers.Emit(run.Request.ActionKey == PrepareDiscard ? "Released" : "Prepared"); }
            if (progress >= 1) run.Operation.TryComplete();
        }
        private void Update() => AdvanceAll(Time.unscaledDeltaTime);
        private void Release(Run run)
        {
            foreach (var card in run.Cards) if (card != null) card.ReleaseAction(run);
            runs.Remove(run);
        }
        private void ReleaseAll()
        {
            foreach (var run in runs.ToArray()) if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run);
            CancelPresentationActions();
        }
        public override void Synchronize(CardZoneDisplayData snapshot) { ReleaseAll(); previewClock = false; if (View != null) View.PresentCardZone(snapshot); }
        private void OnDisable() => ReleaseAll();
    }
}
