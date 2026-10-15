# 单场战斗：1v1 与 n v n

`SingleBattleDemo.unity` 保留 1v1；`TeamBattleDemo.unity` 提供 2 位玩家对 3 个怪物。打开后 Play 即可，也可用 `Tools > Poker > Single Battle > 打开多人战局 Demo（2v3）`。这里的一场战斗是 Run 中的一个节点，不是完整 Run。

## 配置归属

Host 不再保存生命、攻击、能量、牌数等数值，仅持有玩家/怪物 Actor 数组、公共规则引用、UI/表现绑定与运行时流程。数组顺序定义阵容席位与怪物行动顺序，双方人数可不同，总单位数受 CombatState 的 64 上限约束。

| 位置 | 内容 | 默认 Demo 资产 |
| --- | --- | --- |
| PlayerBattleActor → 玩家配置 | 单位 ID、最大生命、初始格挡、能量上限/初始值、出牌/弃牌费用、手牌上限、弃牌次数及无限开关、独立出/弃牌张数上限、两种补牌时机、出牌伤害、弃牌收益、攻击/承伤修正资产 | `Assets/SingleBattle/Profiles/SingleBattlePlayer.asset` |
| EnemyBattleActor → 怪物配置 | 单位 ID、最大生命、初始格挡、基础攻击伤害、攻击/承伤修正资产；没有能量或牌组字段 | `Assets/SingleBattle/Profiles/SingleBattleEnemy.asset` |
| SingleBattleHost → 公共战局规则 | 规则版本、回合上限、玩家格挡清除时机、共享牌型计分基础表 | `Assets/SingleBattle/Profiles/SingleBattleRules.asset` |

使用 Assets/Create/玩法/战局 创建不同角色/怪物的配置，分别拖到对应 Actor。多个 Actor 可复用同一模板，但需要不同数值时应复制或另建配置再绑定。共享资产不是角色当前状态：开战时转成不可变的 BattlePlayerSetup / BattleEnemySetup / BattlePlayerRules，当前 HP、格挡、能量、手牌仅由战局状态持有；修改配置应用于下一场战斗。

单局 `SingleBattleDemo` 使用独立的 `SingleBattle*.asset` 配置：玩家 **800 HP、8 张手牌、3 能量**，怪物 **1200 HP、攻击 240**；双方初始格挡为 0。出牌和弃牌各耗 1 能量、每次最多 5 张、均立即补满，弃牌不限额外次数。`SingleBattleDiscardReward.asset` 提供每张弃牌 **8 格挡**，敌人阶段结束清空玩家剩余格挡；公共回合上限为 **20**。采用标准 52 张牌、1 级牌型与等分数伤害，无强化或反击能力，与离线模拟基础档一致。

现有多人 `TeamBattleDemo` 继续引用原 `DemoPlayer/DemoEnemy/DemoBattleRules/DemoDiscardReward.asset`，保留 40 HP、75 HP/攻击 7、445 回合上限及无弃牌收益的旧配置；单局调整不会修改这些共享资产。初始能量独立设置，允许超过上限；后续回合恢复到该玩家的上限。

### 可选基础规则接口

这些选项在开战时形成不可变快照。以下“默认”指新建配置/规则的代码默认值；单局 Demo 的显式配置见上文。

- `PlayerBattleProfile.unlimitedDiscards` → `BattlePlayerRules.UnlimitedDiscards`：默认 false，仍按整场预算扣次数；true 时不检查、不扣该计数，但仍检查能量、手牌、阶段与张数合法性。`discards = 0` 单独使用仍表示不能弃牌。`CardDuelState.RemainingDiscardBudgetFor(player)` 返回 null 表示不限，`HasDiscardBudgetFor(player)` 查询次数条件；旧 `DiscardsFor/DiscardsRemaining` 仅保留有限计数，在无限模式下保持初始值，不能用它判断可否弃牌。
- `maxPlayCards/maxDiscardCards` → `MaxPlayCards/MaxDiscardCards`：默认各 5。出牌配置范围 1–5（现有分类器的提交牌数范围）；弃牌范围 1–52；实际均取与手牌上限的较小值。`MaxSelection` 仅表示 UI 可选择范围的最大值，不能替代动作各自的上限。正式提交、预览、玩家按钮校验使用同一动作上限；绑定玩家时同步手牌选择上限。
- `BattleRulesProfile.playerBlockExpiry` → `BattleRules.PlayerBlockExpiry`：默认 `Retain`，格挡保留至被消耗。选 `AfterEnemyPhase` 时，最后一个敌人结算完伤害后，清除所有仍存活玩家的剩余格挡；不会在两个敌人之间清除，不修改怪物格挡。到达回合上限时同样清除，但不恢复下一轮能量；玩家提前击杀全部敌人不会触发不存在的敌人阶段。清除通过 CombatEngine 生成 ClearBlock 事件，按伤害事件后顺序追加，0 格挡不生成空事件。
- 新建 `玩法/战局/弃牌收益配置（每张格挡）` 可得到 `PerCardBlockRewardProfile`，独立配置 `blockPerCard`（新建默认 8，允许 0）；引用到玩家配置的弃牌收益槽位后才启用。纯 C# 对应 `PerCardBlockRewardRule`。每个选定存活友方获得 `实际弃牌张数 × 每张格挡`，默认目标策略为自己；多友方目标时每人各获得全额，非均分。规则不计牌型，0 收益不生成战斗事件，溢出拒绝整次动作。

