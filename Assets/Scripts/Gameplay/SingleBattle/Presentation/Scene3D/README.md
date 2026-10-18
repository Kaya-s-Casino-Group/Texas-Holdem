# 主线卡牌战斗：3D 场景表现（Scene3D）

本模块是项目的主线卡牌战斗：玩家用扑克牌型发动攻击，消耗能量，处理生命、格挡、弃牌收益与敌方回合。支持 1v1 和多对多；`SingleBattle` 表示一场战斗，不表示只能有一名玩家。本目录实现现有 3D 场景适配；规则配置位于 `../../Configuration`，会话位于纯 C# 的 PokerEngine，通用显示与调度协议位于 [BattlePresentation](../../../../Presentation/BattlePresentation/README.md)。原有 3D 程序集名与命名空间保留，具体入口更名为 `SingleBattleSceneAdapter`，脚本 GUID 保留。

通用 `BattleSceneHost` 继承 `BaseBattleHost` 并持有纯 C# `BattleHost`；`SingleBattleSceneAdapter` 通过组合接入它，不再持有生命周期。规则链为 `SingleBattleSceneAdapter → BattleSessionPlayback → BattleSession → CardDuelEngine → TeamBattleEngine → CombatEngine`。`TeamBattleEngine` 是内部阵营回合规则，`TeamBattleDemo` 是本模块的多人阵容示例，两者都不是另一个并列玩法。[分数挑战（DrawBattle）](../../../DrawBattle/README.md) 是独立的达标计分模式，不负责本模块的抽牌。两种玩法共享 [卡牌交互](../../../../Presentation/CardInteraction/README.md) 与 [卡牌表现](../../../../Presentation/CardPresentation/README.md)。

主入口为 **工具 > 卡牌战斗 > 数值配置**。同组的“默认配置”“验算”“场景接入”“示例场景”“截图”分别提供规则资产、结果检查、表现绑定和示例维护。示例场景工具仍按原有场景限制执行；菜单改名不会自动迁移或修改场景。测试入口为 **工具 > 测试面板**。

外层 [关卡流程（RunFlow）](../../../RunFlow/README.md) 负责地图配置；当前卡牌战斗场景尚未接入完整的 Unity Run 流程。


`SingleBattleDemo.unity` 保留 1v1；`TeamBattleDemo.unity` 提供 2 位玩家对 3 个怪物。打开后 Play 即可，也可用 `工具 > 卡牌战斗 > 示例场景 > 打开多人战斗（2对3）`。这里是一场独立战斗；未来可作为 Run 的战斗节点，当前不是完整 Run。

## 目录与程序集

`Runtime/Hosting` 负责 Session 接入与场景绑定；`Actors` 是场景单位适配；`Presentation` 保存 `SingleBattleSceneAdapter.Presentation.cs` 的显示投影、牌区适配，以及攻击、VAT、护盾和音频表现；`UI` 只消费中性显示快照与候选模型。`IBattleActorPresentation` 是保留的局部 3D 兼容协议，现有组件另实现 `IBattleActorDisplayPresentation` 接收中性单位数据，其他表现风格不需要实现这两个局部接口。

`Poker.SingleBattle.Runtime`、`.Editor` 和两套测试程序集保留原名。Runtime 引用共享 Hosting、Configuration、`Poker.BattlePresentation.Shared` 与 `BattlePresentation.Runtime / .Unity`；场景设置、数值审计和布局编辑器仍属于本前端。`Assets/GameAssets/SingleBattle/Presentation/Scene3D` 下的 `Shaders` 与 `ShieldAssets` 只服务此风格。

## 策划集中配置入口

`SingleBattleDemoNew` 已切换到 XML 内容入口：`External/BattleToolkit/Engine/Content/SingleBattleDemoNew.xml`，默认编成 `single-battle-new`。SingleBattleSceneAdapter Inspector 选择 `XmlContent`、XML 文件和编成 ID；双方 Actor 数组按编成中的玩家/敌人顺序绑定，旧 RulesProfile 和 Actor Profile 在此场景已解绑。其他旧场景仍可选择 `LegacyProfiles`。

使用 [外部 HTML 配置工具](../../../../../../External/BattleToolkit/Tools/BattleContentEditor/README.md) 导入该 XML，修改后导出覆盖原文件；Unity 刷新后重新开战生效。`工具 > 卡牌战斗 > 数值配置` 在 XML 模式显示当前文件、编成及统一校验入口；SingleBattleSceneAdapter Inspector 也可校验。校验会真实组装未开局会话，不发牌、不触发开战效果。读取或引用错误、编成人数或接入会话的参与者顺序不匹配均会拒绝；失败重开保留原会话，诊断在 Inspector 与 HUD 显示。

XML 按策划文档 revision 1277 配置：高牌/一对/两对单体伤害，三条/四条群伤，顺子三段伤害，同花伤害加按筹码治疗，葫芦单体眩晕一次行动窗口加自身免控至下个敌人阶段结束，皇家同花顺消灭全部存活敌人。免控沿用已确认的“提前施加”语义；普通同花顺和五条暂为显式单体伤害兼容项。文档未定数值均在 XML 注释为测试值，计分表及角色基础值沿用原场景。共享三点能量，出牌/弃牌各一点、立即补牌；弃牌每张八盾，流程图的有限次数暂为整场三次。

