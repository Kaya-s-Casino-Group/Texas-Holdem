using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PokerEngine;

namespace Poker.SingleBattle
{
    /// <summary>A detached editor draft. Deck rows are shown in draw order, unlike engine storage.</summary>
    public sealed class BattleAdminDraft
    {
        public sealed class Vitals
        {
            public ParticipantId Id;
            public string Health, Maximum, Block, Gold;
        }
        public sealed class Card
        {
            public string InstanceId, DefinitionId;
            public CardEffects Effects;
            public bool Disabled;
            public Card Copy() => new Card { DefinitionId = DefinitionId, Effects = Effects, Disabled = Disabled };
        }
        public sealed class Relic
        {
            public string InstanceId, DefinitionId, Level, Charges, Growth, Cooldown, Uses;
        }
        public sealed class Player
        {
            public ParticipantId Id;
            public readonly Dictionary<string, List<Card>> Zones = new Dictionary<string, List<Card>>();
            public readonly List<Relic> Relics = new List<Relic>();
        }
        public static readonly string[] ZoneKinds = { CardZoneKinds.Hand, CardZoneKinds.Deck, CardZoneKinds.Discard };
        public BattleSession Session { get; }
        public long Revision { get; }
        public readonly List<Vitals> Units = new List<Vitals>();
        public readonly List<Player> Players = new List<Player>();
        public bool IsCurrent(BattleSession session) => ReferenceEquals(Session, session) && session.State.Revision == Revision;

        public BattleAdminDraft(BattleSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            var state = session.State; Revision = state.Revision;
            foreach (var unit in state.Duel.Combat.Combatants)
                Units.Add(new Vitals { Id = unit.Id, Health = N(unit.Health), Maximum = N(unit.MaxHealth), Block = N(unit.Block),
                    Gold = state.Duel.Players.Any(p => p.Id == unit.Id) ? N(state.GoldFor(unit.Id)) : null });
            foreach (var owner in state.Duel.Players)
            {
                var player = new Player { Id = owner.Id }; Players.Add(player);
                foreach (var kind in ZoneKinds)
                {
                    IEnumerable<CardInstanceId> ids = state.Zone(kind, owner.Id).Cards;
                    if (kind == CardZoneKinds.Deck) ids = ids.Reverse();
                    player.Zones.Add(kind, ids.Select(id => {
                        var card = state.Table.GetCard(id);
                        return new Card { InstanceId = id.Value, DefinitionId = card.DefinitionId.Value, Effects = card.Effects, Disabled = card.IsDisabled };
                    }).ToList());
                }
                player.Relics.AddRange(owner.Relics.Relics.Select(r => new Relic { InstanceId = r.InstanceId, DefinitionId = r.DefinitionId,
                    Level = N(r.Level), Charges = r.RemainingCharges.HasValue ? N(r.RemainingCharges.Value) : "",
                    Growth = N(r.Growth), Cooldown = N(r.CooldownRounds), Uses = N(r.UsesThisRound) }));
            }
        }

        public BattleAdminPatch Build()
        {
            var patch = new BattleAdminPatch(
                vitals: Units.Select(u => new BattleAdminVitals(u.Id, Parse(u.Health, "生命"), Parse(u.Maximum, "生命上限"), Parse(u.Block, "护盾"))),
                gold: Units.Where(u => u.Gold != null).Select(u => new BattleAdminGold(u.Id, Parse(u.Gold, "金币"))),
                zones: Players.SelectMany(p => ZoneKinds.Select(kind => new BattleAdminZone(p.Id, kind,
                    (kind == CardZoneKinds.Deck ? p.Zones[kind].AsEnumerable().Reverse() : p.Zones[kind])
                    .Select(c => new BattleAdminCard(c.InstanceId, c.DefinitionId, c.Effects, c.Disabled))))),
                relics: Players.Select(p => new BattleAdminRelics(p.Id, p.Relics.Select(r =>
                    new BattleAdminRelic(r.DefinitionId, r.InstanceId, Small(r.Level, "遗物等级"),
                        string.IsNullOrWhiteSpace(r.Charges) ? (int?)null : Small(r.Charges, "剩余次数"),
                        Parse(r.Growth, "成长值"), Small(r.Cooldown, "冷却"), Small(r.Uses, "本回合使用次数"))))),
                reason: "Unity 运行内管理员修改");
            patch.Validate();
            return patch;
        }
        private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static long Parse(string text, string label)
        {
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException(label + "必须是 0 至 " + long.MaxValue + " 的整数。");
            return value;
        }
        private static int Small(string text, string label)
        {
            long value = Parse(text, label);
            if (value > int.MaxValue) throw new ArgumentException(label + "超出整数范围。");
            return (int)value;
        }
    }
}
