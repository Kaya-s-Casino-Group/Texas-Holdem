using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePresentation
{
    public static class CardZoneActions
    {
        public const string Draw = "cards.draw", Arrange = "cards.arrange", Prepare = "cards.prepare",
            Play = "cards.play", Discard = "cards.discard", Selection = "cards.selection",
            State = "cards.state", Settle = "cards.settle";
        public static IReadOnlyList<ActionDescriptor> BuiltIn { get; } = Array.AsReadOnly(new[] {
            new ActionDescriptor(Draw, new[] { "AllArrived" }, new[] { "cards.layout" }),
            new ActionDescriptor(Arrange, new[] { "Settled" }, new[] { "cards.layout" }),
            new ActionDescriptor(Prepare, new[] { "Prepared" }, new[] { "cards.preparation" }),
            new ActionDescriptor(Play, new[] { "Released", "AllArrived" }, new[] { "cards.layout" }),
            new ActionDescriptor(Discard, new[] { "Released", "AllArrived" }, new[] { "cards.layout" }),
            new ActionDescriptor(Selection, new[] { "Settled" }, new[] { "cards.layout" }),
            new ActionDescriptor(State, new[] { "Settled" }, new[] { "cards.layout" }),
            new ActionDescriptor(Settle, new[] { "Settled" })
        });
    }

    /// <summary>Only display data. No rule commands, Unity objects, prefab or rendering assumptions.</summary>
    public sealed class CardZonePresentationRequest
    {
        public string ActionKey { get; }
        public string ExecutionId { get; }
        public PresentationScope Scope { get; }
        public CardZoneDisplayData Before { get; }
        public CardZoneDisplayData After { get; }
        public IReadOnlyList<string> ViewIds { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public PlaybackMode Mode { get; }
        // Existing frontends publish snapshots at the director's checkpoint. Observe them without publishing twice.
        public bool ObserveProjection { get; }
        public CardZonePresentationRequest(string actionKey, string executionId, PresentationScope scope,
            CardZoneDisplayData before, CardZoneDisplayData after, IEnumerable<string> cardIds = null,
            PlaybackMode mode = PlaybackMode.Animated, bool observeProjection = false,
            IDictionary<string, string> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(actionKey) || string.IsNullOrWhiteSpace(executionId)) throw new ArgumentException("An action and execution identity are required.");
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
            if (before.Id != after.Id) throw new ArgumentException("A request presents one zone.");
            ActionKey = actionKey; ExecutionId = executionId; Mode = mode; ObserveProjection = observeProjection;
            var visible = before.Cards.Concat(after.Cards).ToArray();
            var ids = (cardIds ?? Enumerable.Empty<string>()).Select(id =>
                visible.FirstOrDefault(card => card.ViewId == id || card.CardId == id)?.ViewId ??
                throw new ArgumentException("Card is not in this zone's before/after snapshot: " + id)).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new ArgumentException("Duplicate card identity.");
            ViewIds = Frozen.List(ids); Parameters = Frozen.Map(parameters);
        }
    }

    public interface ICardZonePresentationPump { void Tick(); }

    public interface ICardZonePresentation
    {
        bool IsPresenting { get; }
        CardZoneDisplayData Current { get; }
        IReadOnlyList<ActionDescriptor> Capabilities { get; }
        IPresentationOperation Play(CardZonePresentationRequest request, IMarkerSink markers);
        // Cancels owned animation and restores the latest supplied display snapshot. Never rewinds rules.
        void Synchronize(CardZoneDisplayData snapshot);
    }

    /// <summary>Optional custom behaviour, registered on a frontend. Declare actual markers and owned channels.</summary>
    public interface ICardZonePresentationAction
    {
        ActionDescriptor Descriptor { get; }
        IPresentationOperation Play(CardZonePresentationRequest request, IMarkerSink markers);
    }
}