同包提供 `single-battle-dot`、`single-battle-reflect`、`single-battle-overheal` 三个可选编成，分别测试顺子逐段附流血/中毒、弃牌盾反、同花治疗增幅与溢疗转伤；默认编成不装备遗物。切换场景适配器的编成 ID 即可重开测试。左侧 HUD 文字区从中性模型读取预览、状态及期限、遗物次数冷却，并仅显示已揭示的触发和阶段结算；原有 Actor/VAT/护盾/卡牌组件已作为新调度器的动作适配器接入。敌人仍为明确配置的固定攻击，文档中未细化的召唤、护甲、污染牌库和完整 Run 流程未在本次新增。

运行验证入口：在 `SingleBattleDemoNew` Play 模式执行 `工具 > 卡牌战斗 > XML内容 > 验证当前运行场景`；验证出牌、弃牌、补牌、格挡和完整回合并写入 `Library/SingleBattleTests/xml-scene.json`，成功后重新开战。该验证会消费当前测试对局的行动，请用于刚开局的默认编成。

旧 SO 模式下，打开 `工具 > 卡牌战斗 > 数值配置`，窗口只读取当前活动场景实际绑定的 SingleBattleSceneAdapter。多个战局入口时必须手动选择。

- **配置总览**：查看公共规则、玩家与敌人的实际资产、阵容顺序和保存范围，直接跳到对应角色页。
- **公共规则**：编辑回合上限、玩家格挡清除时机、共享牌型基础筹码/倍率、每级成长与单牌筹码。
- **玩家配置**：选择阵容内玩家，编辑生命、能量、出弃牌预算、补牌时机和个人牌型等级；展开编辑出牌伤害、弃牌收益、友方与攻击目标策略、攻击和承伤修正。
- **敌人配置**：选择敌人，编辑生命、初始格挡、基础攻击及该敌人的目标策略、攻击和承伤修正。

每项独立资产显示路径并可定位；角色内部的规则资产引用可直接替换，场景适配器 / Actor 的根配置绑定仍在 Inspector 管理。修改通过 SerializedObject 支持撤销，修改共享资产会影响所有引用者；需要独立数值时应先复制资产再替换引用。保存前调用规则层 Build 方法静态校验，不启动对局、不执行战斗动作；保存按钮仅保存当前战局关联且已修改的配置资产，不保存场景、美术或其他无关资源。关窗不会自动撤销已经修改的资产，Unity 自身的保存行为仍然有效。

所有数值在下次开战时生成快照；运行期间窗口只读。本窗口不提供局内状态修改、阵容编辑、卡组编辑或表现参数配置。

## 配置归属

`BattleSceneHost` 仅管理每局生命周期。`SingleBattleSceneAdapter` 持有玩家/怪物 Actor 数组、内容来源与 UI/表现绑定，不保存生命、攻击、能量、牌数等规则数值。数组顺序定义阵容席位与怪物行动顺序，双方人数可不同，总单位数受 CombatState 的 64 上限约束。

| 位置 | 内容 | 默认 Demo 资产 |
| --- | --- | --- |
| PlayerBattleActor → 玩家配置 | 单位 ID、最大生命、初始格挡、能量上限/初始值、出牌/弃牌费用、手牌上限、弃牌次数及无限开关、独立出/弃牌张数上限、两种补牌时机、出牌伤害、弃牌收益、攻击/承伤修正资产 | `Assets/GameAssets/SingleBattle/Configuration/Profiles/SingleBattlePlayer.asset` |
| EnemyBattleActor → 怪物配置 | 单位 ID、最大生命、初始格挡、基础攻击伤害、攻击/承伤修正资产；没有能量或牌组字段 | `Assets/GameAssets/SingleBattle/Configuration/Profiles/SingleBattleEnemy.asset` |
| SingleBattleSceneAdapter → 公共战局规则 | 规则版本、回合上限、玩家格挡清除时机、共享牌型计分基础表 | `Assets/GameAssets/SingleBattle/Configuration/Profiles/SingleBattleRules.asset` |

使用 Assets/Create/玩法/战局 创建不同角色/怪物的配置，分别拖到对应 Actor。多个 Actor 可复用同一模板，但需要不同数值时应复制或另建配置再绑定。共享资产不是角色当前状态：开战时转成不可变的 BattlePlayerSetup / BattleEnemySetup / BattlePlayerRules，当前 HP、格挡、能量、手牌仅由战局状态持有；修改配置应用于下一场战斗。

单局 `SingleBattleDemo` 使用独立的 `SingleBattle*.asset` 配置：玩家 **800 HP、8 张手牌、3 能量**，怪物 **1200 HP、攻击 240**；双方初始格挡为 0。出牌和弃牌各耗 1 能量、每次最多 5 张、均立即补满，弃牌不限额外次数。`SingleBattleDiscardReward.asset` 提供每张弃牌 **8 格挡**，敌人阶段结束清空玩家剩余格挡；公共回合上限为 **20**。采用标准 52 张牌、玩家配置中默认各 1 级的牌型与等分数伤害，无强化或反击能力，与离线模拟基础档一致。

