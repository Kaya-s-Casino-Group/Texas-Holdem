#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        private int previousSceneTestTimeout;

        [OneTimeSetUp]
        public void AllowFullLengthScenePlayback()
        {
            // Test Framework 1.3.3 starts its coroutine timer before applying method-level Timeout.
            // Set the fixture context before child test contexts (and their timers) are created.
            previousSceneTestTimeout = TestContext.CurrentTestExecutionContext.TestCaseTimeout;
            TestContext.CurrentTestExecutionContext.TestCaseTimeout = 600000;
        }

        [OneTimeTearDown]
        public void RestoreSceneTestTimeout() =>
            TestContext.CurrentTestExecutionContext.TestCaseTimeout = previousSceneTestTimeout;

        [UnityTest, Timeout(600000)]
        public IEnumerator SevenStationBossTwentyRealRoundsShowFailureAndReturnHome()
        {
            yield return ReachBattle("boss");
            var view = host.BattleView;
            var hud = Object.FindObjectsOfType<SingleBattleHud>().Single();
            AssertRoster(6);
            Assert.AreEqual(0, host.Session.State.Duel.CompletedRounds);
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(host.Adventure.PlayerId, health: 100000, maxHealth: 100000) },
                relics: new[] { new BattleAdminRelics(host.Adventure.PlayerId, Array.Empty<BattleAdminRelic>()) },
                reason: "Twenty-round UI verification: durable hero only")).Accepted, host.Message);
            yield return Idle();
            string trace = Path.Combine(Output, "boss-twenty-round-trace.txt");
            File.WriteAllText(trace, "Unity " + Application.unityVersion + " / " + SystemInfo.graphicsDeviceType +
                " / render " + Screen.width + "x" + Screen.height + "\n" +
                "Fixture: player health/maxHealth=100000; no relics; boss, hands, round limit and presentation unchanged.\n" +
                "round,adventurePhase,duelPhase,playerHP,bossHP,revision\n");
            var enemyPhases = new HashSet<int>();
            var completedSnapshots = new HashSet<int>();
            Action<global::BattlePresentation.DisplayUpdate> observe = update => {
                var snapshot = update.Current;
                if (snapshot.Phase == "enemy") enemyPhases.Add(checked((int)snapshot.CompletedRounds));
                int completed = checked((int)snapshot.CompletedRounds);
                if (completed > 0 && completedSnapshots.Add(completed))
                    File.AppendAllText(trace, "presented," + completed + "," + snapshot.Phase + "," +
                        snapshot.Unit(host.Adventure.PlayerId.Value).Health + "\n");
            };
            view.PresentationDirector.Display.Changed += observe;
            double presentationFinishedAt = -1;
            int clicks = 0;
            try
            {
                while (host.Session.State.Duel.CompletedRounds < 20 && clicks < 40)
                {
                    long previousRound = host.Session.State.Duel.CompletedRounds;
                    AssertPhase(AdventurePhase.Battle);
                    Assert.IsFalse(host.IsBusy);
                    if (previousRound == 19) yield return CaptureWrathProof("boss-round-20-before-end-turn");
                    Assert.IsTrue(view.CanEndTurn);
                    Assert.IsTrue(hud.EndTurnButton.isActiveAndEnabled);
                    Assert.IsTrue(hud.EndTurnButton.interactable);
                    long revision = host.Revision;
                    yield return ClickButton(hud.EndTurnButton);
                    clicks++;
                    Assert.Greater(host.Revision, revision, "Real raycast/click must submit end-turn after round " + previousRound);
                    var resolution = view.LastResolution;
                    File.AppendAllText(trace, "click," + clicks + ",completed=" + previousRound + "->" +
                        host.Session.State.Duel.CompletedRounds + ",steps=" + string.Join("|", resolution.Steps.Select(step =>
                            step.Action + ":" + step.Before.Duel.CompletedRounds + "->" + step.Transition.State.Duel.CompletedRounds)) + "\n");
                    float deadline = Time.realtimeSinceStartup + 40;
                    while (view.IsPresenting && Time.realtimeSinceStartup < deadline)
                    {
                        int frame = Time.frameCount;
                        yield return null;
                        Assert.Greater(Time.frameCount, frame, "The test runner must advance a frame instead of draining a timed-out coroutine synchronously.");
                    }
                    Assert.IsFalse(view.IsPresenting, "Enemy playback timeout after completed round " + previousRound);
                    Assert.IsNull(view.PresentationDirector.LastError);
                    Assert.AreSame(host.Session.State, view.PresentedState);
                    long completed = host.Session.State.Duel.CompletedRounds;
                    if (completed == 20)
                    {
                        presentationFinishedAt = Time.unscaledTimeAsDouble;
                        break;
                    }
                    yield return Idle();
                    if (completed == previousRound)
                        Assert.AreEqual(BattlePlayerWindow.Ending, host.Session.State.FlowFor(host.Adventure.PlayerId).Window,
                            "A click without an enemy phase is only valid for the explicit turn-ending decision.");
                    else
                    {
                        Assert.That(completed - previousRound, Is.InRange(1L, 2L),
                            "The binding hand may skip one player window and play the following enemy phase automatically.");
                        AssertPhase(AdventurePhase.Battle);
                        Assert.IsFalse(VisibleFailureForRoundLimit());
                        if (completed == 1 || completed == 19) yield return CaptureWrathProof("boss-completed-round-" + completed);
                    }
                }
            }
            finally { view.PresentationDirector.Display.Changed -= observe; }
            Assert.AreEqual(20, host.Session.State.Duel.CompletedRounds);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 20), completedSnapshots,
                "All twenty completed-round boundaries must be presented, including rounds during player stun.");
            AssertPhase(AdventurePhase.Failed);
            Assert.AreEqual(ScoredDuelPhase.Lost, host.Session.State.Duel.Phase);
            Assert.AreEqual("RoundLimit", view.Display.Labels["resultReason"]);
            Assert.Greater(host.Adventure.Health, 0, "This must be a timeout loss, not player death.");
            Assert.AreEqual(2000, view.Enemies[0].DisplayUnit.Health);
            Assert.IsTrue(view.Enemies.All(actor => actor.DisplayUnit.IsAlive));
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 20), enemyPhases,
                "All twenty distinct enemy phases must pass through actual presentation snapshots.");
            Assert.GreaterOrEqual(presentationFinishedAt, 0);
            Assert.IsTrue(host.IsBusy);
            Assert.IsTrue(view.isActiveAndEnabled);
            Assert.IsFalse(VisibleFailureForRoundLimit());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Output, "boss-round-20-result-delay.png")));
            while (Time.unscaledTimeAsDouble - presentationFinishedAt < 1.2)
            {
                Assert.IsTrue(host.IsBusy, "Navigation must remain locked during the result delay.");
                Assert.IsTrue(view.isActiveAndEnabled, "Battle must remain visible during result delay.");
                Assert.IsFalse(VisibleFailureForRoundLimit());
                int frame = Time.frameCount;
                yield return null;
                Assert.Greater(Time.frameCount, frame, "Result delay must advance in real Unity frames.");
            }
            float resultDeadline = Time.realtimeSinceStartup + 5;
            while (host.IsBusy && Time.realtimeSinceStartup < resultDeadline) yield return null;
            Assert.IsFalse(host.IsBusy, "Failure UI did not open after the real result delay.");
            double delay = Time.unscaledTimeAsDouble - presentationFinishedAt;
            Assert.That(delay, Is.InRange(1.4, 2.2), "Actual end-of-playback to failure-page delay");
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsFalse(view.isActiveAndEnabled);
            Assert.IsTrue(VisibleFailureForRoundLimit(), "The failure page must have visible artwork or fallback text.");
            Assert.AreEqual(0, ActiveEnemyHudCount());
            Assert.IsTrue(screen.GetComponentsInChildren<TMP_Text>().Any(label => label.isActiveAndEnabled && label.text.Contains("/ 7 站")));
            var home = screen.GetComponentsInChildren<Button>().Single(button => button.isActiveAndEnabled &&
                button.GetComponentInChildren<TMP_Text>(true)?.text == "返回首页");
            Assert.IsTrue(home.interactable);
            File.AppendAllText(trace, "enemyPresentationPhases=" + string.Join(",", enemyPhases.OrderBy(x => x)) +
                "\nresultReason=" + view.Display.Labels["resultReason"] + "\nresultDelaySeconds=" + delay.ToString("F3", CultureInfo.InvariantCulture) +
                "\nendTurnButtonClicks=" + clicks + "\nfailureLogoVisible=" + VisibleFailureForRoundLimit() + "\nreturnHomeInteractable=" + home.interactable + "\n");
            yield return CaptureWrathProof("boss-round-20-failure");
            yield return ClickButton(home);
            yield return null;
            Assert.IsTrue(host.IsAtMenu, "Real failure-page button must return to the main menu.");
            Assert.IsTrue(VisibleText("开始新旅程"));
            yield return CaptureWrathProof("boss-round-20-returned-home");
            File.AppendAllText(trace, "returnedToMainMenu=true\n");
        }

        private bool VisibleFailureForRoundLimit() => VisibleText("旅程结束") ||
            screen.GetComponentsInChildren<Image>().Any(image => image.isActiveAndEnabled &&
                image.name == "Failure" && image.sprite != null && image.sprite == DeskUISkin.Get("FailureLogo"));

    }
}
#endif
