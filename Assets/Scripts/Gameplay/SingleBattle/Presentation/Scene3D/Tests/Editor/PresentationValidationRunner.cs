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
