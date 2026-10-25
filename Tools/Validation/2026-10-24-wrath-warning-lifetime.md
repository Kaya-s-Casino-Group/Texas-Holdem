# 七站暴怒预警文字与红光同步（2026-10-24）

## 修改范围

用户要求：背部预警红光和大招文字同时出现／消失；最长4秒，最迟下一回合开始时关闭，不依赖玩家造成伤害。保持第1回合敌方阶段开始预警、第2回合释放、第7回合再次预警，以及大招伤害、冷却、断手打断等规则不变。

根因：`SingleBattleHud.WrathPrompts` 的文字采用一次性4秒截止时间，`WrathBossArtworkLayout` 却只依据权威 `wrath.warning`，后者必须保留至大招结算或打断，所以红光会超过文字期限。

- 冒险HUD将同一个 `ultimateVisible` 传给背部红光呈现层。布局每帧重绘也必须通过此显示门控，不会因权威仍有待释放大招而重新点亮。
- 当前回合到达 `wrath.releaseRound` 时，提前结束提示。规则投影的回合计数是已完成回合数，使用 `CompletedRounds + 1` 判定，避免偏移一回合。
- 实际第7轮发现后续预警的 `PhaseEnding` / `PhaseEnded` / `round-refresh` 原先在同一帧连续发布，直接进入第8轮；只加截止门控会使第7轮预警完全看不见。因此在七站暴怒本体的 `PhaseEnded` 呈现检查点等待同一个提示时钟的剩余时间，结束后才推进下一回合画面。没有重新启动4秒计时，也不生成预测或重复执行战斗规则。
- 打断、战斗结束、菜单退出都沿用原隐藏流程；同步／取消会终止上述纯呈现等待，同一场同一次预警过期后不重播，新预警仍有独立期限。
- 独立WrathBossDemo保留原权威红光行为；本次用户指定的是七站冒险。
- 不修改BattleToolkit、牌型、伤害、冷却、手臂再生、美术资源或布局。

## “特殊行动”解释

这不是Boss的技能名称，是 `BattleDemoEnemyHudModel.Intents` 的UI兜底：预览成功、没有眩晕跳过，但筛选后没有可显示的本体主动作效果时，显示“特殊行动”。正常循环为普攻→普攻→龟缩免疫2次→蓄力+10。

此前龟缩已有免伤时再次施放会产生 `Refreshed` 事件，旧HUD会过滤它，所以可能在第7／11／15／19回合显示此兜底；上一轮精英意图修复已允许主动作 `Refreshed + ApplyStatus`，同时覆盖了这种龟缩刷新。本轮不重复修改。

当前仍可出现的边界是预演判断Boss将在行动前被中毒毒死：周期中毒先结算，本体不再执行主动作，故没有本体主动作事件，进入该兜底。规则结算本身正确，兜底文案含义不够明确。本轮仅解释，未将其改成新的技能或改动战斗规则。

## 验证

Unity2022.3.62f3。使用已有隔离验证项目 `/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`，保留用户打开的主项目与存档。

- RED：实际Play逐帧复现文字已关但红光仍亮；`playmode-red.xml` 为1失败，期望false、实际true。
- RED：新增Editor提前回合边界检查失败，旧代码仍允许未到4秒的提示进入释放回合。最终有效RED为 `editmode-red3.xml`；前两次夹具构造中修正了回合字段及0基计数，保留原日志。
- GREEN：相关EditMode 58/58通过，Unity退出0，无跳过。包含文字生命周期、提前回合关闭、不重播、打断、UI射线、PSD拼装、目标／免伤、手臂意图及投影。
- 首次GREEN实际Play：第1轮同步及第2轮释放通过，但第7轮预警没有可渲染停留而失败。保留 `playmode-green.xml`，补充上述PhaseEnded呈现等待后重新验证。
- 中间新Editor夹具缺少必填 `zones` 参数导致编译失败（`playmode-green2.log`），修正夹具后继续验证；不把该次运行算作Play通过。
- 最终编译与相关Editor：72/72通过，无跳过，退出0。证据 `editmode-final.xml` / `.log`；包含新增真实Director检查点回归、原有58项暴怒相关检查，以及通用SceneTimeline配置检查。
- 最终实际Play：`SevenStationBossShowsReadableOpeningAndWarningsBeforeRoundTwoAndEightUltimates` 1/1通过，退出0，实际跑到第8回合，耗时约87秒。证据 `playmode-green3.xml` / `.log`。
- 逐渲染帧检查文字和背部预警层同显隐；每次进入下一玩家回合两者都已关闭；等待过程中未对Boss造成伤害。
- 第1轮进入敌方阶段、伤害前即出现预警；第7轮在真实本体动作后完整展示预警，再推进第8轮。期限为同一个4秒时钟，不累计新的4秒。
- 第2轮本体实际伤害为普通33+大招99；第8轮蓄力后大招159。第1／7预警、第2／8释放、菜单恢复及重复刷新不重播都通过。
- 实际截图6张保存到 `Library/WrathWarningLifetime-20261024/screenshots/`，包含第1／7轮可见预警、过期画面和第2／8轮释放。
- 本轮范围 `git diff --check -- Assets/Scripts Tools/Validation` 通过。

证据目录 `Library/WrathWarningLifetime-20261024/`。实际Play使用正常图形Editor／Metal、1280×720，不使用无图形纯C#结果代替场景验收。

## 工作区保留

开始前保存71个已有未提交文件的内容、SHA256、两个仓库状态及补丁。结束核对71项全部仍在，70项逐字节未变，另1项为本次增量更新的既有Play回归文件；两个字体缓存均与开始前SHA256一致。另备份本轮原本干净的两个暴怒呈现脚本、TurnBanner升级器及Editor回归文件。新增边界动作及相关测试均位于既有项目内。

验证副本的7个修改脚本与主项目逐字节一致，见 `verification-copy-parity.json`；保留清单见 `preservation-before.json` / `preservation-after.json`。BattleToolkit仍干净，未写入任何引擎代码。主仓库HEAD仍为 `310f0f091c6e5a63c0d368dd8eac3a7a5f0d5fb6`，子仓库HEAD仍为 `f3f353a5f520c1bc662680296973ab82d956c310`。未重置、未提交、未推送。
