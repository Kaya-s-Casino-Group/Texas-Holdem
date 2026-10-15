using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardPresentation
{
    public partial class HandZoneView
    {
        [SerializeField] private CardZoneDiscardSettings discardSettings = CardZoneDiscardSettings.Default;
        [SerializeField] private Vector3 discardLocalPosition = new Vector3(-0.38f, 0.035f, 0);
        [SerializeField] private Transform discardAnchor;
        private readonly List<CardInstanceView> departing = new List<CardInstanceView>();

        public CardZoneDiscardSettings DiscardSettings => discardSettings;
        public Vector3 DiscardLocalPosition => discardLocalPosition;
        public Transform DiscardAnchor => discardAnchor;
        public Vector3 DiscardWorldPosition => discardAnchor != null ? discardAnchor.position : transform.TransformPoint(discardLocalPosition * CardScale);
        /// <summary>Visual ghosts only. These cards have already left Data/Views and cannot receive input.</summary>
        public IReadOnlyList<CardInstanceView> DepartingViews => departing.AsReadOnly();

        protected override void OnCardScaleChanged(float scale)
        {
            base.OnCardScaleChanged(scale);
            foreach (var view in departing) if (view != null) view.SetPresentationScale(scale);
        }

        public void SetDiscardDestination(Vector3 localPosition, Transform anchor = null)
        {
            ValidateOffset(localPosition); discardLocalPosition = localPosition; discardAnchor = anchor;
        }

        public void SetDiscardSettings(CardZoneDiscardSettings settings)
        {
            settings.Validate(); discardSettings = settings;
        }

        private HashSet<string> ValidateDiscards(CardZoneViewData next, IEnumerable<string> ids)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null) return result;
            var retained = new HashSet<string>(next.Cards.Select(card => card.ViewId), StringComparer.Ordinal);
            foreach (var id in ids)
                if (id == null || !result.Add(id) || Data == null || Data.ZoneId != next.ZoneId ||
                    !ContainsViewIdentity(id) || retained.Contains(id))
                    throw new ArgumentException("Discard identities must be distinct cards removed from this zone's current snapshot.", nameof(ids));
            return result;
        }

        private void PrepareDepartures(CardZoneViewData next, bool animate, bool reuse)
        {
            var restored = new HashSet<string>(next.Cards.Select(card => card.ViewId), StringComparer.Ordinal);
            for (int i = departing.Count - 1; i >= 0; i--)
                if (!animate || !reuse || departing[i] == null || restored.Contains(departing[i].ViewId))
                { Release(departing[i]); departing.RemoveAt(i); }
        }

        private void AdvanceDepartures(float deltaTime)
        {
            foreach (var view in departing) if (view != null) view.AdvanceMotion(deltaTime);
            // Consume before releasing: a long frame may cross both launch and arrival of a queued discard.
            ConsumeDiscardSounds();
            for (int i = departing.Count - 1; i >= 0; i--)
            {
                var view = departing[i];
                if (view == null || !view.IsTransitioning)
                { Release(view); departing.RemoveAt(i); }
            }
        }

        private void ClearDepartures()
        {
            foreach (var view in departing) Release(view);
            departing.Clear();
        }
    }
}
