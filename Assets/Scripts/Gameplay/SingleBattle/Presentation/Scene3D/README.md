# 主线卡牌战斗：3D 场景表现（Scene3D）

本模块是项目的主线卡牌战斗：玩家用扑克牌型发动攻击，消耗能量，处理生命、格挡、弃牌收益与敌方回合。支持 1v1 和多对多；`SingleBattle` 表示一场战斗，不表示只能有一名玩家。本目录实现现有 3D 场景适配；规则配置来自 XML 内容包，会话位于纯 C# 的 PokerEngine，通用显示与调度协议位于 [BattlePresentation](../../../../Presentation/BattlePresentation/README.md)。原有 3D 程序集名与命名空间保留，具体入口更名为 `SingleBattleSceneAdapter`，脚本 GUID 保留。

通用 `BattleSceneHost` 继承 `BaseBattleHost` 并持有纯 C# `BattleHost`；`SingleBattleSceneAdapter` 通过组合接入它，不再持有生命周期。规则链为 `SingleBattleSceneAdapter → BattleSessionPlayback → BattleSession → CardDuelEngine → TeamBattleEngine → CombatEngine`。`TeamBattleEngine` 是内部阵营回合规则，`TeamBattleDemo` 是本模块的多人阵容示例，两者都不是另一个并列玩法。[分数挑战（DrawBattle）](../../../DrawBattle/README.md) 是独立的达标计分模式，不负责本模块的抽牌。两种玩法共享 [卡牌交互](../../../../Presentation/CardInteraction/README.md) 与 [卡牌表现](../../../../Presentation/CardPresentation/README.md)。

主入口为 **工具 > 卡牌战斗 > 数值配置**。同组的“验算”“场景接入”“示例场景”“截图”提供结果检查、表现绑定和示例维护。示例场景工具仍按原有场景限制执行；菜单改名不会自动迁移或修改场景。测试入口为 **工具 > 测试面板**。

外层 [关卡流程（RunFlow）](../../../RunFlow/README.md) 负责地图配置；当前卡牌战斗场景尚未接入完整的 Unity Run 流程。


`SingleBattleDemo.unity` 保留 1v1；`TeamBattleDemo.unity` 当前保存的是 1v1 阵容，保留其原始配置；多人示例生成器在目标场景不存在时创建 2 位玩家对 3 个怪物。打开后 Play 即可，也可用 `工具 > 卡牌战斗 > 示例场景 > 打开多人战斗（2对3）`。这里是一场独立战斗；未来可作为 Run 的战斗节点，当前不是完整 Run。

## 目录与程序集

`Runtime/Hosting` 负责 Session 接入与场景绑定；`Actors` 是场景单位适配；`Presentation` 保存 `SingleBattleSceneAdapter.Presentation.cs` 的显示投影、牌区适配，以及攻击、VAT、护盾和音频表现；`UI` 只消费中性显示快照与候选模型。`IBattleActorPresentation` 是保留的局部 3D 兼容协议，现有组件另实现 `IBattleActorDisplayPresentation` 接收中性单位数据，其他表现风格不需要实现这两个局部接口。

`Poker.SingleBattle.Runtime`、`.Editor` 和两套测试程序集保留原名。Runtime 引用共享 Hosting、`Poker.BattlePresentation.Shared` 与 `BattlePresentation.Runtime / .Unity`；场景设置、数值审计和布局编辑器仍属于本前端。`Assets/GameAssets/SingleBattle/Presentation/Scene3D` 下的 `Shaders` 与 `ShieldAssets` 只服务此风格。

## 策划集中配置入口

所有战斗场景仅使用 XML。`SingleBattleDemoNew` 使用 `Packages/com.kaya.battle-engine/Content/SingleBattleDemoNew.xml`，默认编成 `single-battle-new`。`SingleBattleDemo` / `ForestRuins` 使用 `Assets/GameAssets/SingleBattle/Content/SceneBattles.xml` 的 `single-battle`；现存 `TeamBattleDemo` 使用同包的 `demo-battle`，2v3 生成器使用 `team-battle`。

SingleBattleSceneAdapter Inspector 直接绑定 XML 和编成 ID；双方 Actor 数组按编成顺序绑定。旧 Configuration 程序集、19 个战斗 SO、Profile 字段、旧配置工厂与创建菜单已经删除，不再提供 SO 回退。其他玩法仍使用的 `PokerScoreProfile`、卡牌美术和渲染配置属于独立模块，继续保留。

使用 [外部 HTML 配置工具](../../../../../../External/BattleToolkit/Tools/BattleContentEditor/README.md) 导入该 XML，修改后导出覆盖原文件；Unity 刷新后重新开战生效。`工具 > 卡牌战斗 > 数值配置` 在 XML 模式显示当前文件、编成及统一校验入口；SingleBattleSceneAdapter Inspector 也可校验。校验会真实组装未开局会话，不发牌、不触发开战效果。读取或引用错误、编成人数或接入会话的参与者顺序不匹配均会拒绝；失败重开保留原会话，诊断在 Inspector 与 HUD 显示。

XML 按策划文档 revision 1277 配置：高牌/一对/两对单体伤害，三条/四条群伤，顺子三段伤害，同花伤害加按筹码治疗，葫芦单体眩晕一次行动窗口加自身免控至下个敌人阶段结束，皇家同花顺消灭全部存活敌人。免控沿用已确认的“提前施加”语义；普通同花顺和五条暂为显式单体伤害兼容项。文档未定数值均在 XML 注释为测试值，计分表及角色基础值沿用原场景。共享三点能量，出牌/弃牌各一点、立即补牌；弃牌每张八盾，流程图的有限次数暂为整场三次。

同包提供 `single-battle-dot`、`single-battle-reflect`、`single-battle-overheal` 三个可选编成，分别测试顺子逐段附流血/中毒、弃牌盾反、同花治疗增幅与溢疗转伤；默认编成不装备遗物。切换场景适配器的编成 ID 即可重开测试。左侧 HUD 文字区从中性模型读取预览、状态及期限、遗物次数冷却，并仅显示已揭示的触发和阶段结算；原有 Actor/VAT/护盾/卡牌组件已作为新调度器的动作适配器接入。敌人仍为明确配置的固定攻击，文档中未细化的召唤、护甲、污染牌库和完整 Run 流程未在本次新增。

运行验证入口：在 `SingleBattleDemoNew` Play 模式执行 `工具 > 卡牌战斗 > XML内容 > 验证当前运行场景`；验证出牌、弃牌、补牌、格挡和完整回合并写入 `Library/SingleBattleTests/xml-scene.json`，成功后重新开战。该验证会消费当前测试对局的行动，请用于刚开局的默认编成。