离线验证：`Assets/PokerEngine/Tests/Editor/BattleConfigurationTests.cs` 覆盖默认兼容、独立上限、次数/能量约束、实际弃牌计数、预览与执行、回滚、多敌人格挡时序与基础方案组合；这是规则正确性验证，不代表这些参数已经完成平衡验证。Windows 可运行 `Tools/test-poker-engine.ps1 -Compiler <csc.exe> -Runtime <mono.exe> -NUnit <已有的 NUnit3 DLL> -Fixtures BattleConfigurationTests,CardDuelTests,TeamBattleTests,DiscardRewardTests,CombatTests,AttackContextTests,ScoredHandDamageTests,ModuleBoundaryTests`。脚本按模块分别编译，不启动 Unity、不下载依赖；编译输出默认保存在系统临时目录。已有 bash 脚本也支持将测试类名作为参数传入。

## 牌型计分

`Tools/Poker/牌型计分/打开默认配置` 编辑默认筹码与倍率。SingleBattleRules 与 DemoBattleRules 的「牌型计分配置」引用此独立资产，开战后使用不可变快照。支持牌型基础值/等级成长、单牌点数筹码与指定牌面覆盖；手牌、牌库悬停读取当前战局相同配置。默认参考小丑牌，示例一对 K（带不计分的 A）为 `(10 + 10 + 10) × 2 = 60` 分，默认等额产生 60 点基础伤害。新战局仍固定 1 级牌型；玩家成长接线另行扩展。详见 `Assets/PokerScoring/README.md`。

## 独立弃牌收益与只读验算

- `PlayerBattleProfile` 的「弃牌收益配置」引用独立 `DiscardRewardProfile`。单局玩家绑定 `SingleBattleDiscardReward.asset`（PerCardBlockRewardProfile）；多人玩家保留 `DemoDiscardReward.asset`（NoDiscardRewardProfile）。未绑定时使用 `NoDiscardRewardRule`。`Tools/Poker/战局/打开默认弃牌收益配置` 打开单局玩家当前绑定的收益资产。公共 BattleRulesProfile 与怪物配置不持有弃牌收益槽位。
- 配置的 `BuildRule()` 在开局生成不可变、纯 C# 的 `IDiscardRewardRule`，由该玩家的 `BattlePlayerRules.DiscardReward` 持有。执行与预览均按弃牌者取规则；友方目标只决定收益作用位置，不决定使用哪位玩家的收益规则。不同玩家可绑定不同收益，空槽位不继承其他玩家或公共规则。修改资产在下一场生效，回合切换保留快照。未来规则通过独立实现及派生配置接入，Host、牌区和验算面板不承担收益计算。费用、次数、移牌和补牌由战局原流程管理，收益规则不能自行修改它们。
- `DiscardRewardContext` 包含弃牌前的只读 TeamBattleState、弃牌者、实际弃掉的卡牌/有效牌面快照及已验证的友方目标。不会调用牌型分类、ScoreEngine、出牌伤害或攻击/承伤修正，也不伪造攻击来源；弃牌分数不进入战斗总分。直接调用 TeamBattle 的 Discard 时须提供真实弃牌快照；CardDuel 会自动构建。
- `DiscardRewardPlan` 返回版本、说明和效果列表；默认列表为空，不调用 CombatEngine、不增加 Combat revision、不生成 0 点收益事件。当前效果通路只允许 Combat 已有的 GainBlock / Heal，来源必须是弃牌者，目标必须属于选定的存活友方；按张数格挡是一个可选具体实现。非法输出、版本不匹配或数值溢出原子拒绝整次弃牌，不扣费、不移牌、不消耗随机数。
- `Tools/Poker/战局/弃牌收益验算` 是独立中文只读面板，展示弃牌快照、收益目标、原有费用/次数/过牌结果、规则说明以及额外收益和实际结算，按当前观察玩家的规则快照显示格挡或“无额外收益”。「当前选牌预计结果」调用 `Host.PreviewSelectedDiscard → CardDuelEngine.PreviewDiscard → Execute`，不提交返回状态；「最近一次实际弃牌」读取 Host 缓存的已提交返回值，重新开局清空。
- 测试入口 `Tools/测试面板` →「弃牌收益与友方边界」。规则与目标策略必须纯函数，不能读取外部随机源、改变状态或触发表现，保证预览与实际执行一致。

