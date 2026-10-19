using CardArt;
using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Hand-specific deal/discard flights, selection lift and audio on the shared zone renderer.</summary>
    [AddComponentMenu("Cards/Hand Zone View")]
    public partial class HandZoneView : CardZoneView
    {
        [SerializeField] private Vector3 raisedOffset = new Vector3(0, 0, 0.02f);
        // Retained only for old scene/prefab serialization and API compatibility; hover no longer translates.
        [SerializeField, HideInInspector] private Vector3 hoveredOffset;
        [SerializeField] private CardZoneLiftSettings liftSettings = CardZoneLiftSettings.Default;
        [SerializeField] private CardZoneTransitionSettings transitionSettings = CardZoneTransitionSettings.Default;
        [SerializeField] private Vector3 deckLocalPosition = new Vector3(0.38f, 0.035f, 0);
        [SerializeField] private Transform deckAnchor;
        private double nextDealTime;
        public override string DefaultZoneKind => CardZoneKinds.Hand;
        public Vector3 RaisedOffset => raisedOffset;
        public Vector3 SelectedOffset => raisedOffset;
        public Vector3 HoveredOffset => Vector3.zero;
        public const float HoverAreaMultiplier = 1.2f;
        public CardZoneLiftSettings LiftSettings => liftSettings;
        public CardZoneTransitionSettings TransitionSettings => transitionSettings;
        public Vector3 DeckLocalPosition => deckLocalPosition;
        public Transform DeckAnchor => deckAnchor;
        public Vector3 DeckWorldPosition => deckAnchor != null ? deckAnchor.position : transform.TransformPoint(deckLocalPosition * CardScale);
        public override bool IsTransitioning => selectionAction?.Operation.keepWaiting == true || departing.Count > 0 || base.IsTransitioning;

        protected override void ValidatePresentationSettings()
        {
            ValidateOffset(raisedOffset); ValidateOffset(hoveredOffset); ValidateOffset(deckLocalPosition);
            liftSettings.Validate(); transitionSettings.Validate(); discardSettings.Validate();
            ValidateSelectionSettings(); hapticSettings.Validate();
        }

        public override void Present(CardZoneViewData data, bool animate = true) => base.Present(data, animate);

        /// <summary>Explicit removal cause; ordinary list deletions remain immediate.</summary>
        public void Present(CardZoneViewData data, IEnumerable<string> discardedViewIds, bool animate = true) =>
            ApplyPresentation(data, () => new HandPresentationUpdate(this, data, animate, discardedViewIds));

        public void Present(CardZoneViewData data, bool animate, IEnumerable<string> discardedIds) =>
            ApplyPresentation(data, () => new HandPresentationUpdate(this, data, animate, discardedIds));

        /// <summary>Completes when this batch has left the View, independently of retained-card reflow.</summary>
        public CardActionViewOperation PresentDiscardCommitted(CardZoneViewData data, IEnumerable<string> discardedIds, Action commitData, bool animate = true)
        {
            var operation = new CardActionViewOperation();
            try { ApplyPresentation(data, () => new HandPresentationUpdate(this, data, animate, discardedIds, operation), commitData); }
            catch (Exception error) { operation.Fail(error); throw; }
            return operation;
        }

        public void Present(CardZone zone, Func<CardInstanceId, CardVisualData> project, bool animate,
            IEnumerable<string> discardedViewIds) => Present(CardZoneViewData.FromZone(zone, project), animate, discardedViewIds);

        protected override PresentationUpdate CreatePresentationUpdate(CardZoneViewData data, bool animate) =>
            new HandPresentationUpdate(this, data, animate, null);

        private sealed class HandPresentationUpdate : PresentationUpdate
        {
            private readonly HandZoneView hand;
            private readonly CardZoneViewData data;
            private readonly bool animateInteraction, animate, reuse;
            private readonly bool[] selections, deselections, arrivals;
            private readonly float[] delays;
            private readonly HashSet<string> discards;
            private readonly Vector3 source, discardDestination;
            private readonly List<CardInstanceView> immediateDiscards = new List<CardInstanceView>();
            private double queue;
            private float discardDelay;
            private bool reflowStarted;
            private readonly CardActionViewOperation discardOperation;

            public HandPresentationUpdate(HandZoneView hand, CardZoneViewData data, bool requestedAnimation, IEnumerable<string> discardedViewIds, CardActionViewOperation discardOperation = null)
            {
                this.hand = hand; this.data = data; this.discardOperation = discardOperation;
                discards = hand.ValidateDepartures(data, discardedViewIds);
                discardDestination = hand.transform.InverseTransformPoint(hand.DiscardWorldPosition);
                ValidateOffset(discardDestination);
                if (!CardZoneRowLayout.Finite(discards.Count * hand.discardSettings.Interval + hand.discardSettings.FlightSeconds))
                    throw new ArgumentException("Discard queue exceeds finite animation time.");
                animateInteraction = requestedAnimation && hand.isActiveAndEnabled;
                animate = animateInteraction && hand.transitionSettings.Enabled;
                reuse = hand.Data != null && hand.Data.ZoneId == data.ZoneId;
                var previouslyUnselected = animateInteraction && reuse ? new HashSet<string>(
                    hand.Data.Cards.Where(card => !card.Interaction.IsSelected).Select(card => card.ViewId), StringComparer.Ordinal) : null;
                var previouslySelected = animateInteraction && reuse ? new HashSet<string>(
                    hand.Data.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.ViewId), StringComparer.Ordinal) : null;
                selections = new bool[data.Cards.Count]; deselections = new bool[data.Cards.Count];
                arrivals = new bool[data.Cards.Count]; delays = new float[data.Cards.Count];
                queue = reuse ? hand.nextDealTime : hand.AnimationTime;
                source = hand.transform.InverseTransformPoint(hand.DeckWorldPosition);
                ValidateOffset(source);
                for (int i = 0; i < data.Cards.Count; i++)
                {
                    selections[i] = previouslyUnselected != null && data.Cards[i].Interaction.IsSelected &&
                        previouslyUnselected.Contains(data.Cards[i].ViewId);
                    deselections[i] = previouslySelected != null && !data.Cards[i].Interaction.IsSelected &&
                        previouslySelected.Contains(data.Cards[i].ViewId);
                    arrivals[i] = !reuse || !hand.ContainsViewIdentity(data.Cards[i].ViewId);
                    if (!animate || !arrivals[i]) continue;
                    queue = Math.Max(queue, hand.AnimationTime); delays[i] = (float)(queue - hand.AnimationTime);
                    if (!CardZoneRowLayout.Finite(delays[i] + hand.transitionSettings.FlightSeconds + hand.transitionSettings.SettleSeconds))
                        throw new ArgumentException("Deal queue exceeds finite animation time.");
                    queue += hand.transitionSettings.DealInterval;
                }
            }

            public override void BeforeApply() => hand.PrepareDepartures(data, animate, reuse);
            public override void PresentCard(CardInstanceView view, CardVisualData card, CardShaderAppearance appearance,
                CardZoneCardPose pose, int index, bool created)
            {
                // Compare final layout targets, not the in-flight display pose, so repeated snapshots stay silent.
                if (animate && !created && !arrivals[index] && !view.IsEntering &&
                    (view.transform.localPosition != pose.Position || view.transform.localRotation != pose.Rotation))
                    reflowStarted = true;
                view.Present(card, hand.hoveredOffset, hand.raisedOffset, appearance, hand.liftSettings, animateInteraction, hoverScaling: true);
                // Lost visuals for an existing identity are restored without replaying the deal.
                view.SetPose(pose, hand.transitionSettings, animate && (arrivals[index] || !created), arrivals[index], source, delays[index]);
            }
            public override void RemoveCard(CardInstanceView previous)
            {
                if (animate && discards.Contains(previous.ViewId) && previous.IsTransitionVisible)
                {
                    previous.BeginDiscard(discardDestination, discardDelay, hand.discardSettings);
                    hand.departing.Add(previous); discardDelay += hand.discardSettings.Interval;
                }
                else if (animateInteraction && !animate && discards.Contains(previous.ViewId)) immediateDiscards.Add(previous);
                else Release(previous);
            }
            public override void AfterCommit()
            {
                hand.ReconcileSelectionAction();
                hand.TrackDiscard(discardOperation, discards);
                var committed = hand.ordered;
                hand.nextDealTime = animate && committed.Length > 0 ? queue : hand.AnimationTime;
                hand.ConsumeDealSounds(); hand.ConsumeDiscardSounds();
                if (animateInteraction && !animate)
                    for (int i = 0; i < committed.Length; i++) if (arrivals[i])
                    { hand.NotifyDealStarted(committed[i]); hand.NotifyCardEnteredZone(committed[i]); }
                foreach (var previous in immediateDiscards) hand.NotifyDiscardStarted(previous);
                for (int i = 0; i < committed.Length; i++)
                {
                    var view = committed[i];
                    if (view == null || !hand.TryGetView(view.ViewId, out var current) || current != view) continue;
                    if (selections[i] && view.Data.Interaction.IsSelected) hand.NotifyCardSelected(view);
                    else if (deselections[i] && !view.Data.Interaction.IsSelected) hand.NotifyCardDeselected(view);
                }
                if (reflowStarted && ReferenceEquals(hand.Data, data) && hand.isActiveAndEnabled) hand.NotifyReflowStarted();
            }
            public override void Dispose() { foreach (var previous in immediateDiscards) Release(previous); }
        }

        protected override void ApplyLayoutPose(CardInstanceView view, CardZoneCardPose pose, bool animate)
        {
            view.SetPose(pose, transitionSettings, animate && transitionSettings.Enabled && isActiveAndEnabled);
            view.ApplyInteractionOffset(hoveredOffset, raisedOffset, liftSettings, animate, hoverScaling: true);
        }

        public void SetTransitionSettings(CardZoneTransitionSettings settings)
        {
            settings.Validate(); transitionSettings = settings;
            if (!settings.Enabled) CompleteTransitions();
        }

        public void SetLiftSettings(CardZoneLiftSettings settings)
        {
            settings.Validate(); liftSettings = settings;
            foreach (var view in ordered) if (view != null) view.ApplyInteractionOffset(hoveredOffset, raisedOffset, liftSettings, hoverScaling: true);
        }

        /// <summary>Fallback deck source in zone-local coordinates; an optional scene anchor takes precedence.</summary>
        public void SetDeckSource(Vector3 localPosition, Transform anchor = null)
        {
            ValidateOffset(localPosition); deckLocalPosition = localPosition; deckAnchor = anchor;
        }

        /// <summary>Sets the selection offset. Hover is always an area enlargement without translation.</summary>
        public void SetRaisedOffset(Vector3 offset) => SetInteractionOffsets(offset, offset);

        /// <summary>Legacy offset API: hovered is validated for compatibility but has no visual effect; selected is zone-local.</summary>
        public void SetInteractionOffsets(Vector3 hovered, Vector3 selected)
        {
            ValidateOffset(hovered); ValidateOffset(selected);
            hoveredOffset = hovered; raisedOffset = selected;
            foreach (var view in ordered) if (view != null) view.ApplyInteractionOffset(hoveredOffset, raisedOffset, liftSettings, hoverScaling: true);
        }

        public override void CompleteTransitions()
        {
            CancelSelectionAction(); ClearDepartures(); base.CompleteTransitions(); nextDealTime = AnimationTime;
        }
        protected override void OnMotionAdvanced(float deltaTime)
        {
            AdvanceSelectionAction(deltaTime); AdvanceDepartures(deltaTime); ConsumeDealSounds();
        }
        public override void Clear()
        {
            CancelSelectionAction(); ClearDepartures(); base.Clear(); nextDealTime = 0; StopCardAudio();
        }
        protected override void OnDisable() { CancelDiscardBatches(); base.OnDisable(); StopCardAudio(); }
        protected override void OnDestroy() { base.OnDestroy(); ReleaseCardAudio(); }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            float scale;
            try { scale = CardScale; } catch (ArgumentException) { return; }
            var oldMatrix = Gizmos.matrix; var oldColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            var deck = transform.InverseTransformPoint(DeckWorldPosition);
            Gizmos.color = new Color(1, 0.7f, 0.2f);
            Gizmos.DrawWireCube(deck, new Vector3(0.064f, 0.006f, 0.096f) * scale);
            Gizmos.DrawLine(deck, new Vector3(Layout.Size.x / 2, 0, 0));
            var discard = transform.InverseTransformPoint(DiscardWorldPosition);
            Gizmos.color = new Color(0.9f, 0.35f, 0.45f);
            Gizmos.DrawWireCube(discard, new Vector3(0.064f, 0.006f, 0.096f) * scale);
            Gizmos.DrawLine(new Vector3(-Layout.Size.x / 2, 0, 0), discard);
            Gizmos.matrix = oldMatrix; Gizmos.color = oldColor;
        }
    }
}
