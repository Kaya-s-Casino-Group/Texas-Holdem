using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    public sealed class HandGuideEffectOverviewTests
    {
        // Catches a summary guessed from the hand's name rather than the active effect configuration.
        [TestCase(false, "Self", null, "自身获得护盾")]
        [TestCase(false, "Opponents", "AllLiving", "所有敌人获得护盾")]
        [TestCase(true, "Self", null, "自身获得护盾")]
        [TestCase(true, "Opponents", "AllLiving", "所有敌人获得护盾")]
        public void GuideExplainsConfiguredEffectAndTargetBeforeItsFormula(bool candidate, string target, string selection, string explanation)
        {
            var xml = XDocument.Load("External/BattleToolkit/Engine/Content/SingleChapterBattle.xml");
            var hand = xml.Root.Element("HandRules").Elements("HandRule").First().Elements("Hand")
                .Single(value => (string)value.Attribute("category") == "OnePair");
            hand.RemoveNodes();
            hand.Add(new XElement("Effect", new XAttribute("id", "guide-guard"), new XAttribute("kind", "GainBlock"),
                new XAttribute("amount", 9), new XAttribute("target", target), selection == null ? null : new XAttribute("selection", selection)));
            var content = BattleContentCompiler.CompileXml(xml.ToString());
            Assert.IsTrue(content.Success, string.Join("\n", content.Diagnostics.Select(d => d.Message)));
            var session = BattleContentFactory.Create(content.Content, new BattleSetupRequest("single-chapter-battle-1", "overview-test",
                HmacDeckShuffler.FromSeed(new byte[32]))).Session;
            var actor = new ParticipantId("player");
            System.Collections.Generic.IReadOnlyList<string> lines;
            if (candidate)
            {
                Assert.IsTrue(session.Execute(0, actor, CardDuelAction.Start).Accepted);
                Assert.IsTrue(session.ApplyAdmin(session.State.Revision, new BattleAdminPatch(zones: new[] {
                    new BattleAdminZone(actor, CardZoneKinds.Hand, new[] { new BattleAdminCard(definitionId: "standard:1:2"),
                        new BattleAdminCard(definitionId: "standard:2:2") }),
                    new BattleAdminZone(actor, CardZoneKinds.Deck, System.Array.Empty<BattleAdminCard>()),
                    new BattleAdminZone(actor, CardZoneKinds.Discard, System.Array.Empty<BattleAdminCard>()) })).Accepted);
                var result = session.Engine.QueryHandGuide(session.State, actor, new HandGuideRequest(HandGuideType.OnePair));
                var preview = session.Engine.PreviewHandGuide(session.State, result.Candidates.Single());
                lines = new BattleHandGuideText(preview, session.Engine.Duel.Definition.StatusCatalog).FormulaLines;
            }
            else lines = BattleHandGuideText.BasicRuleLines(HandGuideType.OnePair, false, session.State.Duel.Player(actor),
                session.Engine.Scoring, session.Engine.Duel.Definition.StatusCatalog);
            Assert.That(lines[0], Does.Contain(explanation));
            Assert.That(lines[0], Does.Not.Contain("向下取整"));
            Assert.That(string.Join("\n", lines), Does.Contain("护盾：9"), "The explanation must retain the detailed rule below it.");
        }
    }
}
