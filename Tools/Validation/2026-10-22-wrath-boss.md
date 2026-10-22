# 暴怒 Boss 实现与验证记录

日期：2026-10-22。实施规格见 `docs/superpowers/specs/2026-10-22-wrath-boss-design.md`。

后续截图中的文字提示、占位图标、点击选敌和手臂效果限制，见最新补充记录 `Tools/Validation/2026-10-22-wrath-prompts.md`。下方数字记录核心规则第一阶段的验证，不代表后续新增用例数量。

## 交付范围

- 规则核心已实现 1500 血本体、四饮血手加一束缚手、动态攻击 D、成功蓄力、两层龟缩、独立断手再生、预警后追加大招及 20 轮判负。
- 大招沿用正式攻击修正、反伤和替死中断；只结算一次常规行动完成。再生复用原单位 ID，所有 Boss 计时保存在不可变规则状态中。
- Unity 与 H5 读取同一 `WrathBossQuery`，展示本体血量、五手状态、再生倒计时、八个常规意图和独立大招预警/基础伤害。
- 已更新当前 `.21.xml` 和独立模板 `10_WrathThousandHands.xml`。历史 `.20.xml`、普通牌型/道具规则和标准冒险地图未改；五手豁免章节生命倍率，本体沿用成长。
- Unity 提供独立场景生成入口；本次未修改原 BattleDemo 的默认精英配置。工作区原有精英、字体、渲染等未提交修改保留。
- 两个仓库均在 `codex/wrath-boss-20261022`，本次未提交、推送或合并。

## 自动验证结果

| 验证 | 结果 |
| --- | --- |
| 新暴怒规则集成测试 | 32/32 通过 |
| 新独立伤害免疫测试 | 9/9 通过 |
| 新章节生命成长测试 | 2/2 通过 |
| 新冒险存档重放测试 | 1/1 通过 |
| 新 Unity 共享显示测试（纯 C#） | 7/7 通过 |
| H5 真实 HTTP 引擎投影 | 5/5 通过 |
| H5 配置/独立 Chrome DOM | 5 组通过 |
| H5 普通和暴怒 375px 布局 | 2/2 通过 |
| 原 H5 精英投影/DOM | 5/5、3 组通过 |
| 原试玩浏览器流程 | 15/15 通过 |
| 原 H5 场景回归 | 17/17 通过 |
| .NET Host 构建 | 0 警告、0 错误 |
| 当前 XML / 暴怒模板编译校验 | 均通过，0 诊断 |
| Unity Editor 场景测试 | 已新增 2 项，未运行 |

新规则/冒险测试共 **44 项通过**。测试覆盖实际顺子与葫芦的免伤计次和附加状态、手雷不耗行动清手、皇家击杀后同次治疗、普通攻击/大招/手阶段触发替死中断、毒与反伤、本体死亡即胜、再生同轮行动、眩晕及免控、20 轮失败、预览和失败命令不污染状态。

存档测试从真实冒险指令出发，经历蓄力、使用手雷、断手、导出/恢复，再同步推进至再生和第 12 轮大招，比较 Boss 查询、再生期限及完整存档结果。

独立审查发现并修复了两项问题，各自补充先失败再通过的回归：追加大招遗漏攻击倍率/固定值修正；阶段结束后的旧回放快照导致倒计时、取消预警或再生意图短暂错误。原审查探针也已独立复跑通过。

## 全量回归与已有失败

最终规则全量：**1652 项，1639 通过、13 失败、0 跳过**。使用子模块干净 HEAD `2228665bf9d8afa8b5c0807db32e933653a0c463` 的临时源码副本运行全套：1568 项，1555 通过、同样 13 项失败。按完整测试名称比较，失败集合完全一致，没有发现本次新增失败。

这 13 项为：

```text
SnapshotRejectsChangedContentRevisionDigestAndIllegalBattleAction
CreationHasNoHandAndFirstTurnDealsExactlyOnceWithoutSpendingEnergy
PrimaryOutgoingUsesBatchStartAndIncomingUsesLatestStateExactlyOnce
FailureInALaterEnemyReactionRollsBackTheEntireSessionIncludingEarlierReactions
NumericOverflowInTheProviderOrAReactionRollsBackEveryFact(False)
LaterFailureRollsBackImmunityConsumptionAndEarlierDamage
RewardOverflowRollsBackCostsCardsAndRandomState
InvalidCompletionBatchRollsBackEarlierDamageAndActionPayment(True)
InvalidBoundaryBatchRollsBackTheEntireStart(True)
AppendedOverflowRollsBackDamageCardsAndResources
InvalidEffectiveCapacityFailsAtBattleCreation(HandLimit,-8)
TriggerEffectOverflowRollsBackHealthCardsBudgetAndReports
LateFailureRollsBackExpiryControlConsumptionDamageEnergyScoreAndCardMovement
```

