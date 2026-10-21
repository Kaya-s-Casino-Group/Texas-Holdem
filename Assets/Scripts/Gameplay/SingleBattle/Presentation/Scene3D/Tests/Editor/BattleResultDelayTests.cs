using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleResultDelayTests
    {
        [MenuItem("工具/局外冒险/验证对局结束延时")]
        public static void ValidateSceneDelay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("请在编辑模式运行验证。");
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter {
                testMode = TestMode.PlayMode,
                testNames = new[] { "Poker.Adventure.Tests.SingleChapterAdventureSceneTests" }
            }));
        }

        [Test] public void TerminalUiWaitsForAllPresentationThenOneAndAHalfSeconds()
        {
            var delay = new BattleResultDelay();
            Assert.IsFalse(delay.Ready(true, true, 0));
            Assert.IsFalse(delay.Ready(true, true, 10), "Rule completion alone must not start the timer.");
            Assert.IsFalse(delay.Ready(true, false, 11));
            Assert.IsFalse(delay.Ready(true, false, 12.49));
            Assert.IsTrue(delay.Ready(true, false, 12.5));
            Assert.IsTrue(delay.Ready(true, false, 13), "Repeated UI refreshes must not restart the delay.");
        }

        [Test] public void ResumedPresentationOrANewBattleCancelsThePreviousCountdown()
        {
            var delay = new BattleResultDelay();
            Assert.IsFalse(delay.Ready(true, false, 0));
            Assert.IsFalse(delay.Ready(true, true, 1));
            Assert.IsFalse(delay.Ready(true, false, 2));
            Assert.IsFalse(delay.Ready(true, false, 3));
            Assert.IsTrue(delay.Ready(true, false, 3.5));
            Assert.IsFalse(delay.Ready(false, false, 4));
            Assert.IsFalse(delay.Ready(true, false, 5));
            delay.Reset();
            Assert.IsFalse(delay.Ready(true, false, 7));
        }
    }
}
