using System.Collections.Generic;
using global::BattlePresentation;
using NUnit.Framework;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleEffectSelectionTests
    {
        [TestCase("Hit", 6)]
        [TestCase("Shield", 32)]
        [TestCase("Control", 32)]
        [TestCase("Debuff", 28)]
        [TestCase("Cleanse", 24)]
        [TestCase("Lifesteal", 8)]
        public void ShippedFlipbooksHaveAllFrames(string name, int expected)
        {
            var frames = Resources.LoadAll<Texture2D>("BattleEffects/" + name);
            Assert.AreEqual(expected, frames.Length);
            foreach (var frame in frames) Assert.AreEqual(1, frame.mipmapCount, "UI effects must not blur into mipmaps.");
        }

        [TestCase(5, 5, "Hit")]
        [TestCase(5, 0, "Hit")]
        [TestCase(0, 5, "Shield")]
        [TestCase(0, 0, null)]
        public void DamageThroughPartialBlockStillShowsHit(long lost, long absorbed, string expected)
        {
            var before = new UnitDisplayData("player", "Player", "player", 100, 100, absorbed, 3, 3);
            var after = new UnitDisplayData("player", "Player", "player", 100 - lost, 100, 0, 3, 3);
            var snapshot = new BattleDisplaySnapshot("battle", "player", 0, "enemy", 1, "None", new[] { after }, null);
            var effect = new EffectDisplayData("Damage", "enemy", "player", lost + absorbed, lost, absorbed, before, after);
            var fact = new BattleDisplayChange(0, DisplayCursor.Empty, snapshot, "EffectSettled", effect: effect);
            Assert.AreEqual(expected, BattleEffectTimelineAction.EffectFor(fact));
        }

        [TestCase("Applied", "Control", "Control")]
        [TestCase("Refreshed", "Control, Negative", "Control")]
        [TestCase("Applied", "Negative", "Debuff")]
        [TestCase("Cleansed", "Negative", "Cleanse")]
        [TestCase("Expired", "Control", null)]
        [TestCase("Consumed", "Negative", null)]
        [TestCase("Applied", "None", null)]
        public void OnlyRealStatusChangesProduceTheirMatchingEffect(string change, string tags, string expected)
        {
            var snapshot = new BattleDisplaySnapshot("battle", "player", 0, "Action", 1, "None", null, null);
            var fact = new BattleDisplayChange(0, DisplayCursor.Empty, snapshot, "StatusChanged", values:
                new Dictionary<string, string> { ["statusChange"] = change, ["statusTags"] = tags });
            Assert.AreEqual(expected, BattleEffectTimelineAction.EffectFor(fact));
        }
    }
}
