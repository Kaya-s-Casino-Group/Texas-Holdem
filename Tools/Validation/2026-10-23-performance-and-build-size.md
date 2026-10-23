# SingleChapterAdventure 性能与包体排查

## 优化完成与验收（2026-10-23）

交付包：`Builds/macOS-20261023-162414-Verified/Pocket Poker.app`；压缩包为同目录 `Pocket Poker.zip`。构建只包含 `Assets/Scenes/SingleChapterAdventure.unity`，StandaloneOSX / BuildOptions.None，Succeeded，0 errors。已按完整路径启动正式包，确认进入 Shuffled Fate 单章七站主菜单。正式程序集不包含验证驱动 `PerformanceValidationPlayer`。原始构建的 Unity 内购插件资源封签不一致，已在独立副本重做 ad-hoc 签名，并通过 `codesign --verify --deep --strict`；Data 内 188 个文件逐一 SHA-256 相同，游戏代码与资源未改。交付链接使用 Verified 副本，当前打开的游戏未关闭或覆盖。此签名校验不等于 Developer ID 公证。

| 同口径实际大小 | 优化前 | 优化后 |
|---|---:|---:|
| App 内所有文件 | 848.51 MiB | 459.25 MiB |
| ZIP | 272.28 MiB | 159.50 MiB |
| BuildReport 贴图 | 719.49 MiB | 330.35 MiB |
| StylizedRuins 旧背景组 | 102.01 MiB | 7.33 KiB（仅保留仍被使用的 Shader） |

App 减少 **45.88%**。当前大头仍是角色/Boss贴图约185.69 MiB；没有为了进一步压缩而改变 Boss 透明点击所需的 CPU 可读性，或强行改变非规则贴图的宽高比。

### 实际修改

- 牌型指南只搜索当前选择的条件，切换时暂停其它搜索，关闭时取消，避免打开便计算20个条件。每次搜索推进从32节点改为1节点，在帧内约2ms搜索预算中调度；结果呈现和首次结算仍可能超过这段搜索预算。
- 只结算当前显示候选；同一状态和目标重开复用文案，状态/引擎/玩家/目标变化时失效。目标绑定合并到一次 PreviewHandGuide 调用，保留原有完整假设结算与正式出牌验证。
- 引擎复用已验证分类和评分事实，减少子集枚举与集合包装分配；四条搜索新增必要条件，当前前缀即使补足剩余牌也无法达到四张同点数时，提前排除，不进入完整分类。保持其它牌型与大于5张的原有边界。
- 两套 HUD 布局仅在尺寸、安全区、配置、层级或相关展示数据变化时应用；固定遗物背景的层级顺序。排序文案与选中色统一由按钮视图持有，避免停止每帧重排后被旧文案覆盖。
- HUD 未选牌时跳过出牌/弃牌规则查询；两按钮复用同次捕获的选择。候选先检查状态、玩家、目标、输入可用性和选牌集合，再解析目标；同 revision 的新快照也会失效。正式提交继续完整验证。
- 桌面启用 VSync 跟随显示器刷新率，消除无上限跑帧；移动端原有60FPS设置保留。
- 只从发布场景移除9个已替换且停用的背景根，保留源素材及其它场景。120张明确白名单贴图按图标512、普通UI1024、角色/背景2048等角色上限设置桌面导入，同步修复生成工具，防止重新制作界面时覆盖设置。
- 55张贴图实际导入为BC7；65张NPOT仍为RGBA32，按实际导入结果记录，没有把配置标签当成压缩成功。精英立绘、Boss光晕、普通/精英背景四图关闭mipmap后，实证尺寸不变且实际BC7；其余NPOT未强制POT变形，Boss可读性保留。

### 性能证据及边界

相同内容/seed的 Unity Editor Mono 微基准：84个五张牌预览样本中位数 **15.26→5.89ms**，P95 **25.30→9.97ms**。独立.NET四类候选预览的托管分配减少81.6–84.9%。这些都是调用微基准，不能当成发布包帧率提升比例。

四条追加剪枝的独立.NET基准（同实际局面，seed312，10轮预热+30轮测量）：首轮4000节点/无候选/82,573,906B/47.250ms，变为2097节点/1候选/49,504B/0.141ms；扩展12000节点/2候选但截断/255,479,462B/146.989ms，变为5053节点/相同2候选且完整/150,838B/0.470ms。状态引用保持不变。