## 角色攻击与承伤配置

攻击修正与承伤修正属于角色战斗规则，玩家与怪物共用 `BattleActorProfile` 的两个槽位，不属于 `BattleRulesProfile`。攻击者的资产决定攻击修正，实际受击目标的资产决定承伤修正。它们独立于计分、基础伤害构建和当前生命/格挡；本层尚不提供 Buff 调度或条件规则列表。

- 新建资产：`Assets/Create/玩法/角色/攻击修正（倍率与固定值）`、`承伤修正（倍率与固定值）`。拖到 PlayerBattleProfile / EnemyBattleProfile 的对应槽位。空槽位原值通过。
- 两类基础资产均配置版本、非负倍率和有符号固定增减值，计算 `floor(max(0, 输入伤害 × 倍率 + 固定值))`。例如输入 100、倍率 0.8、固定值 -5，输出 75；先应用攻击修正，再对其输出应用目标承伤，最后扣格挡。超限拒绝整次动作，不截断成上限。
- 单局玩家引用 `SingleBattlePlayerAttackModifier.asset` / `SingleBattlePlayerIncomingModifier.asset`，怪物引用 `SingleBattleEnemyAttackModifier.asset` / `SingleBattleEnemyIncomingModifier.asset`。四份资产互相独立，初始均为 1 倍、0 增减，不改变已有伤害。多人配置未绑定时保持原行为。
- `BuildRule()` 复制资产数值，规则不得持有 ScriptableObject 引用。`BuildSetup()` 将两类规则组成 `BattleActorDamageRules` 放入每个角色的开战快照。修改或替换资产作用于下一场；扣费、结束回合和恢复能量不会丢失角色规则。
- 扩展条件策略时派生 `AttackDamageModifierProfile` / `IncomingDamageModifierProfile`，返回纯 C# 的 `IAttackDamageModifier` / `IIncomingDamageModifier`。可读取现有 AttackContext 与 CombatState，不修改来源、目标、状态、随机数或触发表现。玩家出牌、怪物攻击及正式动作预览共用接线；候选牌型 GUI 仍只做计分，不调用伤害修正。

## 出牌伤害与只读验算

- `PlayerBattleProfile` →「出牌伤害配置」引用独立 `ScoredHandDamageProfile`。现有 SingleBattlePlayer / DemoPlayer 均迁移原 `DemoScoreDamage.asset` 引用，保持得分等额基础伤害；不同玩家可以绑定不同资产。入口 `Tools/Poker/战局/打开默认伤害配置` 打开单局玩家实际绑定的资产。新建玩家配置必须绑定伤害配置，缺失或工厂返回空规则时报错；公共规则与怪物配置不持有该槽位。
- `ScoredHandDamageProfile.BuildRule()` 在开战时构建纯 C#、不可变的 `IScoredHandDamageRule`，保存于该玩家的 `BattlePlayerRules.DamagePolicy`。预览与正式出牌按出牌者选取规则；怪物直接提供基础攻击，不调用玩家的出牌伤害规则。修改资产下一场生效，已有回合转换保留玩家规则快照。新增策略通过实现接口与派生配置资产接入；公共战局配置、Host 和牌区不维护转换参数。
- `ScoredHandDamageContext` 包含计分结果、攻击者、由目标策略验证后的目标列表、只读 CombatState 和正式出牌生成的 HandAttackInfo。`DamagePlan` 返回每个目标的 `DamageRequest` 及说明。当前协议要求每个已选目标按顺序恰好一条请求，不允许偷换来源/目标/规则版本；异常输出会原子拒绝本次出牌。
- 边界为：牌型计分 → 构建基础伤害 → 攻击修正 → 目标承伤修正 → 格挡与生命。卡牌/Joker 对筹码倍率的影响属于计分层，不再在伤害阶段重复计算。
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

`Tools/Poker/战局/出牌伤害验算` 为 Editor 专用中文只读窗口。在 Play 模式游戏中选择手牌，面板自动读取当前观察玩家，展示攻击来源、提交牌/牌型组成牌/实际计分牌、筹码倍率步骤、总分、基础伤害、攻击修正、目标承伤修正（含规则版本、说明、触发来源/原因、检查范围和关联牌）、格挡前伤害以及各目标格挡、实际掉血、溢出和死亡。多个场景宿主可通过「观察战局」手动选择。面板不负责选目标，使用与当前出牌相同的目标策略。

