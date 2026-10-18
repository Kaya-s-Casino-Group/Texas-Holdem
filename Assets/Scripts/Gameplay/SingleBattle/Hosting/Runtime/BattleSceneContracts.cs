using PokerEngine;

namespace Poker.BattleHosting
{
    /// <summary>Creates a new, undealt session. It does not start rules or presentation.</summary>
    public interface IBattleSessionSource
    {
        BattleSession CreateSession();
    }

    /// <summary>Per-battle boundary for input, projection and scene-specific bindings.</summary>
    public interface IBattleSceneAdapter : IBattleSessionPresentation
    {
        void BindHost(BattleSceneHost host);
        void ValidateSession(BattleSession session);
        void SessionAttached();
    }
}
