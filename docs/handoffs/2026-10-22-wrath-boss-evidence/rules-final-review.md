# 暴怒 Boss 最终规则与迁移复核

日期：2026-10-22。范围为当前工作区规则、内容配置、迁移证据及本轮已完成的 .NET 测试；本次复核没有再次运行完整测试集，没有启动 Unity，没有修改代码或其他现有文档。下述规则数据和测试断言不能代替 Unity 场景、演出或平衡实机验收。

## 结论与证据口径

当前默认暴怒配置与已确认实施规格的关键边界一致：独立五手、动态 D、四步常规序列、独立大招、逐段免伤、R+8 再生、手先于本体行动、替死中断和第 20 轮判负均有实现及对应规则测试。未发现默认内容新增的阻断性规则冲突。完整规则集仍有 13 项既有失败，因此不能宣称全量全绿。

这里的“默认暴怒 XML”是独立暴怒场景实际引用的 [StandardSingleBattle-2026-10-21.xml](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:215)，场景入口常量见 [WrathBossSetup.cs](/Users/mt/code-yang/Texas-Holdem/Assets/Scripts/Gameplay/SingleBattle/Presentation/Scene3D/Editor/WrathBossSetup.cs:15)。不带日期的 `StandardSingleBattle.xml` 和历史 `.20.xml` 不是本次暴怒验收的配置来源；旧 `WrathThousandHandsTests` 也不能替代新规则用例。

实施依据为 [2026-10-22-wrath-boss-design.md](/Users/mt/code-yang/Texas-Holdem/docs/superpowers/specs/2026-10-22-wrath-boss-design.md)。旧 revision 4300 中的歧义已经通过该规格收敛，本报告不重新引入“每 10 轮大招”“提前 8 轮预告”或旧五手功能。

## 默认规则与平衡基准

以下数值直接来自当前 XML；已核对独立模板 [10_WrathThousandHands.xml](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/Templates/10_WrathThousandHands.xml:215) 的本体、手及计时参数相同。

| 项目 | 当前基准 |
| --- | --- |
| 本体、五手生命 | 本体 1500；四饮血手和一束缚手各 1，正式再生恢复 1 |
| 初始攻击 | 基础 8；D = 8 + 5 × 存活手数 + 10 × 成功蓄力次数，因此五手初始 D=33，全断且未蓄力 D=8 |
| 常规周期 | 普攻 → 普攻 → 防御 → 蓄力；预览未来 8 次常规意图；蓄力成功永久 +10，眩晕跳过不加、不推进序列 |
| 防御 | 本体刷新到 2 层独立 DamageImmunity；保留至消耗；不提供数值护盾，不附带旧存活手减伤 |
| 饮血 | 每只存活且可行动的饮血手每敌阶段治疗本体 5，四只最多 20；不足血量才产生有效治疗，不超过上限 |
| 束缚 | 全场第 5、10、15、20 轮；使玩家跳过下一玩家行动窗口；错过不补发，免控可挡 |
| 大招 | 首次第 5 轮预警、第 6 轮释放；之后正常为 11/12、17/18；普攻/防御/蓄力在前，额外 3D 在后 |
| 断手再生 | R 轮死亡，R+8 轮敌阶段开始再生；独立期限，同轮到期全部复活，当轮即可参与行动 |
| 胜负与护盾 | 本体死亡即可胜利；第 20 轮完整结算后判负；玩家旧护盾在 PlayerTurnStart 清除 |
| 默认玩家 | 800 最大生命、初始护盾 0，baseline 无遗物；这不是耐久测试夹具的 100000 血玩家 |

配置位置：[规则和状态](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:34)、[玩家](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:153)、[本体与手](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:215)、[无遗物 loadout](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:310)。

在“五手始终存活、本体不受控、没有攻击/承伤修正、玩家足够存活”的假设下，下面是从配置与公式推导的排程，不是新增引擎测量或 Unity 运行记录：