## 配置归属

`BattleSceneHost` 管理每局生命周期。`SingleBattleSceneAdapter` 绑定 XML 编成、场景角色、卡牌输入和表现；Actor 不再持有规则配置，只从当前会话的显示快照查询。XML 编译与组装失败会保留已有对局；编辑 XML 只影响下一次开战。

| XML 节点 | 职责 |
| --- | --- |
| ScoringProfiles / BattleRules | 计分表、回合上限、格挡清除时机 |
| Players / Enemies | 生命、能量、费用、补牌、弃牌收益、攻击/承伤修正与目标策略 |
| HandRules / Statuses / Relics | 牌型效果、状态与遗物规则 |
| Decks / Loadouts | 独立牌组、个人牌型等级、遗物配置 |
| Encounters | 参与者 ID、双方阵容顺序、角色和装配引用 |

`SceneBattles.xml` 的 `single-battle` 保留玩家 800 HP、怪物 1200 HP/240 攻击、3 能量、8 张手牌、20 回合上限；出弃各 1 能量、最多 5 张且立即补满，无限弃牌，每张弃牌获得 8 格挡，敌方阶段结束清盾。玩家攻击目标保留最低生命，双方伤害修正保留 1 倍、0 固定增减。

`demo-battle` 与 `team-battle` 保留玩家 40 HP、怪物 75 HP/7 攻击、445 回合上限，出牌下轮补满、弃牌立即补满、整场 3 次弃牌、无弃牌收益。默认采用标准 52 张牌、1 级牌型、等分数伤害。`SingleBattleDemoNew` 的策划牌型技能不被这些兼容场景配置覆盖。

个人牌型等级通过 Loadout 的 HandLevel 配置。`discardBlockPerCard` 缺省为无收益；`unlimitedDiscards` 仅取消次数预算，仍检查能量、牌数和阶段。`blockExpiry="AfterEnemyPhase"` 在完整敌方阶段后清除玩家剩余格挡，不在敌人之间清除。

攻击/承伤使用 `attackMultiplier / attackFlat / incomingMultiplier / incomingFlat`。流程为基础伤害 → 攻击修正 → 承伤修正 → 格挡/生命；每次修正计算 `floor(max(0, 输入 × 倍率 + 固定值))`。所有规则只由内容编译器创建，表现组件不实现这些计算。

## 只读验算

`BattleInteractionQueries`、`PreviewSelectedPlay` 和 `PreviewSelectedDiscard` 从当前会话生成候选与结算预览。预览不推进 Revision、不修改手牌、不触发表现；编辑下一局 XML 不会改变当前预览所用的规则快照。正式提交与预览共用规则链，只有被接受的动作发布表现事实。

`工具 > 卡牌战斗 > 验算` 的出牌伤害、弃牌收益面板继续显示当前会话的计分、来源、目标与结果，不再定位或编辑旧 SO。扩展规则需在纯规则内容模型、XML 编译器和测试中实现，再由现有桥接投影结果；无需向 Actor 添加配置槽位。

## 回合与牌组

1. Create 时每位玩家分别创建标准不含大小王的 52 张牌，拥有自己的抽牌、手牌、出牌、弃牌四区，起手为 0。Start 首次进入玩家阶段，为所有玩家按各自策略免费发牌。
2. 存活玩家共享行动阶段，可交错出牌/弃牌，每人独立扣费。能量为 0 自动标记该玩家结束；也可手动结束。初始能量为 0 时仍可手动空过，费用不足但非 0 时不强行结束。
3. 所有存活玩家均结束，才进入怪物阶段。存活怪物按阵容列表顺序行动。BattleSession 在本次玩家命令内自动结算整个怪物队列；表现层按有序事实及其动作依赖播放，不再提交怪物命令。
4. 全部怪物完成后算一整轮。仍在战斗时，所有存活玩家恢复各自能量并按各自策略补牌。死亡玩家不再参与行动/等待条件，也不恢复能量或补牌。
5. 怪物全灭胜利、玩家全灭失败；达到回合上限在完整怪物阶段后判负。部分队友/怪物死亡不提前终止整场。

选择 1–5 张真实手牌，由 PokerHandClassifier / ScoreEngine 计算牌型，总分默认由 EqualScoreDamageRule 等额生成基础伤害，经过攻击修正后交给 CombatEngine 处理目标承伤、格挡与 HP。默认出牌延迟至下一玩家阶段补满，弃牌立即补满；每位玩家可单独配置。抽牌堆不足只洗回该玩家的弃牌堆。

本地 Demo 用一套 CardZone/UI 观察当前玩家，点击“切换玩家”或 Tab 切换。结束后自动转到仍可行动的队友。切换只重新绑定观察者牌区和命令接收者，不移动逻辑卡牌、不重开战局；牌库总览也仅包含当前玩家的完整构筑。表现/发牌过程中禁止切换与重复输入。

场景首次自动开局在 BattleSceneHost.Start 中执行，等待各牌区完成 OnEnable 后，先绑定空手牌，再提交 Start 发牌并播放入列动画；避免 Host 较早的 OnEnable 在 HandZoneView 尚未激活时跳过动画。逻辑手牌一次性提交，逐张飞入属于表现；重新启用已有对局仍恢复当前状态，不再次发开局牌。

手牌默认按点数理牌，开局发牌及后续补牌落位后自动执行；切换玩家也按当前选择的方式理牌。手动切换为花色理牌后，后续发牌沿用花色排序。排序只调整观察者的显示顺序，不改变权威牌区或洗牌状态。时间线在新命令开始时同步最新规则值，但保留当前手牌的显示顺序，避免出牌／弃牌前跳回规则牌序。

这不是联网多人功能：目前共享阶段中的命令由同一权威按 Revision 串行结算，本地输入一次控制一名玩家。规则层没有固定的玩家行动游标。

## Session 与共用表现接口

`BattleSession`、`BattleSessionResult/Step`、`IBattleSessionPresentation` 和 `BattleSessionPlayback` 均位于不引用 Unity 的 `PokerEngine.Composition`。公共前端协议、显示存储和调度器位于既不引用 Unity 也不引用 PokerEngine 的 `BattlePresentation.Runtime`。当前连接为：

```text
玩家命令 → BattleSessionPlayback → BattleSession → 结果 + 有序 Timeline
                    ↓ IBattleSessionPresentation
          BattlePresentationBridge：过滤观察者数据，投影每个事实
                    ↓ PresentationBatch / BattleDisplaySnapshot
          IBattlePresentationFrontend
                    ├─ ScenePresentationDirector：当前 3D 适配
                    └─ Battle2DView：独立 2D 适配
                    ↓
          DisplayStore + PresentationDirector + InteractionSession
```

