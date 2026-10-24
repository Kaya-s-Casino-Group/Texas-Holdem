# 20 回合结算验收：测试运行器超时修复

日期：2026-10-23。完整图形 PlayMode 的 20 回合、失败页及返回首页验收已通过。故障源是长时验收脚本受到测试框架默认超时干扰；正式结算逻辑无需修改。

> 2026-10-24提交说明：测试运行器超时预算修复、20回合回归及本验证记录随本次提交发布。沿用已经通过的完整图形验收，不重复运行；末尾“不提交、不推送”描述的是当时验证结束时的状态。

> 后续回归状态：下文的旧七站导航失败已在界面提交`fc853ae`中修正并实际复跑通过，见[界面与计分验证](2026-10-24-adventure-ui-scoring.md)。精英独立用例的旧500血断言尚未修正；保留下文原始记录，不宣称整个场景测试组全量通过。

## 根因与初测结论更正

此前“第 20 回合卡住、不显示失败页”的初测受测试运行器自身影响，不能据此认定正式游戏存在相同故障。本次没有修改游戏运行时代码、战斗规则、演出或页面布局。

使用当前工作区的隔离副本进行诊断：先用 19 轮 instant + 第 20 轮 animated 的快速定位测试，未修改产品代码即可正常显示失败页面；随后恢复完整 20 轮动画和真实 UI 点击，加入帧号、游戏时间及等待循环日志。在末尾等待阶段，日志反复记录同一帧 `92984` 和同一游戏时间 `181.497824792`，但 `yield return null` 两侧的测试代码持续执行。

本地实际安装的 `com.unity.test-framework@1.3.3` 源码给出完整解释：

1. `TestCommandBuilder` 把方法的 `Timeout` 上下文命令包在 `IgnoreTestCommand` 内。
2. `CoroutineTestWorkItem` 只对最外层上下文命令提前设置超时，因此方法上的 `[Timeout(600000)]` 未能在协程计时器启动前生效。
3. `CoroutineRunner` 先捕获默认 `180000 ms` 启动计时器。随后方法属性虽把上下文改为 600000，已经启动的计时器不会重建。
4. 超时后，`CoroutineTestWorkItem` 用 `while (executeEnumerable.MoveNext()) {}` 同步耗尽剩余枚举器，不再逐帧等待。
5. 初测 A 的实时 40 秒截止时间仍会增长，但动画无法更新，因此制造了演出超时失败；初测 B 使用按帧更新的 `Time.unscaledTimeAsDouble` 等待 1.2 秒，同帧时间不变，导致测试无限执行并使编辑器无响应。

本次诊断中的无响应同样只终止了隔离测试编辑器。正式工程未启动、未重置。

## 修改范围

- 原 `SingleChapterAdventureSceneTests` 仅增加 `partial`，保留原有全部未提交测试调整。
- 新增 `SingleChapterAdventureSceneTests.RoundLimit.cs`：在 fixture 的 `OneTimeSetUp` 提前设置 600000 ms，子测试上下文创建及计时器启动时即可获得完整预算；`OneTimeTearDown` 恢复原值。
- 新增正式七站 Boss 的完整 20 回合 PlayMode 回归：从第 1 回合开始，通过真实 UI 射线和按钮点击执行全部敌方演出；检查全部回合边界、超时判负、结算等待、失败页及实际点击返回首页。
- 新增逐帧推进断言，若测试运行器再次在同一帧耗尽协程，则明确报告测试运行器异常，避免无限等待。
- 没有修改 Unity Package、版本依赖、战斗规则或失败页面逻辑，也没有强制跳过任何演出。

## 验证条件

Unity 2022.3.62f3，macOS / Metal，1280×720，图形 PlayMode，未使用 batchmode/nographics。测试只在运行时把主角生命设为 100000 并清空遗物，以排除提前死亡；正式 Boss、五只手、眩晕、回合上限和演出均保留。前置站点使用测试辅助流程通过，Boss 的 20 回合全部实际播放。

束缚手会跳过部分玩家行动窗口，17 次 UI 点击对应完整 20 个敌方回合；回归同时核对敌方阶段 0～19 和完成边界 1～20，避免把点击次数误当回合数。

## 实际运行记录