现有多人 `TeamBattleDemo` 继续引用原 `DemoPlayer/DemoEnemy/DemoBattleRules/DemoDiscardReward.asset`，保留 40 HP、75 HP/攻击 7、445 回合上限及无弃牌收益的旧配置；单局调整不会修改这些共享资产。初始能量独立设置，允许超过上限；后续回合恢复到该玩家的上限。

### 可选基础规则接口

这些选项在开战时形成不可变快照。以下“默认”指新建配置/规则的代码默认值；单局 Demo 的显式配置见上文。

- `PlayerBattleProfile.unlimitedDiscards` → `BattlePlayerRules.UnlimitedDiscards`：默认 false，仍按整场预算扣次数；true 时不检查、不扣该计数，但仍检查能量、手牌、阶段与张数合法性。`discards = 0` 单独使用仍表示不能弃牌。`CardDuelState.RemainingDiscardBudgetFor(player)` 返回 null 表示不限，`HasDiscardBudgetFor(player)` 查询次数条件；旧 `DiscardsFor/DiscardsRemaining` 仅保留有限计数，在无限模式下保持初始值，不能用它判断可否弃牌。
- `maxPlayCards/maxDiscardCards` → `MaxPlayCards/MaxDiscardCards`：默认各 5。出牌配置范围 1–5（现有分类器的提交牌数范围）；弃牌范围 1–52；实际均取与手牌上限的较小值。`MaxSelection` 仅表示 UI 可选择范围的最大值，不能替代动作各自的上限。正式提交、预览、玩家按钮校验使用同一动作上限；绑定玩家时同步手牌选择上限。
- `BattleRulesProfile.playerBlockExpiry` → `BattleRules.PlayerBlockExpiry`：默认 `Retain`，格挡保留至被消耗。选 `AfterEnemyPhase` 时，最后一个敌人结算完伤害后，清除所有仍存活玩家的剩余格挡；不会在两个敌人之间清除，不修改怪物格挡。到达回合上限时同样清除，但不恢复下一轮能量；玩家提前击杀全部敌人不会触发不存在的敌人阶段。清除通过 CombatEngine 生成 ClearBlock 事件，按伤害事件后顺序追加，0 格挡不生成空事件。
- 新建 `玩法/战局/弃牌收益配置（每张格挡）` 可得到 `PerCardBlockRewardProfile`，独立配置 `blockPerCard`（新建默认 8，允许 0）；引用到玩家配置的弃牌收益槽位后才启用。纯 C# 对应 `PerCardBlockRewardRule`。每个选定存活友方获得 `实际弃牌张数 × 每张格挡`，默认目标策略为自己；多友方目标时每人各获得全额，非均分。规则不计牌型，0 收益不生成战斗事件，溢出拒绝整次动作。

离线验证：`External/BattleToolkit/Engine/Tests/Editor/BattleConfigurationTests.cs` 覆盖默认兼容、独立上限、次数/能量约束、实际弃牌计数、预览与执行、回滚、多敌人格挡时序与基础方案组合；这是规则正确性验证，不代表这些参数已经完成平衡验证。Windows 可运行 `Tools/test-poker-engine.ps1 -Fixtures BattleConfigurationTests,CardDuelTests,TeamBattleTests,DiscardRewardTests,CombatTests,AttackContextTests,ScoredHandDamageTests,ModuleBoundaryTests`。脚本按模块分别编译，不启动 Unity；首次运行由启动器准备 .NET SDK，并从 NuGet 还原测试依赖。已有 bash 脚本也支持将测试类名作为参数传入。

## 牌型计分

`工具/共享规则/牌型计分配置` 编辑默认筹码与倍率。SingleBattleRules 与 DemoBattleRules 的「牌型计分配置」引用此独立资产，开战后使用不可变快照。支持牌型基础值/等级成长、单牌点数筹码与指定牌面覆盖；手牌、牌库悬停读取当前战局相同配置。默认参考小丑牌，示例一对 K（带不计分的 A）为 `(10 + 10 + 10) × 2 = 60` 分，默认等额产生 60 点基础伤害。牌型实际等级从当前玩家的 BattlePlayerRules.HandLevels 读取，候选预览与正式出牌共用；公共资产只定义基础值与每级增长公式。详见 `Assets/Scripts/Rules/PokerScoring/README.md`。

## 个人牌型等级

在当前场景绑定的 `SingleBattlePlayer` 配置中，使用“个人牌型等级 > 各牌型等级”逐项设置高牌、一对、两对等牌型等级；DemoPlayer 也有自己的等级表。最低 1 级，重复牌型、无效牌型、非正等级及空条目在构建配置时拒绝，未列出的牌型默认 1 级以兼容已有资产。现有资产显式写入全部 10 种牌型的 1 级默认值，不改变原平衡。