规则侧兼容接口保留 `Initialize(initialState)`、`Present(batch, animate)`、`IsPresenting` 和 `Synchronize(state)`，由共享 Bridge 转为中性的 `IBattlePresentationFrontend` 调用；后者的 `Present(batch, PlaybackMode)` 返回可等待操作。实现者自行决定布局、动画与输入方式。`IsPresenting` 覆盖前端的阻塞动作，当前 3D 也包含开局发牌和手牌实际过渡。BattleSessionPlayback 负责共用开局、当前版本提交、规则资格与预览、本地门禁及派发；无画面模拟可直接调用 Session，不受动画门禁影响。

一次 `Session.Execute(expectedRevision, actor, action, selected, targets)` 接受 Start / Play / Discard / EndTurn。EnemyAction / CollectPlayed 仅由 Session 自动推进。`BattleSessionResult` 保留命令前状态、最终状态和旧 `Steps`，新增 `Timeline` 记录每个事实发生时的中间状态，以 `Sequence` 排序，不能用可能相同的 revision 代替顺序。节点包含 `BattleStarted`、`PhaseStarted`、`ActionStarted`、判型/费用/效果/状态、移牌/发牌、`ActionResolved`、`PhaseEnding / PhaseEnded / PhaseClosed` 和 `BattleEnded`。自动步骤全部成功才提交权威状态；拒绝返回原状态及空步骤、空时间线。相同规则、初始状态、随机种子和命令得到相同结果。

保留原有过牌顺序：出牌先进入 Played，再按策略补牌，最后收进 Discard；不可提前把本次出牌洗回。弃牌先入 Discard，可以按原规则立即洗回；终局出牌仍留在 Played。每个敌人动作按引擎原顺序收牌，最后敌人完成后才开始下一玩家阶段。

`SingleBattleSceneAdapter` 承担 Unity 场景组装、输入与旧 CardMatch 接入，序列化时间线交由 `ScenePresentationDirector` 执行；规则推进仍在 Session。`StartBattle` 收集 Actor 的 Setup 后交给共用工厂创建未开局 Session，绑定场景后调用 `Playback.Start`；`AttachSession(session)` 可把已有 Session 接入人数匹配的场景，不重新开局。[独立 2D 验证模块](../Minimal2D/README.md) 使用同一个 Bridge、DisplayStore 和 Director，以卡牌和矩形移动验证中性边界；其 View 不读取规则对象，不依赖 CardZoneView、VAT 或 3D Actor 协议。

- `Session.State / Host.AuthorityState`：已经提交的权威状态，用于命令和预览。
- `Host.Display`：`DisplayStore` 已发布的中性快照，供 Actor 显示、生命、能量、牌区、意图和 HUD 使用。当前事实的 checkpoint 才能发布对应数据，不提前曝光最终批次。
- `Host.Candidate / Host.Interaction`：候选读模型与独立交互会话；预选变化不会修改规则状态或 DisplayStore。
- `Host.PresentedState / Host.State`：显示游标对应的规则快照，仅保留给 Host、旧 CardMatch/Actor 绑定和验算工具兼容；新 UI 与动作不得通过它重新查询规则。
- `CardMatchHost.State`：当前观察者的卡牌投影与本地排序；不承担战斗权威。

旧立即显示与手动逐步测试适配器继续保留，新测试同时覆盖真实 Director 的信号与 checkpoint。最简 2D 入口为 `工具 > 卡牌战斗 > 2D验证 > 打开验证场景`；该场景使用 `Assets/Art/Cards/StandardClassic` 牌面对应的 `Assets/Art/CardAtlases/Classic.asset` 图集，沿用原卡牌网格与 CardView，通过专用 Unlit 材质验证共同边界。2D 与 3D 共用 `Assets/Settings/MainURP.asset`，验证相机选择固定 `Assets/Settings/Battle2DRenderer.asset` 对应的 `Simple2DRenderer`；主 3D 和 UI 的既有 Renderer 槽位不变，编辑模式与 Play 不切换管线。现有 3D 美术与组件保留，由新时间线调度。

## 数据与接口边界

- `CardDuelState` 拥有全部卡牌、每位玩家四区、弃牌预算及洗牌状态；保留类名以兼容原调用，已支持多对多。新代码使用 `Zone(kind, playerId)` / `DiscardsFor(playerId)`，不使用默认第一玩家的便捷别名。
- `TeamBattleState` 拥有共同 CombatState、各玩家能量与结束标记、怪物队列和战斗阶段。`TeamBattleEngine` 负责阵营回合、费用、伤害与胜负，`BattleRules` 只含公共规则。
- 角色根节点持有 PlayerBattleActor / EnemyBattleActor，负责输入与参与者身份；新动作通过 `DisplayId / DisplayUnit` 接收中性数据，Model 子物体继续承载 VAT、DOTween、裂纹与音效。旧 `Unit` 规则查询仅为绑定和兼容保留。角色必须在 Host 阵容中具有对应席位，不能冒用同队另一个角色的身份。
- `IBattleTargetPolicy.SelectTargets(BattleTargetContext)` 由行动来源角色的 BattleActorTargetRules.Attack 提供，接收源单位、当前状态、可选请求和存活对手集合，返回一个或多个目标。默认选第一个存活对手（或校验显式单目标）；引擎拒绝未知、死亡、友方和重复目标。接口支持多个伤害事件，但当前 Demo 不提供选目标或 AOE 操作 UI，也未定义范围/友方治疗规则。
- `IBattleAllyTargetPolicy.SelectTargets(BattleAllyTargetContext)` 预留独立友方目标接口，默认自己，也可请求存活队友；引擎验证请求与策略输出，拒绝敌方、死亡、未知和重复目标。由行动来源角色的 BattleActorTargetRules.Support 提供；XML 当前使用默认自身或指定单个队友策略。弃牌通过独立 IDiscardRewardRule 生成收益计划，默认空计划；费用/次数和过牌仍由原有流程处理，不按牌型换取格挡。
- `BattleContentFactory` 可通过 `BattleSetupRequest` 组装 XML 编成；纯规则 `BattlePlayerSetup` 可消费 Run 当前构筑和生命；缺省牌组为独立标准 52 张。Core 不依赖 ScriptableObject 或 Unity。
- `ICardDuelDealPolicy` 上下文含 PlayerId，只返回抽牌数量，不能移动或创建卡牌。每位玩家可独立注入策略。
- `ScoredDuelEngine` / 旧快照协议保留给原有独立 1v1 规则调用；新 Demo 使用 TeamBattleEngine。完整多人战局存档、Run 奖励写回、复活和增援尚未实现。

