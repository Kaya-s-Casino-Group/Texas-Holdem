using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattlePresentation.Unity
{
    public abstract class CardZonePresentationBehaviour : MonoBehaviour, ICardZonePresentation
    {
        [SerializeField] private MonoBehaviour[] customActions = Array.Empty<MonoBehaviour>();
        private readonly List<PresentationScope> actionScopes = new List<PresentationScope>();
        protected void CancelPresentationActions()
        { foreach (var scope in actionScopes.ToArray()) scope.Dispose(); actionScopes.Clear(); }
        public abstract bool IsPresenting { get; }
        protected virtual IEnumerable<ActionDescriptor> BuiltInCapabilities => CardZoneActions.BuiltIn;
        public abstract CardZoneDisplayData Current { get; }
        public IReadOnlyList<ActionDescriptor> Capabilities => BuiltInCapabilities.Concat(Custom.Select(x => x.Descriptor)).ToArray();
        private IEnumerable<ICardZonePresentationAction> Custom => customActions.Select(item =>
            item as ICardZonePresentationAction ?? throw new InvalidOperationException("Custom card actions must implement ICardZonePresentationAction."));
        public void ConfigureCustomActions(params MonoBehaviour[] actions)
        {
            var previous = customActions; customActions = actions?.ToArray() ?? Array.Empty<MonoBehaviour>();
            try { ValidateCapabilities(); } catch { customActions = previous; throw; }
        }
        private void ValidateCapabilities()
        {
            var descriptors = Capabilities;
            if (descriptors.Any(x => x == null || string.IsNullOrWhiteSpace(x.Key)) ||
                descriptors.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != descriptors.Count)
                throw new InvalidOperationException("Card action keys must be valid and unique.");
        }
        public IPresentationOperation Play(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (request == null || markers == null) throw new ArgumentNullException();
            if (!isActiveAndEnabled || !request.Scope.IsActive) return PresentationOperation.Failed("InactiveCardZone", name);
            PresentationScope owned = null;
            try
            {
                ValidateCapabilities();
                var descriptor = Capabilities.FirstOrDefault(x => x.Key == request.ActionKey);
                if (descriptor == null) return PresentationOperation.Failed("UnsupportedCardAction", request.ActionKey);
                owned = new PresentationScope(request.ExecutionId + "/zone");
                var lifetime = owned;
                actionScopes.Add(lifetime); lifetime.Own(() => actionScopes.Remove(lifetime));
                request.Scope.Own(lifetime);
                var nested = new CardZonePresentationRequest(request.ActionKey, request.ExecutionId, lifetime,
                    request.Before, request.After, request.ViewIds, request.Mode, request.ObserveProjection,
                    request.Parameters.ToDictionary(x => x.Key, x => x.Value));
                var outer = new PresentationOperation(() => lifetime.Dispose());
                lifetime.Own(() => { if (outer.Status == PresentationStatus.Pending) outer.Cancel(); });
                var sink = new ScopedMarkers(lifetime, descriptor, markers) { Operation = outer };
                var custom = Custom.FirstOrDefault(x => x.Descriptor.Key == request.ActionKey);
                var inner = custom != null ? custom.Play(nested, sink) : PlayBuiltIn(nested, sink);
                if (inner == null) throw new InvalidOperationException("Card actions must return an operation.");
                lifetime.Own(() => { if (inner.Status == PresentationStatus.Pending) inner.Cancel(); });
                void Observe(PresentationStatus status)
                {
                    if (status == PresentationStatus.Completed) outer.TryComplete();
                    else if (status == PresentationStatus.Cancelled) outer.Cancel();
                    else if (status == PresentationStatus.Failed)
                    { outer.TryFail(inner.Error); lifetime.Dispose(); }
                }
                inner.Finished += Observe; lifetime.Own(() => inner.Finished -= Observe);
                Observe(inner.Status); return outer;
            }
            catch (Exception error) { owned?.Dispose(); return PresentationOperation.Failed("CardActionFailed", error.Message); }
        }
        protected void AdvanceCustomPreview(float deltaTime)
        {
            foreach (var clock in Custom.OfType<ICardZonePresentationPreviewClock>()) clock.AdvancePreview(deltaTime);
        }
        protected abstract IPresentationOperation PlayBuiltIn(CardZonePresentationRequest request, IMarkerSink markers);
        public abstract void Synchronize(CardZoneDisplayData snapshot);
        private sealed class ScopedMarkers : IMarkerSink
        {
            private readonly PresentationScope scope;
            private readonly ActionDescriptor descriptor;
            private readonly IMarkerSink target;
            private readonly HashSet<string> emitted = new HashSet<string>();
            internal IPresentationOperation Operation;
            internal ScopedMarkers(PresentationScope scope, ActionDescriptor descriptor, IMarkerSink target)
            { this.scope = scope; this.descriptor = descriptor; this.target = target; }
            public bool Emit(string marker)
            {
                if (!scope.IsActive || Operation != null && Operation.Status != PresentationStatus.Pending) return false;
                if (!descriptor.Markers.Contains(marker)) throw new InvalidOperationException("Undeclared card marker: " + marker);
                return emitted.Add(marker) && target.Emit(marker);
            }
        }
    }
}
