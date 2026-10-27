using System;
using System.Linq;
using BattlePresentation;
using BattlePresentation.Unity;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Projects director checkpoints into the world renderer. Owns no rules or input.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(WorldCardZonePresentation))]
    public sealed class WorldCardZoneDisplayBinding : MonoBehaviour
    {
        [SerializeField] private ScenePresentationDirector director;
        [SerializeField, Tooltip("Empty follows the current observer's hand, including after a restart.")]
        private string zoneId;
        private WorldCardZonePresentation view;
        private IDisplayReader subscribed;
        private PresentationScope projection;
        private long generation;
        public PresentationError LastError { get; private set; }

        public void Configure(ScenePresentationDirector source, string zone = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Unsubscribe(); director = source; zoneId = zone;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            view = GetComponent<WorldCardZonePresentation>();
            if (director == null) return;
            subscribed = director.Display;
            subscribed.Changed += OnDisplayChanged;
            if (subscribed.Current != null) view.Synchronize(ResolveZone(subscribed.Current));
        }

        private CardZoneDisplayData ResolveZone(BattleDisplaySnapshot snapshot) =>
            !string.IsNullOrWhiteSpace(zoneId) ? snapshot.Zone(zoneId) :
            snapshot.Zones.FirstOrDefault(zone => zone.Kind == "hand" && zone.OwnerId == snapshot.ObserverId);

        private void OnDisplayChanged(DisplayUpdate update)
        {
            var after = ResolveZone(update.Current);
            if (update.IsSynchronization || after == null)
            {
                ReleaseProjection(); view.Synchronize(after); LastError = null; return;
            }
            // An explicit timeline action may already have applied this same projection.
            if (WorldCardZonePresentation.Matches(view.Current, after)) return;
            bool animate = view.ObservesProjection;
            ReleaseProjection();
            var before = view.Current ?? new CardZoneDisplayData(after.Id, after.OwnerId, after.Kind, null);
            var departing = before.Cards.Where(card => !after.Cards.Any(next => next.ViewId == card.ViewId))
                .Select(card => card.ViewId).ToArray();
            var arriving = after.Cards.Where(card => !before.Cards.Any(previous => previous.ViewId == card.ViewId))
                .Select(card => card.ViewId).ToArray();
            string key = departing.Length > 0 ? CardZoneActions.Discard : arriving.Length > 0 ? CardZoneActions.Draw : CardZoneActions.State;
            projection = new PresentationScope("world-projection/" + ++generation);
            var operation = view.Play(new CardZonePresentationRequest(key, projection.Id, projection,
                before, after, departing.Length > 0 ? departing : arriving,
                animate ? PlaybackMode.Animated : PlaybackMode.Instant), IgnoreMarkers.Instance);
            LastError = operation.Error;
            if (operation.Status == PresentationStatus.Failed)
            {
                ReleaseProjection();
                throw new InvalidOperationException("Card projection failed: " + LastError);
            }
        }

        private void ReleaseProjection() { projection?.Dispose(); projection = null; }
        private void Unsubscribe()
        {
            if (subscribed != null) subscribed.Changed -= OnDisplayChanged;
            subscribed = null; ReleaseProjection();
        }
        private void OnDisable() => Unsubscribe();
        private sealed class IgnoreMarkers : IMarkerSink
        {
            internal static readonly IgnoreMarkers Instance = new IgnoreMarkers();
            public bool Emit(string marker) => true;
        }
    }
}