## 公共规则与后续边界

XML BattleRules 配置版本、回合上限、格挡清除时机和计分引用；纯 C# BattleRules 持有规则快照；计分表由 CardDuelEngine 独立持有。出牌伤害与弃牌收益归属 BattlePlayerRules，攻击/承伤修正归属双方角色的 BattleActorDamageRules，不再存在全局修正回退。旧 ScoredDuelDefinition 适配入口将自己的伤害转换显式写入默认玩家规则，旧 standalone 快照格式保持。

尚待后续拆分的边界：

- 个人牌型等级已从玩家配置接入开战快照；Relic 仍为空，战斗中升级及 Run 成长带入尚未接入。
- SingleBattleHud 同时处理信息显示、按钮命令、Tab 切换和世界空间意图对象的生命周期。未来可拆成信息展示、操作绑定和意图 Presenter；游戏数值仍由规则层提供。

## 角色目标配置

XML Player / Enemy 的 `attackTarget` 支持 `FirstLiving / LowestHealth / HighestHealth / AllLiving`；`allowRequestedTargets` 默认 true。生命比较使用当前绝对生命，同值按阵容顺序；合法的显式目标优先。各参与者持有独立规则快照，敌人意图与实际攻击读取同一目标策略。策略扩展属于纯规则层和 XML 编译器，表现层只消费实际命中的参与者列表。

## 敌人行动预览

`TeamBattleEngine.PreviewEnemyAction(state, enemy)` 是只读规则接口；玩家阶段假设所有尚未结束的玩家立即结束回合，随后按剩余敌人队列预演至指定敌人。所有阶段转换、目标选择、攻击修正、承伤修正、格挡与生命结算均复用正式 `Execute`。前序敌人消耗格挡或击倒目标会影响后续预览；实际状态、随机数与牌区不被改写。注入策略必须是确定性纯函数，状态变化后重新计算。

返回 `EnemyActionPreview`，包含来源、原始规则 revision、错误及逐目标 `Hits`（正式 CombatEvent）；其中 `SettledDamage` 是两层修正后的格挡前伤害，`BlockAbsorbed` / `HealthLost` / `Overkill` 分别是格挡吸收、预计扣血和溢出伤害。没有安排本阶段行动、已经行动、死亡或终局返回 null；前序行动或当前行动失败则返回错误，不回退基础攻击值。

共享 `BattleDisplayProjector` 调用只读预览，将对应显示检查点的意图文字写入 `BattleDisplaySnapshot.Labels["intent/" + unitId]`。世界空间标签只读该字段，显示 `攻击 N`（每目标的 SettledDamage）；多人时标明玩家序号，每个目标独立一行。面板高度及运行时 Gizmo 随行数调整，预览失败显示“意图不可用”。`EnemyBattleActor.ReadIntent()` 保留给旧调用方，HUD 不再调用它；每次显示更新使用当前已揭示状态的预测。

## 战局信息层

左侧候选牌型面板只读 `Host.Candidate`，显示牌型、选中/计分牌数、筹码与倍率，未选牌显示零值。Host 捕获选择后通过共享 `BattleInteractionQueries` 调用规则预览，生成 `CandidatePresentationModel`；其中牌型识别与 `CanPlay / CanDiscard` 独立，因此能量不足仍可展示有效牌型。完整出/弃牌预览作为模型标签提供，UI 不再自行调用 Engine、Actor.Unit 或规则快照。两个数字各自使用白色矩形背景，保持现有布局。

数字显示由独立 UIFoundation.Runtime 的 RollingNumberText 完成，仅接收 decimal 数字；增减均从当前显示值追向新 target，相同 target 不重启，最终值精确落位。`HandCandidatePresenter` 只负责模型到文字和数字的映射，禁用时终止未完成插值，启用时重新读取最新候选。

`InteractionSession` 用战局、观察者、规则 revision、选择版本和卡牌 ID 校验预览结果，旧请求不能覆盖新选择。相同牌型更新现有反馈，切换牌型释放旧作用域并接入新反馈；清空选择回到 Empty，提交先冻结意图并进入 Committing，阶段切换、同步和禁用关闭预选。取消只撤去该预选自己的偏移、色彩或临时对象；不能恢复旧卡牌列表、旧排序或旧生命。镜头/角色预选可从当前显示姿态平滑转向新目标，独立贡献层归零后仍服从最新基础布局。

战局 HUD 是只读信息层：小尺寸信息栏位于屏幕右侧，展示生命、格挡、能量与动作费用、回合、牌堆数量及最近得分/伤害；不显示操作教学或内部计算流程。结束回合、重开及多人切换保留为紧凑按钮，胜负信息也显示在右侧。

每个存活怪物的攻击意图使用独立 World Space Canvas，统一放在 HUD 运行时创建的场景根节点 `WorldUIRoot` 下，不继承怪物或 HUD 的缩放与旋转。LateUpdate 跟随模型底部锚点，朝向统一与摄像机画面平行，透视模式下也不会因偏离画面中心而倾斜。标签采用固定世界尺寸，保留近大远小，死亡/终局隐藏；HUD 的“敌人意图（世界空间）”可调整世界单位/像素与世界偏移。HUD 销毁时清理其创建的 WorldUIRoot 与标签。Scene 视图开启 Gizmos 可见黄色面板范围、青色模型底部与偏移连线、蓝色朝向箭头；Edit 模式也能预览，使用与实际 Canvas 相同的世界尺寸和摄像机朝向，不叠加怪物缩放。意图来自 `Host.Display`，Actor 引用仅用于定位场景模型，标签不持有战局命令权限。`工具 > 卡牌战斗 > 场景接入 > 整理战斗信息界面` 可仅更新当前场景的 HUD 布局，保留阵容、模型、牌区和相机配置；新建 Demo 使用相同布局。

## 可等待的表现协议

通用调度器只调用 `IPresentationAction.Play(ActionContext, IMarkerSink)`，取得 `IPresentationOperation`；动作接收中性事实、目标绑定、作用域和信号出口。卡牌、角色、UI、镜头或场景物件均可实现此接口，不必继承 Actor，也不获得战局命令权限。

现有角色组件通过 `BattleActor.PresentDisplay` 适配为通用动作。`BattlePresentationCue` 的当前路径使用字符串 Actor ID、`EffectDisplayData`、`IMarkerSink` 和表现专用 `ShieldSnapshot`；`IBattleActorDisplayPresentation.SynchronizeDisplay(UnitDisplayData)` 负责单位外观同步。旧 `IBattleActorPresentation`、`Synchronize(CombatantState)`、含 `CombatEvent` 的 cue 构造和观察事件继续为绑定、工具与旧调用保留，新动作/HUD 不从这些兼容入口读取规则。现有角色组件的完成协议为：