`PlayerBattleProfile.BuildHandLevels()` 将作者数据复制成不可变 `BattleHandLevels`，保存在 `BattlePlayerRules.HandLevels`；纯 C# 可通过 BattlePlayerRules 构造参数 `handLevels:` 注入。等级与玩家 Rules 一起跨扣费、结束回合、敌人阶段和能量恢复保留；修改作者配置仅影响下次开战。独立玩家配置可以赋予不同等级，公共 ScoreProfile 不保存个人等级。

`CardDuelEngine.PreviewHand` 判型后按 actor 读取 `HandLevels.LevelFor(pattern.Category)`；左侧候选面板、出牌伤害预览与正式 Play 共用这条计分路径。基础筹码 = 牌型基础值 + 每级筹码增长 × (等级 − 1)，基础倍率同理；之后照常加入有效计分牌等效果。当前未加入战斗内升级命令、Run 等级自动带入或 Relic 接线。

## 独立弃牌收益与只读验算

- `PlayerBattleProfile` 的「弃牌收益配置」引用独立 `DiscardRewardProfile`。单局玩家绑定 `SingleBattleDiscardReward.asset`（PerCardBlockRewardProfile）；多人玩家保留 `DemoDiscardReward.asset`（NoDiscardRewardProfile）。未绑定时使用 `NoDiscardRewardRule`。`工具/卡牌战斗/默认配置/弃牌收益` 打开单局玩家当前绑定的收益资产。公共 BattleRulesProfile 与怪物配置不持有弃牌收益槽位。
- 配置的 `BuildRule()` 在开局生成不可变、纯 C# 的 `IDiscardRewardRule`，由该玩家的 `BattlePlayerRules.DiscardReward` 持有。执行与预览均按弃牌者取规则；友方目标只决定收益作用位置，不决定使用哪位玩家的收益规则。不同玩家可绑定不同收益，空槽位不继承其他玩家或公共规则。修改资产在下一场生效，回合切换保留快照。未来规则通过独立实现及派生配置接入，Host、牌区和验算面板不承担收益计算。费用、次数、移牌和补牌由战局原流程管理，收益规则不能自行修改它们。
- `DiscardRewardContext` 包含弃牌前的只读 TeamBattleState、弃牌者、实际弃掉的卡牌/有效牌面快照及已验证的友方目标。不会调用牌型分类、ScoreEngine、出牌伤害或攻击/承伤修正，也不伪造攻击来源；弃牌分数不进入战斗总分。直接调用 TeamBattle 的 Discard 时须提供真实弃牌快照；CardDuel 会自动构建。
- `DiscardRewardPlan` 返回版本、说明和效果列表；默认列表为空，不调用 CombatEngine、不增加 Combat revision、不生成 0 点收益事件。当前效果通路只允许 Combat 已有的 GainBlock / Heal，来源必须是弃牌者，目标必须属于选定的存活友方；按张数格挡是一个可选具体实现。非法输出、版本不匹配或数值溢出原子拒绝整次弃牌，不扣费、不移牌、不消耗随机数。
- `工具/卡牌战斗/验算/弃牌收益` 是独立中文只读面板，展示弃牌快照、收益目标、原有费用/次数/过牌结果、规则说明以及额外收益和实际结算，按当前观察玩家的规则快照显示格挡或“无额外收益”。「当前选牌预计结果」调用 `Host.PreviewSelectedDiscard → BattleSessionPlayback.PreviewDiscard → CardDuelEngine.PreviewDiscard → Execute`，不提交返回状态；「最近一次实际弃牌」读取 Host 缓存的已提交返回值，重新开局清空。
- 测试入口 `工具/测试面板` →「弃牌收益与友方边界」。规则与目标策略必须纯函数，不能读取外部随机源、改变状态或触发表现，保证预览与实际执行一致。

## 角色攻击与承伤配置

攻击修正与承伤修正属于角色战斗规则，玩家与怪物共用 `BattleActorProfile` 的两个槽位，不属于 `BattleRulesProfile`。攻击者的资产决定攻击修正，实际受击目标的资产决定承伤修正。它们独立于计分、基础伤害构建和当前生命/格挡；本层尚不提供 Buff 调度或条件规则列表。

- 新建资产：`Assets/Create/玩法/角色/攻击修正（倍率与固定值）`、`承伤修正（倍率与固定值）`。拖到 PlayerBattleProfile / EnemyBattleProfile 的对应槽位。空槽位原值通过。
- 两类基础资产均配置版本、非负倍率和有符号固定增减值，计算 `floor(max(0, 输入伤害 × 倍率 + 固定值))`。例如输入 100、倍率 0.8、固定值 -5，输出 75；先应用攻击修正，再对其输出应用目标承伤，最后扣格挡。超限拒绝整次动作，不截断成上限。
- 单局玩家引用 `SingleBattlePlayerAttackModifier.asset` / `SingleBattlePlayerIncomingModifier.asset`，怪物引用 `SingleBattleEnemyAttackModifier.asset` / `SingleBattleEnemyIncomingModifier.asset`。四份资产互相独立，初始均为 1 倍、0 增减，不改变已有伤害。多人配置未绑定时保持原行为。
- `BuildRule()` 复制资产数值，规则不得持有 ScriptableObject 引用。`BuildSetup()` 将两类规则组成 `BattleActorDamageRules` 放入每个角色的开战快照。修改或替换资产作用于下一场；扣费、结束回合和恢复能量不会丢失角色规则。
- 扩展条件策略时派生 `AttackDamageModifierProfile` / `IncomingDamageModifierProfile`，返回纯 C# 的 `IAttackDamageModifier` / `IIncomingDamageModifier`。可读取现有 AttackContext 与 CombatState，不修改来源、目标、状态、随机数或触发表现。玩家出牌、怪物攻击及正式动作预览共用接线；候选牌型 GUI 仍只做计分，不调用伤害修正。

