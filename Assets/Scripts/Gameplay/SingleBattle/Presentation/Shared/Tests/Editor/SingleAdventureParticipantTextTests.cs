using System.IO;
using System.Linq;
using global::BattlePresentation;
using NUnit.Framework;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    public sealed class SingleAdventureParticipantTextTests
    {
        private sealed class Frontend : IBattlePresentationFrontend
        {
            public PresentationBatch Batch;
            public bool IsPresenting => false;
            public void Initialize(BattleDisplaySnapshot snapshot) { }
            public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode)
            { Batch = batch; return PresentationOperation.Completed(); }
            public void Synchronize(BattleDisplaySnapshot snapshot) { }
        }
        private static BattleSession SevenStation()
        {
            var compiled = BattleContentCompiler.CompileXml(File.ReadAllText("External/BattleToolkit/Engine/Content/SingleChapterBattle.xml"), "participant-text-test");
            var created = BattleContentFactory.Create(compiled.Content,
                new BattleSetupRequest("single-chapter-gatekeeper", "participant-text-test", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.IsTrue(created.Success); return created.Session;
        }

        [Test]
        public void SingleAdventureUsesYouInCommittedBleedingFeedbackAndDetailedLog()
        {
            var session = SevenStation(); var player = session.State.Duel.PlayerId;
            var frontend = new Frontend(); var bridge = new BattlePresentationBridge(session.Engine, player, frontend);
            bridge.Initialize(session.State);
            var start = session.Execute(session.State.Revision, player, CardDuelAction.Start);
            Assert.IsTrue(start.Accepted); bridge.Present(start, false);
            Assert.That(BattlePresentationRuleText.Participant(session.State.Duel, player), Is.EqualTo("你"));
            var result = session.Execute(session.State.Revision, player, CardDuelAction.EndTurn);
            Assert.IsTrue(result.Accepted); bridge.Present(result, true);
            if (session.State.FlowFor(player).Window == BattlePlayerWindow.Ending)
            {
                result = session.Execute(session.State.Revision, player, CardDuelAction.FinishTurn);
                Assert.IsTrue(result.Accepted); bridge.Present(result, true);
            }
            var status = frontend.Batch.Changes.First(change => change.Values.TryGetValue("floatingFeedback", out var value) && value.Contains("流血"));
            Assert.That(status.Values["floatingFeedback"], Does.StartWith("你：").And.Not.Contain("玩家 1"));
            Assert.That(status.Values["feedback"], Does.StartWith("你：").And.Not.Contain("玩家 1"));
        }

        [Test]
        public void GenericAndMultiplayerBattlesKeepNumberedParticipantNames()
        {
            var hero = new ParticipantId("hero"); var ally = new ParticipantId("ally"); var enemy = new ParticipantId("enemy");
            var engine = new TeamBattleEngine(new BattleRules("participant-text", 20));
            var generic = engine.Create("generic", new[] { new BattlePlayerSetup(hero, new CombatantDefinition("hero", 100)) },
                new[] { new BattleEnemySetup(enemy, new CombatantDefinition("enemy", 100), 8) });
            Assert.That(BattlePresentationRuleText.Participant(generic, hero), Is.EqualTo("玩家 1"));
            var modern = SevenStation(); var modernRules = modern.State.Duel.Players[0].Rules;
            var modernEngine = new TeamBattleEngine(new BattleRules("participant-text-modern", 20,
                statusCatalog: modern.Engine.Duel.Definition.StatusCatalog));
            var multi = modernEngine.Create("multi", new[] { new BattlePlayerSetup(hero, new CombatantDefinition("hero", 100), rules: modernRules),
                    new BattlePlayerSetup(ally, new CombatantDefinition("ally", 100), rules: modernRules) },
                new[] { new BattleEnemySetup(enemy, new CombatantDefinition("enemy", 100), 8) });
            Assert.That(BattlePresentationRuleText.Participant(multi, hero), Is.EqualTo("玩家 1"));
            Assert.That(BattlePresentationRuleText.Participant(multi, ally), Is.EqualTo("玩家 2"));
        }
    }
}