「当前选牌预计结果」通过 `Host.PreviewSelectedPlay → CardDuelEngine.PreviewPlay → Execute` 走完整正式出牌入口，包括合法性、计分、伤害、CombatEngine 和补牌；只保留返回值供显示，绝不将返回状态写入 Host，不触发 UI/表现/回合提交。反复读取不改变当前牌区、能量或随机状态。注入的攻击构建、攻击/承伤修正、选目标和发牌策略必须保持纯函数约定，不读取外部随机数或产生副作用。

「最近一次实际出牌」读取 Host 缓存的已提交 CardDuelTransition，包含当时的牌型、分数、DamagePlan 和 CombatEvent；不重新计算历史结果。重新开局清空记录。不可行动或演出中不展示过时预览。当前工具不提供独立造牌/修改生命/编辑配置或提交出牌按钮。

测试入口 `Tools/测试面板` →「攻击来源与触发依据」「出牌伤害与只读验算」，场景接入/配置快照测试在「角色与表现协议」。

## 回合与牌组

1. Create 时每位玩家分别创建标准无 Joker 的 52 张牌，拥有自己的抽牌、手牌、出牌、弃牌四区，起手为 0。Start 首次进入玩家阶段，为所有玩家按各自策略免费发牌。
2. 存活玩家共享行动阶段，可交错出牌/弃牌，每人独立扣费。能量为 0 自动标记该玩家结束；也可手动结束。初始能量为 0 时仍可手动空过，费用不足但非 0 时不强行结束。
3. 所有存活玩家均结束，才进入怪物阶段。存活怪物按阵容列表顺序行动，一次命令只结算一个怪物；表现结束才调用下一个。
4. 全部怪物完成后算一整轮。仍在战斗时，所有存活玩家恢复各自能量并按各自策略补牌。死亡玩家不再参与行动/等待条件，也不恢复能量或补牌。
5. 怪物全灭胜利、玩家全灭失败；达到回合上限在完整怪物阶段后判负。部分队友/怪物死亡不提前终止整场。

选择 1–5 张真实手牌，由 PokerHandClassifier / ScoreEngine 计算牌型，总分默认由 EqualScoreDamageRule 等额生成基础伤害，经过攻击修正后交给 CombatEngine 处理目标承伤、格挡与 HP。默认出牌延迟至下一玩家阶段补满，弃牌立即补满；每位玩家可单独配置。抽牌堆不足只洗回该玩家的弃牌堆。

本地 Demo 用一套 CardZone/UI 观察当前玩家，点击“切换玩家”或 Tab 切换。结束后自动转到仍可行动的队友。切换只重新绑定观察者牌区和命令接收者，不移动逻辑卡牌、不重开战局；牌库总览也仅包含当前玩家的完整构筑。表现/发牌过程中禁止切换与重复输入。

场景首次自动开局在 SingleBattleHost.Start 中执行，等待各牌区完成 OnEnable 后，先绑定空手牌，再提交 Start 发牌并播放入列动画；避免 Host 较早的 OnEnable 在 HandZoneView 尚未激活时跳过动画。逻辑手牌一次性提交，逐张飞入属于表现；重新启用已有对局仍恢复当前状态，不再次发开局牌。

手牌默认按点数理牌，开局发牌及后续补牌落位后自动执行；切换玩家也按当前选择的方式理牌。手动切换为花色理牌后，后续发牌沿用花色排序。排序只调整观察者的显示顺序，不改变权威牌区或洗牌状态。

这不是联网多人功能：目前共享阶段中的命令由同一权威按 Revision 串行结算，本地输入一次控制一名玩家。规则层没有固定的玩家行动游标。

## 数据与接口边界