## 出牌伤害与只读验算

- `PlayerBattleProfile` →「出牌伤害配置」引用独立 `ScoredHandDamageProfile`。现有 SingleBattlePlayer / DemoPlayer 均迁移原 `DemoScoreDamage.asset` 引用，保持得分等额基础伤害；不同玩家可以绑定不同资产。入口 `工具/卡牌战斗/默认配置/出牌伤害` 打开单局玩家实际绑定的资产。新建玩家配置必须绑定伤害配置，缺失或工厂返回空规则时报错；公共规则与怪物配置不持有该槽位。
- `ScoredHandDamageProfile.BuildRule()` 在开战时构建纯 C#、不可变的 `IScoredHandDamageRule`，保存于该玩家的 `BattlePlayerRules.DamagePolicy`。预览与正式出牌按出牌者选取规则；怪物直接提供基础攻击，不调用玩家的出牌伤害规则。修改资产下一场生效，已有回合转换保留玩家规则快照。新增策略通过实现接口与派生配置资产接入；公共战局配置、Host 和牌区不维护转换参数。
- `ScoredHandDamageContext` 包含计分结果、攻击者、由目标策略验证后的目标列表、只读 CombatState 和正式出牌生成的 HandAttackInfo。`DamagePlan` 返回每个目标的 `DamageRequest` 及说明。当前协议要求每个已选目标按顺序恰好一条请求，不允许偷换来源/目标/规则版本；异常输出会原子拒绝本次出牌。
- 边界为：牌型计分 → 构建基础伤害 → 攻击修正 → 目标承伤修正 → 格挡与生命。卡牌/Relic 对筹码倍率的影响属于计分层，不再在伤害阶段重复计算。
- 攻击与承伤规则由各角色的 `BattleActorProfile` 独立资产生成，并在开战时冻结到 `BattleActorDamageRules`。攻击阶段按 Source、承伤阶段按 Target 取对应角色规则；空槽位使用 Identity 原值通过。`BattleRules` 不再接受全局攻击/承伤修正；纯 C# 调用也必须通过各自 Setup 的 `BattleActorDamageRules` 配置，未提供时使用 Identity。上下文包含只读 CombatState、目标、该阶段输入伤害，以及共用的 `AttackContext`（攻击者、来源类型、基础伤害和出牌快照）。`Amount` 是该阶段输入，`Attack.BaseDamage` 始终是原始基础伤害；返回 `DamageModification`，记录版本、说明、输入与输出及可选触发原因，只能改变数值，不能偷换来源快照、目标或修改状态。
- 攻击修正在 TeamBattleEngine 构建各目标请求时执行，读取本次攻击开始时的状态；目标承伤修正在 `CombatEngine.Resolve` 内、格挡前执行，读取同一原子批次前序效果结算后的状态。无效输出或数值溢出会拒绝整个动作，已计算的中间结果不写回。
- 只有 `CombatEngine.Resolve` 扣格挡/生命并生成掉血、溢出、死亡事件。怪物普通攻击直接产生 DamageRequest，不经过牌型计分，但仍经过攻击与承伤两个修正接口。
- 旧 standalone `ScoredDuelEngine`/快照保留 ScoreDamagePolicy 的兼容入口。当前 Demo 使用等额规则与新修正接口，不修改旧存档格式；显式注入旧 ScoreDamagePolicy 的兼容调用仍保留除法行为。

出牌信息只在 `CardDuelEngine` 正式判型/计分后、移牌前生成一次。`HandAttackInfo.Pattern` 是同一个判型结果；`AttackCardSnapshot` 保存实例 ID、定义/Run 来源 ID、当时有效点数与花色、效果和禁用状态，不引用 Unity、美术或实时牌区。

- `SubmittedCards`：全部实际提交牌，包括杂牌和失效牌。
- `PatternCards`：判型结果选出的组成牌，包括失效牌。
- `ScoringCards`：组成牌中允许逐牌计分的牌，排除失效牌；筹码为 0 不代表没参加计分。现阶段仍采用“仅组成牌计分”的正式规则，不在伤害层重判牌型。
- 花色条件由扩展实现明确使用 `Attack.Hand.Cards(AttackCardScope.Submitted/Pattern/Scoring)`，没有隐式默认范围。各目标共用同一份 HandAttackInfo；每个目标的攻击修正和承伤修正接收同一个 AttackContext 对象，卡牌移动后仍读取原始快照。
- 怪物普通攻击标记 `EnemyAttack`，Hand 为空。原始 CombatEffect 和只提交分数的旧 TeamBattle 调用标记 `Other`，不会根据 ScoreResult 伪造牌面；需要牌条件的实现必须先确认有出牌快照。
- `DamageModification.Triggers` 是实际修正实现返回的 `DamageTriggerReason` 列表，包含来源 ID、说明、检查范围与关联实例牌 ID。引擎校验关联牌属于声明范围，错误记录原子拒绝。没有触发时列表为空；记录结构本身不实现条件、叠加顺序或能力公式。本次未添加生产用花色增伤、牌型减伤或能力配置。

