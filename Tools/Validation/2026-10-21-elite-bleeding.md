# 新精英：流血与攻击成长

## 已实现规则

- 当前标准配置 `External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml` 的精英生命为 400，初始攻击为 8。每个完整回合结束后攻击 +10，眩晕跳过行动的回合仍会成长。
- 技能按「施法 → 普攻」循环。施法先造成当前攻击力的 50% 伤害，再施加 3 层流血；普攻造成当前攻击力的 100% 伤害。
- 新状态为独立的 `elite-bleeding`。每层使敌人对玩家的攻击伤害增加 50%，层数线性相加，合成为一次伤害后扣护盾。旧 `bleed` 中毒定义不变，道具自伤、周期中毒和反伤不受流血放大。
- 精英实际行动结束后减 1 层。眩晕跳过不减层、不推进技能；玩家出牌和道具不减层。施法伤害采用叠加新流血之前的层数。
- 保留葫芦净化、控制免疫、眩晕、替死鬼复活清状态并中止后续效果、盾反击杀中止后续效果，以及休息规则。跨战斗不保留流血和攻击成长，实际生命损耗继续沿用。
- 移除当前精英的开局逐击护甲、召唤护卫、群攻。普通怪、Boss 和旧 `08_ArmoredSummoner.xml` 历史模板保留原规则。

没有其他修正时的数值：

| 回合 | 攻击力 | 技能 | 伤害前流血 | 最终伤害 | 行动后流血 |
|---|---:|---|---:|---:|---:|
| 1 | 8 | 施法 | 0 | 4 | 2 |
| 2 | 18 | 普攻 | 2 | 36 | 1 |
| 3 | 28 | 施法 | 1 | 21 | 3 |
| 4 | 38 | 普攻 | 3 | 95 | 2 |

攻击系数和承伤结算沿用引擎向下取整规则。

## 实现位置

- Combat：新增 `IncomingAttackDamagePerStackSpec` 和独立 `StackDecaySpec`；减层只产生状态事件，增伤只产生一个伤害事实。流血审计使用 `incoming.attack-per-stack`，与已有减伤证据共存。
- Composition：可选 `Behavior.attack / attackPerRound`、敌人行动伤害的 `amountKind="EnemyAttackScale"`。旧配置默认行为不变，不修改全局规则契约或存档 schema；旧 .20 配置指纹固定断言通过。
- 内容：更新当前 .21 精英，新增独立 `11_BleedingElite.xml` 示例。精英与遭遇 ID 保持原值，显示名改为「流血精英」。
- Unity 共享展示：技能名、攻击成长、权威伤害预览、流血层数、增伤贡献和减层说明；保护自定义极大攻击成长导致的展示溢出。
- H5 编辑器：新字段、流血状态说明与图标、技能状态预览、准确增伤审计。攻击系数仅在敌人行动伤害表单中提供，保留旧召唤示例。

配置内容改变后指纹随之改变，已有存档继续遵循原来的内容一致性校验；没有增加旧存档迁移。

## 验证

下表及 Unity 场景专项记录为修改数值前（1800 生命、90 初始攻击）的完整功能验证。400/8 的验证单独记录于下节，避免将历史结果视为本次数值调整后的 Unity 重跑。

基线为 BattleToolkit 提交 `2228665bf9d8afa8b5c0807db32e933653a0c463` 的干净归档；主仓库基线为 `1500cb1223dc8cc968256563d9420f7784893634`。

| 检查 | 结果 |
|---|---|
| 修改前引擎全量 | 1568 项：1555 通过，13 失败 |
| 修改后引擎全量 | 1608 项：1595 通过，13 失败；失败项与基线完全相同，新增失败 0 |
| 本次新增引擎测试 | 40/40 通过（Combat 23、配置集成 11、实际交互 6） |
| Unity 共享展示及 HUD 纯 C# 回归 | 21/21 通过，其中新增展示测试 6 项 |
| Unity Editor 战斗相关 10 个程序集 | EditMode 588/588 通过 |
| Unity 场景 2 个 PlayMode 程序集 | 14/14 通过，包含新精英专项 5 项 |
| 复活检查点修复的引擎相关回归 | 41/41 通过；修复后重跑全量仍为 1595/1608，13 项基线失败不变 |
| 新精英真实 HTTP 宿主测试 | 5/5 通过 |
| 既有设计 HTTP 回归 | 5/5 通过 |
| 完整浏览器入口 | 81/81 通过：编辑器 29、试玩 15、新精英 3、H5 17、冒险 17 |
| 独立代码审查 | 发现并修复 Unity 展示溢出；复核无剩余问题 |
| diff 检查 | 本次代码、子模块改动无空白错误 |

新增测试覆盖：施法/普攻连续累积、盾前单次伤害、额外减伤下的准确流血贡献、伤害来源排除、按施加者减层、眩晕、净化、免疫、复活、盾反中断、吸血、自伤、跨战斗清理、预览不提交，以及旧内容兼容。