- 快速末回合诊断：通过，约 33.54 秒。规则 `Lost / RoundLimit`，主角 98774/100000，Boss 2000/2000、五只手存活；失败页显示、战斗 HUD 隐藏。
- 修复后的完整 20 回合、失败页及返回首页：**通过**，Unity 退出码 0，NUnit `total=1 / passed=1 / failed=0`，耗时约 **183.35 秒**，已实际跨过此前 180 秒超时点。
- 完整运行记录全部 20 个敌方阶段和 20 个完成回合边界；17 次真实 UI 点击，最后状态 `Lost / RoundLimit`。主角 98774/100000，Boss 2000/2000、五只手存活，确认由回合耗尽触发。
- 实际演出结束至失败页显示 **1.511 秒**。结算等待期间战斗保持可见、导航锁定；之后失败图案、6/7 站统计和「返回首页」按钮可见，战斗 HUD 隐藏。
- 实际通过 UI 射线命中校验并点击「返回首页」，主菜单和「开始新旅程」按钮正常显示。
- 编译：本次测试代码及当前游戏代码在 Unity 中完成编译，图形 PlayMode 测试通过，日志无 C# 编译错误。
- 同一 fixture 其余五项场景回归：**3 通过、2 失败**，Unity 测试进程退出码 2。结果如下；不能声称整个场景测试组全部通过。

| 场景测试 | 结果 |
| --- | --- |
| `AdventureBattleUsesOutcomeRelicsKickerFlightsAndAshBeforeReturningToRewards` | 通过：普通战斗、道具使用、牌型分项飞行及击杀结算 |
| `SevenStationBossSeversAndRegeneratesFiveOriginalActorsWithPsdArtwork` | 通过：五只手断裂与再生及现有美术 |
| `SevenStationBossShowsReadableOpeningAndWarningsBeforeRoundTwoAndEightUltimates` | 通过：入场提示、预警、大招和 4 秒提示消失 |
| `AuthoredSceneCompletesSevenStopsThroughActualButtonsAndBattleAuthority` | 失败：旧流程测试的 `ClickWhere` 找不到启用的预期按钮；尚未定位到具体旧导航步骤 |
| `SevenStationEliteUsesGrowingBleedingAttacksAndKeepsRemoteArtwork` | 失败：旧断言预期最大生命 500，当前场景和 `SingleChapterBattle.xml` 的 `summoner` 实为 800；尚未执行其后四轮断言 |

为区分已有问题与本次测试设置变更，已在隔离副本恢复本次操作前的测试文件精确字节（哈希核对），移除本次新增测试分部，再单独复跑这两个失败用例：两项均以相同原因失败，耗时约 20.48 秒，退出码 2，确认不是本次变更引入。之后已把隔离副本恢复为最终测试代码。正式工作区不受该对照操作影响；没有为使旧断言通过而改动血量或其他规则。这两项旧回归仍未修复，不在本次 20 回合结算修复中变更其他行为。

证据目录：`Library/RoundLimitFix-20261023/`。

- `quick-diagnostic.xml`、`quick-diagnostic/terminal-trace.txt`：快速定位测试结果与失败页切换过程。
- `full-diagnostic/boss-twenty-round-trace.txt`：修复前再次复现的同帧循环证据。
- `full-diagnostic.log`、`full-diagnostic-sample.txt`：修复前完整运行日志和原生采样。
- `verified-twenty.xml`、`verified-twenty.log`：修复后的完整 20 回合结果与 Unity 日志。
- `verified-twenty-screenshots/boss-twenty-round-trace.txt`：完整 20 轮记录、1.511 秒结算等待及返回首页结果。
- `verified-twenty-screenshots/boss-round-20-failure.png`：实际失败页面截图。
- `verified-twenty-screenshots/boss-round-20-returned-home.png`：实际返回首页后的截图。
- `related-scenes.xml`、`related-scenes.log`：同一 fixture 其余五项场景回归（3 通过、2 失败）。
- `original-baseline-scenes.xml`、`original-baseline-scenes.log`：原测试文件对照复跑，两项均以相同原因失败。
- `source-comparison.json`：隔离副本与当前工程 1635 个代码、场景、内容及设置文件一致；仅关闭隔离编辑器的 MCP 自动启动，不改变游戏行为。

`workspace-preservation.json` 已复核：所有原有未提交内容均保留；只在原测试类声明中追加 `partial`、在初测报告前追加更正提示。BattleToolkit 状态和原有文件哈希保持不变。本次不提交、不推送。
