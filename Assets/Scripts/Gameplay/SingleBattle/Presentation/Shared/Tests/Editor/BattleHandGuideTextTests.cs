using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    public sealed class BattleHandGuideTextTests
    {
        static readonly ParticipantId Player = new ParticipantId("player"), Enemy = new ParticipantId("enemy");
        static BattleSession Setup(bool missing, string relic = null, Func<string, string> configure = null)
        {
            string xml = File.ReadAllText("External/BattleToolkit/Engine/Content/SingleChapterBattle.xml");
            var content = BattleContentCompiler.CompileXml(configure == null ? xml : configure(xml));
            var session = BattleContentFactory.Create(content.Content, new BattleSetupRequest("single-chapter-battle-1", "guide-copy", HmacDeckShuffler.FromSeed(new byte[32]))).Session;
            Assert.IsTrue(session.Execute(0, Player, CardDuelAction.Start).Accepted);
            var cards = new[] { 2, 4, 7, 9, 13 }.Select(rank => new BattleAdminCard(definitionId: "standard:1:" + rank, effects: rank == 13 ? CardEffects.Enhanced : CardEffects.None)).ToArray();
            var patch = session.ApplyAdmin(session.State.Revision, new BattleAdminPatch(
                zones: new[] { new BattleAdminZone(Player, CardZoneKinds.Hand, cards.Take(missing ? 4 : 5)),
                    new BattleAdminZone(Player, CardZoneKinds.Deck, Array.Empty<BattleAdminCard>()),
                    new BattleAdminZone(Player, CardZoneKinds.Discard, missing ? cards.Skip(4) : Array.Empty<BattleAdminCard>()) },
                vitals: new[] { new BattleAdminVitals(Player, 800, 800, 0), new BattleAdminVitals(Enemy, 100000, 100000, 0) },
                relics: new[] { new BattleAdminRelics(Player, relic == null ? Array.Empty<BattleAdminRelic>() : new[] { new BattleAdminRelic(relic) }) }));
            Assert.IsTrue(patch.Accepted, patch.Error.ToString()); return session;
        }
        [TestCase(null, 17)]
        [TestCase("11002", 28)]
        public void TextKeepsExactBaseHealingSeparateFromRelicRoundingAndFullHealth(string relic, int healing)
        {
            var session = Setup(false, relic);
            var result = session.Engine.QueryHandGuide(session.State, Player, new HandGuideRequest(HandGuideType.HighCard, true));
            var preview = session.Engine.PreviewHandGuide(session.State, result.Candidates.Single(), true, new[] { Enemy });
            var text = new BattleHandGuideText(preview, session.Engine.Duel.Definition.StatusCatalog);
            Assert.AreEqual("同花", text.Heading);
            CollectionAssert.Contains(text.EffectTags, "治疗");
            CollectionAssert.Contains(text.FormulaLines, "同花治疗基值：35 × 0.5 = 17.5");
            CollectionAssert.Contains(text.SettlementLines, "治疗 " + healing + " · 实际恢复 0 · 溢出 " + healing);
            Assert.AreEqual(0, text.SampleCards.Count); Assert.AreEqual(5, text.HighlightCardIds.Count);
            Assert.AreEqual("已凑齐 · 100%", BattleHandGuideText.AvailabilityText(result));
        }
        [Test]
        public void FlushRuleWithoutCandidatesReadsTheActualConfiguredDamageAndHealing()
        {
            var session = Setup(false, "11002", configure: xml => xml.Replace("flushDamageMultiplier=\"5\"", "flushDamageMultiplier=\"7\"")
                .Replace("amountKind=\"FlushBaseTotalScale\" amount=\"0.5\"", "amountKind=\"FlushBaseTotalScale\" amount=\"0.25\""));
            var player = session.State.Duel.Player(Player);
            var lines = BattleHandGuideText.BasicRuleLines(HandGuideType.HighCard, true, player,
                session.Engine.Scoring, session.Engine.Duel.Definition.StatusCatalog);
            Assert.That(string.Join("\n", lines), Does.Contain("基础倍率 ×7"), "The static guide must read the active rule instead of a second hard-coded multiplier table.");
            Assert.That(string.Join("\n", lines), Does.Contain("同花牌基础点数合计 × 0.25"));
            string summary = BattleHandGuideText.BasicRuleSummary(HandGuideType.HighCard, true, player);
            Assert.That(summary, Does.Contain("点数合计"));
            Assert.That(summary, Does.Contain("同花额外 ×7"));
            Assert.That(summary, Does.Contain("基础治疗25%（未计遗物）"), "The first-screen rule must show the configured base rate even when Heart to Heart changes the current heal.");
        }
        [Test]
        public void FullHouseGuideKeepsItsEffectiveComponentMultipliersSeparateFromStandaloneTriple()
        {
            var session = Setup(false);
            var player = session.State.Duel.Player(Player);
            string fullHouse = string.Join("\n", BattleHandGuideText.BasicRuleLines(HandGuideType.FullHouse, false,
                player, session.Engine.Scoring, session.Engine.Duel.Definition.StatusCatalog));
            Assert.That(fullHouse, Does.Contain("三条分项伤害：基础倍率 ×4"));
            Assert.That(fullHouse, Does.Contain("对子分项伤害：基础倍率 ×4"));
            string triple = string.Join("\n", BattleHandGuideText.BasicRuleLines(HandGuideType.ThreeOfAKind, false,
                player, session.Engine.Scoring, session.Engine.Duel.Definition.StatusCatalog));
            Assert.That(triple, Does.Contain("基础倍率 ×3"));
        }
        [Test] public void MissingSamplesShareHandArtAndEffectsWithoutSuggestingDrawOdds()
        {
            var session = Setup(true);
            var result = session.Engine.QueryHandGuide(session.State, Player, new HandGuideRequest(HandGuideType.HighCard, true));
            var candidate = result.Candidates.Single();
            var text = new BattleHandGuideText(session.Engine.PreviewHandGuide(session.State, candidate), session.Engine.Duel.Definition.StatusCatalog);
            Assert.AreEqual(candidate.Id, text.CandidateId); Assert.AreEqual(session.State.Revision, text.Revision);
            Assert.AreEqual(4, text.HighlightCardIds.Count); Assert.AreEqual(1, text.SampleCards.Count);
            Assert.AreEqual("可补齐 · 还差 1 张", text.Availability); StringAssert.Contains("不代表下次抽牌", text.Notice);
            Assert.AreEqual("clubs_k", text.SampleCards[0].ArtKey);
            Assert.AreEqual(((int)CardEffects.Enhanced).ToString(), text.SampleCards[0].Properties["effects"]);
        }
    }
}
