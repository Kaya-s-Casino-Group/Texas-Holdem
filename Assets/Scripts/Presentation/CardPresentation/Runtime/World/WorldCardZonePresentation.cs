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
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(HandZoneView))]
    public sealed partial class WorldCardZonePresentation : CardZonePresentationBehaviour, ICardZonePresentationPump, ICardZonePresentationPreview
    {
        private HandZoneView hand;
        private HandZoneView Hand => this == null ? null : hand != null ? hand : (hand = GetComponent<HandZoneView>());
        public override bool IsPresenting => retained.Any(run => run.Operation.Status == PresentationStatus.Pending) ||
            Hand != null && (Hand.IsTransitioning || Hand.Views.Any(card => card.IsLifting || card.IsHoverScaling));
        internal bool ObservesProjection => retained.Any(run => run.Operation.Status == PresentationStatus.Pending && run.Request.ObserveProjection);
        private CardZoneDisplayData current;
        private readonly Dictionary<string, CardInteractionState> interaction = new Dictionary<string, CardInteractionState>(StringComparer.Ordinal);
        private Run active;
        private readonly List<Run> retained = new List<Run>();
        private sealed class Run
        {
            internal CardZonePresentationRequest Request;
            internal IMarkerSink Markers;
            internal PresentationOperation Operation;
            internal CardActionViewOperation Preparation;
            internal bool Started, Released, Superseded, ProjectionObserved;
        }
        public override CardZoneDisplayData Current => current ?? (Hand == null || Hand.Data == null ? null : new CardZoneDisplayData(
            Hand.Data.ZoneId.Value, null, "hand", Hand.Data.Cards.Select(card => new CardDisplayData(card.ViewId,
                card.CardId?.Value, null, card.FaceArtId, !card.FaceUp, card.IsDisabled ? "disabled" : null,
                new Dictionary<string, string> { ["effects"] = ((int)card.Effects).ToString(),
                    ["selected"] = card.Interaction.IsSelected.ToString(), ["hovered"] = card.Interaction.IsHovered.ToString() }))));
        private CardZoneViewData Project(CardZoneDisplayData snapshot)
        {
            EndReorder(true, false);
            return new CardZoneViewData(new ZoneId(snapshot.Id), OrderedCards(snapshot).Select(card => {
                var previous = Hand.Data?.Cards.FirstOrDefault(x => x.ViewId == card.ViewId);
                bool Flag(string key, bool fallback) => card.Properties.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;
                var effects = !card.Hidden && card.Properties.TryGetValue("effects", out var bits) && int.TryParse(bits, out var flags) ? (CardEffects)flags : CardEffects.None;
                return new CardVisualData(card.ViewId, card.Hidden ? null : card.ArtKey,
                    card.Hidden || string.IsNullOrEmpty(card.CardId) ? (CardInstanceId?)null : new CardInstanceId(card.CardId),
                    previous?.Tint, effects, !card.Hidden && card.DisabledReason != null,
                    interaction.TryGetValue(card.ViewId, out var local) ? local : new CardInteractionState(Flag("hovered", false), Flag("selected", false)),
                    previous?.ArtBindings ?? CardStateArtBindings.Standard);
            }));
        }
        /// <summary>Local candidate feedback only; never publishes a fact or retargets layout/flight tweens.</summary>
        public void PresentInteraction(IEnumerable<KeyValuePair<string, CardInteractionState>> states, bool animate = true)
        {
            interaction.Clear();
            foreach (var pair in states ?? Enumerable.Empty<KeyValuePair<string, CardInteractionState>>())
                interaction[pair.Key] = pair.Value;
            if (Hand == null) return;
            foreach (var card in Hand.Views)
            {
                if (card == null || card.Data == null) continue;
                var data = card.Data;
                var state = interaction.TryGetValue(card.ViewId, out var value) ? value : default;
                if (data.Interaction.IsHovered == state.IsHovered && data.Interaction.IsSelected == state.IsSelected) continue;
                Hand.PresentInteraction(card, state, animate);
            }
        }
        protected override IPresentationOperation PlayBuiltIn(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (request.ObserveProjection && request.ActionKey == CardZoneActions.Prepare)
                return PresentationOperation.Failed("InvalidObservation", "Preparation must own its animation.");
            if (!request.ObserveProjection && active != null && active.Operation.Status == PresentationStatus.Pending)
            {
                if (active.Request.ActionKey == CardZoneActions.Prepare && active.Released)
                    active = null; // A released preparation may hand its instances to a later card fact.
                else if (request.ActionKey == CardZoneActions.Selection && active.Request.ActionKey == CardZoneActions.Selection)
                { active.Superseded = true; active.Operation.Cancel(); }
                else return PresentationOperation.Failed("CardZoneBusy", request.ActionKey);
            }
            var run = new Run { Request = request, Markers = markers,
                ProjectionObserved = request.ObserveProjection && Matches(Current, request.After) };
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
                    else if ((!request.ObserveProjection && request.ActionKey != CardZoneActions.Settle) || request.Mode == PlaybackMode.Instant)
                    {
                        var after = Project(request.After);
                        if (request.ActionKey == CardZoneActions.Play || request.ActionKey == CardZoneActions.Discard)
                            Hand.Present(after, request.ViewIds, request.Mode == PlaybackMode.Animated);
                        else Hand.Present(after, request.Mode == PlaybackMode.Animated);
                        current = request.After;
                        ObserveCurrentProjection();
                    }
                }
                if (request.Mode == PlaybackMode.Instant && run.Preparation == null) CompleteMotion();
                if (run.Preparation != null)
                {
                    if (run.Preparation.keepWaiting) return;
                    if (run.Preparation.Status == CardActionViewStatus.Cancelled) { run.Operation.Cancel(); return; }
                    if (run.Preparation.Status == CardActionViewStatus.Failed) throw run.Preparation.Error;
                    run.Operation.TryComplete(); return;
                }
                // Observe requests may arrive before the fact checkpoint. Never report an old settled zone as the new one.
                if (request.ObserveProjection && !run.ProjectionObserved)
                {
                    run.ProjectionObserved = Matches(Current, request.After);
                    if (!run.ProjectionObserved) return;
                }
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
        private void ObserveCurrentProjection()
        {
            // Several checkpoints may publish in one frame. Remember each observed snapshot
            // before a later projection replaces Current, then wait for the remaining motion.
            foreach (var run in retained)
                if (run.Request.ObserveProjection && Matches(current, run.Request.After)) run.ProjectionObserved = true;
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
            EndReorder(true, false);
            if (snapshot == null) { localOrder.Clear(); orderZoneId = null; }
            CancelRuns(); CancelPresentationActions();
            interaction.Clear();
            if (Hand == null) return;
            Hand.CompleteTransitions();
            if (snapshot == null) Hand.Clear(); else Hand.Present(Project(snapshot), false);
            current = snapshot;
        }
        public IDisposable PreservePreviewState()
        {
            var data = Hand.Data;
            var snapshot = current;
            var feedback = interaction.ToArray();
            var order = localOrder.ToArray(); var orderZone = orderZoneId;
            var restoration = new PresentationScope("world-preview-restore");
            restoration.Own(() => {
                Synchronize(null);
                if (Hand != null && data != null) Hand.Present(data, false);
                current = snapshot;
                localOrder.AddRange(order); orderZoneId = orderZone;
                PresentInteraction(feedback, false);
            });
            return restoration;
        }
        public void AdvancePreview(float deltaTime)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Preview clock is EditMode only.");
            AdvanceCustomPreview(deltaTime);
            if (Hand != null) Hand.AdvanceMotion(deltaTime);
            Tick();
        }
        // Observation must see the entire ordered projection, including state-only changes.
        internal static bool Matches(CardZoneDisplayData left, CardZoneDisplayData right)
        {
            if (left == null || right == null || left.Id != right.Id || left.OwnerId != right.OwnerId ||
                left.Kind != right.Kind || left.Cards.Count != right.Cards.Count) return false;
            for (int i = 0; i < left.Cards.Count; i++)
            {
                var a = left.Cards[i]; var b = right.Cards[i];
                if (a.ViewId != b.ViewId || a.CardId != b.CardId || a.FaceKey != b.FaceKey || a.ArtKey != b.ArtKey ||
                    a.Hidden != b.Hidden || a.DisabledReason != b.DisabledReason || a.Properties.Count != b.Properties.Count ||
                    a.Properties.Any(pair => !b.Properties.TryGetValue(pair.Key, out var value) || value != pair.Value)) return false;
            }
            return true;
        }
        private void Update() => Tick();
        private void OnDisable() { EndReorder(true, false); CancelRuns(); CancelPresentationActions(); }
    }
}
