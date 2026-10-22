# 暴怒专项收尾与代码同步

日期：2026-10-22。Unity 2022.3.62f3；.NET SDK 10.0.402。

本轮按用户决定保留远端表现与本地新版规则，正式五手美术、动画后续接入，不进行 20 轮 Unity 实机场景测试。两仓使用 `codex/wrath-boss-20261022` 分支。主仓先快进到远端 `2a22e79c2ecbc6bad5e55ee29ce9f9ea87163a9e`，保留最新手牌排列、出牌离场、整理和补牌顺序修复。

## 代码与范围

- BattleToolkit 实现已提交为 `60722a05a62f9a89815d836ad69fbb49ad348fff`，并已推送同名分支；主仓提交其确切子模块指针。
- 本轮生产规则没有再调整，仅新增两项规则测试。七站仍使用远端六格等大布局、FourBeasts 牌面及原灰烬死亡／复原路径，逻辑为一个本体、四饮血手、一束缚手。
- `BattleDemo.unity`、`SingleChapterAdventure.unity`、`AdventureBattleLayout.cs` 及普通牌面／交互生产代码与最新远端相同。独立 `WrathBossDemo` 的验证面板与七站外部命令模式保持隔离。
- 普通战、牌型、道具与旧三章暴食配置保留。旧三章 H5 精英入口会读取原有本地新版 `.21` 内容包，因此也使用流血精英；这项已有本地行为没有回退，不能表述为旧三章所有精英仍使用护甲召唤。

## 本轮规则与接口实际结果

| 验证 | 实际结果 | 原始证据（同目录 `2026-10-22-wrath-final-evidence/`） |
| --- | --- | --- |
| 暴怒、精英、七站与经济核心规则 | 124/124 通过，0 跳过 | `wrath-core-final.trx` |
| 旧 Boss、普通牌型、道具、目标、状态和会话兼容 | 476 项，468 通过、8 项已有失败，0 新失败、0 跳过 | `wrath-compat.trx`、`rules-summary.json` |
| 新增多层毒／受控饮血手断言 | 独立 2/2 通过，已包含在上述 124 项，不重复计数 | `wrath-added-cases.trx` |
| H5 暴怒规则 HTTP 投影 | 8/8 通过 | `h5-wrath-projection.txt` |
| H5 流血精英规则 HTTP 投影 | 5/5 通过 | `h5-elite-projection.txt` |
| H5 NativeServer Release 构建 | 0 错误、0 警告 | `h5-build.txt`、`h5-summary.json` |
| Unity EditMode | 286/286 通过，0 跳过；含远端新增手牌能力检查 | `unity-editmode.xml` |

新增两项断言验证：三层毒在每个持有者上各合并为一段伤害，本体仅消耗一层免伤；受控饮血手在本体缺血时确实停止治疗，但仍计入本体攻击，解除控制后恢复治疗。没有改变既有毒、免伤、控制或治疗规则来满足测试。

兼容组 8 项失败逐项与此前全量 TRX 的名称及消息对照，均为已有的溢出／回滚、容量与状态引用断言问题，详见 `rules-summary.json`。此前全量仍有 12 项历史失败，本轮未重跑全量或修改这些普通规则，不能称全仓测试全绿。

H5 验证使用真实本地 HTTP 宿主和已有测试，辅助高生命／免控夹具只用于观察机制；不代表浏览器视觉、原始难度或 `/api/adventure` 的全流程验收。临时服务已停止。

## Unity 实际 Play：暴怒 9 项全部通过

通过图形 Editor 的 Unity Test Runner API 加载保存场景、真实宿主、异步回放。原始结果为 `unity-playmode-initial.xml`，以下 9 项均为实际 `Passed`，不是纯 C# 规则推断。