| 轮次 | 常规动作 | 该轮基础伤害合计 | 说明 |
| --- | --- | ---: | --- |
| 1 / 2 | 普攻 / 普攻 | 各 33 | 初始 D=33 |
| 3 / 4 | 防御 / 蓄力 | 各 0 | 第 4 轮后 D=43 |
| 5 | 普攻 | 43 | 预警；束缚下一玩家窗口 |
| 6 | 普攻 | 172 | 43 + 大招 129 |
| 7 / 8 | 防御 / 蓄力 | 各 0 | 第 8 轮后 D=53 |
| 9 / 10 | 普攻 / 普攻 | 各 53 | 第 10 轮束缚 |
| 11 | 防御 | 0 | 第二次预警 |
| 12 | 蓄力 | 189 | 蓄力先使 D=63，再释放 3D |
| 13 / 14 | 普攻 / 普攻 | 各 63 | 第 15 轮防御、束缚；16 轮蓄力 |
| 17 / 18 | 普攻 / 普攻 | 73 / 292 | 17 轮预警；18 轮为 73 + 219 |
| 19 / 20 | 防御 / 蓄力 | 各 0 | 第 20 轮后 D=83，完整阶段结束判负 |

上述条件下前 12 轮基础伤害累计 576，前 20 轮 1067；800 血玩家若全程无防御、治疗、断手或控制，按算术会在第 18 轮死亡。每轮最多 20 的治疗是可用治疗量，不代表本体每轮实际恢复 20。胜率、玩家实际输出和通关回合分布尚无本报告可引用的多种子测量，不能由这组排程推出“平衡已通过”。

## 已跑实际规则测试 observations

本轮先前已用仓库 `.runtime/dotnet/dotnet`（SDK 10.0.402、Runtime 10.0.13）执行真实规则测试；本次只重新读取 TRX，不重跑。编译 bin/obj 位于独立 `Library/WrathUnityValidation/DotnetArtifacts`。

| 结果 | 计数 |
| --- | --- |
| 新暴怒定向 | 59 执行、59 通过、0 失败、0 跳过 |
| 定向构成 | WrathBossRules 32、CombatWrathImmunity 9、WrathHandEffectRestrictions 15、WrathAdventureHealth 2、WrathAdventureReplay 1 |
| 完整规则 | 1667 执行、1654 通过、13 失败；跳过、中止、超时均 0 |
| 与附件干净 HEAD 基线及最新附件比较 | 13 个失败名称和错误消息完全相同；新增失败 0，消失失败 0 |

通过用例中的具体数值断言包括：

- 初始 D=33；第 6 轮真实预览和结算都是 `[43,129]`、合计 172；第 12 轮先蓄力后实际伤害 189，常规游标每轮只前进一次。见 [WrathBossRulesTests.cs:80](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:80)。这些推进用例默认把玩家最大生命提高到 100000，仅用于排除提前死亡干扰，见同文件第 14 行。
- 玩家阶段断手立即取消预警、D 降 5、显示剩余 8；第 1、2 轮分别断手，对应期限为第 9、10 轮；同轮到期的四饮血手同时复活并立即治疗。见 [断手与独立期限](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:99)、[同步再生](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:230)。
- 本体先受 100 点伤害、一个饮血手和本体各有 1 层毒时：该手在自己入口被毒杀，剩余三手治疗 15，本体再受毒 1，结果 1414。四手同轮复活且本体有 1 层毒的场景，净恢复 19。见 [毒与治疗顺序](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:133)。
- 免伤测试确认前两段归零、第三段才扣护盾/生命；原始零伤害及打手不消本体层；毒和反伤可消层；免伤不阻止附加状态及 Eliminate；旧 layer-armor 仍为四分之一伤害。见 [CombatWrathImmunityTests.cs:31](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/CombatWrathImmunityTests.cs:31)。
- 普攻、大招、手入口触发玩家替死均覆盖；普通反伤击杀本体阻止后续大招；第 20 轮判负不消耗替死道具。额外大招没有第二次 ActionCompleted：人为附加的精英流血 5 层只减至 4。见 [完成次数](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:169)、[替死与反伤](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:255)、[20 轮](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:187)。
- 手拒绝正向攻击和护盾，但玩家、本体及普通敌人的相同增益仍有效；复活/净化不删除身份限制。章节生命倍率 2 时，本体为 3000、手仍为 1，普通敌人继续成长；冒险导出/恢复跨越断手、蓄力、再生至第 12 轮 189 伤害保持一致。见 [手限制测试](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathHandEffectRestrictionsTests.cs:39)、[生命成长](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathAdventureHealthTests.cs:12)、[存档重放](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathAdventureReplayTests.cs:57)。