| 调用 | 协议要求 |
| --- | --- |
| `Play(cue)` | 返回 `BattlePresentationOperation`；实际动作结束时调用 `Complete()`，中断/错误时调用 `Cancel()` / `Fail(error)`。 |
| `SynchronizeDisplay(unit)` | 同步中性单位快照，例如生命分档裂纹；不播放动作、不修改生命、不阻塞流程。 |
| `Snap(cue)` | 同步显示最终姿态，用于不播放动画的执行或跳过；不能保留异步工作。 |
| `CancelPresentation()` | 停止旧协程/动画/回调；旧句柄不得再影响新绑定。 |
| `ResetPresentation()` | 取消旧表现并恢复初始姿态，用于新战局、重开和禁用。 |

操作句柄在 Unity 主线程使用，仅允许一次终态（Completed / Cancelled / Failed），迟到或重复完成返回 false。动作依赖可以等待 Started、已声明的 Marker 或 Completed；一组依赖按 AllOf 满足。checkpoint 决定何时发布该事实并允许下一事实开始，阻塞动作完成决定何时释放整批输入。所需信号未发送、依赖成环、动作异常或取消会使本批失败并恢复到已提交最终快照，不能猜测时长或伪造完成。旧作用域/播放代次的迟到回调无效。双方可独立接入 Animator、Timeline 或其他实现，当前基础变换与 VAT 已通过该路径运行。

## 场景动作与预选配置入口

`SingleBattleSceneAdapter` 只接收输入、转接规则事实和同步显示镜像；不再生成出牌、受击或镜头动作计划。场景必须保存有节点的 `ScenePresentationDirector.timeline`。当前 `SingleBattleDemoNew` 的完整配方由 `工具 > 卡牌战斗 > 表现时间线 > 接入当前场景` 写入；此菜单将旧的空组迁移成实际节点，重复执行保留已有自定义组。没有配置的旧场景需先执行该菜单；运行时不会偷偷补回代码默认顺序。

- `targets`：以 key 绑定任意 Unity 对象。Host 在运行时绑定 `cards` 为 HandZoneView、参与者 ID 为对应 Actor；镜头、UI、场景物体可使用其他 key。Actor 动作目标留空时使用当前事实的实际行动者/效果目标，兼容多角色。
- `timeline`：每组按 `semanticKey`、可选 `ruleAction` 和 `filter` 匹配。filter 支持效果类型、目标 ID/阵营/与观察者或行动者的关系、行动者筛选、实际伤害/格挡消耗、格挡增减、死亡及跳过条件；所有条件必须同时满足。同一事实的匹配组按配置顺序合并，`replaceDefault` 表示替换此前累计计划，Host 不再提供默认计划。
- `nodes`：填写唯一 id、派生 `PresentationActionBehaviour` 的组件、targetKey、blocking、after 依赖；节点也能配置 filter。依赖只引用同一事实最终计划内实际存在的节点，不能引用已过滤掉的节点或上一事实的节点。
- `explicitCheckpoint` 开启后用 checkpoint 依赖指定整条显示快照的公开点；空数组表示立刻公开。未指定且没有继承 checkpoint 时，等所有阻塞节点完成。节点的 Started / Marker / Completed 可交叉编排，多条件按 AllOf 满足。
- 当前出牌配置是 `cards.Prepared → attack`，`attack.Impact` 放行下一事实；真实效果通过筛选进入受击/格挡动作，在 `effect.Revealed` 后启动伤害浮字和目标镜头反馈。UI 提示自身也发 `Revealed`，可用于控制显示快照公开时点；HUD 的血量、能量等读取公开后的快照。
- `CardZoneTimelineAction`、`CardZoneTimelineAction`、`BattleActorTimelineAction` 是现有牌区/角色协议适配器；`TransformTimelineAction` 是通用分段变换；`UiFeedbackTimelineAction` 是通用文字淡入、上浮、淡出。全部派生基础表现组件，规则对象不传入这些动作。每次 UI 调用独立克隆模板，取消只释放自己的临时物体。
- UI 模板、颜色、时长、位移和文本 token 可配置；例如 `{pattern}`、`{score}`、`{cost}`、`{damage}`、`{block}`、`{heal}`、`{before:target.health}`、`{after:target.health}`。数值来自本次事实快照，不能从实时权威层补查。
- `SceneCandidateFeedback.routes` 继续按候选状态或 patternKey 路由 `IInteractionFeedback`；它与已提交的时间线分离，提交/取消时释放自己的偏移，不改规则或 CardList。

`HandZoneView` 的 `selectionActionOffset / selectionActionSeconds / selectionActionReadyProgress` 控制卡牌准备动作；ReadyProgress 是缓动后的实际位移进度。角色攻击在真实前冲结束发出 `Impact`。修改任意动作间的依赖、显示 checkpoint 或目标筛选只需修改场景配方，不修改 Host 或规则。

## 当前石像表现方案

- 攻击：DOTween Sequence 前冲后回位。双方 Inspector 可独立调整 attackSeconds 和 attackOffset；默认玩家沿桌面 +X、怪物沿 -X 前冲 0.06。
- 受击：DOTween DOPunchPosition，独立配置 hitSeconds、hitPunch、hitVibrato。Tween 实际完成后才完成协议句柄；取消先停止 Tween 并取消句柄，重开恢复模型姿态。
- 生命变化：VatBattleActorPresenter 将只读快照交给 Model 上的 HealthCrackPresenter。在绑定、恢复和效果表现检查点同步；受击时瞬间跨档，治疗时可恢复档位，格挡不改变生命档位。
- 死亡：裂纹进入最低生命档，完整播放 VAT 的 0→1 时间线；使用播放器自身的结束状态，完成后才释放终局流程。

| 剩余生命比例 | 裂纹档位 | `_CrackProgress` | `_CrackDepth` |
| --- | --- | --- | --- |
| >75% | 0 | 0 | 0 |
| ≤75%、>50% | 1 | 0.35 | 原材质最大深度 ×0.35 |
| ≤50%、>25% | 2 | 0.7 | 原材质最大深度 ×0.7 |
| ≤25% | 3 | 1 | 原材质最大深度 |

`HealthCrackPresenter.stages` 可配置降序生命阈值、裂纹显现和深度倍数；没有逐帧插值。使用 Renderer 级 MaterialPropertyBlock 保留 `_VATProgress` 与其他属性，不实例化或修改共享材质。两种石像沿用各自材质最大深度，准备好的 VAT 网格保持单材质。纯规则层不依赖 DOTween 或 shader。

