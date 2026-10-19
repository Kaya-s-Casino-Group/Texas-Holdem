using System;
using System.Linq;
using System.Collections;
using UnityEngine;

namespace BattlePresentation.Unity
{
    [Serializable]
    public sealed class CardZoneActionParameter { public string key; public string value; }

    /// <summary>The same timeline entry dispatches to Canvas, 2D, 3D or custom card frontends.</summary>
    [AddComponentMenu("Battle Presentation/Card Zone Timeline Action")]
    public sealed class CardZoneTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private MonoBehaviour source;
        [SerializeField] private string actionKey = CardZoneActions.Prepare;
        [SerializeField] private string zoneId;
        [SerializeField] private bool observeProjection;
        [SerializeField] private string[] markers = { "Prepared" };
        [SerializeField] private string[] channels = { "cards.preparation" };
        [SerializeField] private CardZoneActionParameter[] parameters = Array.Empty<CardZoneActionParameter>();
        public override ActionDescriptor Descriptor => Resolve(source)?.Capabilities.FirstOrDefault(x => x.Key == actionKey)
            ?? new ActionDescriptor(actionKey, markers, channels);
        public void Configure(MonoBehaviour frontend, string key, string zone = null, bool observe = false)
        {
            if (!(frontend is ICardZonePresentation view)) throw new ArgumentException("Bind ICardZonePresentation.");
            var descriptor = view.Capabilities.Single(x => x.Key == key);
            source = frontend; actionKey = key; zoneId = zone; observeProjection = observe;
            markers = descriptor.Markers.ToArray(); channels = descriptor.Channels.ToArray();
        }
        public static ICardZonePresentation Resolve(object target)
        {
            if (target is UnityEngine.Object unityObject && unityObject == null) return null;
            if (target is ICardZonePresentation view) return view;
            var go = target as GameObject ?? (target as Component)?.gameObject;
            return go == null ? null : go.GetComponentsInParent<MonoBehaviour>(true).OfType<ICardZonePresentation>().FirstOrDefault();
        }
        public override IPresentationOperation Play(ActionContext context, IMarkerSink sink)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            var view = Resolve(source) ?? Resolve(context.Target) ?? Resolve(context.Targets?.Resolve("cards", context));
            if (view == null) return PresentationOperation.Failed("MissingCardZone", context.TargetKey ?? "cards");
            try
            {
                var capability = view.Capabilities.SingleOrDefault(x => x.Key == actionKey);
                if (capability == null || !Descriptor.Markers.SequenceEqual(capability.Markers) || !Descriptor.Channels.SequenceEqual(capability.Channels))
                    return PresentationOperation.Failed("CardCapabilityMismatch", actionKey);
                string id = !string.IsNullOrEmpty(zoneId) ? zoneId : view.Current?.Id;
                var before = context.Before.Zones.FirstOrDefault(x => id != null ? x.Id == id : x.Kind == "hand" && x.OwnerId == context.Before.ObserverId);
                var after = context.After.Zones.FirstOrDefault(x => x.Id == (before?.Id ?? id));
                if (before == null || after == null) return PresentationOperation.Failed("MissingCardZoneSnapshot", id ?? "hand");
                var visible = before.Cards.Concat(after.Cards).ToArray();
                var ids = context.Data.CardIds.Where(x => visible.Any(c => c.CardId == x || c.ViewId == x));
                var values = context.Data.Values.ToDictionary(x => x.Key, x => x.Value);
                foreach (var parameter in parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.key)) throw new InvalidOperationException("Card parameter keys cannot be empty.");
                    values[parameter.key] = parameter.value;
                }
                var operation = view.Play(new CardZonePresentationRequest(actionKey, context.ActionInstanceId, context.Scope,
                    before, after, ids, observeProjection: observeProjection,
                    parameters: values), sink);
                if (operation.Status == PresentationStatus.Pending && view is ICardZonePresentationPump pump &&
                    (Application.isPlaying || StartPresentationRoutine != null))
                    context.Scope.Own(StartRoutine(Pump(pump, operation)));
                return operation;
            }
            catch (Exception error) { return PresentationOperation.Failed("CardTimelineFailed", error.Message); }
        }
        private static IEnumerator Pump(ICardZonePresentationPump pump, IPresentationOperation operation)
        { while (operation.Status == PresentationStatus.Pending) { yield return null; pump.Tick(); } }
        public override void Synchronize(ActionSyncContext context)
        {
            var view = Resolve(source) ?? Resolve(context.Targets?.Resolve("cards", null));
            var id = string.IsNullOrEmpty(zoneId) ? view?.Current?.Id : zoneId;
            var snapshot = context.Snapshot.Zones.FirstOrDefault(x => id != null ? x.Id == id : x.Kind == "hand" && x.OwnerId == context.Snapshot.ObserverId);
            if (view != null && snapshot != null) view.Synchronize(snapshot);
        }
    }
}
