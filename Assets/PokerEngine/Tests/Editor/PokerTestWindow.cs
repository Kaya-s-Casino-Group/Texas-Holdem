#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokerEngine.Tests
{
    /// <summary>Editor-only Chinese entry point. Runs selected tests explicitly and never automatically runs a full suite.</summary>
    public sealed class PokerTestWindow : EditorWindow
    {
        private sealed class Entry
        {
            public string Id, Title, Description, Assembly, Type, Method, Report, RequiredComponent;
            public string[] Fixtures = Array.Empty<string>();
            public bool Play, Capture, Legacy;
            public bool IsSuite => Method == null;
        }
        private static Entry Suite(string id, string title, string description, string assembly, params string[] fixtures) =>
            new Entry { Id = id, Title = title, Description = description, Assembly = assembly, Fixtures = fixtures, Report = "Library/" + id + "/status.json" };
        private static Entry Live(string id, string title, string description, string assembly, string type, string method, string report, string component, bool legacy = false) =>
            new Entry { Id = id, Title = title, Description = description, Assembly = assembly, Type = type, Method = method, Report = report,
                RequiredComponent = component, Play = true, Legacy = legacy, Capture = report.EndsWith(".png") };
        private static readonly Entry[] Focused = {
            Suite("CardArtworkPreview", "卡牌实例预览与光源", "自由参数组合、最小 Shader 协议、实例渲染与可控光源。", "CardArt.Tests.Editor", "CardArtworkPreviewTests", "CardShaderCombinationTests"),
            Suite("CardArtworkIntegration", "卡牌属性与共享美术", "状态映射、组合叠加、Run/战局投影、Canvas 与跨区保留。", "CardPresentation.Tests.Editor", "CardAppearanceTests", "CanvasDeckOverviewTests", "CardZoneViewPrefabExporterTests"),
            Suite("AttackContextTests", "攻击来源与触发依据", "正式出牌快照、提交/组成/计分范围、双方共用上下文与原因记录。", "PokerEngine.Tests.Editor", "AttackContextTests"),
            Suite("ScoredHandDamageTests", "出牌伤害与只读验算", "转换接口、逐目标请求、正式结算复用及状态/随机数不变。", "PokerEngine.Tests.Editor", "ScoredHandDamageTests"),
            Suite("ScoreProfileTests", "牌型筹码与倍率", "默认数值、单牌覆盖、成长、计分子集与配置隔离。", "PokerEngine.Tests.Editor", "ScoreProfileTests", "CardScoreQueryTests"),
            Suite("CardDuelTests", "战局发牌与四牌区", "空手初始化、补牌策略、牌区守恒、出牌与弃牌。", "PokerEngine.Tests.Editor", "CardDuelTests"),
            Suite("TeamBattleTests", "多人战局与阵营回合", "独立牌组、共享玩家阶段、怪物队列、死亡跳过与目标接口。", "PokerEngine.Tests.Editor", "TeamBattleTests"),
            Suite("PlayerTurnTests", "能量与回合", "独立能耗、超上限能量、自动与主动结束回合。", "PokerEngine.Tests.Editor", "PlayerTurnTests"),
            Suite("BattleSettlementTests", "生命、伤害与结算", "共用战斗状态、伤害换算、胜负及动作账本。", "PokerEngine.Tests.Editor", "CombatTests", "ScoredDuelTests"),
            Suite("CardZoneWindowTests", "实战牌区工具", "只读数据、表现参数、派生 View 和安全关闭。", "CardPresentation.Tests.Editor", "CardMatchHostTests", "CardZoneInheritanceTests", "PlayedZoneViewTests"),
            Suite("SingleBattleActorTests", "角色与表现协议", "玩家/怪物边界、等待表现、裂纹与能量权限。", "Poker.SingleBattle.Tests.Editor"),
            Suite("EditorIsolationTests", "编辑器隔离与打包边界", "测试程序集限定 Editor、Player 不引用测试、资源元数据。", "PokerEngine.Tests.Editor", "PackagingTests")
        };
        private static readonly Entry[] LiveChecks = {
            Live("team-battle-ui", "多人战局 2v3", "切换玩家、独立手牌、全员结束、怪物依次演出与整轮恢复。", "Poker.SingleBattle.Tests.Editor", "Poker.SingleBattle.Tests.TeamBattlePlayValidation", "Run", "Library/SingleBattleTests/team-play.json", "Poker.SingleBattle.SingleBattleHost"),
            Live("dealing", "单战局发牌专项", "真实按钮与动画：动作后立刻补满、下回合补满。", "Poker.SingleBattle.Tests.Editor", "Poker.SingleBattle.Tests.SingleBattlePlayValidation", "RunDealing", "Library/SingleBattleTests/dealing.json", "Poker.SingleBattle.SingleBattleHost"),
            Live("battle-ui", "单战局完整流程", "能量、连续出牌、怪物回合、胜负与重开。", "Poker.SingleBattle.Tests.Editor", "Poker.SingleBattle.Tests.SingleBattlePlayValidation", "Run", "Library/SingleBattleTests/play.json", "Poker.SingleBattle.SingleBattleHost"),
            Live("live-window", "实战牌区窗口", "读取当前牌局、调整表现参数、切换与关闭。", "CardPresentation.Tests.Editor", "CardPresentation.Tests.CardMatchHostTests", "ValidateLiveWindow", "Library/CardPresentationTests/live-validator.json", "Poker.SingleBattle.SingleBattleHost"),
            Live("deck-motion", "牌堆与总览动画", "牌堆图标、面板与提示的显示动画，保持牌局状态。", "CardPresentation.Tests.Editor", "CardPresentation.Tests.DeckOverviewPlayValidation", "ValidateMotion", "Library/DeckOverviewValidation/motion.json", "CardInteraction.DrawPilePresenter"),
            Live("draw-battle", "分数目标战局", "旧 Draw 玩法的按钮、计分、补牌和流程恢复。", "Poker.DrawBattle.Tests.Editor", "Poker.DrawBattle.Tests.DrawBattlePlayValidation", "Run", "Library/DrawBattleTests/play.json", "Poker.DrawBattle.DrawBattleHost")
        };
        private static readonly Entry[] Modules = {
            Suite("PokerEngineTests", "GameCore 全模块", "运行全部引擎规则；仅在需要广泛回归时使用。", "PokerEngine.Tests.Editor"),
            Suite("CardPresentationTests", "卡牌表现全模块", "布局、动画、输入、牌库与相关资源。", "CardPresentation.Tests.Editor"),
            Suite("RunFlowEditorTests", "关卡流程编辑器", "Run 配置、层与节点树资产。", "Poker.RunFlow.Tests.Editor"),
            Suite("DrawBattleTests", "分数目标战局逻辑", "Draw 玩法的场景接入与状态投影。", "Poker.DrawBattle.Tests.Editor"),
            Suite("CardArtTests", "卡牌美术", "卡面、图集与外观资源。", "CardArt.Tests.Editor"),
            Suite("RenderingTests", "渲染模块", "渲染配置与边界。", "RetroRendering.Tests.Editor"),
            Suite("EditorToolsTests", "编辑器通用工具", "窗口与通用编辑器组件。", "EditorTools.Tests.Editor")
        };
        private static readonly Entry[] Legacy = {
            Live("legacy-actions", "旧场景手牌按钮", "仅用于没有外部玩法权威的独立牌区测试场景。", "CardPresentation.Tests.Editor", "CardPresentation.Tests.HandActionPlayValidation", "Run", "Library/CardPresentationTests/hand-actions-play.json", "CardInteraction.HandActionPresenter", true),
            Live("legacy-overview", "旧场景牌库交互", "包含旧本地牌区抽牌，只允许在旧验证场景运行。", "CardPresentation.Tests.Editor", "CardPresentation.Tests.DeckOverviewPlayValidation", "Run", "Library/DeckOverviewValidation/play.json", "CardInteraction.DeckOverviewPresenter", true)
        };
        private static readonly Entry[] Captures = {
            Live("summary-capture", "牌堆悬停摘要", "生成当前 Game 画面截图。", "CardPresentation.Tests.Editor", "CardPresentation.Tests.DeckOverviewPlayValidation", "CaptureSummary", "Library/DeckOverviewValidation/hover-summary.png", "CardInteraction.DrawPilePresenter"),
            Live("hover-capture", "牌库与单牌提示", "生成当前牌库和提示的截图。", "CardPresentation.Tests.Editor", "CardPresentation.Tests.DeckOverviewPlayValidation", "CaptureHover", "Library/DeckOverviewValidation/hover.png", "CardInteraction.DeckOverviewPresenter")
        };
        private static Entry active;
        private static DateTime previousStamp;
        private static double started;
        private static string lastTitle, lastStatus, lastError, lastReport;
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private Vector2 scroll;
        [SerializeField] private int tab;
        [SerializeField] private bool showModules, showLegacy;
        public static bool IsBusy => active != null || PokerEngineTestMenu.IsRunning;

        [MenuItem("Tools/测试面板", false, 100)]
        public static void Open()
        {
            var window = GetWindow<PokerTestWindow>("测试面板"); window.minSize = new Vector2(520, 460); window.Show();
        }
        private void OnEnable()
        {
            titleContent = new GUIContent("测试面板"); EditorApplication.update += Refresh;
            PokerEngineTestMenu.Updated += RunnerUpdated; RunnerUpdated();
        }
        private void OnDisable() { EditorApplication.update -= Refresh; PokerEngineTestMenu.Updated -= RunnerUpdated; }
        private void RunnerUpdated()
        {
            if (!string.IsNullOrEmpty(lastTitle) && lastReport == "Library/" + new DirectoryInfo(PokerEngineTestMenu.ReportDirectory).Name + "/status.json")
            {
                if (!PokerEngineTestMenu.IsRunning)
                {
                    lastStatus = PokerEngineTestMenu.Status == "Passed" ? "通过" : PokerEngineTestMenu.Status == "Cancelled" ? "已停止" : "失败";
                    lastStatus += "（通过 " + PokerEngineTestMenu.Passed + "，失败 " + PokerEngineTestMenu.Failed + "）";
                    lastError = PokerEngineTestMenu.Error;
                }
            }
            Repaint();
        }
        private void Refresh() { if (IsBusy) Repaint(); }
        private void OnGUI()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("项目测试", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("仅在 Unity 编辑器中使用。默认运行选中的专项测试；测试代码与辅助组件不进入游戏构建。", MessageType.Info);
            EditorGUILayout.LabelField("当前模式", Application.isPlaying ? "实战运行（Play）" : "编辑模式");
            EditorGUILayout.LabelField("当前场景", SceneManager.GetActiveScene().name);
            DrawProgress();
            tab = GUILayout.Toolbar(tab, new[] { "编辑模式测试", "实战验证", "截图" });
            using (var area = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = area.scrollPosition;
                if (tab == 0)
                {
                    foreach (var entry in Focused) DrawEntry(entry);
                    showModules = EditorGUILayout.Foldout(showModules, "更多模块 / 全量回归", true);
                    if (showModules) foreach (var entry in Modules) DrawEntry(entry);
                }
                else if (tab == 1)
                {
                    EditorGUILayout.HelpBox("先进入相应玩法场景的 Play。验证可能临时调整测试数值，完成后由对应验证流程恢复。", MessageType.Info);
                    foreach (var entry in LiveChecks) DrawEntry(entry);
                    showLegacy = EditorGUILayout.Foldout(showLegacy, "旧验证场景（不用于实战牌局）", true);
                    if (showLegacy) foreach (var entry in Legacy) DrawEntry(entry);
                }
                else foreach (var entry in Captures) DrawEntry(entry);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开 Unity Test Runner")) EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
                if (GUILayout.Button("刷新测试数量")) counts.Clear();
            }
        }
        private void DrawEntry(Entry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string title = entry.Title;
                if (entry.IsSuite)
                {
                    if (!counts.TryGetValue(entry.Id, out int count))
                    {
                        try { count = PokerEngineTestMenu.FindTests(entry.Assembly, entry.Fixtures).Length; } catch { count = -1; }
                        counts[entry.Id] = count;
                    }
                    title += count >= 0 ? " · " + count + " 项" : " · 程序集未加载";
                }
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedMiniLabel);
                string reason = DisabledReason(entry);
                using (new EditorGUI.DisabledScope(reason != null))
                    if (GUILayout.Button(entry.Capture ? "生成截图" : "运行此项", GUILayout.Width(110))) StartEntry(entry);
                if (reason != null) EditorGUILayout.LabelField(reason, EditorStyles.wordWrappedMiniLabel);
            }
        }
        private void DrawProgress()
        {
            if (PokerEngineTestMenu.IsRunning)
            {
                EditorGUILayout.LabelField("运行中：" + lastTitle);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), (float)(PokerEngineTestMenu.Passed + PokerEngineTestMenu.Failed) / PokerEngineTestMenu.Total,
                    "通过 " + PokerEngineTestMenu.Passed + " / 失败 " + PokerEngineTestMenu.Failed + " / 共 " + PokerEngineTestMenu.Total);
                EditorGUILayout.LabelField("当前用例", PokerEngineTestMenu.CurrentTest, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("停止编辑模式测试")) PokerEngineTestMenu.Cancel();
            }
            else if (active != null) EditorGUILayout.LabelField("运行中：" + active.Title + "（等待验证报告）");
            else if (!string.IsNullOrEmpty(lastStatus)) EditorGUILayout.LabelField(lastTitle + "：" + lastStatus);
            if (!string.IsNullOrEmpty(lastError)) EditorGUILayout.HelpBox(lastError, MessageType.Warning);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastReport) || !File.Exists(lastReport)))
                if (GUILayout.Button("打开最近报告")) EditorUtility.RevealInFinder(Path.GetFullPath(lastReport));
        }
        private static bool HasComponent(string type) => Resources.FindObjectsOfTypeAll<MonoBehaviour>().Any(component => component != null &&
            component.isActiveAndEnabled && component.gameObject.scene.IsValid() && component.GetType().FullName == type);
        private static bool HasAuthority() => Resources.FindObjectsOfTypeAll<MonoBehaviour>().Any(component => component != null &&
            component.isActiveAndEnabled && component.gameObject.scene.IsValid() && component.GetType().FullName == "CardInteraction.CardMatchHost" &&
            component.GetType().GetProperty("Commands")?.GetValue(component) != null);
        private static string DisabledReason(Entry entry)
        {
            if (IsBusy) return "已有测试运行中。";
            if (EditorApplication.isCompiling) return "等待脚本编译完成。";
            if (EditorApplication.isPlayingOrWillChangePlaymode != Application.isPlaying) return "等待模式切换完成。";
            if (entry.Play != Application.isPlaying) return entry.Play ? "请进入对应玩法场景的 Play。" : "请退出 Play 后运行。";
            if (entry.Play && !HasComponent(entry.RequiredComponent)) return "当前场景缺少对应的玩法或 UI 组件。";
            if (entry.Legacy && HasAuthority()) return "检测到实战权威，请使用实战专项验证。";
            return null;
        }
        private static void StartEntry(Entry entry)
        {
            if (DisabledReason(entry) != null) return;
            lastTitle = entry.Title; lastError = null; lastStatus = "运行中"; lastReport = entry.Report;
            try
            {
                if (entry.IsSuite) PokerEngineTestMenu.RunSelection(entry.Assembly, entry.Id, entry.Fixtures);
                else
                {
                    active = entry; previousStamp = File.Exists(entry.Report) ? File.GetLastWriteTimeUtc(entry.Report) : DateTime.MinValue;
                    started = EditorApplication.timeSinceStartup; entry.Capture |= entry.Report.EndsWith(".png");
                    EditorApplication.update += WatchReport;
                    var method = Assembly.Load(entry.Assembly).GetType(entry.Type, true).GetMethod(entry.Method, BindingFlags.Public | BindingFlags.Static);
                    if (method == null) throw new InvalidOperationException("未找到验证入口。");
                    method.Invoke(null, null);
                }
            }
            catch (Exception exception)
            {
                StopWatching(); lastStatus = "未完成"; lastError = (exception.InnerException ?? exception).Message;
            }
        }
        private static void WatchReport()
        {
            if (active == null) { StopWatching(); return; }
            if (File.Exists(active.Report) && File.GetLastWriteTimeUtc(active.Report) > previousStamp)
            {
                if (active.Capture) { lastStatus = "截图已生成"; lastError = null; }
                else
                {
                    var result = JsonUtility.FromJson<Report>(File.ReadAllText(active.Report));
                    lastStatus = result.status == "Passed" ? "通过" : "失败"; lastError = result.error;
                }
                StopWatching();
            }
            else if (!Application.isPlaying || EditorApplication.timeSinceStartup - started > 135)
            { lastStatus = "已中断"; lastError = "未收到新的验证报告，请查看控制台。"; StopWatching(); }
        }
        private static void StopWatching() { active = null; EditorApplication.update -= WatchReport; }
        [Serializable] private sealed class Report { public string status, error; }
    }
}
#endif
