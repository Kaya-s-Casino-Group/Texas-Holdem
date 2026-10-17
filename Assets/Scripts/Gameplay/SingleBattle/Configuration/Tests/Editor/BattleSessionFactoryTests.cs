using System;
using System.Linq;
using NUnit.Framework;
using Poker.Scoring;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Configuration.Tests
{
    public sealed class BattleSessionFactoryTests
    {
        private static readonly ParticipantId Player = new ParticipantId("custom/player"), Enemy = new ParticipantId("custom/enemy");
        private BattleRulesProfile rules;
        private PokerScoreProfile scoring;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<BattleRulesProfile>();
            scoring = ScriptableObject.CreateInstance<PokerScoreProfile>();
            ConfigureRules("factory.original", 9, PlayerBlockExpiry.Retain);
            ConfigureScoring("factory.score.original", 37);
        }

        [TearDown]
        public void TearDown()
        {
            if (rules != null) UnityEngine.Object.DestroyImmediate(rules);
            if (scoring != null) UnityEngine.Object.DestroyImmediate(scoring);
        }

        private static BattlePlayerSetup[] Players() => new[] {
            new BattlePlayerSetup(Player, new CombatantDefinition("hero", 80), initialHealth: 63,
                rules: new BattlePlayerRules(handSize: 6, discards: 2)) };
        private static BattleEnemySetup[] Enemies() => new[] {
            new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 10000), 7) };
        private static RandomState Seed() => HmacDeckShuffler.FromSeed(Enumerable.Range(0, 32).Select(value => (byte)value));
        private BattleSession Create() => BattleSessionFactory.Create(rules, Players(), Enemies(), "factory/seeded", Seed());

        [Test]
        public void CreateLeavesCardsUndealtUntilTheCallerStartsTheSession()
        {
            var session = Create();
            Assert.That(session.State.HasStarted, Is.False);
            Assert.That(session.State.Revision, Is.Zero);
            Assert.That(session.State.Duel.Actions, Is.Empty);
            Assert.That(session.State.Zone(CardZoneKinds.Deck).Cards.Count, Is.EqualTo(52));
            Assert.That(session.State.Table.Zones.Where(zone => zone.Kind != CardZoneKinds.Deck).SelectMany(zone => zone.Cards), Is.Empty);
            var start = session.Execute(session.State.Revision, Player, CardDuelAction.Start);
            Assert.That(start.Accepted, Is.True);
            Assert.That(start.Steps.Select(step => step.Action), Is.EqualTo(new[] { CardDuelAction.Start }));
            Assert.That(session.State.Zone(CardZoneKinds.Hand).Cards.Count, Is.EqualTo(6));
            Assert.That(session.State.Duel.Player(Player).Energy.Energy, Is.EqualTo(3));
        }

        [Test]
        public void FixedSeedMatchesDirectEngineConstructionThroughACompleteRound()
        {
            var players = Players(); var enemies = Enemies(); var random = Seed();
            var inputBytes = random.StateBytes.ToArray();
            var directEngine = new CardDuelEngine(rules.BuildRules(), scoring: rules.BuildScoring());
            var expected = new BattleSession(directEngine, directEngine.Create("factory/replay", random, players, enemies));
            var actual = BattleSessionFactory.Create(rules, players, enemies, "factory/replay", random);
            AssertEquivalent(expected.State, actual.State);
            foreach (var action in new[] { CardDuelAction.Start, CardDuelAction.Play, CardDuelAction.Discard, CardDuelAction.EndTurn })
            {
                var selected = action == CardDuelAction.Play || action == CardDuelAction.Discard
                    ? expected.State.Zone(CardZoneKinds.Hand).Cards.Take(1).ToArray() : Array.Empty<CardInstanceId>();
                var left = expected.Execute(expected.State.Revision, Player, action, selected);
                var right = actual.Execute(actual.State.Revision, Player, action, selected);
                Assert.That(left.Accepted, Is.True, action.ToString());
                Assert.That(right.Accepted, Is.True, action.ToString());
                CollectionAssert.AreEqual(left.Steps.Select(step => step.Action), right.Steps.Select(step => step.Action));
                AssertEquivalent(expected.State, actual.State);
            }
            Assert.That(actual.State.Duel.CompletedRounds, Is.EqualTo(1));
            CollectionAssert.AreEqual(inputBytes, random.StateBytes, "Creating and running a session must not mutate its supplied random snapshot.");
        }

        [Test]
        public void ExplicitRosterOrderDeckHealthAndPoliciesArePreserved()
        {
            var firstRules = new BattlePlayerRules(handSize: 3, initialEnergy: 5, discards: 1);
            var secondRules = new BattlePlayerRules(handSize: 4, initialEnergy: 2, discards: 4);
            var firstDeck = StandardDeck.Cards().Take(7).Select((face, index) =>
                new DeckEntry("first/origin/" + index, face.Id, CardEffects.Enhanced, isDisabled: index == 0)).ToArray();
            var secondDeck = StandardDeck.Cards().Skip(13).Take(9).Select((face, index) =>
                new DeckEntry("second/origin/" + index, face.Id, CardEffects.Laminated)).ToArray();
            var players = new[] {
                new BattlePlayerSetup(new ParticipantId("z-player"), new CombatantDefinition("mage", 90, 4), firstDeck, 39, firstRules),
                new BattlePlayerSetup(new ParticipantId("a-player"), new CombatantDefinition("guard", 120, 8), secondDeck, 71, secondRules) };
            var enemies = new[] {
                new BattleEnemySetup(new ParticipantId("z-enemy"), new CombatantDefinition("ogre", 150), 13),
                new BattleEnemySetup(new ParticipantId("a-enemy"), new CombatantDefinition("archer", 45), 3) };
            var session = BattleSessionFactory.Create(rules, players, enemies, "campaign/node-7", Seed());
            var state = session.State;
            Assert.That(state.Table.MatchId.Value, Is.EqualTo("campaign/node-7"));
            CollectionAssert.AreEqual(players.Select(player => player.Id), state.Duel.Players.Select(player => player.Id));
            CollectionAssert.AreEqual(enemies, state.Duel.Enemies);
            Assert.That(state.Table.Cards.Count, Is.EqualTo(16));
            foreach (var player in players)
            {
                var unit = state.Duel.Combat.Get(player.Id);
                Assert.That(unit.DefinitionId, Is.EqualTo(player.Definition.Id));
                Assert.That(unit.Health, Is.EqualTo(player.InitialHealth));
                Assert.That(unit.Block, Is.EqualTo(player.Definition.InitialBlock));
                Assert.That(state.Duel.Player(player.Id).Rules, Is.SameAs(player.Rules));
                Assert.That(state.Duel.Player(player.Id).DamageRules, Is.SameAs(player.DamageRules));
                Assert.That(state.Duel.Player(player.Id).TargetRules, Is.SameAs(player.TargetRules));
                Assert.That(state.Duel.Player(player.Id).Energy.Energy, Is.EqualTo(player.Rules.InitialEnergy));
                Assert.That(state.DiscardsFor(player.Id), Is.EqualTo(player.Rules.Discards));
                var owned = state.Zone(CardZoneKinds.Deck, player.Id).Cards.Select(state.Table.GetCard).ToArray();
                CollectionAssert.AreEquivalent(player.Deck.Select(entry => entry.Id), owned.Select(card => card.OriginId));
                foreach (var entry in player.Deck)
                {
                    var card = owned.Single(candidate => candidate.OriginId == entry.Id);
                    Assert.That(card.DefinitionId, Is.EqualTo(entry.DefinitionId));
                    Assert.That(card.Effects, Is.EqualTo(entry.Effects));
                    Assert.That(card.IsDisabled, Is.EqualTo(entry.IsDisabled));
                }
            }
            state.Table.Validate();
        }

        [Test]
        public void ProfileChangesOnlyAffectSessionsCreatedAfterTheChange()
        {
            var original = Create();
            ConfigureRules("factory.changed", 1, PlayerBlockExpiry.AfterEnemyPhase);
            ConfigureScoring("factory.score.changed", 91);
            var changed = Create();
            Assert.That(original.Engine.Duel.Definition.Version, Is.EqualTo("factory.original"));
            Assert.That(original.Engine.Duel.Definition.RoundLimit, Is.EqualTo(9));
            Assert.That(original.Engine.Duel.Definition.PlayerBlockExpiry, Is.EqualTo(PlayerBlockExpiry.Retain));
            Assert.That(original.Engine.Scoring.Version, Is.EqualTo("factory.score.original"));
            Assert.That(original.Engine.Scoring.Rule(HandCategory.HighCard).BasePoints, Is.EqualTo(37));
            Assert.That(changed.Engine.Duel.Definition.Version, Is.EqualTo("factory.changed"));
            Assert.That(changed.Engine.Duel.Definition.RoundLimit, Is.EqualTo(1));
            Assert.That(changed.Engine.Duel.Definition.PlayerBlockExpiry, Is.EqualTo(PlayerBlockExpiry.AfterEnemyPhase));
            Assert.That(changed.Engine.Scoring.Version, Is.EqualTo("factory.score.changed"));
            Assert.That(changed.Engine.Scoring.Rule(HandCategory.HighCard).BasePoints, Is.EqualTo(91));
            UnityEngine.Object.DestroyImmediate(rules);
            UnityEngine.Object.DestroyImmediate(scoring);
            Assert.That(original.Execute(original.State.Revision, Player, CardDuelAction.Start).Accepted, Is.True);
        }

        [Test]
        public void OmittedIdentityAndRandomnessCreateIndependentBattles()
        {
            var first = BattleSessionFactory.Create(rules, Players(), Enemies());
            var second = BattleSessionFactory.Create(rules, Players(), Enemies());
            Assert.That(first.State.Table.MatchId.Value, Does.StartWith("battle/"));
            Assert.That(Guid.TryParseExact(first.State.Table.MatchId.Value.Substring("battle/".Length), "N", out _), Is.True);
            Assert.That(first.State.Table.MatchId, Is.Not.EqualTo(second.State.Table.MatchId));
            Assert.That(first.State.Random.Algorithm, Is.EqualTo(HmacDeckShuffler.Algorithm));
            Assert.That(first.State.Random.StateBytes.Take(32), Is.Not.EqualTo(second.State.Random.StateBytes.Take(32)));
            Assert.That(first.State.HasStarted || second.State.HasStarted, Is.False);
        }

        [TestCase("scene-3d")]
        [TestCase("minimal-2d")]
        [TestCase("campaign/node")]
        public void BattleIdKeepsTheCallerPrefix(string prefix)
        {
            var id = BattleSessionFactory.CreateBattleId(prefix);
            Assert.That(id, Does.StartWith(prefix + "/"));
            Assert.That(Guid.TryParseExact(id.Substring(prefix.Length + 1), "N", out _), Is.True);
        }

        [Test]
        public void MissingOrDestroyedRulesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => BattleSessionFactory.Create(null, Players(), Enemies()));
            UnityEngine.Object.DestroyImmediate(rules);
            Assert.Throws<ArgumentNullException>(() => BattleSessionFactory.Create(rules, Players(), Enemies()));
        }

        [TestCase("null-players")]
        [TestCase("null-enemies")]
        [TestCase("empty-players")]
        [TestCase("empty-enemies")]
        [TestCase("null-member")]
        [TestCase("duplicate-player")]
        [TestCase("duplicate-teams")]
        [TestCase("too-many-units")]
        public void InvalidRostersAreRejectedByTheRulesEngine(string invalid)
        {
            var players = Players(); var enemies = Enemies();
            switch (invalid)
            {
                case "null-players": players = null; break;
                case "null-enemies": enemies = null; break;
                case "empty-players": players = Array.Empty<BattlePlayerSetup>(); break;
                case "empty-enemies": enemies = Array.Empty<BattleEnemySetup>(); break;
                case "null-member": enemies = new BattleEnemySetup[] { null }; break;
                case "duplicate-player": players = new[] { players[0], players[0] }; break;
                case "duplicate-teams": enemies = new[] { new BattleEnemySetup(Player, new CombatantDefinition("enemy", 40), 1) }; break;
                case "too-many-units": enemies = Enumerable.Range(0, 64).Select(index =>
                    new BattleEnemySetup(new ParticipantId("enemy/" + index), new CombatantDefinition("enemy", 40), 1)).ToArray(); break;
            }
            Assert.That(() => BattleSessionFactory.Create(rules, players, enemies, "invalid", Seed()),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void AnUnplayableCustomDeckIsRejectedInsteadOfReplacedWithAStandardDeck()
        {
            var deck = StandardDeck.Cards().Take(2).Select((face, index) => new DeckEntry("short/" + index, face.Id));
            var player = new BattlePlayerSetup(Player, new CombatantDefinition("hero", 40), deck,
                rules: new BattlePlayerRules(handSize: 3));
            Assert.Throws<ArgumentException>(() => BattleSessionFactory.Create(rules, new[] { player }, Enemies(), "invalid/deck", Seed()));
        }

        private void ConfigureRules(string version, int rounds, PlayerBlockExpiry expiry)
        {
            using (var serialized = new SerializedObject(rules))
            {
                serialized.FindProperty("version").stringValue = version;
                serialized.FindProperty("roundLimit").intValue = rounds;
                serialized.FindProperty("playerBlockExpiry").intValue = (int)expiry;
                serialized.FindProperty("scoring").objectReferenceValue = scoring;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private void ConfigureScoring(string version, long highCardPoints)
        {
            using (var serialized = new SerializedObject(scoring))
            {
                serialized.FindProperty("version").stringValue = version;
                var hands = serialized.FindProperty("hands");
                for (int index = 0; index < hands.arraySize; index++)
                {
                    var hand = hands.GetArrayElementAtIndex(index);
                    if (hand.FindPropertyRelative("category").intValue == (int)HandCategory.HighCard)
                        hand.FindPropertyRelative("chips").longValue = highCardPoints;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssertEquivalent(CardDuelState expected, CardDuelState actual)
        {
            Assert.That(actual.Table.MatchId, Is.EqualTo(expected.Table.MatchId));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.HasStarted, Is.EqualTo(expected.HasStarted));
            Assert.That(actual.Duel.Phase, Is.EqualTo(expected.Duel.Phase));
            Assert.That(actual.Duel.CompletedRounds, Is.EqualTo(expected.Duel.CompletedRounds));
            Assert.That(actual.Duel.TotalScore, Is.EqualTo(expected.Duel.TotalScore));
            CollectionAssert.AreEqual(expected.Random.StateBytes, actual.Random.StateBytes);
            foreach (var zone in expected.Table.Zones)
                CollectionAssert.AreEqual(zone.Cards, actual.Table.GetZone(zone.Id).Cards, zone.Id.Value);
            foreach (var unit in expected.Duel.Combat.Combatants)
            {
                Assert.That(actual.Duel.Combat.Get(unit.Id).Health, Is.EqualTo(unit.Health));
                Assert.That(actual.Duel.Combat.Get(unit.Id).Block, Is.EqualTo(unit.Block));
            }
            foreach (var player in expected.Duel.Players)
            {
                Assert.That(actual.Duel.Player(player.Id).Energy.Energy, Is.EqualTo(player.Energy.Energy));
                Assert.That(actual.Duel.Player(player.Id).HasEnded, Is.EqualTo(player.HasEnded));
                Assert.That(actual.DiscardsFor(player.Id), Is.EqualTo(expected.DiscardsFor(player.Id)));
            }
        }
    }
}