- `CardDuelState` 拥有全部卡牌、每位玩家四区、弃牌预算及洗牌状态；保留类名以兼容原调用，已支持多对多。新代码使用 `Zone(kind, playerId)` / `DiscardsFor(playerId)`，不使用默认第一玩家的便捷别名。
- `TeamBattleState` 拥有共同 CombatState、各玩家能量与结束标记、怪物队列和战斗阶段。`TeamBattleEngine` 负责阵营回合、费用、伤害与胜负，`BattleRules` 只含公共规则。
- 角色根节点持有 PlayerBattleActor / EnemyBattleActor，通过 ParticipantId 查询最新快照；Model 子物体继续承载 VAT、DOTween、裂纹与音效。角色绑定要求在 Host 阵容中具有对应席位，不能冒用同队另一个角色的身份。
- `IBattleTargetPolicy.SelectTargets(BattleTargetContext)` 由行动来源角色的 BattleActorTargetRules.Attack 提供，接收源单位、当前状态、可选请求和存活对手集合，返回一个或多个目标。默认选第一个存活对手（或校验显式单目标）；引擎拒绝未知、死亡、友方和重复目标。接口支持多个伤害事件，但当前 Demo 不提供选目标或 AOE 操作 UI，也未定义范围/友方治疗规则。
- `IBattleAllyTargetPolicy.SelectTargets(BattleAllyTargetContext)` 预留独立友方目标接口，默认自己，也可请求存活队友；引擎验证请求与策略输出，拒绝敌方、死亡、未知和重复目标。由行动来源角色的 BattleActorTargetRules.Support 提供；Unity 入口在 PlayerBattleProfile 的友方目标策略槽位。弃牌通过独立 IDiscardRewardRule 生成收益计划，默认空计划；费用/次数和过牌仍由原有流程处理，不按牌型换取格挡。
- `PlayerBattleActor.CreateSetup(id, deck, initialHealth)` / `PlayerBattleProfile.BuildSetup` 可消费 Run 当前构筑和生命；缺省牌组为独立标准 52 张。Core 不依赖 ScriptableObject 或 Unity。
- `ICardDuelDealPolicy` 上下文含 PlayerId，只返回抽牌数量，不能移动或创建卡牌。每位玩家可独立注入策略。
- `ScoredDuelEngine` / 旧快照协议保留给原有独立 1v1 规则调用；新 Demo 使用 TeamBattleEngine。完整多人战局存档、Run 奖励写回、复活和增援尚未实现。

## 公共规则与后续边界

公共 BattleRulesProfile 仅保留版本、回合上限、玩家阵营格挡清除时机及共享牌型计分基础表。纯 C# BattleRules 仅持有前三项；计分表由 CardDuelEngine 独立持有。出牌伤害与弃牌收益归属 BattlePlayerRules，攻击/承伤修正归属双方角色的 BattleActorDamageRules，不再存在全局修正回退。旧 ScoredDuelDefinition 适配入口将自己的伤害转换显式写入默认玩家规则，旧 standalone 快照格式保持。

尚待后续拆分的边界：

- 共享计分基础表可以保留；个人牌型等级、Joker 和成长应来自玩家状态。当前正式计分固定 1 级与空 Joker，尚未接入个人成长。
- SingleBattleHud 同时处理信息显示、按钮命令、Tab 切换和世界空间意图对象的生命周期。未来可拆成信息展示、操作绑定和意图 Presenter；游戏数值仍由规则层提供。

## 角色目标配置

BattleActorProfile（玩家和敌人共有）新增“攻击目标策略”独立资产槽位；PlayerBattleProfile 另有“友方目标策略”槽位，用于弃牌收益等支援行动。开战时 BuildTargetRules 将配置转成纯 C# BattleActorTargetRules，随各自 Setup 输入并保留在玩家状态与敌人阵容中，扣费、结束回合与恢复能量不会丢失。修改资产在下次开战生效；空攻击槽默认为首个存活对手，空友方槽默认为自身或指定单个队友。

创建菜单 `玩法 > 角色 > 攻击目标策略` 提供首个存活、当前生命最低、当前生命最高、全部存活对手四种模式；生命比较使用当前绝对生命值，同值按阵容顺序确定，不引入随机数。“允许命令指定目标”默认开启以保留旧行为；开启时合法显式目标优先，单目标模式只接受一个目标；关闭时拒绝带显式目标的命令。每个敌人的 EnemyBattleProfile 可绑定不同策略资产，敌人意图预览和实际攻击都读取该敌人的同一份策略快照。

SingleBattle / Demo 的玩家、敌人分别绑定独立默认攻击策略资产，玩家分别绑定友方策略资产。若多个场景角色共享同一角色 Profile，它们仍共享作者配置；需要不同策略时绑定各自的角色 Profile。可继承 AttackTargetPolicyProfile / AllyTargetPolicyProfile 扩展确定性策略，BuildPolicy 不得返回读取可变 Unity 资产的运行时规则。

Host、CardDuelEngine、TeamBattleEngine 已移除全局目标策略参数和配置入口，不保留全局回退。纯 C# 调用改为 `new BattlePlayerSetup(..., targetRules: new BattleActorTargetRules(attack, support))` 或 `new BattleEnemySetup(..., targetRules: new BattleActorTargetRules(attack))`。TeamBattleEngine 仅在目标选择边界分派来源角色策略并做公共合法性校验：来源必须存活且属于当前战局，请求及输出须为合法阵营的存活单位、无重复且数量有效；非法请求不能被忽略它的策略绕过。策略失败会拒绝整个命令，预览显示不可用。

## 敌人行动预览

`TeamBattleEngine.PreviewEnemyAction(state, enemy)` 是只读规则接口；玩家阶段假设所有尚未结束的玩家立即结束回合，随后按剩余敌人队列预演至指定敌人。所有阶段转换、目标选择、攻击修正、承伤修正、格挡与生命结算均复用正式 `Execute`。前序敌人消耗格挡或击倒目标会影响后续预览；实际状态、随机数与牌区不被改写。注入策略必须是确定性纯函数，状态变化后重新计算。