| 用例（`WrathBossScenePlaybackTests`） | 实际验证 |
| --- | --- |
| `SavedSceneBindsSixActorsAndShowsInitialAttackEightIntentsAndOneOpening` | 六 Actor、初始 D33、八格意图及仅一次入场提示。 |
| `HudSelectedHandDiesAtImpactShowsRedEightAndEnemyEntryFloatThenRegeneratesAsSameActor` | 实际选手出牌、命中时死亡、红色 8、阶段冒字、同 Actor 原位再生。 |
| `BindingAtTwentyFixtureLetsRealHandPlayCancelWarningAndRestartClearsOldHud` | 断手取消预警、重开清旧提示；仅将束缚周期设为 20 观察前期预警，没有跑至第 20 轮。 |
| `DefaultBindingShowsRoundFiveWarningBeforeRoundSixRegularAndExtraUltimateImpacts` | 默认第 5 轮预警及第 6 轮常规 43、追加 129 的真实顺序。 |
| `RealStraightConsumesTwoBodyImmunitiesAndRoyalEliminationStillWinsThroughFreshShell` | 顺子的独立伤害段消耗两层免伤，皇家强制击杀穿过新龟缩并结束战斗。 |
| `RealReviverDuringRoundSixNormalAttackStopsThePendingUltimateAndLateDeathPose` | 替死在常规攻击中发生，终止待释放大招，后续死亡姿势不覆盖复活。 |
| `HudGrenadeSeversFiveHandsWithoutActionsConsumesTwoShellLayersAndRegeneratesSameFiveActorsTogether` | 真实手雷不消耗行动、断五手、消耗免伤、同步再生复用五个对象。 |
| `RealFullHouseBeforeDefaultFifthBindingShowsControlAndImmunityAndKeepsTheNextPlayerWindow` | 真实葫芦控制／免控与默认第 5 轮束缚交互，保留下一玩家窗口。 |
| `RealLethalFlushWinsWithFiveLivingHandsAndStillPlaysItsAttachedHealing` | 五手活着时本体死亡即可胜利，同次同花附带治疗仍完成，等待原结果面板延迟。 |

同一轮还实际通过七站场景 4 项、精英 5 项、弃牌 3 项；普通时间线初轮 9 项中 8 项通过，1 项目标点击检查失败。原始 30 项结果为 29 通过、1 失败，没有将其记为一次完整通过。

失败已独立复现：新远端最终理牌在 `Update` 结束，而候选状态／目标按钮在 `LateUpdate` 刷新；旧测试在中间直接调用 `ChooseTarget`。诊断 RED 为 `input=True, presenting=False, candidate=Closed, choices=0, selected=0, moving=False, pendingSort=False, authority=True`，且无播放错误。仅在该测试初始 `Idle` 后等待 `WaitForEndOfFrame`，不改产品、不直接刷新候选，也未削弱目标、射线、轮廓、伤害或九条群攻分支断言。

修正后整组普通时间线 **9/9 实际通过**，同一步诊断变为 `candidate=Empty`。证据分别为 `unity-target-readiness-red.xml` 与 `unity-ordinary-final.xml`。最终覆盖的不同 Play 用例共 **30 项，最新执行均通过，0 跳过**：暴怒 9、七站 4、精英 5、弃牌 3、普通 9；这是初轮加定向复验的覆盖结果，不伪称单次 30 项全绿。

明确排除 `DurableHeroFixtureCompletesDefaultTwentyRoundsThenLosesWithoutSpendingReviver`；本轮 NUnit 的实际用例列表中没有这项。20 轮完整结算后判负及不耗替死仍由规则测试覆盖，不称 Unity 实机通过。

## 保留与未扩展项

两仓已有未提交源码和新增文件均保留，功能文件纳入本次提交。无关动态字体已精确恢复为用户原有未提交字节，不恢复为 Git HEAD。临时验证驱动及其 `.meta` 已移除。按开工清单核查主仓 76 个、子仓 71 个文件全部存在；除两处历史文档更新和新增两项规则断言之外，其余清单文件逐字节一致，记录在 `workspace-preservation.json`。完整开工备份及哈希清单位于 `/Users/mt/.codex/backups/Texas-Holdem/push-wrath-20261022-132420/`。

正式手部专用素材／断手动画、所有分辨率视觉验收、移动设备／独立 Player、正常构筑多种子胜率及平衡测量留待后续。本轮没有依据高生命测试夹具调整平衡数值。