独立 macOS 验证使用源场景副本，仅关闭自动存档并编入显式测试驱动，正式构建不包含驱动。Apple M2 Max / macOS27.0.1 / Metal / Unity2022.3.62f3 / 2340×1080，VSync=1。每阶段约3秒；时间来自真实 Player 的 Time.unscaledDeltaTime，包含显示同步等待，**不是 CPU/GPU Profiler 耗时，也没有原始旧发布包的同场景帧率基线**。此设备观察到约90Hz的帧间隔；不推论其它硬件都能达到此帧率。

| 阶段 | P50 ms | P95 ms | P99 ms | 最大 ms | GC计数增量 |
|---|---:|---:|---:|---:|---:|
| 关窗闲置 | 11.10 | 12.13 | 12.42 | 15.56 | 11 |
| 首次打开高牌 | 11.11 | 12.13 | 12.38 | 50.01 | 3 |
| 切换四条 | 11.10 | 12.09 | 12.23 | 24.69 | 4 |
| 切换葫芦 | 11.10 | 12.01 | 12.29 | 24.72 | 4 |
| 切换顺子 | 11.11 | 12.01 | 15.96 | 27.77 | 5 |
| 开启同花筛选 | 11.11 | 11.29 | 12.55 | 24.75 | 4 |
| 关闭窗口 | 11.10 | 11.87 | 12.24 | 22.37 | 8 |
| 重新打开 | 11.10 | 12.27 | 12.62 | 13.58 | 1 |

全流程完成，无错误日志，指南没有改变权威战斗状态；四条和葫芦截图已核对。首次打开仍有一次约50ms长帧，尚未用调用栈确定原因，不能声称完全没有卡顿。重复打开最大13.58ms。本次同VSync的中间版本对照中，三秒四条阶段GC计数200→4，闲置22→11；GC.CollectionCount是运行时计数，**不是停顿次数/停顿耗时**。非Development Player的分配计数器不可用，JSON中 allocationCounterAvailable=false，分配字节为0不能解释成零分配。

### 回归与复现

Unity相关44项全通过：牌型窗口2、目标缓存1、布局4+2、按钮9、输入规则6、资源策略11、冒险战斗布局5、Boss分层/透明点击4。引擎分类/预览相关141项通过，四条剪枝相关29项通过（其中7项新增）。引擎全量1777项中1764通过、13失败；这13项已在只撤回本次引擎优化、保留原有用户修改的隔离副本中复现，失败名称与消息完全一致，**并非宣称全量全绿**。

证据目录 `output/performance-optimization-2026-10-23/`：正式 BuildReport CSV、package-comparison.json、preview-comparison.json、44项Unity测试XML、engine/基线失败对照、four-kind/基准和测试、player-final/帧时间和截图。资源设置实测位于 `output/build-asset-optimization/20261023-073045.json`，副本压缩实验位于同目录 `compression-probe-20261023-072746.json`。发布场景修改前备份保留在证据目录。

可用菜单 `Tools/Performance/Build Isolated Adventure Validation` 重建隔离验证包，执行该包的 `Contents/MacOS/Pocket Poker --poker-performance-output <绝对输出目录>`；菜单只用于诊断。正式构建仍使用 `工具/构建发布/macOS/构建游戏`。临时微基准Editor探针已移出Assets保存在证据目录，临时验证应用已清理，源码、正式App/ZIP和测量证据保留。

---

以下为优化前的原始排查记录，行号和当时状态仅描述排查时版本。

2026-10-23。排查目标是 `Assets/Scenes/SingleChapterAdventure.unity` 的 macOS 包。

## 范围与证据

- 已核对最新构建 `Builds/macOS-20261023-144233/Pocket Poker.app`，build-status 明确为 `Succeeded: Assets/Scenes/SingleChapterAdventure.unity; errors=0`；BuildReport 为 StandaloneOSX、BuildOptions.None。
- 已按完整路径打开该包，确认是 Shuffled Fate 单章七站版本。此前按应用名打开的旧版扑克桌应用不作为本次证据。
- 包体数据来自该包的实际文件与 Unity LastBuild.buildreport，不是 Assets 源文件大小。
- 性能数据来自当前源码/单章实际配置的 Unity Editor Mono 微基准，**不是发布包内的帧率、CPU/GPU Profiler 捕获**。不能把这些毫秒数直接当作发布包帧时间。
- 本次没有修改玩法、场景或资源导入设置；临时 Editor 探针测完已移出 Assets，保留在证据目录供复现。

