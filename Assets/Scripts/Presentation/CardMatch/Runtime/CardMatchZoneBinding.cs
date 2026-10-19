using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>A frontend owns its objects, artwork and animation. Snapshots are copied observer data.</summary>
    public interface ICardMatchZoneView
    {
        void Present(CardMatchZoneData snapshot);
        void Clear();
    }

    /// <summary>Optional generic binding for Canvas, sprites, meshes or other custom frontends.</summary>
    public sealed class CardMatchZoneBinding : MonoBehaviour
    {
        [SerializeField] private CardMatchHost match;
        [SerializeField] private string zoneId;
        [SerializeField] private MonoBehaviour view;
        private CardMatchHost subscribed;
        public CardMatchHost Match => match;
        public void Configure(CardMatchHost owner, ZoneId zone, MonoBehaviour target)
        {
            if (owner == null || !zone.IsValid || !(target is ICardMatchZoneView) ||
                owner.gameObject.scene != gameObject.scene || target.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind a match, zone identity and ICardMatchZoneView in this scene.");
            Unsubscribe(); (view as ICardMatchZoneView)?.Clear(); match = owner; zoneId = zone.Value; view = target;
            if (isActiveAndEnabled) Subscribe();
        }
        public void Refresh()
        {
            if (!(view is ICardMatchZoneView target)) return;
            if (match == null || !match.IsRunning || string.IsNullOrEmpty(zoneId) || !match.ContainsZone(new ZoneId(zoneId)))
            { target.Clear(); return; }
            target.Present(match.Snapshot(new ZoneId(zoneId)));
        }
        private void Changed(CardTableChange change, IReadOnlyList<object> presented)
        { if (!presented.Any(token => ReferenceEquals(token, view))) Refresh(); }
        private void InteractionChanged(ZoneId zone, object presented)
        { if (zone.Value == zoneId && !ReferenceEquals(presented, view)) Refresh(); }
        private void Clear() => (view as ICardMatchZoneView)?.Clear();
        private void Subscribe()
        {
            if (match == null || subscribed != null) return;
            subscribed = match; subscribed.Restarted += Refresh; subscribed.Changed += Changed;
            subscribed.InteractionChanged += InteractionChanged; subscribed.Stopped += Clear; Refresh();
        }
        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Restarted -= Refresh; subscribed.Changed -= Changed;
            subscribed.InteractionChanged -= InteractionChanged; subscribed.Stopped -= Clear; subscribed = null;
        }
        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe(); Clear(); }
    }
}