返回 `EnemyActionPreview`，包含来源、原始规则 revision、错误及逐目标 `Hits`（正式 CombatEvent）；其中 `SettledDamage` 是两层修正后的格挡前伤害，`BlockAbsorbed` / `HealthLost` / `Overkill` 分别是格挡吸收、预计扣血和溢出伤害。没有安排本阶段行动、已经行动、死亡或终局返回 null；前序行动或当前行动失败则返回错误，不回退基础攻击值。

`EnemyBattleActor.ReadIntent()` 仅包装该预览及 CardDuel 命令 revision。世界空间标签显示 `攻击 N`，N 取每个目标的 SettledDamage；多人时标明玩家序号，每个目标独立一行。面板高度及运行时 Gizmo 随行数调整，预览失败显示“意图不可用”。该数值是攻击伤害，格挡后的实际扣血可从同一预览事件读取。每次 StateChanged 刷新，不冻结玩家后续行动之前的旧预测。

## 战局信息层

左侧新增候选牌型面板，根据当前观察玩家选中的手牌显示牌型、选中/计分牌数、整数筹码 × 整数倍率。候选使用 CardDuelEngine.PreviewHand，与正式 Play 共用判型计分；不进入费用、伤害或目标策略，未选牌显示零值。两个数字各自使用白色矩形背景，居中显示整数，不显示结果行。选择、取消和观察玩家变化后更新数字目标。

数字显示由独立 UIFoundation.Runtime 的 RollingNumberText 完成，仅接收 decimal 数字；增减均从当前显示值追向新 target，相同 target 不重启，最终值精确落位。候选面板是唯一游戏数据适配层。

战局 HUD 是只读信息层：小尺寸信息栏位于屏幕右侧，展示生命、格挡、能量与动作费用、回合、牌堆数量及最近得分/伤害；不显示操作教学或内部计算流程。结束回合、重开及多人切换保留为紧凑按钮，胜负信息也显示在右侧。

每个存活怪物的攻击意图使用独立 World Space Canvas，统一放在 HUD 运行时创建的场景根节点 `WorldUIRoot` 下，不继承怪物或 HUD 的缩放与旋转。LateUpdate 跟随模型底部锚点，朝向统一与摄像机画面平行，透视模式下也不会因偏离画面中心而倾斜。标签采用固定世界尺寸，保留近大远小，死亡/终局隐藏；HUD 的“敌人意图（世界空间）”可调整世界单位/像素与世界偏移。HUD 销毁时清理其创建的 WorldUIRoot 与标签。Scene 视图开启 Gizmos 可见黄色面板范围、青色模型底部与偏移连线、蓝色朝向箭头；Edit 模式也能预览，使用与实际 Canvas 相同的世界尺寸和摄像机朝向，不叠加怪物缩放。意图来自 EnemyBattleActor 的只读结果，标签不持有战局命令权限。`Tools > Poker > 战局 > 整理信息层 HUD` 可仅更新当前场景的 HUD 布局，保留阵容、模型、牌区和相机配置；新建 Demo 使用相同布局。

## 可等待的表现协议

逻辑角色绑定任意实现 `IBattleActorPresentation` 的子物体组件，宿主通过角色调用协议，不依赖具体 Presenter 类型、动画名称或持续时间。协议只接收已结算的只读 `BattlePresentationCue`（PrepareAttack / Attack / Hit / Death / GainBlock / BlockHit / ClearBlock、ActorId、可选 CombatEvent 和 ShieldSnapshot）；表现不获得战局命令权限。

| 调用 | 协议要求 |
| --- | --- |
| `Play(cue)` | 返回 `BattlePresentationOperation`；实际动作结束时调用 `Complete()`，中断/错误时调用 `Cancel()` / `Fail(error)`。 |
| `Synchronize(unit)` | 同步投影只读单位快照，例如生命分档裂纹；不播放动作、不修改生命、不阻塞流程。 |
| `Snap(cue)` | 同步显示最终姿态，用于不播放动画的执行或跳过；不能保留异步工作。 |
| `CancelPresentation()` | 停止旧协程/动画/回调；旧句柄不得再影响新绑定。 |
| `ResetPresentation()` | 取消旧表现并恢复初始姿态，用于新战局、重开和禁用。 |

操作句柄在 Unity 主线程使用，仅允许一次终态（Completed / Cancelled / Failed），迟到或重复完成返回 false。宿主等待句柄完成后才进入下一表现/流程检查点；不再用固定 scoreHoldSeconds / enemyTellSeconds 推测动作时长。PrepareAttack 的停留也由 Presenter 决定。双方可独立替换成 Animator、Timeline 或其他适配器；当前只提供基础变换和 VAT 实现。

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

