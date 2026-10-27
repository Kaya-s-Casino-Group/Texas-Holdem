using System;
using System.Collections.Generic;
using CardPresentation;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>One mouse/zone context. Keeps gesture identity only; hover and selection belong to source cards.</summary>
    public sealed class CardZonePointerController
    {
        private readonly CardZoneView zone;
        private readonly ICardInteractionSource source;
        private string pressedViewId;
        private string zoneContext;
        private double pressedAt;
        private Vector2? pressPosition;
        private bool dragging, longPressConsumed;
        public float LongPressSeconds { get; set; } = .35f;
        public float PressMoveTolerance { get; set; } = 12;
        public bool IsDragging => dragging;
        public string HoveredViewId { get; private set; }
        public event Action StateChanged;

        public CardZonePointerController(CardZoneView zone, ICardInteractionSource source)
        {
            this.zone = zone != null ? zone : throw new ArgumentNullException(nameof(zone));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>pressed/released are left-button edges. blocked covers UI, focus loss or viewport exit.</summary>
        public void ProcessPointer(Ray ray, bool pressed = false, bool released = false, bool blocked = false,
            double? timestamp = null, Vector2? screenPosition = null)
        {
            string context = zone != null && zone.Data != null ? zone.Data.ZoneId.Value : null;
            if (context != zoneContext) { CancelPointer(); zoneContext = context; }
            if (blocked || zone == null || !zone.isActiveAndEnabled ||
                source is ICardInteractionGate gate && !gate.CanInteract) { CancelPointer(); return; }
            double now = timestamp ?? Time.unscaledTimeAsDouble;
            if (dragging)
            {
                if (!((ICardReorderSource)source).UpdateReorder(ray)) dragging = false;
                if (released) { ((ICardReorderSource)source).EndReorder(false); ResetPress(); }
                return;
            }
            string hitId = CardZoneRaycaster.TryHit(zone, ray, HoveredViewId, out var hit, includeEntering: true) &&
                source.TryGetInteraction(hit.ViewId, out _) ? hit.ViewId : null;
            bool changed = UpdateHover(hit != null && hit.IsEntering ? null : hitId);
            if (pressed)
            {
                pressedViewId = hitId; pressedAt = now; pressPosition = screenPosition; longPressConsumed = false;
            }
            if (pressedViewId != null && !longPressConsumed)
            {
                bool moved = pressPosition.HasValue && screenPosition.HasValue
                    ? Vector2.Distance(pressPosition.Value, screenPosition.Value) > PressMoveTolerance
                    : hitId != pressedViewId;
                if (moved) ResetPress();
                else if (source is ICardReorderSource reorder && now - pressedAt >= Mathf.Max(.05f, LongPressSeconds))
                {
                    longPressConsumed = true;
                    dragging = reorder.BeginReorder(pressedViewId, ray);
                    if (dragging) { UpdateHover(null); StateChanged?.Invoke(); }
                    if (released) { reorder.EndReorder(false); ResetPress(); }
                    return;
                }
            }
            if (released)
            {
                if (!longPressConsumed && hitId != null && hitId == pressedViewId && source.TryGetInteraction(hitId, out var state))
                {
                    bool canSelect = state.IsSelected || !hit.IsEntering && source.CanSelect(hitId) &&
                        (!(zone is HandZoneView hand) || GetSelectedViewIds().Count < hand.MaxSelectedCards);
                    if (canSelect)
                    { source.SetInteraction(hitId, state.WithSelected(!state.IsSelected)); changed = true; }
                    else if (zone is HandZoneView rejectedHand) rejectedHand.PlaySelectionDenied(hitId);
                }
                ResetPress();
            }
            if (changed) StateChanged?.Invoke();
        }

        /// <summary>Clear hover/gesture on exit or unbind, preserving each card's pending selection.</summary>
        public void CancelPointer()
        {
            if (dragging && source is ICardReorderSource reorder) reorder.EndReorder(true);
            ResetPress();
            if (UpdateHover(null)) StateChanged?.Invoke();
        }
        private void ResetPress() { pressedViewId = null; pressPosition = null; dragging = false; longPressConsumed = false; }

        public IReadOnlyList<string> GetSelectedViewIds()
        {
            var result = new List<string>();
            if (zone != null)
                foreach (var view in zone.Views)
                    if (view != null && source.TryGetInteraction(view.ViewId, out var state) && state.IsSelected) result.Add(view.ViewId);
            return result.AsReadOnly();
        }

        private bool UpdateHover(string hitId)
        {
            bool changed = false;
            // Also clear a removed card if the host still owns it outside this zone.
            if (HoveredViewId != null && HoveredViewId != hitId && source.TryGetInteraction(HoveredViewId, out var previous) && previous.IsHovered)
            { source.SetInteraction(HoveredViewId, previous.WithHovered(false)); changed = true; }
            if (zone != null)
                foreach (var view in zone.Views)
                {
                    if (view == null || !source.TryGetInteraction(view.ViewId, out var state)) continue;
                    bool hovered = view.ViewId == hitId;
                    if (state.IsHovered == hovered) continue;
                    source.SetInteraction(view.ViewId, state.WithHovered(hovered)); changed = true;
                }
            HoveredViewId = hitId;
            return changed;
        }
    }
}