新增行为先观察 RED 再实现 GREEN；审查发现的展示溢出同样先复现 `OverflowException`，再加入保护并复跑共享回归。

桌面和 375px 截图已人工查看：意图显示下次普攻 200、实际基础攻击 100、流血增伤 +100、行动结束减层，玩家状态显示 2 层。

后续补充了 Unity Editor 与真实场景 PlayMode 验证，记录见下节。尚未执行 Player 或 IL2CPP 构建。场景验证结束后，按用户要求将 `BattleDemo.unity` 默认遭遇切换为 `standard-armored-summoner`（流血精英），当前 .21 XML 引用保持原样，打开场景点击 Play 即可体验。

本次未提交或推送。原有字体、场景、Renderer 资产的未提交改动保留。

## Unity 场景专项验证（调整数值前）

使用本机 Unity 2022.3.62f3，以 `-batchmode` 启动 Editor；PlayMode 使用实际 `BattleDemo.unity`、当前 .21 XML、原场景 Timeline 与 TMP 控件。测试在运行时切换为精英遭遇，不保存场景。

新增 `EliteBleedingScenePlaybackTests` 的 5 项测试覆盖：

- 连续三轮预览和实际伤害 45 / 200 / 82，玩家生命 800 → 755 → 555 → 473；展示游标依次显示施加与减层检查点，最终层数为 2 / 1 / 3。
- 真实葫芦出牌净化流血并眩晕精英；跳过攻击不推进普攻序列，完整回合结束后攻击仍增长至 110。
- 真实替死鬼在致命施法后将生命从 0 恢复至 480，取消后续 3 层流血；道具耗尽移除，延迟死亡动画不会改写复活后的角色缩放与旋转。
- 带着首轮产生的 2 层流血承受 200 伤害而触发复活时，复活检查点清除已有层数、状态标签消失。沿用原有中断语义：当前普攻没有完成，不推进技能序列，下次普攻预览为无流血加成的 110；正常完成后才切换为攻击力 120 的施法预览 60。
- 2 层流血将 100 合成为一次 200 伤害，60 护盾吸收后掉血 140；预览、伤害事实、增伤审计和显示生命一致。

场景测试复现了意图文本溢出：开场施法需要 260.64px、普攻需要 217.2px，而原文本框仅有 42px。HUD 按 TMP 测量高度扩展文字与底板，保留原字号和完整规则说明，并按倾斜后的边界避开遗物栏。修复后高度分别为 261px、218px，正文位于底板内、底板位于屏幕及画布内，连续刷新 3 次位置和尺寸稳定。

本次检查使用真实 TMP 文本、几何边界与 Timeline 展示事件。batchmode 没有生成 Unity PNG，未进行 GPU 截图或像素视觉检查。

加强首次复活检查点的断言后，PlayMode 实际复现另一处展示错误：权威状态已经清除流血，首个恢复至 480 HP 的 Timeline 帧却仍带着旧 2 层。原因是 `BattleTimelineRecorder.ObserveCombat` 只回放复活后的生命、护盾，没有回放复活同时清除目标全部状态的变化。已在该复活事实处同步清除目标状态；不改变权威结算、其他目标的状态或技能中断语义。现有引擎交互测试也新增同帧断言，先复现失败再验证修复。

EditMode 中原有场景绑定测试仍固定旧 XML 与默认遭遇，已更新为检查当前 .21 XML，并逐项核对场景实际选中遭遇与 XML 的敌人定义。验证阶段保留原先选中的 Boss 遭遇；随后按用户要求将默认遭遇切换为流血精英。

默认异步 EditMode Runner 在结果保存前发生域重载，触发 Test Framework 1.3.3 的空结果异常；该次退出码 3、未生成 XML，不计为通过。改用该版本内置的 `-runSynchronously`，所选 10 个 EditMode 程序集内没有会被该选项过滤的异步 UnityTest。PlayMode 仍使用正常多帧异步运行。

最终结果：`editmode-final2.xml` 为 588/588，`playmode-final3.xml` 为 14/14；两个 Unity 进程均退出码 0、没有跳过测试。`.NET` 的 `elite-revive-timeline-regression.trx` 为 41/41；`elite-revive-timeline-full.trx` 为 1595/1608，与原基线比较无新增失败。

所有 Unity 验证进程退出后，用运行前的完整备份恢复动态字体生成的变化；当时字体、场景、Renderer 的 SHA-256 均与运行前一致。后续默认遭遇切换只改场景的 `encounterId` 一行，其他原有用户改动保留；该配置修改已核对 XML 引用和精英定义，未再次启动 Unity 测试。

## 400 生命、8 初始攻击调整

当前 .21 标准配置及 `11_BleedingElite.xml` 模板只调整精英生命与初始攻击；成长仍为每回合 +10。普通怪、Boss、同花计算以及净化、眩晕、复活、休息规则均未改动。

