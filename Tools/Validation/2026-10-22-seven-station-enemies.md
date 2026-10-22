# 七站冒险接入流血精英与暴怒：验证记录

> 后续回归与代码同步记录见 `2026-10-22-wrath-final-regression.md`。后续已吸收主仓远端 `2a22e79` 的手牌排列、出牌归位与补牌顺序修复；下文保留本阶段的实际结果与取消记录，不代表后续专项仍未完成。

日期：2026-10-22。工作区：`/Users/mt/code-yang/Texas-Holdem`。

## 本轮范围

沿用已确认的本地新版规则，仅将七站 `SingleChapterBattle.xml` 的精英和暴怒定义替换为对应实现。保留 `summoner` / `wrath` 模板 ID、七站 encounter ID、`enemy` / `hand-1..5` 单位 ID，场景无需重新生成。

- 精英：400 生命，初始攻击 8，每完整轮 +10；施法/普攻交替，既有流血叠层与行动后衰减。
- 暴怒：1500 生命、四饮血手与一束缚手；初始 D=33，普攻/普攻/龟缩/蓄力，独立预警与追加大招，手死亡 R+8 原身份再生，本体死亡胜利、20 轮判负。
- 沿用远端 `Elite-Updated.png`、`Enemy.png`、FourBeasts 牌面、目标选择与 `BattleDemoAshPresenter`。`AdventureBattleLayout.cs` 恢复为远端逐字节相同的通用布局，六敌仍为三列两行等大网格；死亡与再生不重排。独立 Demo 的大面板与额外 Canvas 不在七站开启；再生、蓄力、免疫、大招预警复用远端 World HUD 的图标行及 tooltip。
- 普通敌人、牌型、道具、玩家、牌组、BattleRules 和经济保持远端原配置。`StandardAdventure.xml` 的旧三章暴食节点不变，`SingleChapterAdventure.xml` 地图也不改。

本轮修复全玩家倒地、替死复活之间回放帧的意图预测异常：该中间帧无存活目标时跳过攻击预测，继续投影已发生的事实，不改变权威结算。另将历史精英预览测试的 90/45 过期断言更新为当前规则的 8/4；同花测试读取 `IsFlush` 附加属性，不修改 ModernScoring 的牌型分类。

## 环境与实际结果

Unity 2022.3.62f3 (`96770f904ca7`)，Apple Silicon / Metal。使用当前已打开的图形 Editor，通过 Unity Test Runner API 顺序运行；没有对已占用项目并发启动 batch Editor。PlayMode 载入真实保存场景副本、真实宿主与异步时间线，使用临时存档，不覆盖玩家存档。

| 验证 | 实际结果 | 原始证据 |
| --- | --- | --- |
| .NET 新七站技能与原七站/经济 | 23/23 通过；新增 5 项先 RED 后 GREEN | `Library/SevenStationDotnet/` 的定向日志与 TRX |
| .NET 全规则 | 1692 项：1680 通过、12 项历史失败；无新增失败 | `Library/SevenStationDotnet/results/seven-station-full.trx` |
| Unity EditMode：场景、显示、布局、七站技能与经济 | 259/259 通过、0 跳过；含远端布局隔离与 9 项既有 World HUD 规则适配检查 | `Logs/seven-station-remote-editmode-complete.xml` / `2026-10-22-seven-station-evidence/unity-editmode.xml` |
| Unity PlayMode：七站、普通战、弃牌、精英、暴怒 | 最新执行 23 项通过；用户要求不再跑满 20 轮，已停止该测试及其后续 7 项，未报完整 31 项通过。前轮结果与修复见下文 | `Logs/seven-station-remote-playmode-final.progress` / `2026-10-22-seven-station-evidence/playmode-cancelled.json` |

EditMode 最终范围：`Poker.Adventure.Tests`、`Poker.SingleBattle.Tests`、`Poker.BattlePresentation.Tests`、`PokerEngine.Tests.SingleChapterEnemyRulesTests`、`SingleChapterContentTests`、`AdventureEconomyTests`。

PlayMode 范围：`SingleChapterAdventureSceneTests`、`WrathBossScenePlaybackTests`、`EliteBleedingScenePlaybackTests`、`SceneTimelinePlaybackTests`、`DiscardShieldPlaybackTests`。可在 Test Runner 选择以上 fixtures 重跑；关闭图形 Editor 后也可用同版本 Unity 的 `-runTests -testPlatform PlayMode -testFilter`，以分号连接完整 fixture 名，并指定 `-testResults`。不要把 .NET 结果当成 Unity Play 验收。

