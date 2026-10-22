# 七站纯牌面计分回归

> 本文记录 `.3` 版本当时的实际结果；后续 `.4` 已更新同花、主角、怪物与饮血数值，当前基准见 [最新数值同步回归](2026-10-22-balance-values.md)。下列旧数值保留为历史证据。

2026-10-22，用户决定去掉七站其他牌型的额外基础分，伤害按参与计分的牌面点数乘倍率结算；倍率及现有技能效果保持不变。

## 变更范围

仅修改 `External/BattleToolkit/Engine/Content/SingleChapterBattle.xml` 的 `ScoringProfiles/Scoring/Hand`：所有初始 `points` 和每级增长 `pointsPerLevel` 均为 0。倍率、每级倍率增长、牌面点数、牌型判定与计分子集、技能、道具、精英与暴怒规则保持不变。内容版本更新到 `single-chapter-battle.2026-10-22.3`。

这也替代首次仅去掉高牌初始分、保留其每级 +5 分的中间决定。现在升级只沿用原倍率增长，不再产生牌型基础分。遗物／道具原有的合法加成继续生效。

普通一、普通二、流血精英、暴怒四个七站战斗入口使用同一计分表。历史 `final.xml`、标准单局、独立 Boss Demo、旧三章及其暴食节点不变。本轮不改 C# 生产代码或表现资源。

## 新伤害基准

以下均为一级、无遗物或道具、目标无护盾或免伤时的主攻击伤害；未参与计分的杂牌不计分，兼容较低牌型不重复叠加伤害。

| 出牌 | 伤害／效果 |
| --- | --- |
| 单张 2 / 9 / K | 2 / 9 / 13 |
| 对子 22 | 8 |
| 两对 2233，另带杂牌 9 | 20 |
| 三条 222 | 每个敌人 18 |
| 顺子 23456 | 6、9、12、15、18，合计 60；原命中状态和转火机制保留 |
| 同花 K、9、7、4、2 | 高牌 13 × 5 = 65；独立治疗 28 |
| 普通同花顺 23456 | 30、45、60、75、90，合计 300；独立治疗 16 |
| 葫芦 33322 | 三条 27 × 1.5 向下取整为 40，对全体；对子 8 × 1.5 = 12，追加指定目标 |
| 四条 2222 | 每个敌人 48 |
| 五条 22222 | 每个敌人 120 |
| 皇家同花顺 | 继续强制消灭全体，独立治疗不变 |
| 单张 9，持有 13003（倍率 +2） | 27 |
| 对子 22，持有 13003（倍率 +2） | 16 |

葫芦三条 333 的正确中间分为 9 × 3 = 27，再乘 1.5 后向下取整为 40；表中的三条 222 则为 6 × 3 = 18。葫芦仍读取三条与对子各自等级，顺子仍按各段向下取整。同花仍在去花色后的点数牌型上乘 5，治疗按同花牌点数和的 80% 独立结算。

## 实际验证

| 验证范围 | 实际结果 | 证据（相对仓库根目录） |
| --- | --- | --- |
| 修改前全量 .NET 基线 | 1694 项，1682 通过、12 失败、0 跳过 | `Library/HandPoints-20261022/results/full-before.trx` |
| 新增七站行为回归 | 34 项全通过：计分子集、升级、顺子分段与 A、高牌／同花／同花顺、葫芦、皇家、遗物及三项暴怒免伤交互 | `Library/HandPoints-20261022/results/targeted-green.trx` 中 `SingleChapterScoringTests` |
| 定向规则回归 | 188/188 通过，0 失败、0 跳过 | `Library/HandPoints-20261022/results/targeted-green.trx` |
| 修改后全量 .NET | 1728 项，1716 通过、12 失败、0 跳过；失败名称及错误消息与修改前完全相同，无新增失败 | `Library/HandPoints-20261022/results/full-after.trx`、`regression-summary.json` |
| 四战斗入口真实 HTTP | 52/52 通过，逐段预览与实际伤害一致 | `Library/HandPoints-20261022/http-summary.json`、`http-responses.json` |
| 暴怒真实 HTTP 免伤专项 | 2/2 通过，实际执行龟缩后验证连续普通攻击和顺子多段 | `Library/HandPoints-20261022/shell-http-summary.json`、`shell-http-responses.json` |
| XML 语义差异及工作区保护 | 只变 11 项基础分数值和内容版本；倍率、技能、道具、敌人及遭遇完整保留；已有字体和免伤核查记录散列不变 | `Library/HandPoints-20261022/scope-check.json` |

