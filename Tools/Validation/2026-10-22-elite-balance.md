# 流血精英截图数值同步与回归

2026-10-22，依据用户本轮截图。本文区分原工作区验收与本次选择提交内容的独立复验。

## 最终规则

| 项目 | 调整前 | 当前 |
| --- | --- | --- |
| 七站生命 | 500 | **500** |
| 日期版／独立精英生命 | 400 | **500** |
| 初始攻击 | 8 | **8** |
| 每完整回合结束攻击增长 | +10 | **+5** |
| 流血每层敌方主攻击承伤加成 | 50% | **30%** |
| 施法 | 当前攻击×0.5，伤害后加3层流血 | 保持 |
| 普攻与序列 | 当前攻击×1；施法→普攻循环 | 保持 |
| 流血衰减 | 施加者实际行动完成后减1层；眩晕跳过不减层 | 保持 |

| 回合 | 当前攻击 | 行动 | 伤害前流血层数 | 实际伤害 | 行动结束后层数 |
| --- | --- | --- | --- | --- | --- |
| 1 | 8 | 施法 | 0 | **4** | 2 |
| 2 | 13 | 普攻 | 2 | **20** | 1 |
| 3 | 18 | 施法 | 1 | **11** | 3 |
| 4 | 23 | 普攻 | 3 | **43** | 2 |

施法基础伤害先向下取整，再按当前已有流血层数合成一次承伤，之后才挂新流血；第二轮为 floor(13×1.6)=20，第三轮为 floor(floor(18×0.5)×1.3)=11。流血在护盾前结算，不新增第二段伤害。

## 修改范围

- `SingleChapterBattle.xml` 版本 `.2026-10-22.5`。
- `StandardSingleBattle-2026-10-21.xml` 版本 `.2026-10-21.2`，供独立精英场景及当前标准入口读取。
- `Templates/11_BleedingElite.xml` 版本 `elite-bleeding.2`。
- 同步当前精英文档、H5帮助说明、真实配置驱动的规则／HTTP／Unity／浏览器测试预期。

结构比较确认，三个XML中除版本、流血状态名称／比例、精英成长及日期版／独立精英生命外，其余节点一致。普通战、牌型、道具、暴食、暴怒及冒险地图定义均未改；公共状态默认50%与自伤／毒伤／反伤排除逻辑未改。旧 `08_ArmoredSummoner.xml` 保留护甲召唤；独立暴怒模板 `10_WrathThousandHands.xml` 未改，该包唯一遭遇入口不使用其内嵌的旧精英定义。

## 原工作区实际验证（包含既有未提交修复）

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 修改前全量 .NET | 1745项，1733通过、12既有失败 | `full-before.trx` |
| 新预期验证旧配置 | 27项，19通过、8按预期失败 | `elite-red.trx` |
| 新配置精英专项 | **27/27通过** | `elite-green.trx` |
| 修改后全量 .NET | **1746项，1734通过、12既有失败**；失败名称及消息与修改前完全相同 | `full-after.trx`、`full-comparison.json` |
| Unity 2022.3.62f3 编译 | 成功；在线请求0警告，独立测试进程编译成功 | `unity-recompile-final.json`、Unity测试XML |
| 实际 Unity EditMode | **137/137通过** | `unity-editmode.xml`，退出码0 |
| 实际 Unity PlayMode | **6项最终均通过** | 下表；不是单次6项全绿 |
| 真实原生HTTP精英投影 | **5/5通过** | `http-elite-final.log` |
| 修改范围与本地文件保护 | 非精英内容结构未变；原有未提交内容保留 | `scope-and-preservation.json`、`preservation-final.json` |

Unity在线测试有一组10秒超时，不计为通过；随后使用独立Unity进程补验上述137项。EditMode覆盖精英真实配置／存档恢复／眩晕／替死／自伤排除、牌型计分、暴怒规则与免伤、道具运行时记忆以及实际意图文案。

第一轮 PlayMode 6项中独立精英5项通过，七站1项失败于旧的“战斗胜利”文字断言。根因是刚拉取的远端UI已使用 `VictoryLogo` 图片。仅将测试中的胜利可见性检查兼容真实图片及旧文字，保留等待时长、输入锁定、奖励与美术要求；生产UI没有改动。七站用例随后独立重跑通过。

| 实际PlayMode用例 | 结果 | 来源 |
| --- | --- | --- |
| `BleedingMergesBeforeShieldIntoOneAuthoritativeDamageFact` | 通过，5.66秒 | `unity-playmode.xml` |
| `CastStrikeCastPreviewsMatchRealImpactsAndVisibleStatusCheckpoints` | 通过，7.44秒 | `unity-playmode.xml` |
| `RealFullHouseCleansesBleedingAndStunPreservesIntentWhileAttackGrows` | 通过，7.84秒 | `unity-playmode.xml` |
| `RealReviverCancelsLaterCastBleedingAndCannotBeOverwrittenByLateDeathPlayback` | 通过，4.74秒 | `unity-playmode.xml` |
| `RealReviverClearsExistingBleedingBeforeNextIntent` | 通过，7.65秒 | `unity-playmode.xml` |
| `SevenStationEliteUsesGrowingBleedingAttacksAndKeepsRemoteArtwork` | 通过，14.69秒 | `unity-seven-station-final.xml` |

