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
        [SerializeField, Tooltip("Use the facing camera's lower-left area, beyond the bottom screen edge.")] private bool discardOffscreen;
        [SerializeField] private Vector2 discardViewport = new Vector2(.22f, -.12f);
        [SerializeField, Range(0, 1), Tooltip("Start replacement cards after the last departing card has travelled this fraction of its exit path, including queued departures.")] private float refillDepartureWait;
        private readonly List<CardInstanceView> departing = new List<CardInstanceView>();

        private sealed class DiscardBatch
        {
            public CardActionViewOperation Operation;
            public CardInstanceView[] Views;
        }
        private readonly List<DiscardBatch> discardBatches = new List<DiscardBatch>();
        private void TrackDiscard(CardActionViewOperation operation, HashSet<string> ids)
        {
            if (operation == null) return;
            var views = departing.Where(view => view != null && ids.Contains(view.ViewId)).ToArray();
            if (views.Length == 0) { operation.Complete(); return; }
            discardBatches.Add(new DiscardBatch { Operation = operation, Views = views });
        }
        private void CancelDiscardBatches(CardInstanceView removed = null)
        {
            for (int i = discardBatches.Count - 1; i >= 0; i--)
                if (removed == null || discardBatches[i].Views.Contains(removed))
                { discardBatches[i].Operation.Cancel(); discardBatches.RemoveAt(i); }
        }
        private void CompleteDiscardBatches()
        {
            for (int i = discardBatches.Count - 1; i >= 0; i--)
                if (!discardBatches[i].Views.Any(view => departing.Contains(view)))
                { discardBatches[i].Operation.Complete(); discardBatches.RemoveAt(i); }
        }

        public CardZoneDiscardSettings DiscardSettings => discardSettings;
        public Vector3 DiscardLocalPosition => discardLocalPosition;
        public Transform DiscardAnchor => discardAnchor;
        public Vector3 DiscardWorldPosition
        {
            get
            {
                if (discardOffscreen && FacingCamera != null)
                {
                    float depth = FacingCamera.WorldToViewportPoint(transform.position).z;
                    return FacingCamera.ViewportToWorldPoint(new Vector3(discardViewport.x, discardViewport.y,
                        Mathf.Max(FacingCamera.nearClipPlane + .01f, depth)));
                }
                return discardAnchor != null ? discardAnchor.position : transform.TransformPoint(discardLocalPosition * CardScale);
            }
        }
        private float RefillDepartureDelay => refillDepartureWait <= 0 ? 0 : departing.Where(view => view != null)
            .Select(view => view.SecondsUntilExitTravelProgress(refillDepartureWait)).DefaultIfEmpty(0).Max();
        public void ConfigureScreenDiscard(bool enabled, float refillWait = .92f)
        {
            if (float.IsNaN(refillWait) || float.IsInfinity(refillWait) || refillWait < 0 || refillWait > 1)
                throw new ArgumentOutOfRangeException(nameof(refillWait));
            discardOffscreen = enabled; refillDepartureWait = refillWait;
        }
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

        private HashSet<string> ValidateDepartures(CardZoneViewData next, IEnumerable<string> ids)
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
                { CancelDiscardBatches(departing[i]); Release(departing[i]); departing.RemoveAt(i); }
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
            CompleteDiscardBatches();
        }

        private void ClearDepartures()
        {
            CancelDiscardBatches();
            foreach (var view in departing) Release(view);
            departing.Clear();
        }
    }
}