## 逐项覆盖与边界

| 项目 | 证据与验收方式 |
| --- | --- |
| 七站配置实际通路与完整流程 | 场景原 XML GUID → 本地 Engine 包；原完整七站按钮流程已通过，前两战实际出牌、50/60 金币、奖励与菜单恢复、商店/休息均回归。流程测试对精英/Boss 使用管理命令结束战斗；其技能另用真实战斗验证。 |
| 新精英四轮结算 | 规则回归通过；Play 另断言真实冒险目标 400 血及四轮伤害 4/36/21/95，沿用精英立绘。 |
| 流血净化、控制、替死与衰减 | 规则回归与独立精英 Play 场景覆盖；不调整普通状态或道具规则。 |
| 暴怒初始状态、常规意图、蓄力 | 规则回归通过；独立场景 Play 及七站目标场景检查原始生命、D 与真实阶段推进。 |
| 预警、追加大招与中断 | 规则覆盖第 5/6、11/12 轮、取消与冷却；独立 Play 默认配置观察预警与第 6 轮 43+129，替死立即终止追加大招。 |
| 五手消失与 R+8 再生 | 七站 Play 经真实手雷命令和异步灰烬播放，检查五手死亡 `Progress=1`、权威倒计时 8；第 9 轮同五个 Actor 复活，`Progress=0`、原位置、原立绘、既有 World HUD 再生徽记消失，D33→8→53。不以重建对象代替再生。 |
| 死手存档与菜单续玩 | 规则 Export/Restore 回归；七站 Play 在五手死亡后菜单往返，继续同一场并复用 Actor。 |
| 龟缩与牌型/道具互动 | 规则覆盖独立伤害段与控制；独立 Play 覆盖顺子、皇家强制击杀、手雷、葫芦、同花治疗。 |
| 本体死亡与 20 轮终局 | 规则回归通过；前轮独立 Play 的胜利/治疗/20轮权威断言已到达，但结果面板延迟断言失败，未报该用例通过。最新轮在 20 轮用例中按用户要求停止，未完成 20 轮实机复验及其后的胜利用例；仅保留已有规则层覆盖。耐久夹具不作为平衡数据。 |
| 普通战与远端表现 | 保留原 1/2/3/多敌布局；普通六敌 Editor 回归通过；普通出牌、弃牌、灰烬、转火、道具与回放在 Play 套件中回归。 |

上述 Play 项目的最终执行状态以结果表与 NUnit 文件为准；不把测试代码已经写出当成已通过。

尚未完成／本轮不扩展：正式手部专用美术与动画、所有分辨率和逐像素表现验收、移动设备/独立 Player 构建、正常构筑多种子胜率与平衡测量。已区分实际截图观察与自动断言；二者均不代表所有分辨率的完整视觉验收，也不据测试耐久夹具调整数值。

## 历史测试债

全量 .NET 的 12 项失败与交接基线同名同消息，均涉及既有溢出、回滚、容量或快照语义；本轮不改这些普通战斗行为。基线 13 项中的 `SnapshotRejectsChangedContentRevisionDigestAndIllegalBattleAction` 在拉取远端后已通过，不能归因于这次配置接入。

1. `AppendedOverflowRollsBackDamageCardsAndResources`
2. `CreationHasNoHandAndFirstTurnDealsExactlyOnceWithoutSpendingEnergy`
3. `FailureInALaterEnemyReactionRollsBackTheEntireSessionIncludingEarlierReactions`
4. `InvalidBoundaryBatchRollsBackTheEntireStart(True)`
5. `InvalidCompletionBatchRollsBackEarlierDamageAndActionPayment(True)`
6. `InvalidEffectiveCapacityFailsAtBattleCreation(HandLimit,-8)`
7. `LateFailureRollsBackExpiryControlConsumptionDamageEnergyScoreAndCardMovement`
8. `LaterFailureRollsBackImmunityConsumptionAndEarlierDamage`
9. `NumericOverflowInTheProviderOrAReactionRollsBackEveryFact(False)`
10. `PrimaryOutgoingUsesBatchStartAndIncomingUsesLatestStateExactlyOnce`
11. `RewardOverflowRollsBackCostsCardsAndRandomState`
12. `TriggerEffectOverflowRollsBackHealthCardsBudgetAndReports`

## 工作区保护