日志：[定向 log](/Users/mt/code-yang/Texas-Holdem/Logs/WrathUnityValidation/dotnet-targeted.log)、[定向 TRX](/Users/mt/code-yang/Texas-Holdem/Logs/WrathUnityValidation/wrath-rules-targeted.trx)、[完整 log](/Users/mt/code-yang/Texas-Holdem/Logs/WrathUnityValidation/dotnet-full.log)、[完整 TRX](/Users/mt/code-yang/Texas-Holdem/Logs/WrathUnityValidation/rules-full.trx)。历史失败对照为 [test-results-summary.json](/Users/mt/code-yang/Texas-Holdem/docs/handoffs/2026-10-22-wrath-boss-evidence/test-results-summary.json)。早期验证文档中的 1652 项尚未包含后来 15 项手限制测试；当前 TRX 和最新附件为 1667 项。

现存归档 `Library/WrathValidation/wrath-http-final.log` 实际记录 4 项 HTTP 测试通过，且没有逐回合数值采样；不能把旧记录中较大的概括计数或日志缺失的采样当成本轮测量。上面的 observations 来自本轮通过的规则测试断言，推导排程另行标明。

## 关键边界与普通、精英隔离

1. **按配置启用。** 无 `Behavior.Wrath` 时仍走 `AttackAtRound`；手限制也只从已配置本体的五手引用产生。单场只支持一个本体，且每个手引用必须恰好对应一个单位。见 [TeamBattleEngine.cs:186](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:186)、[创建校验](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:271)、[WrathHandEffectRestrictions.cs:10](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/WrathHandEffectRestrictions.cs:10)。
2. **状态与死亡期限。** 蓄力、就绪/释放轮和逐手再生期限保存在不可变运行态；每次 TeamBattleState 构造观察死亡/复活。再生走正式 Revive，保留 ID、清状态和护盾；先再生再构造敌方队列，手排本体前。见 [WrathBoss.cs:40](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/WrathBoss.cs:40)、[死亡观察](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/WrathBoss.cs:118)、[TeamBattleWrathBoss.cs:9](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleWrathBoss.cs:9)。
3. **主攻击及生命周期。** 常规动作及反应结算后才调度大招，大招使用 EnemyAttack 来源及通用攻击/承伤、反应、替死链路；完成时钟在二者之后只跑一次。眩晕、断手、提前结束敌阶段均会取消到期释放并重新冷却。见 [调度](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleWrathBoss.cs:37)、[正式攻击绑定](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:587)、[完成时钟](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:635)。
4. **逐段最终免伤。** 新行为只对显式 DamageImmunity 状态生效，所有承伤修正完成后、扣护盾/生命前归零；Eliminate、LoseHealth 不在该 Damage 分支内。护盾拒收也是最后归零，不能被末端固定加值重新抬起。见 [CombatEngine.cs:388](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Combat/CombatEngine.cs:388)。普通护甲未被改成免伤。
5. **终局优先级。** 本体死亡胜利判定先于轮数判负；到第 20 轮仍完整结算敌阶段后再判负，不通过伤害制造失败。Wrath 的 PlayerTurnStart 护盾到期与普通/精英一致；旧暴食仍保留自己的 AfterEnemyPhase/Enrage 配置。见 [TeamBattleEngine.cs:730](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:730)、[玩家入口清盾](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:759)。
6. **成长和地图。** 五手仅以 `scaleHealthWithAdventure=false` 豁免成长，本体及普通敌人仍走原章节倍率；默认冒险三个 Boss 节点仍指向暴食。见 [BattleAdventureSession.cs:252](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Adventure/BattleAdventureSession.cs:252)、[StandardAdventure.xml:20](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardAdventure.xml:20)。

当前流血精英仍为 **400 血、初始攻击 8、每轮 +10、施法/普攻两步序列**，不是旧资料的 1800/90；其流血通过 `elite-bleeding` 单独施加，暴怒默认没有施加该状态。见 [精英配置](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:177)。`git diff HEAD` 同时包含既有流血精英改动和本次暴怒改动，因此不能把“普通/精英隔离”写成“整个工作区精英与干净 HEAD 相同”；这里的结论来自当前配置、分支条件和同场对照测试。

## 未覆盖风险与内容扩展边界