当前播放顺序为：卡牌落位 → 攻击完成 → 受击完成 → 必要时死亡完成 → 收牌或敌人预备 → 敌人攻击/受击/死亡完成 → 补牌落位 → 释放输入/显示终局。每个攻击者只播放一次攻击，随后按事件等待各目标的受击与死亡；怪物之间也逐个等待。没有命中时刻标记、并发群体演出或镜头编排。观察事件不承担等待职责，所有阻塞工作必须通过协议句柄完成。

**等待发生在 Unity 战局流程层。**纯规则引擎仍同步、确定性地产生并提交结果；已提交的生命/能量状态不因动画耗时而改变。表现期间拒绝新玩家命令，宿主延后敌人调度、收牌/补牌表现、输入释放与终局展示。若操作失败/取消，宿主记录异常并跳过剩余演出，同步到当前权威状态，不重复结算已提交伤害。显式 `SnapToState` 取消当前操作并完成待推进流程；禁用宿主时保留尚未提交的敌人阶段，恢复后继续。不会用任意超时提前推进，协议实现必须报告完成或失败。

`CardDuelEngine.Create` 可传入每位玩家的 `BattlePlayerSetup`（包含当前 `DeckEntry` 列表），保留 OriginId 并按当前构筑数量实例化，例如 53/51 张；当前 Demo 启动入口使用标准 52 张。这里支持标准牌面与卡牌效果标记，尚未接入 RunCard 的完整增强、Joker、能力选择或 Run 宿主。

组合状态目前只在内存中运行。**不要单独保存 ScoredDuelSnapshot 来恢复这个 Demo**：它不包含四牌区、洗牌状态和弃牌次数。完整 CardDuel 快照、跨节点生命/构筑写回、Run 结果账本与奖励结算是下一层工作。当前没有磁盘存档或中途续局按钮。

暂不提供完整敌人 AI、状态效果调度、正式角色动画和数值平衡。场景默认不改 Build Settings，直接打开后 Play 即可；未进行完整 Player/IL2CPP 构建。

## 验证入口

`Tools > 测试面板 > 编辑模式测试` 中，“多人战局与阵营回合”验证独立牌组/独立角色数值、共享阶段、怪物队列、死亡跳过、整队胜负、错误目标和多目标接口；“角色与表现协议”验证 UI 权限切换、配置快照、真实 Actor 接入以及逐单位等待表现完成。仅运行专项，不自动执行全部规则测试。

原“实战验证 > 单战局完整流程 / 单战局发牌专项”限定 SingleBattleDemo 的 1v1，临时克隆玩家/怪物配置完成验证，恢复原配置引用，避免修改共享资产。“实战验证 > 多人战局 2v3”通过实际 HUD 回调与动画验证切换、全员结束、三个怪物依次演出及整轮恢复，结果写入 `Library/SingleBattleTests/team-play.json`。

场景按 Battle / Card Table / Environment / Cameras / UI 五组组织。TeamBattleDemo 的两只 Fox 与三只 Dragon 复用既有美术与独立表现组件；音效槽位仍为空。场景不自动加入 Build Settings。

## 卡牌美术

单牌 CardView 与 Canvas 总览共用 `Assets/CardArt/Profiles/StandardCardArt.asset`；CardMatchHost 使用 CardStateArtBindings 将玩法状态映射为参数组合。Zone 仅管理投影/布局/动画。切换玩家仍沿用同一套卡牌美术，Run DeckEntry 的效果与禁用随卡牌保留。美术入口为 `Tools > 卡牌美术 > Shader 参数组合`；状态映射入口为 `Tools > 玩法配置 > 卡牌状态美术映射`。

## 出牌表现入口

当前 SingleBattle 使用 `ICardDirectPlayCommands` 提交出牌，不依赖 `PlayedZoneView`、出牌区 `CardZonePlayHost` 或 `CardZoneTransferPresenter`。按钮通过 `CardZonePlayHost.PlaySelected(animate)` 调用玩家权限与战斗结算；被打出的卡牌从手牌视图移除，剩余手牌仍使用现有理牌动画，暂不添加替代的出牌动画。计分、伤害、能量、补牌与规则层 Played → Discard 的收集流程保留。

当前 `SingleBattleDemoNew` 已移除旧出牌区对象与飞行组件。其他旧单战场景可用 `Tools > Poker > Single Battle > Remove Played Zone Presentation` 清理并保存；操作支持 Undo。新配置流程也会清理这些旧绑定。旧 DrawBattle / 卡牌跨区示例仍使用原有通用转移组件，不影响当前战斗入口。新出牌表现可在战斗结算后的表现阶段接入，不必重新引入目标牌区。


