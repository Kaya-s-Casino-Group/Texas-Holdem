using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using NUnit.Framework;

namespace Poker.BattlePresentation.Tests
{
    public sealed class WrathPromptPresentationTests
    {
        private static BattleDisplaySnapshot Snapshot(string battle = "wrath", string phase = "player", int round = 1,
            int remaining = 8, bool alive = false, bool activeEnemyPhase = false, bool started = true, bool boss = true, bool finished = false)
        {
            var labels = new Dictionary<string, string>();
            if (boss) { labels["wrath.body"] = "body"; labels["wrath.hand/hand"] = "饮血手"; }
            return new BattleDisplaySnapshot(battle, "player", round, phase, round - 1, finished ? "Won" : null,
                new[] { new UnitDisplayData("body", "暴怒", "enemy", 1500, 1500, 0, 0, 0),
                    new UnitDisplayData("hand", "饮血手", "enemy", alive ? 1 : 0, 1, 0, 0, 0) },
                zones: null, resources: new[] { new ResourceDisplayData("body", "wrath.enemyPhaseActive", activeEnemyPhase ? 1 : 0),
                    new ResourceDisplayData("hand", "wrath.reviveRemaining", remaining) },
                hasStarted: started, completedRounds: round - 1, labels: labels);
        }

        [Test]
        public void OpeningUsesTheApprovedRedWarningTextOnceForEachBattle()
        {
            var tracker = new WrathPromptTracker();
            Assert.AreEqual("警告：每存活一只手，暴怒攻击力+5！！", tracker.Observe(Snapshot()).OpeningText);
            Assert.IsNull(tracker.Observe(Snapshot()).OpeningText);
            Assert.IsNotNull(tracker.Observe(Snapshot("another-battle")).OpeningText);
            Assert.IsNull(tracker.Observe(Snapshot()).OpeningText, "Revisiting an older battle must not replay its opening.");
            Assert.AreEqual("警告：Boss有五只手臂，将释放大招！！", WrathPromptText.Ultimate);
        }

        [Test]
        public void PreviewUnstartedAndOrdinarySnapshotsDoNotConsumeTheOpening()
        {
            var tracker = new WrathPromptTracker();
            Assert.IsNull(tracker.Observe(Snapshot(), committed: false).OpeningText);
            Assert.IsNull(tracker.Observe(Snapshot(started: false)).OpeningText);
            Assert.IsNull(tracker.Observe(Snapshot(boss: false)).OpeningText);
            Assert.IsNotNull(tracker.Observe(Snapshot()).OpeningText);
        }

        [Test]
        public void RegenerationFloatUsesAuthorityOncePerEnemyPhaseAndIgnoresRefreshAndRollback()
        {
            var tracker = new WrathPromptTracker();
            Assert.IsEmpty(tracker.Observe(Snapshot(round: 3, remaining: 6)).Regenerations);
            var entering = Snapshot(phase: "enemy", round: 3, remaining: 6, activeEnemyPhase: true);
            var prompt = tracker.Observe(entering).Regenerations.Single();
            Assert.AreEqual("hand", prompt.ActorId);
            Assert.AreEqual("6个回合后再生", prompt.Text);
            Assert.IsEmpty(tracker.Observe(entering).Regenerations);
            Assert.IsEmpty(tracker.Observe(Snapshot(phase: "enemy", round: 2, remaining: 7, activeEnemyPhase: true)).Regenerations);
            Assert.IsEmpty(tracker.Observe(entering).Regenerations);
            Assert.AreEqual("5个回合后再生", tracker.Observe(Snapshot(phase: "enemy", round: 4, remaining: 5, activeEnemyPhase: true)).Regenerations.Single().Text);
        }

        [Test]
        public void RoundRefreshDoesNotPretendToStartAnotherEnemyPhase()
        {
            var tracker = new WrathPromptTracker();
            Assert.IsEmpty(tracker.Observe(Snapshot(phase: "enemy", round: 2, remaining: 7)).Regenerations);
            Assert.AreEqual(1, tracker.Observe(Snapshot(phase: "enemy", round: 2, remaining: 7, activeEnemyPhase: true)).Regenerations.Count);
        }

        [Test]
        public void LivingAndFinishedUnitsNeverReceiveRegenerationFloats()
        {
            var tracker = new WrathPromptTracker();
            Assert.IsEmpty(tracker.Observe(Snapshot(phase: "enemy", activeEnemyPhase: true, alive: true, remaining: 0)).Regenerations);
            Assert.IsEmpty(tracker.Observe(Snapshot(phase: "enemy", round: 2, activeEnemyPhase: true, finished: true)).Regenerations);
        }

        [TestCase("strike", WrathIntentIcon.Attack)]
        [TestCase("strike-2", WrathIntentIcon.Attack)]
        [TestCase("guard", WrathIntentIcon.Defense)]
        [TestCase("charge", WrathIntentIcon.Charge)]
        [TestCase("wrath-ultimate", WrathIntentIcon.Unknown)]
        public void PlaceholderIconsUseStableActionIdentity(string id, WrathIntentIcon expected)
        { Assert.AreEqual(expected, WrathPromptText.IconFor(id)); }
    }
}