- **已知测试债。** 13 项通用规则失败包括溢出/事务回滚断言、创建时异常、状态引用、快照拒绝及承伤 100/90 的差异。集合未扩大只能说明未观察到新增失败，不能证明这些通用链路可靠，也不能通过修改普通规则或放宽断言掩盖它们。
- **治疗目标依赖默认顺序（代码事实及扩展风险）。** 四饮血手使用 `Allies/FirstLiving`，默认遭遇把本体放在五手之前，因此治疗本体。若未来重排遭遇敌人或在本体前插入友方单位，可能治疗其他单位；创建校验没有约束本体必须排首。当前默认配置没有该问题，也没有覆盖改序用例。见 [遭遇顺序](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Content/StandardSingleBattle-2026-10-21.xml:340)、[目标解析](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/BattleTargetResolver.cs:23)。
- **同批死亡再立即复活（静态推断，未复现）。** 期限/预警取消依据 TeamBattleState 观察到的最终 CombatState。若未来自定义效果在同一个 CombatEngine 批次内先杀手再立即 Revive，最终状态可能只看到存活，遗漏“中途死亡即取消预警”的事实。当前 XML 没有该效果组合；已测的是正式断手与后续阶段再生，不能据此宣称任意批次组合全覆盖。
- **内容校验范围。** 引用数、唯一性、计时和蓄力 actionId 有校验；固定五手是当前结构约束。手最大生命 1、治疗 5、束缚周期 5 来自 XML，不是通用引擎强制不变量。后续编辑模板时需一起复核，不能仅改展示文字。多本体同场明确不支持。
- **平衡及实机覆盖。** 当前真实规则测试多用耐久玩家、确定牌组或定向注入；尚不足以评估默认 800 血、随机牌局、多种子胜率、完整跨场构筑的平衡。本报告不评价正在由主线程进行的 Unity 验收结果，不把 .NET、HTTP 或静态源码证据写成 Unity 实机通过。

## 迁移复核

两仓当前分支仍为 `codex/wrath-boss-20261022`，HEAD 与交接 manifest 一致：主仓 `1500cb1223dc8cc968256563d9420f7784893634`；BattleToolkit `2228665bf9d8afa8b5c0807db32e933653a0c463`。`Packages/manifest.json` 的本地 Engine 依赖和 testables 正确。

首次验证及 .NET 测试结束后的验证均为 **119 个记录文件、0 缺失/变更**。现场 Unity 验收工作进行后，本次最终只读重核为 **119 个记录文件、6 项已变化**：

```text
Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset
Assets/Scripts/Gameplay/SingleBattle/Presentation/Scene3D/Runtime/UI/SingleBattleHud.Wrath.cs
Assets/Scripts/Gameplay/SingleBattle/Presentation/Scene3D/Tests/Editor/WrathBossSceneTests.cs
Assets/Scripts/Gameplay/SingleBattle/Presentation/Scene3D/Tests/Editor/WrathHudPromptTests.cs
Assets/Scripts/Gameplay/SingleBattle/Presentation/Scene3D/Tests/PlayMode/Poker.SingleBattle.Tests.PlayMode.asmdef
External/BattleToolkit/Engine/Tests/Editor/WrathAdventureHealthTests.cs.meta
```

这些是相对于交接快照的当前文件 hash 差异，不是两仓分支/HEAD 漂移。清单内没有规则 C#、暴怒 XML 或规则测试 C# 的新差异；本报告没有覆盖或刷新 manifest，也没有恢复这些现场改动。脚本范围只覆盖记录的变更/未跟踪文件和选定环境文件，并非全项目完整备份校验。新增的本报告不在原 manifest 119 项内。

## 本轮规则覆盖审计：59 条定向与 10 条 Unity 用例

本节只审已确认规格的规则语义，不复审模型、布局或显示质量。重新读取定向 TRX，结果仍为 **59/59 通过**；逐一核对对应测试源码及当前 10 条暴怒 PlayMode 的实际断言。主线程正在运行的完整 PlayMode 入口为 [wrath-and-existing-playmode.log](/Users/mt/code-yang/Texas-Holdem/Logs/wrath-and-existing-playmode.log)，本节的 Unity 列表示源码覆盖，不提前宣称该轮运行通过。既有 `wrath-playmode-second.xml` 是旧 6 条结果，不能替代本轮 10 条。

定向集合没有遗漏：`WrathBossRulesTests` 的 30 个方法计 32 条（主攻击修正方法有 3 个参数案例）；`CombatWrathImmunityTests` 9 条；`WrathHandEffectRestrictionsTests` 的 11 个方法计 15 条（正负攻击组合 4 个、末端加盾 2 个案例）；冒险生命倍率 2 条、冒险重放 1 条。以下按规格逐项映射，其中同一测试可以证明多条规则。