## 弃牌盾牌表现

`BattleActorPresentationCoordinator` 是 Actor 的总表现入口，组合原有模型 Presenter 与 `DiscardShieldPresenter`。`GainBlock` 插入本次弃掉的牌；`BlockHit` 播放盾牌受击和整张溶解；`ClearBlock` 清空盾牌。完全格挡只播放盾牌表现；穿透格挡造成生命损失时，盾牌与角色受击同时执行，两个操作都完成后才释放流程。死亡也清空容器。规则先结算，表现只消费只读结果，不修改格挡或伤害。

每次弃牌记录独立批次：本次牌列表、实际获得格挡、剩余格挡。批次按获得顺序消耗，初始格挡和其他来源格挡占用无卡牌的账目，避免误消耗弃牌盾牌。每批累计溶解张数为 `ceil((初始格挡 - 剩余格挡) × 张数 / 初始格挡)`；例如三张牌提供 24 格挡，累计消耗 1–8 点溶解第一张，9–16 点溶解第二张，17–24 点溶解第三张。已选中的牌播放完整溶解，其他牌保持完整，不停留在半溶解状态；因此最后少量格挡尚在时，牌也可能已经全部溶解。战局重开重置账目，跳过或恢复直接同步最终状态，不重播音效与震动。

当前 `SingleBattleDemoNew` 已接好。选中 `PlayerBattleActor` 下的 `Discard Shields`：

- 青色 `SpawnCenter / SpawnSize` 是卡牌实例化和飞入起点区域。
- 黄色 `RingCenter / RingInnerRadius / RingOuterRadius` 是围绕 Actor 的圆弧落地区域，以卡牌底部为锚点，可在 Scene 中直接移动圆心和拖动内外半径。`LandingHeightVariation` 控制底部高度随机范围。`ArcAngle` 控制圆弧角度（默认 60°），`ArcCenterAngle` 控制朝向（默认 0°，角色前方）。位置按圆弧区域面积随机分布，牌面随机朝内或朝外，并叠加独立俯仰、偏转和倾斜角度。
- `CardHeight` 是唯一的卡牌尺寸参数（容器局部单位），宽度自动按图集原始牌面比例计算，Inspector 显示最终宽高。`PitchRange / YawRange / RollRange` 分别控制俯仰、朝向偏移、左右倾斜的最小/最大角度，允许非对称范围。`OutwardChance` 为朝外概率（0 全朝内、1 全朝外），`Seed` 控制随机结果；同一批次恢复时姿态保持一致。旧宽高及角度配置自动迁移，保留高度和原角度范围。
- DOTween 使用不受 Time.timeScale 影响的时间：`FlightSeconds / CardInterval / ArcHeight` 控制逐张飞入，`DissolveSeconds / DissolveInterval` 控制逐张溶解。
- 每张落地触发材质闪光、生成于卡牌底边中心的可选 `LandingEffect` 粒子（位置包含卡牌旋转与缩放）、`LandingSound` 与设备震动。当前落地音效复用手牌弃牌音效；粒子和 `BlockHitSound` 默认空，可自行配置。严格在实际播放非空且音量大于零的音效时请求一次震动；静默落地、Snap 和取消不会触发震动。手机与手柄能力沿用 `CardHapticsDriver`。

盾牌容器是 Actor 根节点的子节点，配置时采用 Visual 的位置与朝向，保持独立于模型攻击前冲和受击晃动。手牌弃牌时直接移除原牌，随后由盾牌容器创建独立实例，避免旧飞离动画与弃牌音效重复执行。没有盾牌表现入口的旧场景仍保留原弃牌路径。

渲染使用共享 Mesh、共享图集材质和 MaterialPropertyBlock 的实例属性，不创建逐牌材质。`CardArt/Shield Deferred` 提供 URP GBuffer、Forward、ShadowCaster、DepthOnly、DepthNormals Pass；牌面无原卡牌附加视觉效果。Perlin 噪声叠加牌面局部高度渐变，自上向下 alpha clip，两个 step 构成自发光边缘；深度和阴影使用相同裁剪。落地闪光为默认特效，外部粒子可选。

旧单战场景可运行 `Tools > Poker > Single Battle > Configure Discard Shields` 建立绑定和共享资产；支持重复执行。Inspector 提供三张牌的临时预览及清除按钮，预览实例不保存到场景。

专项验证：`DiscardShieldTests` 检查取整、FIFO、不可变快照、共享资源及实际 shader 渲染；`DiscardShieldPlaybackTests` 在 PlayMode 验证错峰落地、暂停时间、整张溶解和取消回调；`BattleActorTests` 覆盖真实弃牌奖励与协议路由。设备实际触感仍需在目标手机或手柄上调校。