攻击、受击、死亡分别预留音效，Clip 默认全空。Model 下的 `Audio / BattleActorAudio` 持有三套 `BattleSoundSettings` 与 `Attack / Hit / Death` 子物体声源，可移动这些子物体调整发声位置。每个槽位保留 Clip、Volume、Pitch、DelaySeconds、SpatialBlend、MinDistance、MaxDistance；组件还保留可选 AudioMixerGroup 输出。默认不循环、不自动播放，空间衰减为 Linear，Doppler 为 0。

表现组件通过可选 `IBattlePresentationAudio` 调用音效，不把音频资源/声源放入 BattleActor 或规则层。每种动作使用独立声源，允许不同动作的尾音重叠；空 Clip 静默跳过，Snap/恢复不播放音效。音效不参与表现操作的完成判定，动画仍按实际结束推进流程；音效错误不阻断动作，取消、重开或禁用会停止声音及延迟播放。当前不接入全局音频管理器或自动加载占位音频，后续可替换音效实现。

当前出牌默认计划为：ActionStarted 中卡牌准备发出 `Prepared` → 玩家攻击在前冲节点发出 `Impact` → 依 Timeline 顺序揭示判型与效果等事实；受击/护盾效果开始时发出 `Revealed` 并发布相应生命/格挡，后续费用、CardsMoved/CardsDealt 事实分别更新能量、移牌与补牌。攻击收招可以和受击重叠；死亡依赖该单位效果动作完成，同一 Actor 的动作串行，多个目标可并行反应。敌人使用 PrepareAttack → Attack/Impact，受控制跳过的行动不播放攻击。整批事实及阻塞动作、手牌实际过渡全部结束后才释放输入；BattleEnded 在规则实际结束位置出现，不制造下一玩家阶段。观察事件不承担等待职责。

**动画等待只发生在表现层。**BattleSession 在返回玩家命令前已结算到下一玩家决策点或终局，包括敌人队列、收牌和补牌；Bridge 把整批有序事实交给 Director。3D 层只控制显示检查点、输入释放与终局界面，不能延后或重复规则结算。播放失败、取消、`SnapToState` 或禁用宿主都会使旧作用域失效，取消演出、补齐表现专用盾牌来源记录并同步最终快照；重新启用不会再执行敌人命令。`Instant` 验证整批后一次发布最终快照，只做同步，不调用普通 Play、不发送中途 marker，也不播放音效。不会用任意超时提前结束动画，协议实现必须报告完成或失败。

`CardDuelEngine.Create` 可传入每位玩家的 `BattlePlayerSetup`（包含当前 `DeckEntry` 列表），保留 OriginId 并按当前构筑数量实例化，例如 53/51 张；当前 Demo 启动入口使用标准 52 张。这里支持标准牌面与卡牌效果标记，尚未接入 RunCard 的完整增强、Relic、能力选择或 Run 宿主。

组合状态目前只在内存中运行。**不要单独保存 ScoredDuelSnapshot 来恢复这个 Demo**：它不包含四牌区、洗牌状态和弃牌次数。完整 CardDuel 快照、跨节点生命/构筑写回、Run 结果账本与奖励结算是下一层工作。当前没有磁盘存档或中途续局按钮。

暂不提供完整敌人 AI、状态效果调度、正式角色动画和数值平衡。场景默认不改 Build Settings，直接打开后 Play 即可；未进行完整 Player/IL2CPP 构建。

## 验证入口

`工具 > 测试面板 > 编辑模式测试` 中，“多人战局与阵营回合”验证独立牌组/独立角色数值、共享阶段、怪物队列、死亡跳过、整队胜负、错误目标和多目标接口；“角色与表现协议”验证 UI 权限切换、配置快照、真实 Actor 接入以及逐单位等待表现完成。仅运行专项，不自动执行全部规则测试。

新架构专项包括规则 `BattleTimelineTests`、公共 `PresentationCoreTests`、Unity `SceneFeedbackTests`、卡牌 `CardZoneTransitionTests` 以及本模块 `BattleActorTests / HandCandidateDisplayTests`。`SceneTimelinePlaybackTests` 在 PlayMode 使用实际 `SingleBattleDemoNew` 验证开局、预选切换/清空、出牌、弃牌、敌方演出中途同步和重开；这些验证与完整 Player/IL2CPP 构建是不同范围。

原“实战验证 > 卡牌战斗完整流程 / 卡牌战斗发牌专项”限定 SingleBattleDemo 的 1v1，临时克隆玩家/怪物配置完成验证，恢复原配置引用，避免修改共享资产。“实战验证 > 卡牌战斗多人阵容（2对3）”通过实际 HUD 回调与动画验证切换、全员结束、三个怪物依次演出及整轮恢复，结果写入 `Library/SingleBattleTests/team-play.json`。

场景按 Battle / Card Table / Environment / Cameras / UI 五组组织。TeamBattleDemo 的两只 Fox 与三只 Dragon 复用既有美术与独立表现组件；音效槽位仍为空。场景不自动加入 Build Settings。

## 卡牌美术

单牌 CardView 与 Canvas 总览共用 `Assets/GameAssets/CardArt/Profiles/StandardCardArt.asset`；CardMatchHost 使用 CardStateArtBindings 将玩法状态映射为参数组合。Zone 仅管理投影/布局/动画。切换玩家仍沿用同一套卡牌美术，Run DeckEntry 的效果与禁用随卡牌保留。美术入口为 `工具 > 卡牌美术 > 着色器参数组合`；状态映射入口为 `工具 > 卡牌美术 > 卡牌状态映射`。

## 出牌表现入口

当前 SingleBattle 使用统一的 `ICardMatchCommands` 提交出牌，不依赖 `PlayedZoneView`、出牌区 `CardZonePlayHost` 或 `CardZoneTransferPresenter`。按钮通过 `CardZonePlayHost.PlaySelected(animate)` 捕获 `CardSelectionRequest`，将对局、玩家、牌区与卡牌 ID 交给 `PlayerBattleActor` 校验。规则接受后，ActionStarted 调用 `HandZoneView.PresentSelectionAction`，只在现有卡牌上添加准备偏移并发出 `Prepared`，此时不改变 `Data.Cards`；对应 CardsMoved 事实发布时才提交手牌成员变化，剩余手牌继续使用原理牌动画。计分、伤害、能量、补牌与规则层 Played → Discard 的收集顺序保留。

