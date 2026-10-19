using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using BattlePresentation.Unity;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>World-space implementation of the render-independent card-zone capability contract.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(HandZoneView))]
    public sealed class WorldCardZonePresentation : CardZonePresentationBehaviour, ICardZonePresentationPump
    {
        private HandZoneView hand;
        private HandZoneView Hand => this == null ? null : hand != null ? hand : (hand = GetComponent<HandZoneView>());
        public override bool IsPresenting => Hand != null && Hand.IsTransitioning;
        private Run active;
        private readonly List<Run> retained = new List<Run>();
        private sealed class Run
        {
            internal CardZonePresentationRequest Request;
            internal IMarkerSink Markers;
            internal PresentationOperation Operation;
            internal CardActionViewOperation Preparation;
            internal bool Started, Released, Superseded;
        }
        public override CardZoneDisplayData Current => Hand == null || Hand.Data == null ? null : new CardZoneDisplayData(
            Hand.Data.ZoneId.Value, null, "hand", Hand.Data.Cards.Select(card => new CardDisplayData(card.ViewId,
                card.CardId?.Value, null, card.FaceArtId, !card.FaceUp, card.IsDisabled ? "disabled" : null,
                new Dictionary<string, string> { ["effects"] = ((int)card.Effects).ToString(),
                    ["selected"] = card.Interaction.IsSelected.ToString(), ["hovered"] = card.Interaction.IsHovered.ToString() })));
        private CardZoneViewData Project(CardZoneDisplayData snapshot)
        {
            return new CardZoneViewData(new ZoneId(snapshot.Id), snapshot.Cards.Select(card => {
                var previous = Hand.Data?.Cards.FirstOrDefault(x => x.ViewId == card.ViewId);
                bool Flag(string key, bool fallback) => card.Properties.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;
                var effects = !card.Hidden && card.Properties.TryGetValue("effects", out var bits) && int.TryParse(bits, out var flags) ? (CardEffects)flags : CardEffects.None;
                return new CardVisualData(card.ViewId, card.Hidden ? null : card.ArtKey,
                    card.Hidden || string.IsNullOrEmpty(card.CardId) ? (CardInstanceId?)null : new CardInstanceId(card.CardId),
                    previous?.Tint, effects, !card.Hidden && card.DisabledReason != null,
                    new CardInteractionState(Flag("hovered", previous?.Interaction.IsHovered ?? false), Flag("selected", previous?.Interaction.IsSelected ?? false)),
                    previous?.ArtBindings ?? CardStateArtBindings.Standard);
            }));
        }
        protected override IPresentationOperation PlayBuiltIn(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (!request.ObserveProjection && active != null && active.Operation.Status == PresentationStatus.Pending)
            {
                if (active.Request.ActionKey == CardZoneActions.Prepare && active.Released)
                    active = null; // A released preparation may hand its instances to a later card fact.
                else if (request.ActionKey == CardZoneActions.Selection && active.Request.ActionKey == CardZoneActions.Selection)
                { active.Superseded = true; active.Operation.Cancel(); }
                else return PresentationOperation.Failed("CardZoneBusy", request.ActionKey);
            }
            var run = new Run { Request = request, Markers = markers };
            run.Operation = new PresentationOperation(() => Release(run)); if (!request.ObserveProjection) active = run; retained.Add(run);
            request.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            // Observe mode waits for the display checkpoint to publish its projection.
            if (!request.ObserveProjection || request.Mode == PlaybackMode.Instant) Tick(run);
            return run.Operation;
        }
        public void Tick() { foreach (var run in retained.ToArray()) Tick(run); }
        private void Tick(Run run)
        {
            if (run.Operation.Status != PresentationStatus.Pending) return;
            if (!run.Request.Scope.IsActive) { run.Operation.Cancel(); return; }
            if (Hand == null || !Hand.isActiveAndEnabled) { Release(run); run.Operation.TryFail("CardZoneDisabled", name); return; }
            try
            {
                var request = run.Request;
                if (!run.Started)
                {
                    if (request.ActionKey == CardZoneActions.Prepare && Hand.IsTransitioning && request.Mode != PlaybackMode.Instant) return;
                    run.Started = true;
                    if (request.ActionKey == CardZoneActions.Prepare)
                    {
                        run.Preparation = Hand.PresentSelectionAction(request.ViewIds,
                            _ => { run.Released = true; if (request.Scope.IsActive) run.Markers.Emit("Prepared"); }, request.Mode == PlaybackMode.Animated);
                    }
                    else if (!request.ObserveProjection && request.ActionKey != CardZoneActions.Settle)
                    {
                        var after = Project(request.After);
                        if (request.ActionKey == CardZoneActions.Play || request.ActionKey == CardZoneActions.Discard)
                            Hand.Present(after, request.ViewIds, request.Mode == PlaybackMode.Animated);
                        else Hand.Present(after, request.Mode == PlaybackMode.Animated);
                    }
                }
                if (request.Mode == PlaybackMode.Instant) CompleteMotion();
                if (run.Preparation != null)
                {
                    if (run.Preparation.keepWaiting) return;
                    if (run.Preparation.Status == CardActionViewStatus.Cancelled) { run.Operation.Cancel(); return; }
                    if (run.Preparation.Status == CardActionViewStatus.Failed) throw run.Preparation.Error;
                    run.Operation.TryComplete(); return;
                }
                // Observe requests may arrive before the fact checkpoint. Never report an old settled zone as the new one.
                if (request.ObserveProjection && request.ActionKey != CardZoneActions.Settle && (Hand.Data == null ||
                    !Hand.Data.Cards.Select(x => x.ViewId).OrderBy(x => x).SequenceEqual(request.After.Cards.Select(x => x.ViewId).OrderBy(x => x)))) return;
                bool departing = request.ActionKey == CardZoneActions.Play || request.ActionKey == CardZoneActions.Discard;
                if (departing && !run.Released) { run.Released = true; run.Markers.Emit("Released"); }
                if (Hand.IsTransitioning || Hand.Views.Any(card => card.IsLifting || card.IsHoverScaling)) return;
                run.Markers.Emit(departing || request.ActionKey == CardZoneActions.Draw ? "AllArrived" : "Settled");
                run.Operation.TryComplete();
            }
            catch (Exception error) { Release(run); run.Operation.TryFail("CardZoneMotionFailed", error.Message, error); }
        }
        private void Release(Run run)
        {
            if (run.Preparation != null && Hand != null) Hand.CancelSelectionAction(run.Preparation);
            else if (ReferenceEquals(active, run) && !run.Superseded && run.Operation.Status != PresentationStatus.Completed) CompleteMotion();
            if (ReferenceEquals(active, run)) active = null;
            retained.Remove(run);
        }
        private void CompleteMotion()
        { if (Hand == null) return; Hand.CompleteTransitions(); foreach (var card in Hand.Views) if (card != null) card.CompleteLift(); }
        private void CancelRuns()
        {
            foreach (var run in retained.ToArray())
                if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run);
        }
        public override void Synchronize(CardZoneDisplayData snapshot)
        {
            CancelRuns(); CancelPresentationActions();
            if (Hand == null) return;
            Hand.CompleteTransitions();
            if (snapshot == null) Hand.Clear(); else Hand.Present(Project(snapshot), false);
        }
        private void Update() => Tick();
        private void OnDisable() { CancelRuns(); CancelPresentationActions(); }
    }
}
