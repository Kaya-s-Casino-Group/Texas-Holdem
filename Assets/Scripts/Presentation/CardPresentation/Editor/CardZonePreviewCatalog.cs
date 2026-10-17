#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PokerEngine;

namespace CardPresentation.Editor
{
    /// <summary>Editor-only mapping between Core's standard definitions and the sample artwork.</summary>
    public static class CardZonePreviewCatalog
    {
        private const string ArtOnlyPrefix = "editor-art:";
        private static readonly Dictionary<string, CardDefinition> definitions = StandardDeck.Cards()
            .ToDictionary(ArtName, StringComparer.Ordinal);
        private static readonly Dictionary<CardDefinitionId, string> artwork = definitions.ToDictionary(pair => pair.Value.Id, pair => pair.Key);

        private static string ArtName(CardDefinition card)
        {
            string rank = card.Rank == Rank.Ace ? "a" : card.Rank == Rank.King ? "k" :
                card.Rank == Rank.Queen ? "q" : card.Rank == Rank.Jack ? "j" : ((int)card.Rank).ToString("00", CultureInfo.InvariantCulture);
            return card.Suit.ToString().ToLowerInvariant() + "_" + rank;
        }

        public static CardDefinitionId DefinitionFor(string artId)
        {
            Guard.Text(artId, nameof(artId));
            return definitions.TryGetValue(artId, out var definition) ? definition.Id : new CardDefinitionId(ArtOnlyPrefix + artId);
        }

        public static string ArtFor(CardDefinitionId id)
        {
            if (!id.IsValid) throw new ArgumentException("Invalid preview definition.", nameof(id));
            if (artwork.TryGetValue(id, out var artId)) return artId;
            if (id.Value.StartsWith(ArtOnlyPrefix, StringComparison.Ordinal))
                return Guard.Text(id.Value.Substring(ArtOnlyPrefix.Length), nameof(id));
            throw new ArgumentException("预览目录中不存在该卡牌定义。", nameof(id));
        }

        /// <summary>Returns Core rank/suit data, or null for explicitly art-only samples such as jokers.</summary>
        public static CardDefinition StandardDefinitionFor(CardDefinitionId id)
        {
            var artId = ArtFor(id);
            return definitions.TryGetValue(artId, out var definition) ? definition : null;
        }

        public static string LabelFor(string artId)
        {
            if (!definitions.TryGetValue(artId, out var card)) return artId + "（美术样例）";
            string suit = card.Suit == Suit.Clubs ? "梅花" : card.Suit == Suit.Diamonds ? "方块" : card.Suit == Suit.Hearts ? "红桃" : "黑桃";
            string rank = card.Rank == Rank.Ace ? "A" : card.Rank == Rank.King ? "K" : card.Rank == Rank.Queen ? "Q" :
                card.Rank == Rank.Jack ? "J" : ((int)card.Rank).ToString(CultureInfo.InvariantCulture);
            return suit + " " + rank;
        }
    }
}
#endif