`工具/卡牌战斗/验算/出牌伤害` 为 Editor 专用中文只读窗口。在 Play 模式游戏中选择手牌，面板自动读取当前观察玩家，展示攻击来源、提交牌/牌型组成牌/实际计分牌、筹码倍率步骤、总分、基础伤害、攻击修正、目标承伤修正（含规则版本、说明、触发来源/原因、检查范围和关联牌）、格挡前伤害以及各目标格挡、实际掉血、溢出和死亡。多个场景宿主可通过「观察战局」手动选择。面板不负责选目标，使用与当前出牌相同的目标策略。

「当前选牌预计结果」通过 `Host.PreviewSelectedPlay → BattleSessionPlayback.PreviewPlay → CardDuelEngine.PreviewPlay → Execute` 走完整正式出牌入口，包括合法性、计分、伤害、CombatEngine 和补牌；只保留返回值供显示，绝不将返回状态写入 Host，不触发 UI/表现/回合提交。反复读取不改变当前牌区、能量或随机状态。注入的攻击构建、攻击/承伤修正、选目标和发牌策略必须保持纯函数约定，不读取外部随机数或产生副作用。

「最近一次实际出牌」读取 Host 缓存的已提交 CardDuelTransition，包含当时的牌型、分数、DamagePlan 和 CombatEvent；不重新计算历史结果。重新开局清空记录。不可行动或演出中不展示过时预览。当前工具不提供独立造牌/修改生命/编辑配置或提交出牌按钮。

测试入口 `工具/测试面板` →「攻击来源与触发依据」「出牌伤害与只读验算」，场景接入/配置快照测试在「角色与表现协议」。

## 回合与牌组

1. Create 时每位玩家分别创建标准不含大小王的 52 张牌，拥有自己的抽牌、手牌、出牌、弃牌四区，起手为 0。Start 首次进入玩家阶段，为所有玩家按各自策略免费发牌。
2. 存活玩家共享行动阶段，可交错出牌/弃牌，每人独立扣费。能量为 0 自动标记该玩家结束；也可手动结束。初始能量为 0 时仍可手动空过，费用不足但非 0 时不强行结束。
3. 所有存活玩家均结束，才进入怪物阶段。存活怪物按阵容列表顺序行动。BattleSession 在本次玩家命令内自动结算整个怪物队列；表现层按有序事实及其动作依赖播放，不再提交怪物命令。
4. 全部怪物完成后算一整轮。仍在战斗时，所有存活玩家恢复各自能量并按各自策略补牌。死亡玩家不再参与行动/等待条件，也不恢复能量或补牌。
5. 怪物全灭胜利、玩家全灭失败；达到回合上限在完整怪物阶段后判负。部分队友/怪物死亡不提前终止整场。

选择 1–5 张真实手牌，由 PokerHandClassifier / ScoreEngine 计算牌型，总分默认由 EqualScoreDamageRule 等额生成基础伤害，经过攻击修正后交给 CombatEngine 处理目标承伤、格挡与 HP。默认出牌延迟至下一玩家阶段补满，弃牌立即补满；每位玩家可单独配置。抽牌堆不足只洗回该玩家的弃牌堆。

本地 Demo 用一套 CardZone/UI 观察当前玩家，点击“切换玩家”或 Tab 切换。结束后自动转到仍可行动的队友。切换只重新绑定观察者牌区和命令接收者，不移动逻辑卡牌、不重开战局；牌库总览也仅包含当前玩家的完整构筑。表现/发牌过程中禁止切换与重复输入。

场景首次自动开局在 BattleSceneHost.Start 中执行，等待各牌区完成 OnEnable 后，先绑定空手牌，再提交 Start 发牌并播放入列动画；避免 Host 较早的 OnEnable 在 HandZoneView 尚未激活时跳过动画。逻辑手牌一次性提交，逐张飞入属于表现；重新启用已有对局仍恢复当前状态，不再次发开局牌。

