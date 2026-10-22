using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using NUnit.Framework;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    public sealed class BattleElitePresentationTests
    {
        private static readonly ParticipantId Player = new ParticipantId("elite-player"), Enemy = new ParticipantId("elite-enemy");

        private static CombatStatusCatalog BleedingCatalog() => new CombatStatusCatalog("elite-ui-status.1", new[] {
            new CombatStatusDefinition(new StatusDefinitionId("elite-bleeding"), StatusTags.Negative,
                new StatusBehaviorSpec[] { new IncomingAttackDamagePerStackSpec(), new StackDecaySpec() },
                StatusLifetimeSpec.UntilRemoved(), StatusReapplyPolicy.AddStacks)
        });

        private static CombatState BleedingState(int stacks, long block = 0) => new CombatState("elite-ui", 0, new[] {
            new CombatantState(Player, "hero", "players", 800, 800, block),
            new CombatantState(Enemy, "elite", "enemies", 1800, 1800)
        }, new[] { new StatusInstance(new StatusInstanceId("elite-bleeding/1"), new StatusDefinitionId("elite-bleeding"),
            EntityRef.Participant(Enemy), EntityRef.Participant(Player), stacks, StatusDuration.UntilRemoved()) });

        private static TeamBattleState Names() => new TeamBattleEngine(new BattleRules("elite-ui-names.1", 20)).Create("elite-ui",
            new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", 800)) },
            new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("elite", 1800), 90) });

        [Test]
        public void BleedingDescriptionExplainsEnemyAttacksShieldAndApplierDecay()
        {
            var catalog = BleedingCatalog();
            string text = BattlePresentationRuleText.Status(BleedingState(1).Statuses.Single(), catalog);
            StringAssert.Contains("流血", text);
            StringAssert.Contains("50%", text);
            StringAssert.Contains("敌人", text);
            StringAssert.Contains("护盾", text);
            StringAssert.Contains("自伤", text);
            StringAssert.Contains("施加者", text);
            StringAssert.Contains("行动后", text);
            StringAssert.Contains("1 层", text);
            StringAssert.Contains("×1", text);
            StringAssert.DoesNotContain("Spec", text);
        }

        [Test]
        public void DamageFeedbackExplainsBleedingContributionWithOneDamageSettlement()
        {
            var catalog = BleedingCatalog();
            var state = BleedingState(2, 100);
            var result = CombatEngine.Resolve(state, state.Revision, new[] {
                new CombatEffect(CombatEffectKind.Damage, Enemy, Player, 100,
                    attack: new AttackContext(Enemy, AttackOrigin.EnemyAttack, 100))
            }, statusCatalog: catalog);
            Assert.IsTrue(result.Accepted, result.Error.ToString());

            string text = string.Join("\n", BattlePresentationRuleText.Events(result.RuleEvents, Names(), catalog));

            StringAssert.Contains("伤害 200", text);
            StringAssert.Contains("扣血 100", text);
            StringAssert.Contains("护盾吸收 100", text);
            StringAssert.Contains("流血", text);
            StringAssert.Contains("+100", text);
            Assert.AreEqual(1, result.Events.Count(e => e.Effect.Kind == CombatEffectKind.Damage));
        }

        [Test]
        public void DecayFeedbackDisplaysPreviousAndRemainingLayers()
        {
            var catalog = BleedingCatalog();
            var state = BleedingState(3);
            var result = CombatEngine.ResolveTiming(state, state.Revision,
                CombatTimingSignal.ActionCompleted("elite-ui/end", Enemy, "cast"), statusCatalog: catalog);
            Assert.IsTrue(result.Accepted, result.Error.ToString());

            string text = string.Join("\n", BattlePresentationRuleText.Events(result.RuleEvents, Names(), catalog));

            StringAssert.Contains("3→2 层", text);
            Assert.IsEmpty(result.Events, "Layer decay must not become a second damage event.");
        }

        [Test]
        public void ConfiguredElitePreviewShowsGrowthAndDamageThenBleedingDecay()
        {
            var compiled = BattleContentCompiler.CompileXml(File.ReadAllText(CurrentContentPath()));
            Assert.IsTrue(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
            var created = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest("standard-armored-summoner",
                "elite-display", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.IsTrue(created.Success);
            var session = created.Session;
            var player = new ParticipantId("player");
            Assert.IsTrue(session.Execute(session.State.Revision, player, CardDuelAction.Start).Accepted);
            var before = session.State;

            var display = BattleDisplayProjector.Build(before, session.Engine, player);
            string text = display.Labels["intent/enemy"];

            Assert.AreEqual(500, before.Duel.Combat.Get(new ParticipantId("enemy")).MaxHealth);
            StringAssert.Contains("施法", text);
            StringAssert.Contains("攻击力 8", text);
            StringAssert.Contains("每回合结束 +5", text);
            StringAssert.Contains("伤害 4", text);
            StringAssert.Contains("30%", text);
            StringAssert.Contains("×3", text);
            StringAssert.Contains("3→2 层", text);
            Assert.AreSame(before, session.State);
            Assert.IsEmpty(before.Duel.Combat.Statuses);
        }

        private static string CurrentContentPath([CallerFilePath] string source = "")
        {
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(source)); directory != null; directory = directory.Parent)
            {
                string path = Path.Combine(directory.FullName, "External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml");
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("Current battle content was not found from the test source.");
        }

        [Test]
        public void OverflowingGrowthDoesNotBreakAnAcceptedNonDamageIntentDisplay()
        {
            var xml = XDocument.Load(CurrentContentPath());
            var behavior = xml.Root.Element("Enemies").Elements("Enemy")
                .Single(e => (string)e.Attribute("id") == "summoner").Element("Behavior");
            behavior.SetAttributeValue("attack", long.MaxValue);
            behavior.SetAttributeValue("attackPerRound", 1);
            foreach (var effect in behavior.Descendants("Effect").Where(e => (string)e.Attribute("kind") == "Damage"))
            {
                effect.SetAttributeValue("kind", "GainBlock");
                effect.SetAttributeValue("amount", 0);
                effect.SetAttributeValue("amountKind", "Constant");
            }
            var compiled = BattleContentCompiler.CompileXml(xml.ToString());
            Assert.IsTrue(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
            var session = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest("standard-armored-summoner",
                "elite-display-overflow", HmacDeckShuffler.FromSeed(new byte[32]))).Session;
            var player = new ParticipantId("player");
            Assert.IsTrue(session.Execute(session.State.Revision, player, CardDuelAction.Start).Accepted);
            Assert.IsTrue(session.Execute(session.State.Revision, player, CardDuelAction.EndTurn).Accepted);
            if (session.State.FlowFor(player).Window == BattlePlayerWindow.Ending)
                Assert.IsTrue(session.Execute(session.State.Revision, player, CardDuelAction.FinishTurn).Accepted);
            Assert.AreEqual(1, session.State.Duel.CompletedRounds);
            Assert.IsTrue(session.Engine.Duel.PreviewEnemyAction(session.State.Duel, new ParticipantId("enemy")).Accepted);
            var before = session.State;

            var display = BattleDisplayProjector.Build(before, session.Engine, player);

            StringAssert.Contains("普攻", display.Labels["intent/enemy"]);
            StringAssert.DoesNotContain("攻击力", display.Labels["intent/enemy"]);
            Assert.AreSame(before, session.State);
        }

        [Test]
        public void EnemyIntentIncludesDeclaredSkillNameWithoutMutatingBattle()
        {
            var behavior = new EnemyBehaviorDefinition(new[] {
                new EnemyBehaviorStage("normal", EnemySelectionMode.Sequence, new[] {
                    new EnemyBehaviorAction("cast", "施法：裂伤", new BattleEffectSequence("cast.1", "施法", new[] {
                        new BattleEffectStep("hit", BattleEffectSpec.Damage(45), EffectTargetSpec.Opponents())
                    }))
                })
            });
            var engine = new CardDuelEngine(new BattleRules("elite-ui.1", 20));
            var session = new BattleSession(engine, engine.Create("elite-ui", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", 800)) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("elite", 1800), 0, behavior: behavior) }));
            Assert.IsTrue(session.Execute(session.State.Revision, Player, CardDuelAction.Start).Accepted);
            var before = session.State;

            var display = BattleDisplayProjector.Build(before, engine, Player);

            StringAssert.Contains("施法：裂伤", display.Labels["intent/" + Enemy.Value]);
            StringAssert.Contains("伤害 45", display.Labels["intent/" + Enemy.Value]);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(800, before.Duel.Combat.Get(Player).Health);
        }
    }
}
