using CardPresentation;
using PokerEngine;

namespace CardInteraction
{
    /// <summary>Read-only rule values supplied by gameplay, independent of the card-zone presentation.</summary>
    public interface ICardScoreSource
    {
        bool TryGetCardBasePoints(CardInstanceId card, out long points);
    }

    /// <summary>Gameplay can accept a play without a destination view or a scene transfer route.</summary>
    public interface ICardDirectPlayCommands : ICardMatchCommands
    {
        int Play(CardZonePlayHost hand, bool animate);
    }

    /// <summary>Gameplay authority for a table that is only a local presentation mirror.</summary>
    public interface ICardMatchCommands
    {
        bool CanInteract { get; }
        bool CanPlay(CardZonePlayHost hand);
        bool CanDiscard(CardZonePlayHost hand);
        int Play(CardZonePlayHost hand, CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate);
        int Discard(CardZonePlayHost hand, bool animate);
    }
}