开始前已完整备份两仓所有未提交文件、二进制 diff、未跟踪文件和 SHA-256 清单：`/Users/mt/.codex/backups/Texas-Holdem/seven-station-20261022-112447/`。本轮没有 reset、clean、stash、提交、推送或强制子模块更新；原远端拉取备份也保留。最终按该清单核查已有文件；Unity 测试产生的动态字体缓存变化会从备份恢复到本轮开始前的已有修改版本，不覆盖用户原有修改。

七站战斗内容指纹随配置版本变化。已有旧七站存档会按原机制提示不匹配并保留文件，本轮没有增加静默迁移或重开。

## 用户指出的表现偏差与修正

本轮最初误将本地独立验证版的“一大本体＋下方五小怪”与大块暴怒专用面板带入了七站。用户明确要求远端表现为准后，已撤回该布局并隔离面板。该偏差不是远端原布局，也不能称为完成了五只手美术。

本阶段原远端基准为 `491a479` / BattleToolkit `b034858`，后续已快进主仓 `2a22e79`；新提交同样没有独立五手模型或贴图，六个逻辑单位共用 `Enemy.png`。规则上的四饮血手、一束缚手与本体已区分，但当前视觉仍是六个远端完整怪物立绘。牌面在七站使用远端 FourBeasts；原本地独立 `WrathBossDemo` 的旧牌面/验证UI仍保留，并与七站隔离。

实际截图人工检查：Boss 为六格等大远端立绘，无本地大面板；精英为远端单立绘；原牌面、理牌、出牌/弃牌按钮完整可见。断手图只保留本体与五个原位红色倒计时；再生图五立绘恢复、倒计时消失，并出现远端目标选择轮廓。下排血条贴近选牌预览区上缘，与实际手牌仍有间距，这沿用远端布局，未另改卡牌排布。

- [精英初始](2026-10-22-seven-station-screenshots/elite.png)
- [Boss 初始](2026-10-22-seven-station-screenshots/boss.png)
- [五手消失、权威倒计时 8](2026-10-22-seven-station-screenshots/boss-severed.png)
- [第 9 轮敌方阶段再生后，进入第 10 轮玩家窗口](2026-10-22-seven-station-screenshots/boss-regenerated.png)

实际修正前截图保存在 `Tools/Validation/2026-10-22-seven-station-screenshots/before-elite.png`、`before-boss.png`。修正后截图由真实七站 PlayMode 场景的 `ScreenCapture` 生成，最终结果见该目录与下方证据记录；没有生成或重绘界面截图。

## 失败与修复记录

- .NET 新七站测试：配置替换前 5 项 RED，替换后连同原流程/经济共 23 项 GREEN。
- Unity 无存活玩家的意图投影：新增真实检查点测试先 RED，再与原投影测试共 9 项 GREEN；不改变权威伤害与复活结算。
- 远端样式检查：5 项中 2 通过、3 失败，复现错误的六单位布局及本地面板泄漏；布局恢复远端并隔离面板后通过。
- World HUD 新检查初次 7 项中 3 通过、4 失败，覆盖再生、预警、蓄力、免疫；适配后 257 项中剩 1 个预警 tooltip 残留失败，已补明确的打开者跟踪与定向关闭。
- 增加 tooltip 保持/变化回归后 259 项中 258 通过、1 失败。定位为新测试把第 0 格固定当作攻击：普通敌人有盾时原规则会在攻击之前预览清盾，原第 0 格说明确实改变，关闭旧说明属于正确行为。夹具改为先建立清盾行，再按语义选择攻击徽记，测试另一徽记的真实盾值变化；未修改普通清盾规则。
- 第一轮 Play 31 项中 28 通过、3 失败，失败均为旧独立 Boss 测试在回放刚结束时立即断言结果面板。远端已实现额外 1.5 秒结果延迟，已修正测试等待，未缩短产品延迟。

最终状态以结果表的最后执行及其原始 NUnit 文件为准。

最终收尾：Editor 259 项全部通过。最新 Play 已通过七站 4、弃牌 3、精英 5、普通场景 9、暴怒 2，共 23 项；用户于 20 轮测试运行中要求停止，已退出 Play 并记录取消。没有将取消记为通过，也没有继续运行后续 7 项。20 轮判负规则仍保留，未按停止测试的指示修改规则。临时验证驱动源码已移除；动态字体文件已从本轮前备份精确恢复，保留其已有未提交版本。
