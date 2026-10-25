# 七站冒险：流血、主动道具与商店提示（2026-10-24）

## 改动范围

- 精英流血意图使用中文“流血”和已有血滴图标；“×3”表示施加3层，不是把伤害直接乘3。
- 玩家原有血条下方状态区域显示血滴、当前流血层数及承受攻击增幅，悬停可查看完整规则。读取展示游标上的已结算状态，不提前泄露权威状态。其他状态继续显示。
- 仅单人七站规则配置使用“你”；通用战斗、旧规则和多人身份保留编号。
- 俄罗斯转盘图标增加“主动”，生效时显示实际当前牌型伤害倍率。详情注明点击使用并显示倍率；战后/图标重绑时清理旧标签。
- 用户确认样图后，商店遗物卡片仍可点击查看详情。金币不足时原按钮整行显示红色“金币不足 · N 金币”；品质/高风险说明保持充足时的原颜色，不重复显示不足提示。金币充足、已售出及详情内确认购买沿用原逻辑。

保持战斗数值、牌型、道具触发、精英/Boss、普通战斗和旧三章节点不变；未改场景、美术资源、整体布局或 BattleToolkit。未提交或推送。

## 突然死亡调查

当前主存档已是新开局，因此只读重放上一局完整备份。官方 Restore 的内容指纹、最终摘要和进度校验均成功（revision 44，Completed）。

- revision 4 在宝箱取得13002“致命一击”，实例 `relic/0`。
- revision 25 对精英出两张牌：先正常扣9血（651→642），再由 `item/execute` 触发 `Eliminate`（642→0）。
- revision 42 对暴怒本体出两张牌：先正常扣6血（1939→1933），同一遗物触发 `Eliminate`（1933→0）。

该遗物对符合条件的牌型主伤害、实际扣血且仍存活的目标独立判定10%斩杀，可以命中精英和Boss。顺子的多段主伤害可多次判定；免伤或完全格挡造成实际生命伤害0的该段不触发。斩杀自身属于 `Eliminate`，不是普通 `Damage`。本次保持这些已有规则。

脱敏重放证据：`Library/StatusReadability-20261024/sudden-death-replay.txt`。主存档和备份读取前后SHA256一致，未写入用户存档。

## 验证环境与记录

Unity 2022.3.62f3（96770f904ca7），已有隔离项目 `/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`。同步工作区脚本；七站场景、Packages、关键ProjectSettings及SingleChapterBattle内容逐字节一致。用户原项目不关闭、不用于测试写存档。

- 已执行红测：流血/称呼原缺失、转盘图标原缺失、点击详情主动说明原缺失、商店品质行原重复不足提示；结果及失败原因保存在本轮Library目录。
- 第一批聚焦EditMode：12/12通过（`status-focused-green.xml`）。
- 引擎相关回归：85/85通过（`engine-related.log`）。
- 最终相关EditMode：127/127通过，0失败（`editmode-final2.xml`）。包含中文意图、真实状态层数与不同catalog比例、自然耗尽/换局/净化、多人称呼兼容、转盘标签复用与战后清理、实际点击详情不激活不花AP、商店金币切换与购买门控，以及已有Boss提示/两次免伤、目标、牌型和连击回归。
- 实际有渲染PlayMode：5个不同用例最终均通过。首批 `playmode-attempt1.xml` 为4通过、1测试夹具失败（seed312的转盘第2回合爆炸，无法观察下一阶段）；仅调整测试夹具，使用只读预览有界选种，seed313/爆炸第6回合，转盘复测 `playmode-roulette-final.xml` 为1/1通过。未改变生产随机规则，也未重复已通过的4项。
- Unity编译通过；最终EditMode及转盘有渲染Play日志均无 `error CS`，均以code0退出。两个实际Play批次的测试清理均断言无游戏Error/Exception；Metal日志中的memoryless depth load/store为编辑器警告。

实际Play验收项目：

| 项目 | 结果 |
| --- | --- |
| 精英流血 | 中文意图/既有血滴图标；真实结算后2层/+60%、下一轮1层/+30%；字形和图标均在屏幕内。真实点击葫芦出牌净化后隐藏流血，控制免疫仍显示。 |
| 俄罗斯转盘 | 持有只显示主动；真实图标射线点击详情不花AP/不激活。真实点使用后×10，真实下一轮×100；图标和详情一致，AP/手牌不因使用改变，已生效不可二次启用。 |
| 商店 | 11金币不足购买全部4件时，品质与充足时文字/颜色一致，按钮整行红色；实际点击图标打开详情，金币/物品/revision不变，购买按钮禁用，关闭正常。 |
| Boss回归 | 保留现有目标轮廓；真实A2345在两次免伤下逐段扣血 `[0,0,9,12,15]`；两次独立高牌实际观察免伤2→1→0。 |
| 普通战斗回归 | 原飞牌/踢牌、主动道具、实际击杀收尾及奖励页流程通过。 |

实际Play测试加载七站原场景临时副本，禁用自动存档，以管理接口准备血量/遗物/金币/手牌夹具并跳过无关关卡；实际操作结算与UI射线点击，分别核验可见字形、图标、效果和状态。纯引擎或EditMode不作为实际Play验收。

引擎命令：

```sh
bash Tools/test-poker-engine.sh SingleChapterEnemyRulesTests CombatEliteBleedingTests EliteBleedingInteractionTests DesignItem2951Tests RelicNumericBattleTests CombatWrathImmunityTests
```

## 已有改动保留

开始前已保存两个仓库状态、补丁、35项原有脏文件及SHA256记录到 `Library/StatusReadability-20261024/`。本轮在已有信息交互改动上增量修改，不重置、暂存或提交。

最终核对：原有35文件无缺失，32项逐字节不变；其余3项为本轮增量修改的商店页面、对应回归和世界HUD的流血caption/图标映射。字体缓存及4份既有调查/验收文档SHA256不变。上一批Boss免伤次数、目标、牌型/连击等改动保持。记录为 `preservation-after.json`。49个修改/新增脚本及meta与实际Unity验证副本逐字节一致（`verification-copy-parity.json`）。

主仓库HEAD仍为 `310f0f091c6e5a63c0d368dd8eac3a7a5f0d5fb6`；BattleToolkit仍为 `f3f353a5f520c1bc662680296973ab82d956c310`，其工作区保持干净。本轮脚本范围 `git diff --check` 通过；全仓原有字体缓存的空白行未处理。

## 实际截图

- [流血2层和承受攻击+60%](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/elite-bleeding-two-stacks.png)
- [流血降至1层和+30%](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/elite-bleeding-one-stack.png)
- [转盘主动使用前](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/roulette-inactive.png)
- [转盘当前×10](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/roulette-active.png)
- [转盘下一轮×100](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/roulette-next-turn.png)
- [商店品质原色与金币不足红字](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/shop-relic-insufficient-gold.png)
- [金币不足仍可查看详情](/Users/mt/code-yang/Texas-Holdem/Library/StatusReadability-20261024/screenshots/shop-relic-unaffordable-detail.png)

本批已确认需求均完成，无待确认商店项。没有导出独立构建、重跑20回合长测或全项目全部测试；本轮采用相关回归和有渲染七站冒险验收。
