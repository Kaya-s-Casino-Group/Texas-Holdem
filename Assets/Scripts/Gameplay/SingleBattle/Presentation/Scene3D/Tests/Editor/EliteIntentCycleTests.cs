using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Poker.BattlePresentation;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class EliteIntentCycleTests
    {
        private static readonly ParticipantId Player = new ParticipantId("player"), Enemy = new ParticipantId("enemy");

        private static BattleSession Session()
        {
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml");
            Assert.NotNull(content);
            var compiled = BattleContentCompiler.CompileXml(content.text);
            Assert.IsTrue(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
            var created = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest("single-chapter-gatekeeper",
                "elite-intent-cycle", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.IsTrue(created.Success);
            Assert.IsTrue(created.Session.Execute(created.Session.State.Revision, Player, CardDuelAction.Start).Accepted);
            return created.Session;
        }

        private static IReadOnlyList<CombatRuleEvent> Round(BattleSession session)
        {
            var events = new List<CombatRuleEvent>();
            var ended = session.Execute(session.State.Revision, Player, CardDuelAction.EndTurn);
            Assert.IsTrue(ended.Accepted, ended.Error.ToString());
            events.AddRange(ended.Steps.SelectMany(step => step.Transition.RuleEvents));
            if (!session.State.Duel.IsFinished && session.State.FlowFor(Player).Window == BattlePlayerWindow.Ending)
            {
                var finished = session.Execute(session.State.Revision, Player, CardDuelAction.FinishTurn);
                Assert.IsTrue(finished.Accepted, finished.Error.ToString());
                events.AddRange(finished.Steps.SelectMany(step => step.Transition.RuleEvents));
            }
            return events;
        }

        [Test]
        public void SixRoundForecastKeepsTheActualCastAttackSequence()
        {
            var session = Session();
            var actions = new[] { "cast", "strike", "cast", "strike", "cast", "strike" };
            for (int round = 0; round < actions.Length; round++)
            {
                var before = session.State;
                var planned = before.Duel.Enemies.Single().BehaviorState.Intent;
                Assert.AreEqual(actions[round], planned.Id, "Upcoming action in round " + (round + 1));
                var preview = session.Engine.Duel.PreviewEnemyAction(before.Duel, Enemy);
                Assert.IsTrue(preview.Accepted, preview.Error.ToString());
                StringAssert.Contains(planned.Name, BattleDisplayProjector.Build(before, session.Engine, Player).Labels["intent/enemy"]);
                Assert.AreSame(before, session.State, "Forecasts must remain read-only.");

                var actual = Round(session);
                var applied = actual.Where(e => e.Effect?.Source == Enemy && e.Effect.Kind == CombatEffectKind.ApplyStatus).ToArray();
                Assert.AreEqual(round % 2 == 0 ? 1 : 0, applied.Length, "Committed status application in round " + (round + 1));
                if (applied.Length != 0)
                {
                    Assert.AreEqual("elite-bleeding", applied.Single().Effect.Status.DefinitionId.Value);
                    Assert.AreEqual(3, applied.Single().Effect.Status.Stacks);
                }
                CollectionAssert.AreEqual(preview.Hits.Where(h => h.Effect.Source == Enemy).Select(h => h.SettledAmount),
                    actual.OfType<CombatEvent>().Where(h => h.Effect.Source == Enemy && h.Effect.Kind == CombatEffectKind.Damage).Select(h => h.SettledAmount),
                    "Forecast and committed damage in round " + (round + 1));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void WorldBadgesKeepBleedingOnEveryCastAndOmitItOnAttacks(int round)
        {
            var session = Session();
            for (int i = 1; i < round; i++) Round(session);
            var before = session.State;
            var planned = before.Duel.Enemies.Single().BehaviorState.Intent;
            var preview = session.Engine.Duel.PreviewEnemyAction(before.Duel, Enemy);
            Assert.IsTrue(preview.Accepted, preview.Error.ToString());
            var bleeding = BattleDemoEnemyHudModel.Intents(preview, plannedAction: planned)
                .Where(b => b.Symbol == BattleDemoEnemySymbol.Bleeding).ToArray();

            Assert.AreEqual(round % 2 == 1 ? 1 : 0, bleeding.Length, "Bleeding intent badge in round " + round);
            if (bleeding.Length != 0)
            {
                Assert.AreEqual("3", bleeding.Single().Value, "Display the new application, not the total remaining stacks.");
                StringAssert.Contains("流血", bleeding.Single().Description);
            }
            Assert.AreSame(before, session.State);
        }
    }
}
