using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    [InitializeOnLoad]
    public sealed class PresentationValidationRunner : ICallbacks
    {
        private static readonly TestRunnerApi Api;
        private const string Folder = "Library/BattlePresentationValidation";
        static PresentationValidationRunner()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new PresentationValidationRunner());
        }
        [MenuItem("工具/卡牌战斗/表现时间线/验证 EditMode")]
        public static void RunEditMode()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/status.txt", "Running EditMode");
            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                assemblyNames = new[] { "CardArt.Tests.Editor", "Poker.BattleHosting.Tests.Editor", "PokerEngine.Integration.Tests.Editor", "BattlePresentation.Tests.Editor", "BattlePresentation.Unity.Tests.Editor", "Poker.BattlePresentation.Shared.Tests", "Poker.SingleBattle.Tests.Editor", "Poker.Battle2DValidation.Tests.Editor", "CardPresentation.Tests.Editor", "CardMatch.Tests.Editor" } }));
        }
        [MenuItem("工具/卡牌战斗/表现时间线/验证 PlayMode")]
        public static void RunPlayMode()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/status.txt", "Running PlayMode");
            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                assemblyNames = new[] { "Poker.SingleBattle.Tests.PlayMode", "Poker.Battle2DValidation.Tests.PlayMode" } }));
        }
        [MenuItem("工具/卡牌战斗/表现时间线/验证出牌弃牌顺序")]
        public static void RunCardSequence()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/status.txt", "Running card sequence PlayMode");
            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                testNames = new[] { "Poker.SingleBattle.Tests.SceneTimelinePlaybackTests.AuthoredSceneUsesTimelineForOpeningSelectionPlayDiscardAndCancellation" } }));
        }
        [MenuItem("工具/卡牌战斗/暴怒美术/验证 EditMode")]
        public static void RunWrathArtworkEditMode()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/status.txt", "Running wrath artwork EditMode");
            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "Poker.SingleBattle.Tests.WrathBossArtworkTests", "Poker.SingleBattle.Tests.WrathBossSceneTests",
                    "Poker.Adventure.Tests.AdventureBattleLayoutTests" } }));
        }
        [MenuItem("工具/卡牌战斗/暴怒美术/验证 PlayMode")]
        public static void RunWrathArtworkPlayMode()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/status.txt", "Running wrath artwork PlayMode");
            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                testNames = new[] {
                    "Poker.SingleBattle.Tests.WrathBossScenePlaybackTests.SavedSceneBindsSixActorsAndShowsInitialAttackEightIntentsAndOneOpening",
                    "Poker.SingleBattle.Tests.WrathBossScenePlaybackTests.HudSelectedHandDiesAtImpactShowsRedEightAndEnemyEntryFloatThenRegeneratesAsSameActor",
                    "Poker.SingleBattle.Tests.WrathBossScenePlaybackTests.BindingAtTwentyFixtureLetsRealHandPlayCancelWarningAndRestartClearsOldHud",
                    "Poker.SingleBattle.Tests.SceneTimelinePlaybackTests.AuthoredSceneUsesTimelineForOpeningSelectionPlayDiscardAndCancellation" } }));
        }
        [MenuItem("工具/卡牌战斗/暴怒美术/验证七站 Boss PlayMode")]
        public static void RunAdventureWrathArtwork()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/status.txt", "Running adventure wrath artwork PlayMode");
            Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                testNames = new[] { "Poker.Adventure.Tests.SingleChapterAdventureSceneTests.SevenStationBossSeversAndRegeneratesFiveOriginalActorsWithPsdArtwork" } }));
        }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/results.xml", result.ToXml().OuterXml);
            File.WriteAllText(Folder + "/status.txt", result.ResultState + " passed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount);
        }
    }
}