手牌默认按点数理牌，开局发牌及后续补牌落位后自动执行；切换玩家也按当前选择的方式理牌。手动切换为花色理牌后，后续发牌沿用花色排序。排序只调整观察者的显示顺序，不改变权威牌区或洗牌状态。

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
- `IBattleAllyTargetPolicy.SelectTargets(BattleAllyTargetContext)` 预留独立友方目标接口，默认自己，也可请求存活队友；引擎验证请求与策略输出，拒绝敌方、死亡、未知和重复目标。由行动来源角色的 BattleActorTargetRules.Support 提供；Unity 入口在 PlayerBattleProfile 的友方目标策略槽位。弃牌通过独立 IDiscardRewardRule 生成收益计划，默认空计划；费用/次数和过牌仍由原有流程处理，不按牌型换取格挡。
- `PlayerBattleActor.CreateSetup(id, deck, initialHealth)` / `PlayerBattleProfile.BuildSetup` 可消费 Run 当前构筑和生命；缺省牌组为独立标准 52 张。Core 不依赖 ScriptableObject 或 Unity。
- `ICardDuelDealPolicy` 上下文含 PlayerId，只返回抽牌数量，不能移动或创建卡牌。每位玩家可独立注入策略。
- `ScoredDuelEngine` / 旧快照协议保留给原有独立 1v1 规则调用；新 Demo 使用 TeamBattleEngine。完整多人战局存档、Run 奖励写回、复活和增援尚未实现。

## 公共规则与后续边界

公共 BattleRulesProfile 仅保留版本、回合上限、玩家阵营格挡清除时机及共享牌型计分基础表。纯 C# BattleRules 仅持有前三项；计分表由 CardDuelEngine 独立持有。出牌伤害与弃牌收益归属 BattlePlayerRules，攻击/承伤修正归属双方角色的 BattleActorDamageRules，不再存在全局修正回退。旧 ScoredDuelDefinition 适配入口将自己的伤害转换显式写入默认玩家规则，旧 standalone 快照格式保持。

尚待后续拆分的边界：

- 个人牌型等级已从玩家配置接入开战快照；Relic 仍为空，战斗中升级及 Run 成长带入尚未接入。
- SingleBattleHud 同时处理信息显示、按钮命令、Tab 切换和世界空间意图对象的生命周期。未来可拆成信息展示、操作绑定和意图 Presenter；游戏数值仍由规则层提供。

## 角色目标配置

BattleActorProfile（玩家和敌人共有）新增“攻击目标策略”独立资产槽位；PlayerBattleProfile 另有“友方目标策略”槽位，用于弃牌收益等支援行动。开战时 BuildTargetRules 将配置转成纯 C# BattleActorTargetRules，随各自 Setup 输入并保留在玩家状态与敌人阵容中，扣费、结束回合与恢复能量不会丢失。修改资产在下次开战生效；空攻击槽默认为首个存活对手，空友方槽默认为自身或指定单个队友。

创建菜单 `玩法 > 角色 > 攻击目标策略` 提供首个存活、当前生命最低、当前生命最高、全部存活对手四种模式；生命比较使用当前绝对生命值，同值按阵容顺序确定，不引入随机数。“允许命令指定目标”默认开启以保留旧行为；开启时合法显式目标优先，单目标模式只接受一个目标；关闭时拒绝带显式目标的命令。每个敌人的 EnemyBattleProfile 可绑定不同策略资产，敌人意图预览和实际攻击都读取该敌人的同一份策略快照。

SingleBattle / Demo 的玩家、敌人分别绑定独立默认攻击策略资产，玩家分别绑定友方策略资产。若多个场景角色共享同一角色 Profile，它们仍共享作者配置；需要不同策略时绑定各自的角色 Profile。可继承 AttackTargetPolicyProfile / AllyTargetPolicyProfile 扩展确定性策略，BuildPolicy 不得返回读取可变 Unity 资产的运行时规则。

Host、CardDuelEngine、TeamBattleEngine 已移除全局目标策略参数和配置入口，不保留全局回退。纯 C# 调用改为 `new BattlePlayerSetup(..., targetRules: new BattleActorTargetRules(attack, support))` 或 `new BattleEnemySetup(..., targetRules: new BattleActorTargetRules(attack))`。TeamBattleEngine 仅在目标选择边界分派来源角色策略并做公共合法性校验：来源必须存活且属于当前战局，请求及输出须为合法阵营的存活单位、无重复且数量有效；非法请求不能被忽略它的策略绕过。策略失败会拒绝整个命令，预览显示不可用。

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
- 当前出牌配置是 `cards.ReadyToAttack → attack`，`attack.Impact` 放行下一事实；真实效果通过筛选进入受击/格挡动作，在 `effect.Revealed` 后启动伤害浮字和目标镜头反馈。UI 提示自身也发 `Revealed`，可用于控制显示快照公开时点；HUD 的血量、能量等读取公开后的快照。
- `HandPreparationTimelineAction`、`HandSettleTimelineAction`、`BattleActorTimelineAction` 是现有牌区/角色协议适配器；`TransformTimelineAction` 是通用分段变换；`UiFeedbackTimelineAction` 是通用文字淡入、上浮、淡出。全部派生基础表现组件，规则对象不传入这些动作。每次 UI 调用独立克隆模板，取消只释放自己的临时物体。
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