当前 `SingleBattleDemoNew` 已移除旧出牌区对象与飞行组件。其他旧单战场景可用 `工具 > 卡牌战斗 > 场景接入 > 移除旧出牌区表现` 清理并保存；操作支持 Undo。新配置流程也会清理这些旧绑定。旧 DrawBattle / 卡牌跨区示例仍使用原有通用转移组件，不影响当前战斗入口。新增出牌表现应接入相应事实的动作组和 marker，不必重新引入目标牌区或修改规则移牌。


## 弃牌盾牌表现

`BattleActorPresentationCoordinator` 是 Actor 的局部表现入口，组合原有模型 Presenter 与 `DiscardShieldPresenter`，通过中性 cue/单位快照适配新 Director。`GainBlock` 插入本次弃掉的牌；`BlockHit` 播放盾牌受击和整张溶解；`ClearBlock` 清空盾牌。完全格挡只播放盾牌表现；穿透格挡造成生命损失时，盾牌与角色受击同时执行，两个操作都完成后本动作才完成。死亡也清空容器。规则先结算，表现只消费只读结果，不修改格挡或伤害。

每次弃牌记录独立批次：本次牌列表、实际获得格挡、剩余格挡。批次按获得顺序消耗，初始格挡和其他来源格挡占用无卡牌的账目，避免误消耗弃牌盾牌。每批累计溶解张数为 `ceil((初始格挡 - 剩余格挡) × 张数 / 初始格挡)`；例如三张牌提供 24 格挡，累计消耗 1–8 点溶解第一张，9–16 点溶解第二张，17–24 点溶解第三张。已选中的牌播放完整溶解，其他牌保持完整，不停留在半溶解状态；因此最后少量格挡尚在时，牌也可能已经全部溶解。战局重开重置账目，跳过或恢复直接同步最终状态，不重播音效与震动。

当前 `SingleBattleDemoNew` 已接好。选中 `PlayerBattleActor` 下的 `Discard Shields`：

- 青色 `SpawnCenter / SpawnSize` 是卡牌实例化和飞入起点区域。
- 黄色 `RingCenter / RingInnerRadius / RingOuterRadius` 是围绕 Actor 的圆弧落地区域，以卡牌底部为锚点，可在 Scene 中直接移动圆心和拖动内外半径。`LandingHeightVariation` 控制底部高度随机范围。`ArcAngle` 控制圆弧角度（默认 60°），`ArcCenterAngle` 控制朝向（默认 0°，角色前方）。位置按圆弧区域面积随机分布，牌面随机朝内或朝外，并叠加独立俯仰、偏转和倾斜角度。
- `CardHeight` 是唯一的卡牌尺寸参数（容器局部单位），宽度自动按图集原始牌面比例计算，Inspector 显示最终宽高。`PitchRange / YawRange / RollRange` 分别控制俯仰、朝向偏移、左右倾斜的最小/最大角度，允许非对称范围。`OutwardChance` 为朝外概率（0 全朝内、1 全朝外），`Seed` 控制随机结果；同一批次恢复时姿态保持一致。旧宽高及角度配置自动迁移，保留高度和原角度范围。
- DOTween 使用不受 Time.timeScale 影响的时间：`FlightSeconds / CardInterval / ArcHeight` 控制逐张飞入，`DissolveSeconds / DissolveInterval` 控制逐张溶解。
- 每张落地触发材质闪光、生成于卡牌底边中心的可选 `LandingEffect` 粒子（位置包含卡牌旋转与缩放）、`LandingSound` 与设备震动。当前落地音效复用手牌弃牌音效；当前场景粒子绑定 `Sword Slash 14 Hit Flashes`，`LandingEffectScale = 0.2`，实例按卡牌高度等比缩放，整组粒子只播放一次，位置仍在牌底；`BlockHitSound` 默认空，可自行配置。严格在实际播放非空且音量大于零的音效时请求一次震动；静默落地、Snap 和取消不会触发震动。手机与手柄能力沿用 `CardHapticsDriver`。

盾牌容器是 Actor 根节点的子节点，配置时采用 Visual 的位置与朝向，保持独立于模型攻击前冲和受击晃动。当前弃牌按真实事实顺序调度：ActionStarted 先完成手牌准备，EffectSettled 启动 `GainBlock` 并在 `Revealed` 发布格挡；随后 CardsMoved 才提交离场，沿原 `DiscardAnchor / DiscardLocalPosition` 飞出，CardsDealt 提交补牌。手牌保持原 CardView 顺序和槽位直到出牌弹道／弃牌离场完成；剩余牌先按原相对顺序收拢，使用 DOTween `OutBack` 惯性缓动；Demo 与冒险场景的理牌时间为 0.35 秒，包含过冲与回稳。收拢落定后补牌，补牌落定后按当前排序方式整理。重复发布相同手牌的收牌／洗牌事实不会中断当前动画。盾牌仍跟随规则事实播放。Director 与 Host 等实际阻塞句柄及最终理牌过渡都结束后释放输入。该顺序不把规则移牌提前，也不要求先飞出再给盾；若需要其他编排，应使用事实节点的动作依赖/checkpoint 配置。

卡牌飞出和盾牌落地各自播放原有音效，每次实际发声请求一次震动；即时模式只同步最终状态。弃牌 View 不持有 Actor 引用，`CardActionViewOperation` 只报告本批离场卡牌（含错峰队列）的实际完成，不把剩余手牌理牌作为它自己的完成条件。取消、清空或禁用 View 会取消未完成句柄，由新调度链进入恢复同步。

渲染使用共享 Mesh、共享图集材质和 MaterialPropertyBlock 的实例属性，不创建逐牌材质。`CardArt/Shield Deferred` 提供 URP GBuffer、Forward、ShadowCaster、DepthOnly、DepthNormals Pass；牌面无原卡牌附加视觉效果。Perlin 噪声叠加牌面局部高度渐变，自上向下 alpha clip，两个 step 构成自发光边缘；深度和阴影使用相同裁剪。落地闪光为默认特效，外部粒子可选。

旧单战场景可运行 `工具 > 卡牌战斗 > 场景接入 > 配置弃牌护盾` 建立绑定和共享资产；支持重复执行。Inspector 提供三张牌的临时预览及清除按钮，预览实例不保存到场景。

专项验证：`DiscardShieldTests` 检查取整、FIFO、不可变快照、共享资源及实际 shader 渲染；`DiscardShieldPlaybackTests` 在 PlayMode 验证错峰落地、暂停时间、整张溶解和取消回调；`BattleActorTests` 覆盖真实弃牌奖励与协议路由。设备实际触感仍需在目标手机或手柄上调校。


### 卡牌动作 View 与 Actor 的统一交接协议

