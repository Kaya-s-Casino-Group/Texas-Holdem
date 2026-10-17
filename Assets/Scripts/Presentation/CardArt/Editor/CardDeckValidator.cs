using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CardArt.Editor
{
    public sealed class CardDeckSlot
    {
        public string id;
        public string label;
        public string group;
        public readonly List<CardAtlasEntry> entries = new List<CardAtlasEntry>();
    }

    public sealed class CardDeckReport
    {
        public readonly List<CardDeckSlot> slots = new List<CardDeckSlot>();
        public readonly List<CardAtlasEntry> extras = new List<CardAtlasEntry>();
        public int MissingCount => slots.Count(slot => slot.entries.Count == 0);
        public int DuplicateCount => slots.Count(slot => slot.entries.Count > 1);
        public bool HasAllCards => MissingCount == 0;
        public bool IsExactDeck => HasAllCards && DuplicateCount == 0 && extras.Count == 0;
    }

    /// <summary>Identity comes from the original filename, independently of atlas ID or replacement art.</summary>
    public static class CardDeckValidator
    {
        private static readonly string[] Suits = { "spades", "hearts", "clubs", "diamonds" };
        private static readonly string[] SuitLabels = { "黑桃 ♠", "红桃 ♥", "梅花 ♣", "方块 ♦" };
        private static readonly string[] Ranks = { "a", "02", "03", "04", "05", "06", "07", "08", "09", "10", "j", "q", "k" };
        private const string SuitPattern = "spades?|hearts?|clubs?|diamonds?|黑桃|红桃|梅花|方块";
        private const string RankPattern = "a|0?[1-9]|10|j|11|q|12|k|13";
        private static readonly Regex SuitFirst = new Regex("^(?<suit>" + SuitPattern + ")[_\\-\\s]*(?<rank>" + RankPattern + ")$", RegexOptions.CultureInvariant);
        private static readonly Regex RankFirst = new Regex("^(?<rank>" + RankPattern + ")[_\\-\\s]*(?<suit>" + SuitPattern + ")$", RegexOptions.CultureInvariant);
        private static readonly Regex VariantSuffix = new Regex(@"(?:[_\-\s]+(?:v\d+|copy\d*|alt\d*|variant\d*|副本\d*)|\s*\(\d+\))$", RegexOptions.CultureInvariant);

        public static bool TryParse(string sourcePath, out string id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(sourcePath)) return false;
            var name = Path.GetFileNameWithoutExtension(sourcePath.Replace('\\', '/')).Trim().ToLowerInvariant();
            // Common alternative/copy suffixes still represent the same playing card.
            while (VariantSuffix.IsMatch(name)) name = VariantSuffix.Replace(name, "");
            var compact = Regex.Replace(name, @"[_\-\s]", "");
            switch (compact)
            {
                case "cardback": case "back": case "卡背": id = "card_back"; return true;
                case "jokerbig": case "bigjoker": case "大王": id = "joker_big"; return true;
                case "jokersmall": case "smalljoker": case "小王": id = "joker_small"; return true;
            }
            var match = SuitFirst.Match(name);
            if (!match.Success) match = RankFirst.Match(name);
            if (!match.Success) return false;
            var suit = match.Groups["suit"].Value;
            switch (suit)
            {
                case "spade": case "黑桃": suit = "spades"; break;
                case "heart": case "红桃": suit = "hearts"; break;
                case "club": case "梅花": suit = "clubs"; break;
                case "diamond": case "方块": suit = "diamonds"; break;
            }
            var rank = match.Groups["rank"].Value;
            if (int.TryParse(rank, out var number))
            {
                switch (number)
                {
                    case 1: rank = "a"; break;
                    case 11: rank = "j"; break;
                    case 12: rank = "q"; break;
                    case 13: rank = "k"; break;
                    default: rank = number.ToString("00"); break;
                }
            }
            id = suit + "_" + rank;
            return true;
        }

        public static List<CardAtlasEntry> OrderForAtlas(IEnumerable<CardAtlasEntry> entries)
        {
            var report = Analyze(entries);
            return report.slots.Single(slot => slot.id == "card_back").entries
                .Concat(report.slots.Where(slot => slot.id != "card_back").SelectMany(slot => slot.entries))
                .Concat(report.extras).ToList();
        }

        public static CardDeckReport Analyze(IEnumerable<CardAtlasEntry> entries, bool includedOnly = false)
        {
            var report = new CardDeckReport();
            for (var suit = 0; suit < Suits.Length; suit++)
                foreach (var rank in Ranks)
                    report.slots.Add(new CardDeckSlot
                    {
                        id = Suits[suit] + "_" + rank,
                        label = SuitLabels[suit] + " " + (int.TryParse(rank, out var number) ? number.ToString() : rank.ToUpperInvariant()),
                        group = SuitLabels[suit]
                    });
            report.slots.Add(new CardDeckSlot { id = "joker_big", label = "大王", group = "大小王 / 卡背" });
            report.slots.Add(new CardDeckSlot { id = "joker_small", label = "小王", group = "大小王 / 卡背" });
            report.slots.Add(new CardDeckSlot { id = "card_back", label = "卡背", group = "大小王 / 卡背" });
            var slots = report.slots.ToDictionary(slot => slot.id, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (includedOnly && !entry.included) continue;
                if (TryParse(entry.sourcePath, out var id)) slots[id].entries.Add(entry);
                else report.extras.Add(entry);
            }
            return report;
        }
    }
}