Unity 记号均指 [WrathBossScenePlaybackTests.cs](/Users/mt/code-yang/Texas-Holdem/Assets/Scripts/Gameplay/SingleBattle/Presentation/Scene3D/Tests/PlayMode/WrathBossScenePlaybackTests.cs)：U1=初始六单位与 D（59）；U2=实际出牌断手及 R+8 再生（97）；U3=取消预警与重开重置（146）；U4=默认束缚及第 6 轮额外大招（202）；U5=顺子免伤及皇家强杀（228）；U6=普攻替死中断（262）；U7=手雷、五手同步再生（293）；U8=葫芦控制与免控组合（348）；U9=活手仍胜利及同花附带治疗（380）；U10=完整 20 轮判负（404）。括号为当前源码行号。

| 规格 | .NET 实际断言与边界 | Unity 规则覆盖 |
| --- | --- | --- |
| 1 本体/五手生命与身份 | [内容 1500、六敌](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:42)；正式复活恢复 1 并清状态见下第 16、21 项 | U1、U2、U7 |
| 2 动态 D、成功蓄力 | [两普攻及成功蓄力](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:55)、[眩晕跳过蓄力](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:142)；断手/再生立即改变 D | U1–U4、U7、U10 |
| 3 四步序列、控制不推进 | 内容前 8 步精确比较；眩晕后下一轮仍蓄力；大招不额外推进 | U1、U4、U8 |
| 4 独立 5/6、11/12 时钟 | [逐轮预览及实际伤害 172/189](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:80)、[R12 预查询与实伤](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:360) | U4 直接覆盖 5/6；U10 仅覆盖 20 轮总伤害 |
| 5 常规在前、共用主攻击、只完成一次 | [精英流血仅衰减 1 次](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:169)、[主攻击倍率/固定加值 3 案例](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:348)；阶段事件普通/暴怒同场景对照见 [300 行](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:300) | U4 普攻/大招逐击；U6 替死 |
| 6 断手立即取消、R+5 后仍缺手等待 | [取消剩余 5、直至 R14 凑齐才预警 R15](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:99) | U3 改束缚周期夹具隔离真实出牌取消 |
| 7 本体受控不行动/不发预警、到期取消 | R4 跳蓄力、R6 取消且冷却继续，见 142 行；尚未直接断言 R5 本体受控不首次预警及取消后精确重启轮 | U8 普通动作受控；未隔离大招到期受控 |
| 8 两段最终免伤、刷新、不耗零伤害 | [免伤 9 条中的逐段、零伤害、末端增伤、毒、反伤、刷新及失败回滚](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/CombatWrathImmunityTests.cs:31)；[真实顺子 0/0/8/10/12](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:387) | U5；U7 道具逐次消层 |
| 9 归零不阻止附带状态、Eliminate 绕过 | [纯状态及强杀、旧护甲仍为 1/4](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/CombatWrathImmunityTests.cs:97)、[真实葫芦仍眩晕](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:402) | U5、U8 |
| 10 各手 R+8、死亡源与回合边界 | [R1/R2 分别死、R9/R10 独立期限](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:115)、[真实手雷清五手](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:287)、[阶段末毒死期限保留](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:417)；反伤断手尚无 Boss 集成断言 | U2；U7 五手同时到期 |
| 11 再生先于手行动、立即参与、需新预警 | [四饮血手再生立即治疗](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:230)、[束缚当轮复活即出手](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:219)、[入口新意图](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:467)；第 6 项覆盖新预警窗口 | U2、U7 |
| 12 每手独立控制、活着仍加 D/满足五手 | [五手受控仍 D43 并预警](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:244)；该案本体满血，未能直接检出饮血手错误治疗 | U8 是本体受控且手已死，不能替代活手受控断言 |
| 13 手毒→存活手治疗→本体毒，单次 tick | [本体 1400→1414，毒死一手](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:133)；再生净治疗 19。均仅 1 层毒，未检出重复 tick 风险 | 当前 10 条没有敌方入口毒顺序断言 |
| 14 束缚玩家、免控、各时钟继续 | [活束缚手 R5 控制、R6 恢复、R10 免控阻挡](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:196) | U4 默认束缚跳窗；U8 同时杀死束缚手，不能单独证明免控阻挡 |
| 15 全场 5/10/15/20、不补发 | [R5 手受控错过、R6 不补、R10 重新施放](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:207)；R10 再生仍沿全场时钟 | U4、U10 贯穿默认时钟；未逐项断言 15/20 的束缚效果 |
| 16 正数实际伤害才断手、正式复活清状态 | [零伤害/纯控制不杀手无期限](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:178)、[毒/晕可施加、复活后均清除](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathHandEffectRestrictionsTests.cs:80) | U2 正数伤害；尚无纯控制活手的 Unity 隔离用例 |
| 17 旧比例/旧手/半血机制移除，无牌型免疫 | 当前 XML 只有单阶段及新五手；真实顺子、葫芦、皇家均按通用效果结算；旧护甲独立回归仍通过 | U5、U8、U9；未专设 750 血阈值测试 |
| 18 本体死即胜利、附带治疗、替死中断整阶段 | [皇家附带同花治疗/预览纯净](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:315)、[普攻/大招/反伤终止](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:255)、[手入口替死取消](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:333)、[取消后的时间线不回滚](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:440) | U6、U9 |
| 19 完整 20 轮判负、不替死、玩家入口清盾 | [20 轮且道具保留](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:187)；42 行断言护盾配置。R20 同轮本体先死优先仅有静态分支证据，非专门测试 | U10 精确总伤 1067、蓄力 5 次、D83、替死保留 |
| 20 生命成长、地图、重开/继承 | [倍率 2：本体 3000/手 1、普通敌照常成长，2 案例](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathAdventureHealthTests.cs:12)、[冒险存档重放至 R12 实伤 189](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathAdventureReplayTests.cs:57)；未串联两场暴怒验证跨场玩家继承 | U3 重开精确回到 R0、D33、全手存活 |
| 21 拒绝手加攻/护盾，身份限制不可净化 | [15 条限制案例](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathHandEffectRestrictionsTests.cs:39)覆盖五手/普通/本体/玩家对照、4 种正负攻击组合、末端护盾加值 2 案例、失败回滚、复活+净化、真实敌方援护、次生伤害及 Enrage；[净化后限制仍在](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathHandEffectRestrictionsTests.cs:131) | 当前 10 条没有主动给手施加增益/净化的隔离断言 |