五项独立场景覆盖真实异步伤害检查点、流血3→2显示、护盾合成一次伤害、葫芦净化与眩晕保留意图、替死清状态及后续行动恢复。七站用例沿实际冒险路线进入精英，核验远端美术、500生命和四轮4／20／11／43；前置战斗使用测试既有管理员快捷结束。没有实际跑满20轮，没有生成独立Player构建。

H5浏览器回归源码预期已同步，**本轮未运行浏览器回归**；真实HTTP回归不能替代浏览器验收。

## 全量既有失败

以下名称和错误消息与修改前完全相同，本次数值调整未修复它们：

- `AppendedOverflowRollsBackDamageCardsAndResources`
- `CreationHasNoHandAndFirstTurnDealsExactlyOnceWithoutSpendingEnergy`
- `FailureInALaterEnemyReactionRollsBackTheEntireSessionIncludingEarlierReactions`
- `InvalidBoundaryBatchRollsBackTheEntireStart(True)`
- `InvalidCompletionBatchRollsBackEarlierDamageAndActionPayment(True)`
- `InvalidEffectiveCapacityFailsAtBattleCreation(HandLimit,-8)`
- `LateFailureRollsBackExpiryControlConsumptionDamageEnergyScoreAndCardMovement`
- `LaterFailureRollsBackImmunityConsumptionAndEarlierDamage`
- `NumericOverflowInTheProviderOrAReactionRollsBackEveryFact(False)`
- `PrimaryOutgoingUsesBatchStartAndIncomingUsesLatestStateExactlyOnce`
- `RewardOverflowRollsBackCostsCardsAndRandomState`
- `TriggerEffectOverflowRollsBackHealthCardsBudgetAndReports`

## 工作区与存档

两个仓库原有未提交代码／测试／文档的工作区字节均保留。本次提交额外包含 `SingleChapterEnemyRulesTests.cs` 的最小 Unity NUnit 兼容前置：移除 Unity 不支持的 `Assert.Multiple` 包装，四条原断言保持，避免推送后测试程序集出现 CS0117。冒险测试中的旧飞牌采样修复保持未提交；本次仅提交精英数值预期和胜利图标识别。PlayMode产生的动态字体缓存单独留档，原字体字节已恢复；用户存档SHA256未变。原文件副本在证据目录 `before/`。

内容数值及指纹已更新，需退出Play后重新读取配置并开启新旅程；旧内容指纹存档沿用现有拒绝恢复逻辑，原文件保留。本轮未新增存档迁移。

本轮完整证据目录：`Library/EliteBalance-20261022-183537`。


## 本次选择提交范围与独立复验

BattleToolkit 提交三个当前精英配置、配套说明、规则／HTTP／H5浏览器预期，以及上述必要的 NUnit 兼容前置。主仓提交精英 Unity 回归、七站精英预期和胜利图片识别、本文以及对应子模块引用。

下列旧改动保持未提交：`BattleRelics.cs`／`BattleAdminCommands.cs` 的道具回合记忆修复及其新增测试，`PortraitBattleActorPresenter.cs` 的致死播放修复，冒险场景测试的飞牌走廊采样修复，字体缓存和其他排查文档。

为防止这些未提交改动影响判断，从 BattleToolkit 暂存树导出独立副本复验，未混入上述运行时代码或新增测试：

| 提交前复验 | 结果 | 证据 |
| --- | --- | --- |
| 独立暂存树全量 .NET | **1738项，1726通过、12既有失败**；失败名称和消息与原基线完全相同 | `staged-full.trx`、`staged-test-summary.json` |
| 独立暂存树精英专项 | **27/27通过** | `staged-elite.trx` |
| 独立暂存树真实原生 HTTP | **5/5通过**，11.23秒 | `staged-http.log`、`staged-http-summary.json` |
| 本地工作区全量 .NET | 1746项，1734通过、12既有失败；与原基线相同 | `worktree-full.trx`、`worktree-comparison.json` |

独立副本比当前工作区少8项，是此次未提交的道具回合记忆测试，没有通过删减既有测试掩盖失败。

**Unity结果边界：** 上文137项 EditMode 及6项 PlayMode是此前完整工作区实测，包含既有未提交修复，不能等同此次选择提交快照的全部 Unity 测试通过。两个精英替死 PlayMode 用例会经过 `PortraitBattleActorPresenter.cs` 的既有致死播放修复；该修复此次保持未提交，所以远端选择提交快照仍有此前已知的替死播放取消风险，尚未在移除该修复的 Unity 快照上重跑。137项 EditMode中也包含此次不提交的道具记忆新增回归。此次独立快照的 .NET 和HTTP结果与实际 Unity 验收分开记录。

提交前独立复验证据：`Library/ElitePush-20261022-191502`。提交 SHA 和推送结果以两仓 Git 历史及远端分支为准。

BattleToolkit 数值与规则回归提交：`7d3b78d06203a41a8cb72345b35e1799ccf55544`。主仓已存在的远端 `dev` 合并历史继续保留，推送仍使用当前 `codex/wrath-boss-20261022` 分支。
