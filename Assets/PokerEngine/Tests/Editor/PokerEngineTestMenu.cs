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

        [MenuItem("Poker/Tests/Run Core In Current Scene")]
        public static void Run() => RunSuite(typeof(PokerEngineTestMenu).Assembly, "PokerEngineTests");

        [MenuItem("Poker/Tests/Run Card Presentation In Current Scene")]
        public static void RunPresentation() => RunSuite(Assembly.Load("CardPresentation.Tests.Editor"), "CardPresentationTests");

        [MenuItem("Poker/Tests/Run Flow Editor In Current Scene")]
        public static void RunFlowEditor() => RunSuite(Assembly.Load("Poker.RunFlow.Tests.Editor"), "RunFlowEditorTests");

        private static void RunSuite(Assembly assembly, string folder)
        {
            if (pending != null) throw new InvalidOperationException("An Editor test run is already pending.");
            var fixtures = assembly.GetTypes().Where(t => t.IsDefined(typeof(TestFixtureAttribute), true)).ToArray();
            var methods = fixtures.SelectMany(t => t.GetMethods()).ToArray();
            string[] unsupported = { "SetUpAttribute", "TearDownAttribute", "OneTimeSetUpAttribute", "OneTimeTearDownAttribute", "TestCaseAttribute", "TestCaseSourceAttribute", "UnityTestAttribute", "UnitySetUpAttribute", "UnityTearDownAttribute", "IgnoreAttribute", "ExplicitAttribute" };
            if (fixtures.Any(t => t.GetCustomAttributes(true).Any(a => unsupported.Contains(a.GetType().Name))) ||
                methods.Any(m => m.GetCustomAttributes(true).Any(a => unsupported.Contains(a.GetType().Name))))
                throw new InvalidOperationException("This suite needs the full Unity Test Runner.");
            var tests = methods.Where(m => m.IsDefined(typeof(TestAttribute), true)).OrderBy(m => m.DeclaringType.FullName).ThenBy(m => m.Name).ToArray();
            if (tests.Length == 0 || tests.Any(m => m.GetParameters().Length != 0 || m.ReturnType != typeof(void)))
                throw new InvalidOperationException("Only synchronous parameterless NUnit tests are supported.");
            outputFolder = folder; Directory.CreateDirectory(OutputDirectory);
            summary = new Summary { runId = Guid.NewGuid().ToString("N"), status = "running", total = tests.Length };
            report = new XElement("test-run", new XAttribute("id", summary.runId)); clock = Stopwatch.StartNew();
            pending = new Queue<MethodInfo>(tests); SaveProgress();
            EditorApplication.LockReloadAssemblies(); EditorApplication.update += Tick;
        }
        private static void Tick()
        {
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
        private static void Finish()
        {
            EditorApplication.update -= Tick; pending = null;
            try
            {
                summary.status = summary.failed == 0 ? "Passed" : "Failed"; summary.currentTest = null; SaveProgress();
                report.SetAttributeValue("result", summary.status); report.SetAttributeValue("total", summary.total);
                report.SetAttributeValue("passed", summary.passed); report.SetAttributeValue("failed", summary.failed); report.SetAttributeValue("duration", summary.seconds);
                new XDocument(report).Save(Path.Combine(OutputDirectory, "results.xml"));
            }
            finally { EditorApplication.UnlockReloadAssemblies(); }
        }
        private static void SaveProgress()
        { summary.seconds = clock.Elapsed.TotalSeconds; File.WriteAllText(Path.Combine(OutputDirectory, "status.json"), JsonUtility.ToJson(summary, true)); }
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