预览及拒绝命令的权威隔离另有 [157 行](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Tests/Editor/WrathBossRulesTests.cs:157)、315 行和 360 行，不能因本轮只关注结算而从 59 条映射中漏算。

**重点结论：未发现默认规则路径的新增可复现错误。** R12 的 189 不是仅看预览：80、360 行及冒险重放均检查实际结算。当前实现先跑对应敌人一次 `TurnStarted`，受控/入口死亡则不执行技能；只有玩家阶段入口再发玩家的 `TurnStarted`，没有对全体敌人重复广播。证据见 [TeamBattleEngine.cs:441](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:441)、[453](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:453)、[783](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleEngine.cs:783)。手排本体前、再生早于队列；蓄力先增加运行态次数，再绑定大招的 EnemyAttack，因此第 12 轮用 63×3，见 [TeamBattleWrathBoss.cs:58](/Users/mt/code-yang/Texas-Holdem/External/BattleToolkit/Engine/Runtime/Composition/Encounters/TeamBattleWrathBoss.cs:58)。

优先补足的两个定向断言已经准备，主线程运行 Unity 期间尚未写入 C#：

1. **多层毒检测每轮只 tick 一次。** 从真实 `.21.xml` 建场，本体先受伤 100，一只饮血手施 1 层毒，本体施 3 层毒。推进第 1 敌阶段：该手先死，其余三手合计治疗 15，本体仅受毒 3，末血应为 **1412、余毒 2 层**、手期限为 R9；末血及余层同时断言，可检出“单层毒首 tick 即移除而掩盖重复 tick”的测试盲点。
2. **受控饮血手不能治疗但仍贡献 D。** 本体先受伤 100，只眩晕一只饮血手。推进第 1 敌阶段：本体末血应为 **1415**，该手仍为 1 血、没有再生期限、D 仍 **33**；下一敌阶段控制消耗后再恢复四手治疗至 **1435**。此案直接区分手本身控制与本体控制，也检出永久漏恢复治疗。

其余未覆盖项属于较低优先级边界：眩晕/替死取消后直至重新预警与释放的精确 R+5/R+6；免控阻挡后 R11 不补发而 R15 正常触发；反伤断手的 Boss 期限集成；R20 本体死亡优先；连续两场冒险的玩家继承。对应实现均有分支证据，现有通过数量不能代替这些断言。

前述“同批死亡后立即 Revive”仍是**非默认组合的静态风险**：限制测试第 80 行确有此 CombatEngine 批次，但仅检查最终清状态及禁盾，未观察 Boss 预警；`Observe` 只读最终状态而不读中间死亡事件。没有默认手部同批复活入口，故本轮不将其列成已发生玩法错误，也不擅改通用复活或其他玩法。