## 1. 牌型指南：同步计算和重复结算是首要问题

### 实测

Apple M2 Max / macOS 27.0.1 / Unity 2022.3.62f3 Editor Mono。使用 SingleChapterBattle.xml 与 SingleChapterAdventure.xml，fool、battle-1，seed 312 / 1；一轮预热后各测两轮。搜索每次 MoveNext 独立计时，帧间等待不计入 CPU 时间；预览按 UI 的双 PreviewHandGuide、目标绑定与 BattleHandGuideText 构造路径计时。

| 指标 | 结果 |
|---|---:|
| 五张牌候选预览，84 个样本中位数 | 15.26 ms |
| 五张牌候选预览 P95 / 最大值 | 25.30 / 56.45 ms |
| 单次搜索 MoveNext 最大值 | 31.94 ms |
| 每个初始快照全部条件搜索累计时间 | 4.40–4.54 秒 |
| 相应候选预览累计时间 | 0.34–0.57 秒 |

累计时间分散在很多帧，**不是一次阻塞四秒**；问题是很多不可中断步骤本身就超过声明的 2 ms 预算。编辑器调度和 GC 会影响尾部耗时，不能据此推算发布包 FPS。

独立 .NET 10.0.13 探针对四类五张候选各预热 20 次、采样 50 次：一次双预览/结算产生约 **2.80–2.99 MiB 临时托管分配**，尚不含文案和 UI。该数值只用于证明 MB 级分配，不能替代 Unity Mono 的精确 GC 数据。Unity 探针的 GC.GetAllocatedBytesForCurrentThread 恒返回零，此处视为不可用，原始 CSV 的零不能解释为无分配。

### 原因

1. `SingleBattleHud.HandGuide.Preload.cs:74` 只在 MoveNext 前检查 2 ms，不能中断其中的 32 节点批次；89–90 行的文案/结算/呈现也同步执行。`103–104` 的剩余文案预热在时间循环之外。
2. `CardDuelHandGuide.cs:80–81` 每个有效搜索叶子完整分类。当前 XML 开启 combineCompatibleEffects，五张牌分类会遍历 31 个子集，通常至少调用 62 次基础分类。
3. `SingleBattleHud.HandGuide.cs:300、313` 同一候选先预览一次，再预览并执行完整假设结算。正常有效路径重复分类、BuildHandFacts 与评分。可共享已求出的分类、手牌事实、分数和效果序列。
4. 打开窗口就排队 10 种牌型 × 两种同花条件；找到一个示例后仍从头扩展至六个。用户没查看的条件也执行结算。相同快照的本次测试累计花费约 4.7–5.1 秒计算。
5. 所有查询和文案完成后仍每帧扫描结果缓存；关窗清空文案，重开重新结算。轮换候选还可能在同帧另生成一条未缓存文案。

卡牌 UI 已池化，描边每帧主要更新活动候选的少量 quad；未发现每帧销毁重建整窗的情况。

建议先只计算当前选择的牌型，按需生成当前方案的结算；复用分类/评分/效果事实。搜索切片应限制实际最坏耗时，后台预热必须可取消且受统一预算约束；查询完成即停止扫描，目标/快照未变则复用文案。复杂查询若迁移到后台，应先验证全部规则回调的线程安全，不能直接把当前逻辑整体 Task.Run。

## 2. 常驻 HUD：两套布局每帧互相覆盖

这是静态代码确认的重复工作，尚未单独测量其帧耗时。

- `DeskBattleLayout.cs:35` 每帧整套 Apply；69 行扫描 HUD 所有 TMP，189 行扫描遗物 Image 并生成数组。牌型 Modal 的文本也在扫描树内。
- `BattleAuthoredUILayout.cs:21、48–52` 随后再应用配置。当前配置有 292 个节点，Includes 后每帧仍遍历 277 项（含关闭的 DeckOverview），以及 72 项文本配置和四个 Canvas 配置。
- 确有同帧冲突：Desk 的 Place 把 Player.sizeDelta 清零，Authored 的配置再写回 Y=-11.422989；这会反复使布局失效，不只是重复赋相同值。
- 遗物栏至少 12 个背景每帧逐个 SetAsFirstSibling；行动点的三个节点同样反复移到首位。

