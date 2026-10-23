using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using NUnit.Framework;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    public sealed class BattleWrathPresentationTests
    {
        private static readonly ParticipantId Player = new ParticipantId("player");

        private static BattleSession Create(string encounter = "standard-wrath-thousand-hands", bool suppressBinding = false)
        {
            var document = XDocument.Load(ContentPath());
            if (suppressBinding)
            {
                // Keep a full five-hand field while observing the warning's player response window.
                var binding = document.Root.Element("Enemies").Elements("Enemy").Single(e => (string)e.Attribute("id") == "wrath-binding");
                binding.Descendants("Effects").First().ReplaceNodes(new XElement("Effect", new XAttribute("id", "idle"),
                    new XAttribute("kind", "GainBlock"), new XAttribute("amount", "0"), new XAttribute("target", "Self")));
                foreach (var player in document.Root.Element("Players").Elements("Player")) player.SetAttributeValue("maxHealth", 100000);
            }
            var compiled = BattleContentCompiler.CompileXml(document.ToString());
            Assert.IsTrue(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
            var assembly = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(encounter,
                "wrath-display", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.IsTrue(assembly.Success, string.Join("\n", assembly.Diagnostics.Select(d => d.Message)));
            var session = assembly.Session;
            Assert.IsTrue(session.Execute(session.State.Revision, Player, CardDuelAction.Start).Accepted);
            return session;
        }

        private static string ContentPath([CallerFilePath] string source = "")
        {
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(source)); directory != null; directory = directory.Parent)
            {
                string path = Path.Combine(directory.FullName, "External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml");
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("Current battle content was not found from the test source.");
        }

        [Test]
        public void OpeningProjectsBodyIdentityDynamicAttackAndEightRegularIntentsWithoutMutation()
        {
            var session = Create();
            var before = session.State;

            var display = BattleDisplayProjector.Build(before, session.Engine, Player);

            Assert.IsTrue(display.Labels.ContainsKey("wrath.body"), "Wrath needs an explicit body identity, independent of roster order.");
            Assert.AreEqual("enemy", display.Labels["wrath.body"]);
            Assert.AreEqual(33, display.Resource("enemy", "wrath.attack").Value);
            Assert.AreEqual(0, display.Resource("enemy", "wrath.charges").Value);
            Assert.AreEqual(0, display.Resource("enemy", "wrath.warning").Value);
            Assert.AreEqual(5, display.Units.Count(u => display.Labels.ContainsKey("wrath.hand/" + u.Id)));
            display.Labels.TryGetValue("wrath.name/hand-1", out var bloodName);
            display.Labels.TryGetValue("wrath.name/hand-5", out var bindingName);
            Assert.AreEqual("饮血手 1", bloodName);
            Assert.AreEqual("束缚手", bindingName);
            StringAssert.Contains("攻击力 33", display.Labels["intent/enemy"]);
            Assert.AreEqual(8, display.Labels.Keys.Count(key => key.StartsWith("wrath.intent/")));
            CollectionAssert.AreEqual(new[] { "普攻", "普攻", "防御", "蓄力", "普攻", "普攻", "防御", "蓄力" },
                Enumerable.Range(0, 8).Select(index => display.Labels["wrath.intent/" + index].Split('·')[0]));
            CollectionAssert.AreEqual(new[] { "strike", "strike-2", "guard", "charge", "strike", "strike-2", "guard", "charge" },
                Enumerable.Range(0, 8).Select(index => display.Labels["wrath.intentId/" + index]));
            Assert.AreEqual("警告：每存活一只手，暴怒攻击力+5！！", display.Labels["wrath.opening"]);
            Assert.AreEqual(0, display.Resource("enemy", "wrath.enemyPhaseActive").Value);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(0, before.Duel.CompletedRounds);
        }

        [Test]
        public void GrenadeLeavesFiveDeadHandCountdownsVisibleAndReducesBodyAttack()
        {
            var session = Create();
            ThrowGrenade(session);

            var display = BattleDisplayProjector.Build(session.State, session.Engine, Player);

            Assert.IsNotNull(display.Resource("enemy", "wrath.attack"), "The body must expose its current attack after hands die.");
            Assert.AreEqual(8, display.Resource("enemy", "wrath.attack").Value);
            var deadHands = display.Units.Where(u => u.Team == "enemy" && !u.IsAlive).ToArray();
            Assert.AreEqual(5, deadHands.Length);
            foreach (var hand in deadHands)
            {
                Assert.AreEqual(8, display.Resource(hand.Id, "wrath.reviveRemaining").Value);
                StringAssert.Contains("再生", display.Labels["wrath.hand/" + hand.Id]);
                Assert.IsFalse(display.Labels.ContainsKey("intent/" + hand.Id));
            }
            StringAssert.Contains("攻击力 8", display.Labels["intent/enemy"]);
        }

        [Test]
        public void WarningIsIndependentFromRegularIntentsAndDisappearsImmediatelyWhenAHandDies()
        {
            var session = Create(suppressBinding: true);
            for (int round = 0; round < 5; round++) EndRound(session);
            Assert.AreEqual(5, session.State.Duel.CompletedRounds);
            var before = session.State;
            var display = BattleDisplayProjector.Build(before, session.Engine, Player);

            Assert.AreEqual(1, display.Resource("enemy", "wrath.warning").Value);
            Assert.AreEqual(6, display.Resource("enemy", "wrath.releaseRound").Value);
            Assert.AreEqual(43, display.Resource("enemy", "wrath.attack").Value);
            Assert.AreEqual(1, display.Resource("enemy", "wrath.charges").Value);
            Assert.AreEqual(129, display.Resource("enemy", "wrath.ultimateDamage").Value);
            StringAssert.Contains("第 6 轮", display.Labels["wrath.ultimate"]);
            StringAssert.StartsWith("警告：Boss有五只手臂，将于下一回合释放大招！！", display.Labels["wrath.ultimate"]);
            StringAssert.Contains("普攻", display.Labels["wrath.intent/0"]);
            StringAssert.Contains("伤害 43", display.Labels["intent/enemy"]);
            StringAssert.DoesNotContain("伤害 129", display.Labels["intent/enemy"], "Additional damage must not be mixed into the regular intent.");
            StringAssert.DoesNotContain("伤害 172", display.Labels["intent/enemy"], "The regular intent must not include the sum of both attacks.");
            Assert.AreEqual(8, display.Labels.Keys.Count(key => key.StartsWith("wrath.intent/")));
            Assert.AreSame(before, session.State);

            ThrowGrenade(session);
            var cancelled = BattleDisplayProjector.Build(session.State, session.Engine, Player);
            Assert.AreEqual(0, cancelled.Resource("enemy", "wrath.warning").Value);
            StringAssert.DoesNotContain("警告：Boss有五只手臂", cancelled.Labels["wrath.ultimate"]);
            Assert.AreEqual(5, cancelled.Resource("enemy", "wrath.cooldown").Value);
            Assert.AreEqual(display.Labels["wrath.intent/0"], cancelled.Labels["wrath.intent/0"]);
            Assert.AreEqual(18, cancelled.Resource("enemy", "wrath.attack").Value);
        }

        [Test]
        public void DeadHandCountdownBecomesOneBeforeRegenerationAndReturnsToAliveOnTheSameIdentity()
        {
            var session = Create(suppressBinding: true);
            ThrowGrenade(session);
            for (int round = 0; round < 7; round++) EndRound(session);
            var before = BattleDisplayProjector.Build(session.State, session.Engine, Player);
            Assert.AreEqual(1, before.Resource("hand-1", "wrath.reviveRemaining").Value);
            EndRound(session);
            Assert.AreEqual(0, BattleDisplayProjector.Build(session.State, session.Engine, Player).Resource("hand-1", "wrath.reviveRemaining").Value);
            EndRound(session);
            var after = BattleDisplayProjector.Build(session.State, session.Engine, Player);

            Assert.IsTrue(after.Unit("hand-1").IsAlive);
            Assert.AreEqual(0, after.Resource("hand-1", "wrath.reviveRemaining").Value);
            StringAssert.Contains("饮血", after.Labels["wrath.hand/hand-1"]);
            Assert.AreEqual(6, after.Units.Count(unit => unit.Team == "enemy"));
        }

        [Test]
        public void ShellStatusExplainsDamageOnlyImmunityAndShowsItsLastRemainingLayer()
        {
            var session = Create(suppressBinding: true);
            for (int round = 0; round < 3; round++) EndRound(session);
            var display = BattleDisplayProjector.Build(session.State, session.Engine, Player);
            StringAssert.Contains("伤害免疫", display.Labels["status/enemy"]);
            StringAssert.Contains("×2", display.Labels["status/enemy"]);
            StringAssert.Contains("不免疫状态", display.Labels["status/enemy"]);
            StringAssert.DoesNotContain("Spec", display.Labels["status/enemy"]);

            ThrowGrenade(session);
            var after = BattleDisplayProjector.Build(session.State, session.Engine, Player);
            StringAssert.Contains("×1", after.Labels["status/enemy"]);
        }

        [Test]
        public void RealTimelineEmitsOneRegenerationPromptPerHandAtEntryAndNoneAtRoundRefreshOrRevival()
        {
            var session = Create(suppressBinding: true);
            ThrowGrenade(session);
            var tracker = new WrathPromptTracker();
            tracker.Observe(BattleDisplayProjector.Build(session.State, session.Engine, Player));
            for (int round = 1; round <= 9; round++)
            {
                var result = EndRound(session);
                var emitted = new System.Collections.Generic.List<WrathRegenerationPrompt>();
                foreach (var entry in result.Timeline)
                {
                    var snapshot = BattleDisplayProjector.Build(entry.State, session.Engine, Player);
                    var prompts = tracker.Observe(snapshot);
                    Assert.IsNull(prompts.OpeningText);
                    if (entry.Kind == BattleTimelineKind.ResourcesChanged && entry.Reason == "round-refresh")
                        Assert.IsEmpty(prompts.Regenerations, "A round-refresh frame is not another enemy-phase entry.");
                    if (prompts.Regenerations.Count > 0) Assert.AreEqual(BattleTimelineKind.PhaseStarted, entry.Kind);
                    emitted.AddRange(prompts.Regenerations);
                    Assert.IsEmpty(tracker.Observe(snapshot).Regenerations, "Repeating a displayed frame cannot replay its float.");
                }
                Assert.AreEqual(round < 9 ? 5 : 0, emitted.Count, "enemy round " + round);
                if (round < 9) Assert.IsTrue(emitted.All(prompt => prompt.Text == (9 - round) + "个回合后再生"));
            }
            Assert.IsTrue(session.State.Duel.Enemies.Skip(1).All(hand => session.State.Duel.Combat.Get(hand.Id).IsAlive));
        }

        [Test]
        public void LethalHitAndRevivalTimelineProjectsEveryCheckpointWithoutForecastingAgainstDeadPlayers()
        {
            var session = Create();
            for (int round = 0; round < 4; round++) EndRound(session);
            Assert.IsTrue(session.ApplyAdmin(session.State.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(Player, health: 44) },
                relics: new[] { new BattleAdminRelics(Player, new[] { new BattleAdminRelic("13004", charges: 1) }) })).Accepted);
            var result = EndRound(session);
            var frames = result.Timeline.Select(entry => entry.State).ToArray();
            Assert.IsTrue(frames.Any(state => !state.Duel.Combat.Get(Player).IsAlive));
            Assert.AreEqual(480, session.State.Duel.Combat.Get(Player).Health);
            foreach (var frame in frames)
            {
                var snapshot = BattleDisplayProjector.Build(frame, session.Engine, Player);
                Assert.AreEqual(frame.Duel.Combat.Get(Player).Health, snapshot.Unit("player").Health);
            }
        }

        private static void ThrowGrenade(BattleSession session)
        {
            var grant = session.ApplyAdmin(session.State.Revision, new BattleAdminPatch(
                relics: new[] { new BattleAdminRelics(Player, new[] { new BattleAdminRelic("11004", charges: 3) }) },
                reason: "Wrath display grenade fixture"));
            Assert.IsTrue(grant.Accepted, grant.Error.ToString());
            string item = session.State.Duel.Player(Player).Relics.Relics.Single().InstanceId;
            var result = session.UseItem(session.State.Revision, Player, item);
            Assert.IsTrue(result.Accepted, result.Error.ToString());
        }

        private static BattleSessionResult EndRound(BattleSession session)
        {
            var result = session.Execute(session.State.Revision, Player, CardDuelAction.EndTurn);
            Assert.IsTrue(result.Accepted);
            if (session.State.FlowFor(Player).Window == BattlePlayerWindow.Ending)
            { result = session.Execute(session.State.Revision, Player, CardDuelAction.FinishTurn); Assert.IsTrue(result.Accepted); }
            return result;
        }

        [TestCase("standard-single-battle")]
        [TestCase("standard-armored-summoner")]
        public void OtherEncountersDoNotReceiveBossLayoutOrBossResources(string encounter)
        {
            var session = Create(encounter);
            var display = BattleDisplayProjector.Build(session.State, session.Engine, Player);

            Assert.IsFalse(display.Labels.Keys.Any(key => key.StartsWith("wrath.")));
            Assert.IsFalse(display.Resources.Any(resource => resource.Key.StartsWith("wrath.")));
            Assert.IsTrue(display.Labels.ContainsKey("intent/enemy"));
        }
    }
}
