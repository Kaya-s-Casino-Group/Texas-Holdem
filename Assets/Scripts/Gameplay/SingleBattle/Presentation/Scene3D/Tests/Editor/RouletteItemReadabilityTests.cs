using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.SingleBattle.Tests
{
    public sealed class RouletteItemReadabilityTests
    {
        private static readonly ParticipantId Hero = new ParticipantId("roulette-reader");
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root = new GameObject("Roulette readability", typeof(RectTransform));
            internal readonly SingleBattleHud Hud;
            internal readonly Image Icon;
            internal Fixture()
            {
                Hud = Root.AddComponent<SingleBattleHud>();
                var source = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
                source.transform.SetParent(Root.transform, false);
                typeof(SingleBattleHud).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Hud, source.GetComponent<TMP_Text>());
                var icon = new GameObject("Russian roulette", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(Root.transform, false);
                Icon = icon.GetComponent<Image>(); Icon.rectTransform.sizeDelta = new Vector2(42, 42);
            }
            internal void Bind(BattleRelicLoadout loadout)
            {
                typeof(SingleBattleHud).GetMethod("BindRelicHover", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Hud, new object[] { Icon, loadout.Relics.Single(), loadout });
            }
            internal TMP_Text[] Labels => Icon.GetComponentsInChildren<TMP_Text>();
            public void Dispose() => Object.DestroyImmediate(Root);
        }

        private static BattleRelicLoadout Loadout(string definition = "15001", int turn = 0)
        {
            var content = BattleContentCompiler.CompileXml(File.ReadAllText("External/BattleToolkit/Engine/Content/SingleChapterBattle.xml"));
            Assert.IsTrue(content.Success, string.Join("\n", content.Diagnostics.Select(item => item.Message)));
            var relic = content.Content.Relics[definition];
            var memory = turn == 0 ? AbilityMemory.Empty : AbilityMemory.Empty
                .WithCounter(BattleItemRuntime.RouletteActiveKey, 1, MemoryScope.Encounter)
                .WithCounter(BattleItemRuntime.RouletteTurnKey, turn, MemoryScope.Encounter)
                .WithCounter(BattleItemRuntime.RouletteBombKey, 6, MemoryScope.Encounter);
            return new BattleRelicLoadout(Hero, new[] { new BattleRelicSetup(relic.Definition,
                new RunRelic("roulette-reader/item", definition, charges: relic.Definition.Charges, memory: memory), relic.Rules) });
        }

        [Test]
        public void HeldRouletteClearlyShowsItRequiresActiveUseWithoutStartingItsMultiplier()
        {
            using (var f = new Fixture())
            {
                var loadout = Loadout(); f.Bind(loadout);
                Assert.That(f.Labels.Any(label => label.text == "主动"), Is.True,
                    "An owned roulette needs a persistent active-use cue before the user opens its panel.");
                Assert.That(f.Labels.Any(label => label.text.StartsWith("×", StringComparison.Ordinal)), Is.False);
                Assert.That(loadout.Relics.Single().Memory.CounterValue(BattleItemRuntime.RouletteActiveKey), Is.Zero);
                Assert.That(BattleItemRuntime.HandMultiplier(loadout), Is.EqualTo(1));
                f.Bind(loadout);
                Assert.That(f.Labels.Count(label => label.text == "主动"), Is.EqualTo(1), "Refreshing cannot stack duplicate labels.");
            }
        }

        [TestCase(1, "×10")]
        [TestCase(2, "×100")]
        [TestCase(3, "×1000")]
        public void ActiveRouletteShowsTheActualGrowingHandDamageMultiplier(int turn, string expected)
        {
            using (var f = new Fixture())
            {
                var loadout = Loadout(turn: turn); f.Bind(loadout);
                Assert.That(f.Labels.Any(label => label.text == expected), Is.True,
                    "The persistent cue must show the current committed multiplier, not merely an enabled boolean.");
                Assert.That(f.Labels.Any(label => label.text == "主动"), Is.True);
                Assert.That(loadout.Relics.Single().Memory.CounterValue(BattleItemRuntime.RouletteTurnKey), Is.EqualTo(turn));
                Assert.That(f.Labels.Any(label => label.text.Contains("bomb") || label.text.Contains("爆炸回合")), Is.False,
                    "UI must not reveal the hidden random bomb turn.");
            }
        }

        [Test]
        public void FinishingTheBattleClearsTheOldMultiplierButKeepsTheActiveUseCue()
        {
            using (var f = new Fixture())
            {
                var running = Loadout(turn: 2); f.Bind(running);
                Assert.That(f.Labels.Any(label => label.text == "×100"), Is.True);
                var item = running.Relics.Single();
                var completed = BattleItemRuntime.CompleteBattle(new RunRelic(item.InstanceId, item.DefinitionId, memory: item.Memory));
                var stopped = running.WithRuntime(new[] { item.WithRuntime(item.RemainingCharges, item.UsesThisRound, item.CooldownRounds, completed.Memory) });
                f.Bind(stopped);
                Assert.That(f.Labels.Any(label => label.text.StartsWith("×", StringComparison.Ordinal)), Is.False,
                    "Returning with the same icon must not leave the previous battle's multiplier visible.");
                Assert.That(f.Labels.Any(label => label.text == "主动"), Is.True);
            }
        }

        [Test]
        public void ReusingTheIconForAnotherRelicRemovesEveryRouletteCue()
        {
            using (var f = new Fixture())
            {
                f.Bind(Loadout(turn: 2));
                Assert.That(f.Labels.Any(label => label.text == "主动"), Is.True);
                Assert.That(f.Labels.Any(label => label.text == "×100"), Is.True);
                f.Bind(Loadout("13003"));
                Assert.That(f.Labels.Any(label => label.text == "主动" || label.text.StartsWith("×", StringComparison.Ordinal)), Is.False,
                    "A pooled relic icon must not inherit the roulette's labels.");
            }
        }
    }
}
