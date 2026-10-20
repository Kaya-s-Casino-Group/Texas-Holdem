using System.Collections.Generic;
using PokerEngine;
using SharedText = Poker.BattlePresentation.BattlePresentationRuleText;

namespace Poker.SingleBattle
{
    /// <summary>Compatibility surface; all renderers use the same rule facts and localization.</summary>
    public static class BattleHudRuleText
    {
        public static string Participant(TeamBattleState state, ParticipantId id) => SharedText.Participant(state, id);
        public static string StatusName(StatusDefinitionId id, CombatStatusCatalog catalog) => SharedText.StatusName(id, catalog);
        public static string Duration(StatusDuration duration) => SharedText.Duration(duration);
        public static string Status(StatusInstance status, CombatStatusCatalog catalog) => SharedText.Status(status, catalog);
        public static string Snapshot(CardDuelState state, ParticipantId playerId, CombatStatusCatalog catalog) => SharedText.Snapshot(state, playerId, catalog);
        public static IReadOnlyList<string> Events(IEnumerable<CombatRuleEvent> events, TeamBattleState state,
            CombatStatusCatalog catalog, bool compact = false) => SharedText.Events(events, state, catalog, compact);
        public static string Transition(CardDuelTransition result, TeamBattleState names, CombatStatusCatalog catalog) => SharedText.Transition(result, names, catalog);
        public static string Error(CardDuelError error) => SharedText.Error(error);
    }
}
