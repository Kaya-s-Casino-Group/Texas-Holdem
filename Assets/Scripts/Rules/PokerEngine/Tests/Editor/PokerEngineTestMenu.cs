#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PokerEngine.Tests
{
    /// <summary>
    /// Runs the module's synchronous NUnit methods in the current Editor scene, without saving or replacing it.
    /// Like the offline runner, this deliberately supports only parameterless Test fixtures without lifecycle hooks.
    /// Use Unity Test Runner for fixtures requiring UnityTest, setup, teardown or parameterization.
    /// </summary>
    public static class PokerEngineTestMenu
    {
        private static Queue<MethodInfo> pending;
        private static Summary summary;
        private static XElement report;
        private static Stopwatch clock;
        private static string outputFolder = "PokerEngineTests";
        private static string OutputDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/" + outputFolder));

        public static void Run() => RunSuite(Assembly.Load("PokerEngine.Tests.Editor"), "PokerEngineTests");

        public static void RunPresentation() => RunSuite(Assembly.Load("CardPresentation.Tests.Editor"), "CardPresentationTests");

        public static void RunFlowEditor() => RunSuite(Assembly.Load("Poker.RunFlow.Tests.Editor"), "RunFlowEditorTests");

        public static void RunDrawBattle() => RunSuite(Assembly.Load("Poker.DrawBattle.Tests.Editor"), "DrawBattleTests");

        public static void RunSingleBattleActors() => RunSuite(Assembly.Load("Poker.SingleBattle.Tests.Editor"), "SingleBattleActorTests");

        public static bool IsRunning => pending != null;
        public static string Status => summary?.status;
        public static int Total => summary?.total ?? 0;
        public static int Passed => summary?.passed ?? 0;
        public static int Failed => summary?.failed ?? 0;
        public static string CurrentTest => summary?.currentTest;
        public static string Error => summary?.error;
        public static double Seconds => clock?.Elapsed.TotalSeconds ?? 0;
        public static string ReportDirectory => OutputDirectory;
        public static event Action Updated;
        private static bool ownsReloadLock;
        public static MethodInfo[] FindTests(string assemblyName, params string[] fixtureNames) => Assembly.Load(assemblyName).GetTypes()
            .Where(type => type.IsDefined(typeof(TestFixtureAttribute), true) && (fixtureNames.Length == 0 || fixtureNames.Contains(type.Name)))
            .SelectMany(type => type.GetMethods()).Where(method => method.IsDefined(typeof(TestAttribute), true))
            .OrderBy(method => method.DeclaringType.FullName).ThenBy(method => method.Name).ToArray();
        public static void RunSelection(string assemblyName, string folder, params string[] fixtureNames) =>
            RunSuite(Assembly.Load(assemblyName), folder, fixtureNames);
        private static void RunSuite(Assembly assembly, string folder, params string[] fixtureNames)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后运行编辑模式测试。");
            if (pending != null) throw new InvalidOperationException("已有测试运行中，请等待结束或停止当前测试。");
            var fixtures = assembly.GetTypes().Where(t => t.IsDefined(typeof(TestFixtureAttribute), true) &&
                (fixtureNames.Length == 0 || fixtureNames.Contains(t.Name))).ToArray();
            var methods = fixtures.SelectMany(t => t.GetMethods()).ToArray();
            string[] unsupported = { "SetUpAttribute", "TearDownAttribute", "OneTimeSetUpAttribute", "OneTimeTearDownAttribute", "TestCaseAttribute", "TestCaseSourceAttribute", "UnityTestAttribute", "UnitySetUpAttribute", "UnityTearDownAttribute", "IgnoreAttribute", "ExplicitAttribute" };
            if (fixtures.Any(t => t.GetCustomAttributes(true).Any(a => unsupported.Contains(a.GetType().Name))) ||
                methods.Any(m => m.GetCustomAttributes(true).Any(a => unsupported.Contains(a.GetType().Name))))
                throw new InvalidOperationException("此测试组需要完整 Unity Test Runner，请从测试面板打开。");
            var tests = methods.Where(m => m.IsDefined(typeof(TestAttribute), true)).OrderBy(m => m.DeclaringType.FullName).ThenBy(m => m.Name).ToArray();
            if (tests.Length == 0 || tests.Any(m => m.GetParameters().Length != 0 || m.ReturnType != typeof(void)))
                throw new InvalidOperationException("此入口仅支持同步、无参数的 NUnit 测试。");
            outputFolder = folder; Directory.CreateDirectory(OutputDirectory);
            summary = new Summary { runId = Guid.NewGuid().ToString("N"), status = "running", total = tests.Length };
            report = new XElement("test-run", new XAttribute("id", summary.runId)); clock = Stopwatch.StartNew();
            pending = new Queue<MethodInfo>(tests); SaveProgress();
            EditorApplication.LockReloadAssemblies(); ownsReloadLock = true;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick; Updated?.Invoke();
        }
        public static void Cancel()
        {
            if (pending == null) return;
            summary.error = "测试已停止，未执行项不计为通过。"; Finish("Cancelled");
        }
        private static void BeforeReload() => Cancel();
        private static void OnPlayMode(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode) Cancel(); }
        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { Cancel(); return; }
            try
            {
                var method = pending.Dequeue(); summary.currentTest = method.DeclaringType.FullName + "." + method.Name; SaveProgress();
                var started = Stopwatch.StartNew(); var entry = new XElement("test-case", new XAttribute("fullname", summary.currentTest));
                try { method.Invoke(Activator.CreateInstance(method.DeclaringType), null); summary.passed++; entry.SetAttributeValue("result", "Passed"); }
                catch (Exception error)
                {
                    summary.failed++; entry.SetAttributeValue("result", "Failed");
                    entry.Add(new XElement("failure", new XElement("message", (error.InnerException ?? error).ToString())));
                }
                entry.SetAttributeValue("duration", started.Elapsed.TotalSeconds); report.Add(entry); SaveProgress();
                if (pending.Count == 0) Finish();
            }
            catch (Exception error) { summary.failed++; summary.error = error.ToString(); Finish(); }
        }
        private static void Finish(string terminalStatus = null)
        {
            EditorApplication.update -= Tick; pending = null;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            try
            {
                summary.status = terminalStatus ?? (summary.failed == 0 ? "Passed" : "Failed"); summary.currentTest = null; SaveProgress();
                report.SetAttributeValue("result", summary.status); report.SetAttributeValue("total", summary.total);
                report.SetAttributeValue("passed", summary.passed); report.SetAttributeValue("failed", summary.failed); report.SetAttributeValue("duration", summary.seconds);
                new XDocument(report).Save(Path.Combine(OutputDirectory, "results.xml"));
            }
            finally { if (ownsReloadLock) { ownsReloadLock = false; EditorApplication.UnlockReloadAssemblies(); } Updated?.Invoke(); }
        }
        private static void SaveProgress()
        { summary.seconds = clock.Elapsed.TotalSeconds; File.WriteAllText(Path.Combine(OutputDirectory, "status.json"), JsonUtility.ToJson(summary, true)); Updated?.Invoke(); }
        [Serializable] private sealed class Summary
        {
            public string runId;
            public string status;
            public int total;
            public int passed;
            public int failed;
            public string currentTest;
            public string error;
            public double seconds;
        }
    }
}

#endif
