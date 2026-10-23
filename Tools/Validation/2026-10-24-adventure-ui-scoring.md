# 七站冒险界面与计分调整验证

日期：2026-10-24（Asia/Shanghai）。基于主仓 `1fdb051`、BattleToolkit `18e7c80`，保留开始时已有的全部未提交改动；本次提交记录见两仓Git历史。

## 提交范围与验证上下文

本次提交仅包含界面优化、牌型规则调整及其相关回归。下述实际测试在保留既有补丁的完整本地工作区运行；此前未提交的首回合敌方阶段预警、四秒自动消字、相关Boss回归、20回合验证及普通出牌飞行观察窗口修正继续留在本地，未包含于本次提交。字体缓存和旧验证文档也未纳入。

新增界面测试需要主测试类为`partial`，该声明随本次测试提交。两个混合文件按改动内容选择性暂存，保留旧补丁。116项EditMode、12项PlayMode等均为完整本地工作区的实际验证结果；按用户要求，提交前没有为选择性暂存的提交树重复运行测试，也不将这些结果描述为纯提交树的独立验收。

## 实现范围

- 休息点显示“命运抽奖 · 有风险”和“免费抽奖 · 有风险”，说明免费参与、无入场费，但结果可能失去道具或最多500金币（不超过当前余额），可直接继续旅程。经济结算、概率和次数限制未改。
- 当前只有一个可用愚者时，角色与开局天赋合并为“出发准备”，默认选择，开始前直接展示天赋描述及最大生命120→144。多塔罗选择和图鉴入口保留。
- 商店点击商品图标或卡片即查看详情，不扣金币；详情中“确认购买 · X 金币”才购买。已售出商品仍可查看。离店按钮扩大并改为“离开商店 → 继续旅程”，右上角明确为“保存并返回首页”。
- 指南增加独立“同花”“同花顺”入口，即使当前牌库无法组成也能读基本规则。首屏展示当前配置的同花额外倍率与基础治疗比例，默认“同花额外 ×5 · 基础治疗50%（未计遗物）”。倍率、等级、葫芦分项及实际预览读取权威配置；动态×7／25%测试防止写死。旧模式保持原入口布局。
- 删除Boss血条下附加的剑形“攻击 !”预警框。原本常规攻击／大招实际伤害预告、红光、断手再生及战斗机制保留；本地既有四秒黑底红字补丁继续保留于工作区。

## 计分变化

仅七站 `SingleChapterBattle.xml` 开启新规则；标准单局和旧三章配置未改，新配置开关默认关闭。

| 项目 | 现在的规则与一级无加成示例 |
|---|---|
| A | 仅A2345（含同花）计1；其他情况计14。单A伤害14、AA伤害56；不完整A234仍按A14。 |
| 顺子 | 原倍率×3、逐张分段保留；A2345合计45，依次3／6／9／12／15。10JQKA合计180，A所在末段42。 |
| 纯同花 | 五张参与计分的同花牌点数总和×5。2、5、7、9、K合计36，伤害180、基础治疗18；A、2、5、7、9伤害185、治疗18。无效牌仍不贡献伤害与治疗。 |
| 三条 | 基础倍率从2改为3，222对每个敌人造成18。等级倍率增长、道具和群攻行为保留。 |
| 葫芦 | 两个分项各有效×4保持不变，33322为全体36＋指定目标16；同花属性再各×5。控制效果不变。 |
| 普通同花顺 | 仍为顺子×3再乘同花×5，合计×15。23456伤害300；A2345伤害225、基础治疗7，携带心连心治疗12。皇家同花顺原特殊效果不变。 |

商店复制牌组成的同花对子／三条／葫芦保留原复合牌型分项，不把旁张改成全部计分。同花治疗仍独立结算，心连心增加30个百分点；既有倍率道具、牌面强化和等级规则保留。

为使独立三条调整不改变葫芦，七站显式保留葫芦三条分项原基础倍率2，再由原葫芦技能系数2结算为有效×4；分项等级、遗物和强化仍正常参与。

## 实际验证

证据目录：[Library/AdventurePolish-20261023](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023)。

Unity **2022.3.62f3 / macOS / Metal**。使用独立验证副本 `/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`，不关闭用户原编辑器、不写玩家存档。图形PlayMode启用真实渲染；纯C#结果未充当Unity验收。26个发生变化的C#/XML文件与验证副本逐字节一致，见 [validated-source-comparison.json](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/validated-source-comparison.json)。