新增测试加载磁盘七站 XML；计分表与技能配置不使用替换夹具。测试使用公开管理员接口设置手牌、遗物和用于承伤／治疗断言的生命；升级用例仅向开局 Loadout 添加等级输入。最终同一测试程序集在隔离旧内容副本中为 22 项失败、12 项通过，失败均为新伤害断言；使用新版内容则 34 项通过。旧内容副本及测试输出位于 `Library/HandPoints-20261022/red-fixture/` 和 `results/scoring-old-content.trx`，未切换或回退工作区文件。

188 项定向范围为 `SingleChapterScoringTests`、`SingleChapterContentTests`、`SingleChapterEnemyRulesTests`、`AdventureEconomyTests`、`DesignScoring2951Tests`、`DesignItem2951Tests`、`CombatWrathImmunityTests`、`WrathBossRulesTests`、`WrathHandEffectRestrictionsTests` 及 `EliteBleeding` 系列。

HTTP 使用未经修改的新版磁盘 XML，四个原始遭遇均未替换敌人或规则，只以管理员接口设置手牌、遗物及治疗验证所需的玩家生命。复验脚本为 `Library/HandPoints-20261022/verify-http.py` 和 `verify-shell-http.py`，临时服务均已停止。它们属于实际规则接口验证，**本轮未运行 Unity 实机出牌或 PlayMode，不把 .NET／HTTP 结果记为 Unity 验收**。

## 重点交互结果

- 顺子 23456：伤害依次为 **6、9、12、15、18**，不再把基础分塞进第一段；五次命中仍各施加 2 层毒。A2345 依次 3、6、9、12、15；10JQKA（不同花）依次 30、33、36、39、42。
- 葫芦 33322：全体 **40**、指定目标追加 **12**；三条和对子独立计分、独立取整，眩晕与免控保留。三级或持有倍率 +2 遗物时分项为 **67、24**。
- 道具／遗物：13003 倍率 +2 后，对子 22 为 **16**、顺子 23456 为 **100**；同花倍率遗物 10001 或倍率 +2 遗物 13003 后，同花顺 23456 为 **500**，纯同花最高 K 为 **195**，各自治疗仍为 **16／28**，不随伤害倍率放大。
- 暴怒龟缩：真实执行第三轮防御后获得 2 层免伤；连续三次单张 9 的 Play 伤害为 **0、0、9**，层数 **2→1→0→0**。第三次耗尽能量后自动进入敌方阶段，饮血手治疗 5+4，使本体最终又回满血；伤害事件明确记录先扣 9，不能仅凭回合后血条判断免伤。
- 暴怒与顺子：两次免伤挡住前两段，实际伤害 **0、0、12、15、18**，合计 **45**；本体 1500→1455，免伤耗尽，10 层毒仍正常施加。
- 暴怒与葫芦：本体两个分项均被免伤，层数耗尽；眩晕仍生效，群攻仍击断五只手并安排原再生倒计时。
- 所有免伤预览均不消耗真实层数；HTTP 连续预览两次后读取状态，确认权威状态不变，预览与提交各段一致。

## 全量套件的已有失败

以下 12 项在本轮修改前后同名、同错误消息，未作为本次新增问题处理：

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

完整名称、错误消息及基线对比见 `Library/HandPoints-20261022/regression-summary.json`。

## 载入与存档

`AdventureSceneHost.Initialize` 只编译一次内容。当前 Unity Play 中的宿主不会热读新配置，应退出 Play 后重新进入，再新开七站旅程。旧内容指纹存档仍按现有规则拒绝恢复，原文件保留；本轮没有迁移或放宽校验。

本轮计分验证结束时保留两个仓库的已有未提交改动，未重置、清理、提交或推送；后续提交说明见最新数值同步记录。开始时的文件散列和二进制 diff 保存在 `Library/HandPoints-20261022/`。