集成、交互、HTTP、H5 浏览器及 Unity 场景测试中的实际配置断言已同步为新数值；独立使用 90 攻击的纯算法测试 fixture 保持原样。跨战斗生命保留测试以无回血的五条击败精英，明确验证第一轮受伤后的 796 生命保留到下一战；反甲中断测试使用显式攻击 fixture，继续覆盖反击致死取消后续流血。

- 引擎精英定向回归：40/40 通过。
- 引擎全量：1595/1608 通过，13 项失败与原基线逐名一致，无新增失败。结果在 `Library/EliteValidation/BalanceArtifacts/TestResults/`，比较记录为 `BalanceArtifacts/baseline-comparison.json`。
- 当前 Web 服务原为旧版引擎，真实开局因未知新精英字段失败。已使用当前源码重新构建并在 `http://127.0.0.1:18767/` 重启；构建零警告、零错误，自检通过。HTTP 新精英 5/5 通过，日志在 `Library/EliteValidation/BalanceWeb/`。
- Chrome 真实界面开局与连续两轮结算验证通过：400 生命、8 攻击、首次伤害 4，玩家 796/760 生命、流血 2/1 层，下次预览分别为 36/21；页面无 JavaScript 异常。完整 H5 浏览器回归 17/17 通过，包含桌面和 375px 移动布局、普通怪、Boss、废牌及计分预览。证据为 `BalanceWeb/browser-smoke.json`、`h5-full.log` 及同目录截图。
- 用户 Unity Editor 当前打开，本次数值调整未关闭 Editor 或启动竞争的 batchmode 测试；PlayMode 断言已同步，之前的 14/14 是调整数值前的结果。

Web 需重新载入标准试炼并新开对局，已有草稿或对局不自动替换内容。Unity 重新进入 Play 时读取当前 XML。

## 复现命令与本地证据

在主仓库根目录执行引擎回归；构建输出放在主仓库的 Library 内，让既有测试资源定位逻辑可以找到内容文件：

```sh
External/BattleToolkit/.runtime/dotnet/dotnet test \
  External/BattleToolkit/Tests/BattleToolkit.Tests.csproj \
  --configuration Release \
  --artifacts-path "$PWD/Library/EliteValidation/ToolkitArtifacts" \
  --logger 'trx;LogFileName=elite-final.trx'
```

在 `External/BattleToolkit` 执行 HTTP 与浏览器回归。浏览器需要安装工具声明的 Playwright，或通过 `NODE_PATH` 使用已有运行时依赖；本次使用 Chrome 渠道：

```sh
python3 -m unittest discover -s Tools/BattleContentEditor/tests \
  -p 'test_elite_projection.py' -v
PLAYWRIGHT_CHANNEL=chrome python3 Tools/BattleContentEditor/tests/run_browser.py
```

可设置 `EDITOR_URL` 复用已启动的本地宿主，否则脚本会建立临时真实 .NET 宿主。

本地未入库证据在 `Library/EliteValidation/`：`baseline.trx`、`elite-final.trx`、`toolkit-full.log`、`unity-shared.trx`、`unity-shared-final.log`、`unity-growth-red.log`、`elite-http.log`、`browser-full.log`、桌面与窄屏截图；`SharedTests.targets` 是共享展示源码的临时编译入口。

Unity 场景证据在 `Library/EliteValidation/Unity/`，包括上文的最终 XML、同名 Editor 日志、`elite-hud-layout.txt`，以及复活检查点的 RED/GREEN 和全量引擎 TRX。

在主仓库根目录复现 Unity 场景回归：

```sh
ELITE_UNITY=/Applications/Unity/Unity.app/Contents/MacOS/Unity
"$ELITE_UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -assemblyNames 'Poker.SingleBattle.Tests.PlayMode;Poker.Battle2DValidation.Tests.PlayMode' \
  -testResults "$PWD/Library/EliteValidation/Unity/playmode-final3.xml" \
  -logFile "$PWD/Library/EliteValidation/Unity/playmode-final3.log"
"$ELITE_UNITY" -batchmode -projectPath "$PWD" -runTests -runSynchronously -testPlatform EditMode \
  -assemblyNames 'CardArt.Tests.Editor;Poker.BattleHosting.Tests.Editor;PokerEngine.Integration.Tests.Editor;BattlePresentation.Tests.Editor;BattlePresentation.Unity.Tests.Editor;Poker.BattlePresentation.Shared.Tests;Poker.SingleBattle.Tests.Editor;Poker.Battle2DValidation.Tests.Editor;CardPresentation.Tests.Editor;CardMatch.Tests.Editor' \
  -testResults "$PWD/Library/EliteValidation/Unity/editmode-final2.xml" \
  -logFile "$PWD/Library/EliteValidation/Unity/editmode-final2.log"
```

## 基线中仍然失败的 13 项

本次没有修改这些断言或对应功能：

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
12. `SnapshotRejectsChangedContentRevisionDigestAndIllegalBattleAction`
13. `TriggerEffectOverflowRollsBackHealthCardsBudgetAndReports`
