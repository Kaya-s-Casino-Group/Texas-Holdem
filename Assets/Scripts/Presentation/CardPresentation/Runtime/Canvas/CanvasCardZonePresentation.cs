using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using BattlePresentation.Unity;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Canvas implementation. Owns RectTransforms only; no HandZoneView or battle commands.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class CanvasCardZonePresentation : CardZonePresentationBehaviour
    {
        [SerializeField] private CardUIGraphic cardTemplate;
        [SerializeField] private Vector2 cardSize = new Vector2(128, 192);
        [SerializeField] private float spacing = 84;
        [SerializeField, Min(0)] private float seconds = .25f;
        [SerializeField, Range(0, 1)] private float preparedProgress = .7f;
        [SerializeField] private Vector2 drawOffset = new Vector2(-400, 100);
        [SerializeField] private Vector2 releaseOffset = new Vector2(500, 200);
        [SerializeField] private Vector2 preparationOffset = new Vector2(0, 40);
        private CardZoneDisplayData current;
        private readonly Dictionary<string, CardUIGraphic> cards = new Dictionary<string, CardUIGraphic>();
        private Run active;
        private sealed class Motion
        {
            internal CardUIGraphic Card;
            internal Vector2 From, To;
            internal bool Departing;
        }
        private sealed class Run
        {
            internal CardZonePresentationRequest Request;
            internal IMarkerSink Markers;
            internal PresentationOperation Operation;
            internal readonly List<Motion> Motions = new List<Motion>();
            internal float Elapsed;
            internal bool Prepared;
        }
        public override bool IsPresenting => active?.Operation.Status == PresentationStatus.Pending;
        public override CardZoneDisplayData Current => current;
        public void Configure(CardUIGraphic template, Vector2 size, float gap = 84)
        {
            if (template == null || size.x <= 0 || size.y <= 0 || gap < 0 ||
                !CardZoneRowLayout.Finite(size.x + size.y + gap)) throw new ArgumentException("Configure a Canvas card template and finite layout.");
            cardTemplate = template; cardSize = size; spacing = gap;
        }
        public bool TryGetCard(string id, out CardUIGraphic card) => cards.TryGetValue(id, out card);
        private Vector2 Slot(int index, int count, CardDisplayData card)
        {
            bool selected = card.Properties.TryGetValue("selected", out var raw) && bool.TryParse(raw, out var value) && value;
            return new Vector2((index - (count - 1) * .5f) * spacing, selected ? 24 : 0);
        }
        private void Present(CardUIGraphic graphic, CardDisplayData card)
        {
            var effects = !card.Hidden && card.Properties.TryGetValue("effects", out var raw) && int.TryParse(raw, out var flags) ? (CardEffects)flags : CardEffects.None;
            bool Flag(string key) => card.Properties.TryGetValue(key, out var value) && bool.TryParse(value, out var result) && result;
            graphic.Present(new CardVisualData(card.ViewId, card.Hidden ? null : card.ArtKey,
                card.Hidden || string.IsNullOrEmpty(card.CardId) ? (CardInstanceId?)null : new CardInstanceId(card.CardId),
                effects: effects, isDisabled: !card.Hidden && card.DisabledReason != null,
                interaction: new CardInteractionState(Flag("hovered"), Flag("selected")), artBindings: CardStateArtBindings.Standard));
        }
        private CardUIGraphic Create(CardDisplayData data)
        {
            var card = Instantiate(cardTemplate, transform); card.name = "Card " + data.ViewId;
            var rect = card.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one; rect.sizeDelta = cardSize;
            card.gameObject.SetActive(true); cards.Add(data.ViewId, card); return card;
        }
        private static void Remove(CardUIGraphic card)
        { if (card == null) return; card.gameObject.SetActive(false); if (Application.isPlaying) Destroy(card.gameObject); else DestroyImmediate(card.gameObject); }
        protected override IPresentationOperation PlayBuiltIn(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (request.ObserveProjection) return PresentationOperation.Failed("UnsupportedProjectionObservation", "Canvas actions consume explicit display snapshots.");
            if (cardTemplate == null) return PresentationOperation.Failed("MissingCanvasCardTemplate", name);
            if (!CardZoneRowLayout.Finite(seconds + preparedProgress) || seconds < 0 || preparedProgress < 0 || preparedProgress > 1)
                return PresentationOperation.Failed("InvalidCanvasTiming", name);
            if (active != null && active.Operation.Status == PresentationStatus.Pending)
            {
                if (request.ActionKey != CardZoneActions.Selection || active.Request.ActionKey != CardZoneActions.Selection)
                    return PresentationOperation.Failed("CardZoneBusy", request.ActionKey);
                var visiblePositions = cards.ToDictionary(x => x.Key, x => x.Value.rectTransform.anchoredPosition);
                active.Operation.Cancel();
                foreach (var item in visiblePositions) if (cards.TryGetValue(item.Key, out var card)) card.rectTransform.anchoredPosition = item.Value;
            }
            // Retire a completed preparation before constructing the next layout.
            if (active != null) Release(active);
            var run = new Run { Request = request, Markers = markers };
            run.Operation = new PresentationOperation(() => Release(run)); active = run;
            request.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            try
            {
                if (request.ActionKey == CardZoneActions.Prepare)
                {
                    foreach (var id in request.ViewIds)
                    {
                        if (!cards.TryGetValue(id, out var card)) throw new ArgumentException("Missing Canvas card: " + id);
                        run.Motions.Add(new Motion { Card = card, From = card.rectTransform.anchoredPosition,
                            To = card.rectTransform.anchoredPosition + preparationOffset });
                    }
                }
                else if (!request.ObserveProjection && request.ActionKey != CardZoneActions.Settle)
                {
                    bool departing = request.ActionKey == CardZoneActions.Play || request.ActionKey == CardZoneActions.Discard;
                    if (departing && request.ViewIds.Any(id => request.After.Cards.Any(card => card.ViewId == id)))
                        throw new ArgumentException("Release actions require a snapshot with those cards removed.");
                    foreach (var old in cards.Where(x => request.After.Cards.All(c => c.ViewId != x.Key)).ToArray())
                    {
                        cards.Remove(old.Key);
                        if (departing && request.ViewIds.Contains(old.Key)) run.Motions.Add(new Motion { Card = old.Value,
                            From = old.Value.rectTransform.anchoredPosition, To = old.Value.rectTransform.anchoredPosition + releaseOffset, Departing = true });
                        else Remove(old.Value);
                    }
                    for (int i = 0; i < request.After.Cards.Count; i++)
                    {
                        var data = request.After.Cards[i]; var target = Slot(i, request.After.Cards.Count, data);
                        if (!cards.TryGetValue(data.ViewId, out var card)) { card = Create(data); card.rectTransform.anchoredPosition = target + drawOffset; }
                        Present(card, data); card.transform.SetSiblingIndex(i);
                        run.Motions.Add(new Motion { Card = card, From = card.rectTransform.anchoredPosition, To = target });
                    }
                    current = request.After;
                    if (departing) markers.Emit("Released");
                }
                if (request.Mode == PlaybackMode.Instant) AdvanceMotion(float.MaxValue);
                else if (!request.ObserveProjection) AdvanceMotion(0);
            }
            catch (Exception error) { Release(run); run.Operation.TryFail("CanvasCardActionFailed", error.Message, error); }
            return run.Operation;
        }
        public void AdvanceMotion(float delta)
        {
            if (!CardZoneRowLayout.Finite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            var run = active;
            if (run == null || run.Operation.Status != PresentationStatus.Pending) return;
            if (!run.Request.Scope.IsActive) { run.Operation.Cancel(); return; }
            try
            {
                if (run.Request.ObserveProjection && (current == null ||
                    !current.Cards.Select(x => x.ViewId).SequenceEqual(run.Request.After.Cards.Select(x => x.ViewId)))) return;
                run.Elapsed = Mathf.Min(seconds, run.Elapsed + delta);
                float progress = seconds == 0 ? 1 : Mathf.Clamp01(run.Elapsed / seconds);
                foreach (var motion in run.Motions)
                {
                    if (motion.Card == null) throw new InvalidOperationException("An animated Canvas card was removed.");
                    motion.Card.rectTransform.anchoredPosition = Vector2.Lerp(motion.From, motion.To, 1 - (1 - progress) * (1 - progress));
                }
                if (run.Request.ActionKey == CardZoneActions.Prepare && !run.Prepared && progress >= preparedProgress)
                { run.Prepared = true; run.Markers.Emit("Prepared"); }
                if (progress < 1) return;
                foreach (var motion in run.Motions.Where(x => x.Departing)) Remove(motion.Card);
                if (run.Request.ActionKey != CardZoneActions.Prepare)
                {
                    bool departure = run.Request.ActionKey == CardZoneActions.Play || run.Request.ActionKey == CardZoneActions.Discard;
                    if (departure && run.Request.ObserveProjection) run.Markers.Emit("Released");
                    run.Markers.Emit(departure || run.Request.ActionKey == CardZoneActions.Draw ? "AllArrived" : "Settled");
                }
                run.Operation.TryComplete();
            }
            catch (Exception error) { Release(run); run.Operation.TryFail("CanvasCardMotionFailed", error.Message, error); }
        }
        private void Release(Run run)
        {
            if (!ReferenceEquals(active, run)) return;
            active = null;
            foreach (var motion in run.Motions) if (motion.Departing) Remove(motion.Card);
            // Reconstruct from the latest accepted display layout, never an old selection baseline.
            if (current != null) for (int i = 0; i < current.Cards.Count; i++)
                if (cards.TryGetValue(current.Cards[i].ViewId, out var card) && card != null)
                    card.rectTransform.anchoredPosition = Slot(i, current.Cards.Count, current.Cards[i]);
        }
        public override void Synchronize(CardZoneDisplayData snapshot)
        {
            if (active != null) { if (active.Operation.Status == PresentationStatus.Pending) active.Operation.Cancel(); else Release(active); }
            CancelPresentationActions();
            foreach (var card in cards.Values) Remove(card); cards.Clear(); current = snapshot;
            if (snapshot == null) return;
            if (cardTemplate == null) throw new InvalidOperationException("Bind a Canvas card template.");
            for (int i = 0; i < snapshot.Cards.Count; i++)
            { var data = snapshot.Cards[i]; var card = Create(data); Present(card, data); card.rectTransform.anchoredPosition = Slot(i, snapshot.Cards.Count, data); }
        }
        private void Update() => AdvanceMotion(Time.unscaledDeltaTime);
        private void OnDisable() { var run = active; if (run != null) { run.Operation.Cancel(); Release(run); } CancelPresentationActions(); }
    }
}
