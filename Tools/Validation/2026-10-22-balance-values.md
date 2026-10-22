# 七站主案最新数值同步与回归

2026-10-22。依据[飞书主案](https://moonton.feishu.cn/wiki/KZGawy0ypiSMPtkmqwDcAKLFnle) revision **4357**，同步玩家生命、怪物生命与攻击、同花、暴怒饮血治疗。用户已明确确认同花治疗 **50%**，心连心仍额外增加 30 个百分点。

## 数值与范围

小怪采用第十三节红色[数值汇总](https://moonton.feishu.cn/wiki/KZGawy0ypiSMPtkmqwDcAKLFnle#ZhUVdKtemo7eJQxLnIgcWOmFnWN)；第十二节仍有旧值。原始读取内容与文档 revision 保存在 `Library/BalanceValues-20261022/`。同花牌型表写 50%，结算段落仍写 20%，本次按用户确认采用 50%。未编辑飞书原文。

| 项目 | 修改前 | 当前七站实际值 |
| --- | --- | --- |
| 玩家基础生命 | 800 | **120** |
| 愚者开局生命／上限 | 960 | **144**，保留 +20% |
| 小怪一 | 450 血；两动作 40／65 | **250 血；两动作均 6** |
| 小怪二 | 节点内 562 血；两动作 50／81 | **250 血；两动作均 8**，节点生命倍率 1 |
| 流血精英 | 400 血；基础攻击 8 | **500 血；基础攻击 8**，每轮 +10、流血与交替动作保留 |
| 暴怒本体 | 1500 血；基础攻击 8 | **1500 血；基础攻击 8**，五手存活、未蓄力时实际攻击 33 |
| 四只饮血手 | 每只每敌方阶段治疗 5 | **每只治疗 10，四只合计 40**；束缚手不治疗 |
| 同花伤害属性 | 额外 ×5 | **额外 ×2** |
| 同花治疗 | 同花牌点数和 ×80% | **×50% 后向下取整**；心连心合为 80% 后统一取整 |

生产改动仅两个七站 XML：`SingleChapterBattle.xml` 版本 `.2026-10-22.4`，`SingleChapterAdventure.xml` 版本 `.2026-10-22.2`。相对本轮开始快照，实际为 15 项数值、4 个饮血行动名称及2个内容版本变更；XML 结构不变。小怪仍轮换 jab／strike，保持原动作和表现。

前轮已清零的所有牌型基础分及每级基础分增长保留。其他牌型倍率及成长、顺子分段／转火／中毒、葫芦分项与控制、皇家消灭、道具定义、龟缩两次免伤、断手 R+8 再生和大招时序均保持。宝箱金币100、休息回血20、旧三章暴食和独立标准单局配置保持原值；本次没有同步文档中其他经济或牌型差异。

## 实际完成的验证

| 检查 | 实际结果 | 证据，均在 `Library/BalanceValues-20261022/` |
| --- | --- | --- |
| 修改前全量 .NET 基线 | 1728 项：1716 通过、12 已有失败 | `results/full-before.trx` |
| 同花期望在旧配置上验证 | 38 项：25 通过、13 按预期数值失败 | `results/scoring-red.trx` |
| 新生命／攻击／饮血期望在旧配置上验证 | 15 项：7 通过、8 按预期数值失败 | `test-stats-red.trx` |
| 最终定向规则回归 | **197/197 通过**，0 失败／跳过 | `results/targeted-final.trx` |
| 最终全量 .NET | **1737 项：1725 通过、12 已有失败**，0 跳过；失败名称和错误消息与基线完全相同 | `results/full-after.trx`、`regression-summary.json` |
| 四战斗入口 HTTP | **76/76 通过** | `http-summary.json`、`http-responses.json` |
| 暴怒真实执行龟缩后的 HTTP | **2/2 通过** | `shell-http-summary.json`、`shell-http-responses.json` |
| 新数值／道具／真实冒险 HTTP | **7/7 通过** | `runtime-http-summary.json`、`runtime-http-responses.json` |
| 增量范围与工作区保护 | 配置范围符合四类数值；既有字体文件 SHA 不变 | `scope-check.json`、`before-manifest.json`、`*-before.diff` |

定向范围包括 `SingleChapterScoringTests`、`SingleChapterContentTests`、`SingleChapterEnemyRulesTests`、`AdventureEconomyTests`、`DesignScoring2951Tests`、`DesignItem2951Tests`、`CombatWrathImmunityTests`、`WrathBossRulesTests`、`WrathHandEffectRestrictionsTests` 和 `EliteBleeding` 系列。四条七站路线与存档恢复继续通过。

测试读取磁盘真实七站配置。计分用例通过公开管理员接口设置手牌、遗物及承伤／治疗空间；升级用例只添加等级输入。长回合精英、Boss大招和再生用例只提高测试玩家生命，以观察机制。另有正常基础120／愚者144的生命和死亡用例，没有修改生产承伤规则来延长生存。

HTTP 使用原始磁盘 XML，经真实规则宿主接口运行，内容 SHA 与当前文件一致。实际冒险用四张 K 击败首战后进入第二站，验证愚者144和第二站250血／攻击8。三个临时服务均已停止。复验脚本为 `verify-http.py`、`verify-shell-http.py`、`verify-runtime.py`。

## 关键交互实测

- 同花 K、9、7、4、2：高牌13 × 1 × 2 = **26**；牌点和35治疗 **17**。持心连心仍伤害26、治疗 **28**，证明先合比例50%+30%再统一取整，不错误分开取整为27。
- 普通同花顺23456：五段 **12、18、24、30、36**，合120；治疗10，心连心治疗16。10001或13003倍率道具后五段 **20、30、40、50、60**，合200，治疗仍10。
- 不同花顺子23456仍为 **6、9、12、15、18**；葫芦33322仍为全体40、指定目标追加12。皇家同花顺仍消灭，基础治疗30／心连心48。
- 龟缩实际执行后获得2层。三次单9的伤害 **0、0、9**，层数 **2→1→0→0**；顺子对龟缩结算 **0、0、12、15、18**，免伤耗尽且仍施加10层毒。预览不会消耗真实层数。
- 三次手雷对龟缩本体结算 **0、0、20**，仍群攻断手且不消耗出牌能量。120血上限下药水仍免费治疗10。
- 四只血手对受伤本体分别治疗10，实际 **1400→1440**，束缚手没有治疗。断一只血手后每敌方阶段治疗30，第R+8轮再生并恢复40。

新增饮血回归曾出现“预期30／实际60”。诊断显示一次冒险命令使已完成轮数 **4→6**：第5轮束缚自动跳过玩家窗口并继续第6轮，两个敌方阶段各治疗30。现用同一真实战斗的 `TeamBattleEngine` 逐阶段检查，每次断言回合数+1，保留30／40和再生时点要求；生产自动推进逻辑未改。诊断在 `blood-heal-diagnostic.log/.trx`，修正后15项夹具全通过，最终197项亦通过。

## 尚未验证的边界

- **本轮没有运行 Unity 编译、EditMode 或实际 PlayMode／界面出牌验收**；以上 .NET 与 HTTP 结果均不记作 Unity 实机通过。Unity 七站 PlayMode 测试源码已同步精英500和长回合玩家耐久夹具，待后续场景执行确认。
- 没有实际跑满20轮Unity场景；保留并通过了20轮判负的纯规则回归。
- 七站当前12件道具没有激活 `LifeSteal`／`OverhealDamage` 状态的入口，公开HTTP管理员入口也不能直接添加状态，故没有把这两种状态列为本次七站HTTP覆盖；验证了满血同花溢出治疗不凭空转伤。通用状态原有测试随全量套件运行。
- 本次只落实已指定数值，没有做完整通关胜率、平均回合数或跨种子平衡测量，因此没有附加调整其他倍率、道具或Boss机制。

## 全量已有失败

以下12项修改前后名称和错误消息完全一致，完整消息见 `regression-summary.json`：

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

## 载入与工作区

`AdventureSceneHost.Initialize` 只读取一次配置。运行中的宿主不会热读本次XML；应退出 Play 后重新进入，再新开七站旅程。旧指纹存档按已有校验拒绝恢复，原文件保留；本次未放宽校验或迁移旧档。

数值调整验证结束时，保留两个仓库所有本轮前未提交内容，未重置、清理、提交或推送。字体既有改动保持不变。前轮计分回归文档保留历史数值，并补充指向本文的版本说明。

## 提交前复验

2026-10-22，用户随后要求提交并推送本次数值调整及回归。两个现有分支均为 `codex/wrath-boss-20261022`，拉取远端分支引用后与本地 HEAD 一致。提交前重新运行全量规则套件：1737 项中1725通过、12失败，失败名称及错误消息与修改前基线完全一致；其中197项专项全部通过。证据位于 `Library/BalanceValues-20261022/push/prepush-full.trx` 与 `prepush-summary.json`。

本次提交范围包含牌型基础分清零、最新平衡数值、相应NUnit及PlayMode测试源码、文档和BattleToolkit子仓引用。字体资源、旧高牌初始排查及暴怒免伤排查记录继续留在本地，不纳入提交。Unity实机验收状态仍按上文记录。