提交准备由 `CardZoneTimelineAction` 调用 `ICardZonePresentation` 的 cards.prepare，真实动作发送 Prepared。CardsMoved/CardsDealt 显示检查点通过 CardZonePlayHost.PresentProjection 调用同一接口，表现接受后提交镜像。原生 HandZoneView 动画仅作为 World 实现内部能力；旧提交协议和专用时间线动作已移除。完成状态为 Pending / Completed / Cancelled / Failed，取消只清理对应作用域，View 不引用 Actor 或修改战斗结算。

Director 用 `Prepared` 连接出牌准备和角色攻击，用 `Impact` 连接攻击与后续规则事实；弃牌默认等待准备动作 Completed 后进入收益效果。卡牌成员变化与补牌只跟随事实显示游标，动作结束不直接改写 cardlist。准备偏移拥有独立作用域，取消只释放自己的贡献；跨过准备信号后可与后续卡牌转移衔接，不能恢复过时位置或卡牌列表。任一动作取消或失败进入恢复同步，不当成成功继续播旧动作。扩展卡牌、镜头或场景物体演出应实现中性动作协议并配置依赖，无需恢复旧逐步协程路径。

当前场景完整配置教程见 [BattlePresentationTimelineConfiguration.md](../../../../../../Tools/BattlePresentationTimelineConfiguration.md)，包含信号顺序、效果/目标筛选、基础 HUD 检查点及 UI 动画模板。

### 阶段静态立绘

BattleDemo 的玩家使用 `PortraitBattleActorPresenter`，继承既有 Actor 攻击、受击及死亡表现；护盾仍由原 Coordinator 管理。桥接层将当前显示游标的阶段写入 `UnitDisplayData.Labels["portraitPose"]`，通过既有 `SynchronizeDisplay` 分发。组件不读取规则会话，也不按最终权威状态提前切图。

Artwork 子节点以 0.12 秒淡出右移、0.18 秒淡入归位切换贴图，使用 MaterialPropertyBlock 保持共享材质不变。阶段重复推送不重播，快速切换可续接当前透明度，Cancel/Reset 与 Actor 生命周期统一恢复。素材和完整生成提示词见 `Assets/Art/Characters/BattleBillboards/PlayerPoses/`；编辑器设置与真实流程检查位于 `Tools/BattleDemo/Portraits`。

### BattleDemo 多目标验证

`Assets/Scenes/BattleDemo.unity` 使用独立的 `BattleDemoValidation.xml` 内容副本，保留标准牌型规则，敌人血量为 600 / 4 / 6。两个低血量目标用于观察顺子击杀后的规则转火；群攻会向三个实际目标分别发射卡牌分支。

场景专用 `BattleDemoTargetSelection` 将肖像点击接到已有 `SelectPlayTarget` 接口。可先点怪物再选牌；选中目标沿贴图透明边缘显示红色描边。只有规则提供的目标槽可被指定，群攻不绑定单体目标，死亡目标不能选择。顺子首个目标可指定，后续转火由规则决定。

`BattleCardEffectBridge` 将顺子的已结算主伤害归到同一次飞行中，每张保留独立命中标记；发射间隔由场景弹道组件的 `straightInterval` 配置，当前为 0.1 秒。上述内容、目标描边材质和桥接组件仅由 BattleDemo 引用，不改变通用表现层约定。

### BattleDemo 小怪世界空间 UI

`BattleDemoEnemyWorldHud` 仅挂在 BattleDemo：头顶逐只显示规则预览的意图，脚下显示实时血条与当前护盾，血条下方显示状态图标及层数。攻击为红剑，防御为银白盾；同一条意图的数值与图标共用颜色。复合意图并排显示，悬停可查看意图或状态说明。

图标由场景专用 `BattleDemoEnemyIcon` 原生 uGUI 网格绘制，世界 Canvas 面向战斗相机并随怪物移动。血量、护盾与状态只读取已展示的快照；意图使用当前展示步骤的只读规则预览，过滤反伤、吸血等衍生效果。旧的合并敌人面板在当前 scene 中通过 CanvasGroup 隐藏；目标红色轮廓描边仍保留。此改动未扩展通用表现协议。

### BattleDemo 出牌、灰烬与遗物美术

出牌桥接直接使用规则结果的 `SubmittedCards / ScoringCards`：非计分牌只在首波攻击时离场，按当前手牌顺序播放与普通弃牌相同的加速飞出、翻面和轻微抬升，不创建攻击拖尾。两者共用 `CardDiscardMotion` 曲线和手牌区的 `DiscardSettings`（飞行时长、逐张间隔、抬升幅度），飞向同一弃牌位置。最后一张非计分牌离场后才允许后续收拢、补牌和整理。计分牌继续沿原牌型弹道命中目标，随后触发受击。`BattleCardProjectileAction` Inspector 的“计分牌拖尾宽度比例”当前为 0.55，拖尾沿长度逐渐变细，横向颜色保持一致。

三个小怪使用场景专用 `BattleDemoAshPresenter` 与 `BattleDemoAsh.shader`。死亡时灰化并逐步剥落为上飘灰烬；Inspector 的“灰烬消散时长”默认为 0.85 秒。进度通过每个 Renderer 的属性块驱动，重开、同步与复活恢复显示，禁用时还原原始材质。其他场景继续使用原来的死亡表现。

`BattleDemoOutcomePreview` 在选牌时直接显示正式规则预览的伤害、群攻数量、可击杀数量与目标生命变化；筹码和倍率放在悬停详情内。预览不提交动作或推进规则。

HUD 上的 `BattleDemoRelicArt` 在 Inspector 配置规则 ID 与 Sprite，也可调整图标尺寸、间距；当前接入 `Assets/Art/UI/Relics` 的全部 12 张 PNG。未命名图片按已确认的映射：遗物1→13002 致命一击、遗物2→10002 急救药水、遗物3→10001 同花倍率、遗物4→13003 牌型倍率。其他图片按规则名称对应。图标同时供遗物栏与主动道具按钮使用；绑定图片不会自动授予遗物。

当前场景的遗物栏仅常驻图标；有次数上限的主动道具在右下角显示规则快照中的剩余次数。悬浮图标展开效果说明与当前冷却，主动道具在同一面板提供使用按钮；六面骰需先选替换对象。移入说明面板可继续操作，离开图标和面板后关闭。按钮仍调用既有 `CanUseItem / UseItem`，演出门禁、次数与冷却均由规则和 Host 处理。未绑定 `BattleDemoRelicArt` 的其他场景保留原 UI。

出牌／弃牌时序回归：`工具 > 卡牌战斗 > 表现时间线 > 验证出牌弃牌顺序`。覆盖原槽位保持、离场后收拢、收拢后补牌、最终排序和中途恢复；结果写入 `Library/BattlePresentationValidation/results.xml`。