建议明确唯一布局来源，静态布局仅在尺寸、配置、可见状态变化时应用；动态值按快照变化更新。先保留屏幕比例、安全区、桌面显示的现有行为，避免直接禁用整个脚本导致布局回归。

桌面还关闭了 VSync 且 targetFrameRate=-1，允许无上限运行，会放大空闲占用，但这本身不能证明低帧率。当前保存场景/管线没有启用主相机后处理、HDR 或 MSAA，没有证据将主要卡顿归因于这些效果。

## 3. 包体：719.49 MiB 是贴图

最新 App 实际文件约 **848.51 MiB**，同目录 ZIP 约 **272.28 MiB**；目录约 1.1 GiB 是两者之和。BuildReport 总构建大小 890,070,233 B（约 848.84 MiB），与签名后文件统计口径略有差异。

BuildReport 用户资源 751.53 MiB，其中贴图 **719.49 MiB，占 95.74%**；音频仅 11.14 MiB。下列是按资源路径分组的实际打包大小，字体组与贴图统计有交叉，不能再次相加：

| 资源组 | 入包大小 |
|---|---:|
| 角色 / Boss / 主角姿态 | 241.22 MiB |
| BattleConcept UI | 165.61 MiB |
| StylizedRuins 旧背景 | 102.01 MiB |
| Desk UI | 85.74 MiB |
| 字体 | 27.20 MiB |
| Underworld UI | 22.55 MiB |

### 已确认的浪费

- 大量 UI/角色导入为 4096 上限、无压缩。Elite 单张 30.03 MiB，Minion 29.17 MiB，四张主角图合计约 76 MiB。`ConceptBattleSetup.cs:34` 与 `AdventureArtworkSetup.cs:105` 会把这些设置重新写回，因此应同步修复生成工具，不能只改 meta。
- 牌型问号图标实际是 1254×1254 RGBA32，入包约 6 MiB。Standalone 的 max512 虽写在 meta 里，但 overridden=false，未生效。三个牌堆统计图标各约 6 MiB。
- MapPaper 导入设置虽为 CompressedHQ，**实际导入格式仍是 RGBA32**，2754×1766、入包 18.55 MiB。说明压缩效果必须检查实际平台格式和构建结果，不能仅看设置标签；具体回退机制本次未确认。
- 约 102.01 MiB 的旧 StylizedRuins 背景已被新背景替换，但场景只禁用节点、仍保留材质引用。15 个相关 MeshRenderer 均位于停用旧背景层下。`AdventureArtworkSetup.cs:49、124` 明确保留旧层并 SetActive(false)，构建报告确认旧贴图依然入包。
- Resources 中有大量旧 UI。一个 Scene 仍然会带入该 Scene 的引用依赖及 Resources 资源。迁移前须核对 Resources.Load，不能直接删目录。

本次 BuildMac 显式只构建 SingleChapterAdventure（PokerBuild.cs:117、137–138）；EditorBuildSettings 中启用的另一 Scene 没有进入这次构建，不是本包膨胀原因。

建议顺序：从发布场景拆除已替换旧背景引用 → 建立并验证角色/UI分平台尺寸与压缩规则 → 清理不再动态加载的旧 Resources → 最后优化字体。仅旧背景的依赖就涉及约 102 MiB，最终节省必须重新构建验证；压缩 ZIP 不能解决纹理运行时占用。

## 后续验收

使用明确路径的 SingleChapterAdventure macOS 包，固定分辨率和同一存档/种子，对比关窗、打开默认牌型、切换四条/葫芦/同花、全部预热完成及反复重开。采集帧时间 P50/P95/P99、GC分配/回收、CPU调用栈与实际纹理内存，再用同口径 BuildReport 比较体积。当前审查已定位问题，但还没有优化后的发布包或帧率改善数字。

原始证据：`output/performance-audit-2026-10-23/`，包含构建摘要、5519条打包资产记录、277条纹理导入记录、Unity查询/预览测量CSV、统计JSON、独立.NET分配探针及两个探针源码。
