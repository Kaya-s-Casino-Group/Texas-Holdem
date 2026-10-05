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
        public string HoveredViewId { get; private set; }
        public event Action StateChanged;

        public CardZonePointerController(CardZoneView zone, ICardInteractionSource source)
        {
            this.zone = zone != null ? zone : throw new ArgumentNullException(nameof(zone));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>pressed/released are left-button edges. blocked covers UI, focus loss or viewport exit.</summary>
        public void ProcessPointer(Ray ray, bool pressed = false, bool released = false, bool blocked = false)
        {
            string context = zone != null && zone.Data != null ? zone.Data.ZoneId.Value : null;
            if (context != zoneContext) { pressedViewId = null; zoneContext = context; }
            if (blocked || zone == null || !zone.isActiveAndEnabled) { CancelPointer(); return; }
            string hitId = CardZoneRaycaster.TryHit(zone, ray, HoveredViewId, out var hit) &&
                source.TryGetInteraction(hit.ViewId, out _) ? hit.ViewId : null;
            bool changed = UpdateHover(hitId);
            if (pressed) pressedViewId = hitId;
            if (released)
            {
                if (hitId != null && hitId == pressedViewId && source.TryGetInteraction(hitId, out var state) &&
                    (state.IsSelected || source.CanSelect(hitId)))
                {
                    source.SetInteraction(hitId, state.WithSelected(!state.IsSelected)); changed = true;
                }
                pressedViewId = null;
            }
            if (changed) StateChanged?.Invoke();
        }

        /// <summary>Clear hover/gesture on exit or unbind, preserving each card's pending selection.</summary>
        public void CancelPointer()
        {
            pressedViewId = null;
            if (UpdateHover(null)) StateChanged?.Invoke();
        }

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