当前出牌默认计划为：ActionStarted 中卡牌准备发出 `ReadyToAttack` → 玩家攻击在前冲节点发出 `Impact` → 依 Timeline 顺序揭示判型与效果等事实；受击/护盾效果开始时发出 `Revealed` 并发布相应生命/格挡，后续费用、CardsMoved/CardsDealt 事实分别更新能量、移牌与补牌。攻击收招可以和受击重叠；死亡依赖该单位效果动作完成，同一 Actor 的动作串行，多个目标可并行反应。敌人使用 PrepareAttack → Attack/Impact，受控制跳过的行动不播放攻击。整批事实及阻塞动作、手牌实际过渡全部结束后才释放输入；BattleEnded 在规则实际结束位置出现，不制造下一玩家阶段。观察事件不承担等待职责。

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

当前 SingleBattle 使用统一的 `ICardMatchCommands` 提交出牌，不依赖 `PlayedZoneView`、出牌区 `CardZonePlayHost` 或 `CardZoneTransferPresenter`。按钮通过 `CardZonePlayHost.PlaySelected(animate)` 捕获 `CardSelectionRequest`，将对局、玩家、牌区与卡牌 ID 交给 `PlayerBattleActor` 校验。规则接受后，ActionStarted 调用 `HandZoneView.PresentSelectionAction`，只在现有卡牌上添加准备偏移并发出 `ReadyToAttack`，此时不改变 `Data.Cards`；对应 CardsMoved 事实发布时才提交手牌成员变化，剩余手牌继续使用原理牌动画。计分、伤害、能量、补牌与规则层 Played → Discard 的收集顺序保留。

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

盾牌容器是 Actor 根节点的子节点，配置时采用 Visual 的位置与朝向，保持独立于模型攻击前冲和受击晃动。当前弃牌按真实事实顺序调度：ActionStarted 先完成手牌准备，EffectSettled 启动 `GainBlock` 并在 `Revealed` 发布格挡；随后 CardsMoved 才提交离场，沿原 `DiscardAnchor / DiscardLocalPosition` 飞出，CardsDealt 提交补牌。盾牌飞入、离场与补牌可以重叠，Director 与 Host 等实际阻塞句柄及手牌过渡都结束后释放输入。该顺序不把规则移牌提前，也不要求先飞出再给盾；若需要其他编排，应使用事实节点的动作依赖/checkpoint 配置。

卡牌飞出和盾牌落地各自播放原有音效，每次实际发声请求一次震动；即时模式只同步最终状态。弃牌 View 不持有 Actor 引用，`CardActionViewOperation` 只报告本批离场卡牌（含错峰队列）的实际完成，不把剩余手牌理牌作为它自己的完成条件。取消、清空或禁用 View 会取消未完成句柄，由新调度链进入恢复同步。

渲染使用共享 Mesh、共享图集材质和 MaterialPropertyBlock 的实例属性，不创建逐牌材质。`CardArt/Shield Deferred` 提供 URP GBuffer、Forward、ShadowCaster、DepthOnly、DepthNormals Pass；牌面无原卡牌附加视觉效果。Perlin 噪声叠加牌面局部高度渐变，自上向下 alpha clip，两个 step 构成自发光边缘；深度和阴影使用相同裁剪。落地闪光为默认特效，外部粒子可选。

旧单战场景可运行 `工具 > 卡牌战斗 > 场景接入 > 配置弃牌护盾` 建立绑定和共享资产；支持重复执行。Inspector 提供三张牌的临时预览及清除按钮，预览实例不保存到场景。

专项验证：`DiscardShieldTests` 检查取整、FIFO、不可变快照、共享资源及实际 shader 渲染；`DiscardShieldPlaybackTests` 在 PlayMode 验证错峰落地、暂停时间、整张溶解和取消回调；`BattleActorTests` 覆盖真实弃牌奖励与协议路由。设备实际触感仍需在目标手机或手柄上调校。


### 卡牌动作 View 与 Actor 的统一交接协议

提交准备使用 `HandZoneView.PresentSelectionAction` 返回 `CardActionViewOperation`，并在真实动作中发送 `ReadyToAttack`；这是不修改牌区成员的临时贡献。正式成员变化由 CardsMoved/CardsDealt 的显示检查点触发：出牌使用 `PresentCommitted`，弃牌离场继续使用 `ICardActionView.PresentActionCommitted(Discard, ...)`。旧 Play/Discard 调用协议继续为其他调用方保留。完成状态为 Pending / Completed / Cancelled / Failed；单个句柄只允许一次终态，View 不引用 Actor，也不修改战斗结算。

Director 用 `ReadyToAttack` 连接出牌准备和角色攻击，用 `Impact` 连接攻击与后续规则事实；弃牌默认等待准备动作 Completed 后进入收益效果。卡牌成员变化与补牌只跟随事实显示游标，动作结束不直接改写 cardlist。准备偏移拥有独立作用域，取消只释放自己的贡献；跨过准备信号后可与后续卡牌转移衔接，不能恢复过时位置或卡牌列表。任一动作取消或失败进入恢复同步，不当成成功继续播旧动作。扩展卡牌、镜头或场景物体演出应实现中性动作协议并配置依赖，无需恢复旧逐步协程路径。

当前场景完整配置教程见 [BattlePresentationTimelineConfiguration.md](../../../../../../Tools/BattlePresentationTimelineConfiguration.md)，包含信号顺序、效果/目标筛选、基础 HUD 检查点及 UI 动画模板。
