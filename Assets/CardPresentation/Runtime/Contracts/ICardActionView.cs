using System;
using System.Collections.Generic;

namespace CardPresentation
{
    public enum CardViewActionKind { Play, Discard }

    /// <summary>Visuals for committed card actions. No Actor references or battle commands.
    /// The host waits for completion before handing the resolved action to Actor presentation.</summary>
    public interface ICardActionView
    {
        CardActionViewOperation PresentActionCommitted(CardViewActionKind action, CardZoneViewData data,
            IEnumerable<string> cardIds, Action commitData, bool animate = true);
    }
}