| 检查 | 实际结果 | 证据 |
|---|---|---|
| Unity编译 | 0个C#编译错误 | `editmode-green.log`、`editmode-guide-final.log`、`playmode-final.log` |
| Unity EditMode | 116/116通过；首屏治疗说明最后调整后专门复跑5/5通过 | `editmode-green.xml`、`editmode-guide-final.xml` |
| Unity图形PlayMode | 12个不同用例最终均通过；六个界面／战斗专项先通过，指南五项与完整七站流程最终6/6通过 | `playmode-ui.xml`、`playmode-final.xml` |
| 相关引擎回归 | 109/109通过 | `scoring-engine-related.log` |
| 引擎全量 | 1805项：1792通过、13项已有失败；准确基线1800项：1787通过、同样13项失败 | `scoring-engine-full.log`、`scoring-engine-baseline-full.log`、`scoring-engine-comparison.json` |
| 工作区检查 | 两仓`git diff --check`通过；原17个未提交文件中15个字节不变，另外两个仅叠加本次测试／配置调整，原改动保留 | `workspace-preservation.json`及`baseline/` |

Unity PlayMode实际覆盖：

1. 愚者准备页在开始前显示效果，开始后生命144。
2. 真实UI射线点击商店图标，查看／关闭不扣款；确认后准确扣款、增加库存；离店进入地图。
3. 抽奖只结算公布结果，没有额外入场费；重复事件被权威层拒绝，余额不为负。
4. 普通出牌／道具／飞行表现／遗物及胜利回奖励流程。
5. Boss五只手原演员及PSD贴图的断手、紫色倒计时、再生。
6. 第1回合敌方阶段入口预警、四秒消字且红光保留，第2回合释放、第7回合再次预警、第8回合释放；移除“攻击 !”不影响这些行为。
7. 指南无可用组合时的同花／同花顺说明、普通入口切换、旧模式兼容、懒查询、预览缓存与目标变化。
8. 经实际按钮走完七站冒险，含普通战、商店存档恢复与改牌服务、精英、休息、Boss及结算。

引擎109项覆盖新A点数、纯同花、三条、无效牌、等级与道具、同花治疗和心连心、复合同花、葫芦分项及控制、皇家同花顺、轮盘×10、顺子分段与逐次附加状态、Boss两次免伤和手雷交互、预览与正式结算一致、旧配置与七站敌人规则。

本次曾出现的PlayMode失败属于旧全流程测试步骤与现行UI不一致：胜利页已无保存返回按钮、商店服务按钮已换布局、精英单轮后攻击应为8+5=13，以及PSD组合Boss不采用普通小怪的远景纵深。测试已按现有产品行为校正并实际复跑通过，生产战斗数值和Boss布局未为迁就测试而改动。普通小怪仍严格检查纵深，Boss保留PSD逐层贴图、演员数量和位于手牌后方的检查。

## 已知未通过项与验证边界

以下13项全量失败在本次改动前的准确基线中全部复现，失败集合相同，没有新增失败；本次未扩展修改这些既有问题：

```text
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
RoyalPlayPreviewIsPureAndEliminationStillCompletesTheAttachedFlushHealing
```

包含溢出／事务回滚、状态对象引用、伤害上下文、手牌容量边界与旧暴怒皇家同花顺治疗断言。最后一项期望99912、实际99909，基线一致。故不宣称引擎全量通过。

当前仅愚者的准备流程已验证；未来增加多个可选塔罗时应补多选页面图形验收。本次没有重复20回合长测，也没有进行平衡性调参或接入新美术。

## 界面截图

- [合并后的出发准备](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/journey-preparation.png)
- [免费抽奖风险说明](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/rest-lottery-risk.png)
- [商店与离店按钮](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/shop-clear-navigation.png)
- [图标点击后的详情与购买确认](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/shop-icon-detail.png)
- [同花指南](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/guide-flush.png)
- [同花顺指南](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/guide-straight-flush.png)
- [Boss文字已消失、红光仍保留（含本地既有四秒提示补丁）](/Users/mt/code-yang/Texas-Holdem/Library/AdventurePolish-20261023/screenshots/boss-round-1-warning-expired.png)

截图中商店大额金币和Boss高生命来自测试夹具，不是正式平衡值。
