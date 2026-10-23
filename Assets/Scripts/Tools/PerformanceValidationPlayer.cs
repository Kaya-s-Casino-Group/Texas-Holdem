#if POKER_PERFORMANCE_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Poker.Adventure;
using Poker.SingleBattle;
using PokerEngine;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Only compiled into the explicit validation build, never the shipping player.
public sealed class PerformanceValidationPlayer : MonoBehaviour
{
    [Serializable] public sealed class Phase
    {
        public string name;
        public int frames, framesOver16ms, framesOver33ms, collections;
        public bool allocationCounterAvailable;
        public long totalAllocatedBytes, maxFrameAllocatedBytes;
        public double medianMs, p95Ms, p99Ms, maxMs, actionMs;
    }
    [Serializable] public sealed class Report
    {
        public string scene, unity, device, gpu, operatingSystem, error;
        public int width, height, vSyncCount, targetFrameRate;
        public bool completed, readOnlyGuide;
        public List<Phase> phases = new List<Phase>();
    }
    static string output;
    readonly Report report = new Report();
    AdventureSceneHost host;
    SingleBattleHud hud;
    bool finished;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Begin()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--poker-performance-output");
        if (index < 0 || index + 1 >= args.Length) return;
        output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
        var runner = new GameObject("Performance validation (isolated save)");
        DontDestroyOnLoad(runner); runner.AddComponent<PerformanceValidationPlayer>();
    }
    void Awake() { Application.runInBackground = true; Application.logMessageReceived += Log; }
    void OnDestroy() { Application.logMessageReceived -= Log; }
    void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(text + "\n" + stack);
    }
    IEnumerator Start()
    {
        var run = Run();
        while (!finished)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception e) { Finish(e.ToString()); yield break; }
            yield return current;
        }
    }
    IEnumerator Run()
    {
        float until = Time.realtimeSinceStartup + 45;
        do { host = FindObjectOfType<AdventureSceneHost>(); yield return null; }
        while ((host == null || host.IsLoading) && Time.realtimeSinceStartup < until);
        Require(host != null && !host.IsLoading, "Adventure startup timed out.");
        Require(!host.HasSavedRun, "Validation scene must have automatic saving disabled.");
        Require(host.NewRun("fool", 312), host.Message);
        Require(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "battle-1")), host.Message);
        until = Time.realtimeSinceStartup + 30;
        while (host.IsBusy && Time.realtimeSinceStartup < until) yield return null;
        Require(!host.IsBusy, "Opening presentation timed out.");
        hud = FindObjectOfType<SingleBattleHud>(); Require(hud != null, "Missing battle HUD.");
        report.scene = SceneManager.GetActiveScene().path; report.unity = Application.unityVersion;
        report.device = SystemInfo.processorType; report.gpu = SystemInfo.graphicsDeviceName;
        report.operatingSystem = SystemInfo.operatingSystem;
        report.width = Screen.width; report.height = Screen.height;
        report.vSyncCount = QualitySettings.vSyncCount; report.targetFrameRate = Application.targetFrameRate;
        // Let shader/font/UI startup settle before idle measurements.
        until = Time.realtimeSinceStartup + 2;
        while (Time.realtimeSinceStartup < until) yield return null;
        var state = host.Session.State;
        yield return Record("idle", null);
        yield return Record("guide-high-card", () => hud.OpenHandGuide());
        yield return Record("guide-four-kind", () => Select("FourOfAKind"));
        yield return Record("guide-full-house", () => Select("FullHouse"));
        yield return Record("guide-straight", () => Select("Straight"));
        yield return Record("guide-straight-flush", () => Select("Flush Filter"));
        yield return Record("guide-closed", () => hud.CloseHandGuide());
        yield return Record("guide-reopened", () => hud.OpenHandGuide());
        hud.CloseHandGuide();
        report.readOnlyGuide = ReferenceEquals(state, host.Session.State);
        Require(report.readOnlyGuide, "Guide changed the authoritative state.");
        report.completed = true; Finish(null);
    }
    IEnumerator Record(string name, Action action)
    {
        var phase = new Phase { name = name };
        long start = System.Diagnostics.Stopwatch.GetTimestamp(); action?.Invoke();
        phase.actionMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        int collections = GC.CollectionCount(0); var samples = new List<double>(600);
        using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
        {
            phase.allocationCounterAvailable = allocations.Valid;
            float until = Time.realtimeSinceStartup + 3;
            do
            {
                yield return null; samples.Add(Time.unscaledDeltaTime * 1000.0);
                if (allocations.Valid)
                {
                    long bytes = allocations.LastValue;
                    phase.totalAllocatedBytes += bytes;
                    phase.maxFrameAllocatedBytes = Math.Max(phase.maxFrameAllocatedBytes, bytes);
                }
            }
            while (!finished && Time.realtimeSinceStartup < until);
        }
        samples.Sort(); phase.frames = samples.Count;
        phase.medianMs = Percentile(samples, .5); phase.p95Ms = Percentile(samples, .95); phase.p99Ms = Percentile(samples, .99);
        phase.maxMs = samples.Last(); phase.framesOver16ms = samples.Count(ms => ms > 1000.0 / 60);
        phase.framesOver33ms = samples.Count(ms => ms > 1000.0 / 30); phase.collections = GC.CollectionCount(0) - collections;
        report.phases.Add(phase); Save();
        if (name == "idle" || name == "guide-four-kind" || name == "guide-full-house")
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            // Keep capture/PNG encoding out of the following phase's frame samples.
            for (int i = 0; i < 3; i++) yield return null;
        }
    }
    static double Percentile(List<double> sorted, double p) => sorted[(int)((sorted.Count - 1) * p)];
    void Select(string name)
    {
        var button = hud.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
        button.onClick.Invoke();
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    void Save() => File.WriteAllText(Path.Combine(output, "player-performance.json"), JsonUtility.ToJson(report, true));
    void Finish(string error)
    {
        if (finished) return;
        finished = true; report.error = error; Save(); Application.Quit(error == null ? 0 : 1);
    }
}
#endif