共享显示及 HUD 文字全量：28 项，27 通过、1 失败。失败是本次开始前已有的精英用例 `ConfiguredElitePreviewShowsGrowthAndDamageThenBleedingDecay`，仍断言攻击 90/伤害 45，而当前工作区精英配置为攻击 8/伤害 4。本次未改这条测试或精英数值。

因此不能将项目描述为“全量测试全绿”或“Unity 端到端验收完成”。上述历史问题没有通过修改普通玩法或放宽断言来掩盖。

## 运行与复现

Unity 导入项目后使用菜单：**工具 → 卡牌战斗 → 示例场景 → 打开暴怒 Boss（本体与五手）**。首次生成 `Assets/Scenes/WrathBossDemo.unity`，随后点击 Play。生成器先绑定六个 Actor，再编译并校验遭遇；五手暂使用缩小的现有敌人美术。

H5 内容编辑器选择 `standard-wrath-thousand-hands`。标准冒险路线仍使用原 Boss 节点；测试以临时冒险配置验证暴怒，不会替换正式地图。

以下命令在 Texas-Holdem 根目录运行。本机使用官方 .NET 10.0.100 ARM64 SDK `/tmp/wrath-dotnet-10/dotnet`；仓库原附带运行时因本机签名加载问题未使用，未修改其签名或系统安全设置。其他机器将 `POKER_DOTNET` 指向可用 SDK。

```sh
export POKER_DOTNET=/tmp/wrath-dotnet-10/dotnet

# 新 Boss 规则、免伤、成长与存档（44 项）
"$POKER_DOTNET" test External/BattleToolkit/Tests/BattleToolkit.Tests.csproj --configuration Release --artifacts-path Library/WrathValidation/RootArtifacts --filter 'FullyQualifiedName~WrathBossRulesTests|FullyQualifiedName~CombatWrathImmunityTests|FullyQualifiedName~WrathAdventureHealthTests|FullyQualifiedName~WrathAdventureReplayTests'

# 全部规则；当前有上述 13 项基线失败
"$POKER_DOTNET" test External/BattleToolkit/Tests/BattleToolkit.Tests.csproj --configuration Release --artifacts-path Library/WrathValidation/RootArtifacts

# H5 真实规则投影；自动启动并关闭临时宿主
python3 -m unittest discover -s External/BattleToolkit/Tools/BattleContentEditor/tests -p 'test_wrath_projection.py' -v

# H5 DOM；需要已安装 Playwright 和 Chrome
npm --prefix External/BattleToolkit/Tools/BattleContentEditor run test:wrath
```

新 C# 测试已由现有测试项目默认纳入，`test:wrath` 已纳入浏览器回归入口。Unity 共享测试在本机通过 `Library/WrathValidation/SharedTests.targets` 纳入纯 C# 编译；该文件含本机绝对路径，换机器需重新生成，Unity Test Runner 可直接运行对应 Editor 测试。

本机日志与截图保存在 `Library/WrathValidation/`：`wrath-rules-final.trx`、`baseline-rules-full.trx`、`wrath-shared-final.trx`、对应 `.log`、`wrath-http-final.log`、`wrath-dom-final.log`、`wrath-host-build.log`、两个 XML 校验 JSON、`wrath-h5-mobile.png`。这些是本地验证产物，未纳入版本控制。

## 尚未完成的现场验收

本机没有 Unity 编辑器，因此未执行 Unity Editor 编译、实际场景生成、两个新场景测试、PlayMode 或实际演出/分辨率验证。纯 C# 显示测试和静态 API 核对不能替代这些检查。专用手部美术、动画和战斗平衡也未据此宣称验收通过。

常规动作按用户 2026-10-22 明确纠正，采用“普攻 → 普攻 → 防御（龟缩）→ 蓄力”四步循环并保留配置能力。两次普攻分别占用序列槽位，防御效果仍为两层免伤；其余规则以本次已确认规格为准。

## 四步意图纠正后的复核

两份 XML 使用 `strike → strike-2 → guard → charge` 四个独立槽位，八格预告展示两轮循环。无断手/控制时，第 6 轮普攻 43 后追加大招 129；第 12 轮先完成蓄力，再追加大招 189。本次只改配置、相关测试和说明，没有修改生产运行逻辑。

纠正后 Wrath 定向 51/51（含历史暴怒测试）、Unity 共享显示 7/7、H5 HTTP 5/5、DOM 5 组及 375px 六单位布局 1/1 均通过。规则全量仍为 1652 项、1639 通过、13 项与干净基线相同的失败，记录在 `Library/WrathValidation/wrath-four-step-rules.trx`；共享显示记录为 `wrath-four-action-projection-final.trx`。

当前本机 `18767` 服务已读取到新 XML，并经独立临时会话确认八格顺序。已打开网页中的配置和已开局会话不会被自动覆盖：点击“载入标准试炼”，选择暴怒后开新局即可使用新顺序，无需重启服务。
