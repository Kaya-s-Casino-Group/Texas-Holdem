# Combat：战斗单位与公共结算

`PokerEngine.Combat` 是纯 C# 模块，只引用 Core、Abilities。玩家和怪物共用 `CombatantState`；当前不建立 Player/Monster 继承树。区别来自模板、阵营和上层控制方式。

## 所有权

| 对象 | 持有 / 负责 | 不负责 |
| --- | --- | --- |
| CombatantDefinition | 模板 ID、最大生命、初始格挡 | AI、手牌、奖励 |
| CombatantState | 实例 ID、模板 ID、阵营、当前/最大生命、格挡 | 出牌、回合推进、Run 成长 |
| CombatState | 本场 ID、Revision、全部单位、集中保存的 StatusInstance | 牌组、胜负、状态效果执行 |
| CombatEngine | 伤害、治疗、获得/清除格挡、施加状态、直接消灭，原子批量结算和事件 | 选目标、费用、分数换算、自动反击 |
| ScoredDuelEngine（Composition） | 最小单玩家/单怪物流程、牌型分数换伤害、玩家能量/结束回合、固定攻击、胜负投影 | 卡牌合法性、洗牌/抽牌、完整 Run、Unity 表现 |

生命值直接存于 `CombatantState.Health`，类型为必填 `int`，范围 0..MaxHealth；`IsAlive` 从生命值派生。单位不再持有 ResourceState，也不携带筹码或能量。不要同时持有另一份可变 AbilityWorld 作为生命真相；未来接能力执行器时，需要由模式投影并原子回写同一权威状态。

ScoredDuelState 组合持有独立的 PlayerTurnState（MaxEnergy / Energy），定义配置每回合能量和出牌消耗，费用与恢复时机由玩法控制；怪物行动由其行为规则决定，无需创建能量字段。既有 ResourceState 保留给 Draw/Holdem 及其能力执行流程，后续按具体玩法需要独立迁移。

状态效果复用 `StatusInstance`，集中放在 CombatState，检查唯一 ID、持续时间与归属。存储只允许指向本场或本场单位；`CombatEffect.ApplyStatus(status)` 要求来源和目标都是对应的单位。一个实例 ID 只能存在一次，最多 256 个实例。`Resolve(..., statusCatalog: catalog)` 可启用不可变的 `CombatStatusCatalog`；显式目录下未知定义拒绝。未传目录时保留旧被动模式：任意合法定义 ID、同定义不同实例独立保存，不执行行为或重施合并。最小 ScoredDuel 仍明确拒绝非空状态集合。

## 可配置状态行为与生命周期

`CombatStatusDefinition` 以任意 `StatusDefinitionId` 声明 Tags、Behaviors、DefaultLifetime 和 ReapplyPolicy，不依据某个 ID 硬编码眩晕或免疫。当前只支持两种强类型行为：`BlockActionWindowSpec` 阻止行动机会；`PreventStatusApplicationSpec(StatusTags.Control)` 阻止带控制标签的状态施加。非法标签、未知行为类型、重复行为、非法重施策略均在构造时拒绝。次数期限不能同时驱动两种消费行为，定义和实际载荷均校验此约束。

施加状态时读取批次内最新状态；前一条施加的免疫可以拦截后一条控制。拦截是正常结算，不回滚同批伤害，也不移除已存在的控制。若多个免疫实例可用，按状态保存顺序选第一个实际拦截者；仅这个实例的次数型期限消耗一次，阶段型或永久免疫保持不变。已死亡目标仍在行为判定前跳过，不消耗免疫。

默认 `KeepLonger` 对同目标同定义保留原实例 ID，来源归因更新为最新成功施加者，Stacks 与剩余次数各取较大值，不相加。阶段／轮次期限取更晚的截止边界；必须是相同期限类型，阶段型还必须有相同 PhaseKey，否则原子拒绝。比较跨轮次期限使用 Composition 绑定的 `combat.Id + "/round/" + (CompletedRounds + 1).ToString("D10", InvariantCulture)` 一基稳定 scope；相同 scope 无需比较轮次。`IndependentInstances` 则允许同定义多个不同 ID，仍拒绝 ID 冲突。被拦截的施加不占容量，刷新现有实例也不额外占容量。

`StatusLifetimeSpec` 是与玩法无关的相对声明：UntilRemoved、ForTriggers、ThroughNextPhase(phaseKey)、ThroughCurrentRound。Combat 不猜测回合或阶段；Composition 在施加前绑定为 `StatusDuration`。`CombatStatusLifecycle.Blockers` 只读查询当前单位的阻止行动状态，死亡单位返回空；只有真正跳过存活单位的行动机会时才调用 `SkipActionWindow`，统一消费所有命中的次数型阻止实例一次，非次数型保留。无阻止状态时跳过请求拒绝，防止凭空产生跳过事实；旧目录为空的被动模式不阻止行动。

`EndPhase(state, revision, scopeId, phaseKey)` 精确移除匹配的 UntilPhaseEnd；`EndRound(state, revision, scopeId)` 只移除该 scope 的 UntilRoundEnd，不顺手清理任何阶段型状态。维护不要求状态来源／目标存活。无状态变化时返回原 CombatState 且不增加 Revision；实际跳过或到期原子推进一次 Revision。玩家阶段、敌人队列和回合推进仍由 Composition 控制，不创建第二份 AbilityWorld，也不直接调用其生命周期修改另一份状态。

## 结算契约

- `CombatEffect` 是上层完成合法性判断后的效果，不是玩家提交的命令。模式负责费用、阵营、目标和数值来源。
- Damage 先扣格挡再扣生命；生命最低为 0，事件单独报告吸收、实际生命损失、溢出伤害和死亡。
- Heal 最高恢复到 MaxHealth，不支持复活。来源和目标必须是本场已知单位，来源必须仍存活；结算时目标已死亡则跳过该条效果，继续处理后续效果，不重新选目标。
- 格挡不会自动在回合边界消失；模式需要时显式提交 ClearBlock。最小示例中格挡持续至消耗完毕。
- ApplyStatus 的强类型 `Status` 载荷必须与效果来源、目标一致；Amount 为 0，不携带攻击上下文或攻击修正。新状态对同批后续承伤修正立即可见；实际新增／刷新时检查实例 ID 与容量，ID 冲突、容量或载荷错误回滚整批。KeepLonger 重施可以引用其原实例 ID。已死亡目标的状态效果不占容量、不预留 ID，Composition 不提前按计划中的状态数量拒绝。
- `CombatEffect.Eliminate(source, target)` 直接将生命归零，不消耗格挡、不移除状态、不调用承伤修正；Amount 为 0，也不携带攻击上下文。它不代表巨额伤害；和伤害致死一样，后续对该死亡目标的任意效果都会跳过。
- 每批 1–64 个效果，共用一次 Revision；未知单位、死亡来源、溢出或任一步真正失败仍回滚整批，不发事件。来源和身份校验先于死亡目标跳过，不能借跳过掩盖错误。非空批次即使全部跳过也接受并推进一次 Revision；旧 Revision 拒绝，输入状态不修改。

`CombatTransition.RuleEvents` 是有序的实际结算事实：`CombatEvent` 记录生命／格挡变化，`CombatStatusEvent` 记录新增；`CombatStatusBlockedEvent` 记录拦截者，`CombatStatusChangedEvent` 记录刷新、消费或到期的前后状态及原因，`CombatActionWindowSkippedEvent` 记录实际跳过的单位和阻止实例。死亡目标跳过不产生事件，原 EffectPlan 保留完整声明；旧 `Events` 仍只投影 CombatEvent，不伪造零伤害。`Index` 是原效果位置，可有缺口，同一效果的多个事实共享 Index；生命周期事件 Index 为 -1、Effect 为 null。唯一事实标识使用 Revision + SequenceIndex，SequenceIndex 在同一次 Combat 提交中从 0 连续递增。Composition 全程透传 RuleEvents，阶段维护可另有独立 Combat Revision。

`CombatEvent.HealthLost` 保留实际生命差；伤害触发应使用 `DamageToHealth` 或检查 Damage 类型。Eliminate 的 DamageToHealth、SettledDamage、BlockAbsorbed 和 Overkill 均为 0，死亡原因是 `CombatDeathCause.Eliminate`；伤害致死为 Damage，未死亡为 None。默认牌型／弃牌规则尚不产出新操作；Composition 可通过可选的 IScoredHandEffectRule 声明五种基础操作及逐效果目标。正式启用消灭时，表现中把 HealthLost 直接当伤害的投影需要相应适配。
- 公共层不判胜负，支持多个阵营/单位；一对一限制只在 ScoredDuel 示例中。

## 独立伤害修正规则

`IAttackDamageModifier` / `IIncomingDamageModifier` 是纯函数协议；`ScaledAttackDamageModifier` / `ScaledIncomingDamageModifier` 保存不可变倍率、固定增减值和版本，按 `floor(max(0, 输入 × 倍率 + 固定值))` 生成 DamageModification。默认 1 倍、0 增减，负倍率拒绝；超出 decimal/long 范围按原子结算的 Overflow 路径拒绝，不写状态。

角色归属和规则分派由 Composition 的 BattleActorDamageRules 与 TeamBattleEngine 管理：按来源选择攻击修正、按目标选择承伤修正。CombatEngine 不持有 Unity 资产，不在 CombatantState / CombatState 的存档字段中存入策略对象；承伤接口仍在每条效果扣格挡前调用，读取该批次前序效果后的上下文。

## 最小可运行的牌型伤害流程

入口位于 `Composition/Encounters/ScoredDuel*.cs`。这套流程验证公共基建，不替换现有 Draw 出牌计分模式。

```csharp
var engine = new ScoredDuelEngine(new ScoredDuelDefinition(
    "duel.example.1",
    new CombatantDefinition("hero", 60),
    new CombatantDefinition("slime", 25),
    new ScoreDamagePolicy(pointsPerDamage: 10), enemyDamage: 6, roundLimit: 8));
var state = engine.Create("encounter-1", new ParticipantId("player"), new ParticipantId("slime-1"));

// authoritativeHost 已验证实际提交牌组，并调用既有分类和 ScoreEngine：
var score = ScoreEngine.EvaluatePattern(engine.NextScoreId(state), scoreProfile,
    classification, handLevel, submittedCards, orderedJokers);
var hand = engine.ResolveScoredHand(state, state.Revision, score);
if (hand.Accepted) state = hand.State;
// 宿主消费 hand.Events 播放动画；能量尚有余额可继续出牌。
// 收到玩家“结束回合”命令时（也可以零次出牌）：
if (state.Phase == ScoredDuelPhase.AwaitingPlayerHand && playerPressedEndTurn)
    state = engine.EndPlayerTurn(state, state.Revision).State;
// 自动或手动结束回合后，宿主安排一次怪物行动：
if (state.Phase == ScoredDuelPhase.AwaitingEnemyAction)
    state = engine.ResolveEnemyAction(state, state.Revision).State;
var progress = engine.ReadProgress(state);
```

分数换伤害暂用 `floor(Total / PointsPerDamage)`，可通过配置调整；它不改变 ScoreResult，也不把分数写入筹码。玩家可以同回合多次行动；出牌与弃牌分别消耗 PlayEnergyCost / DiscardEnergyCost，默认各 1，耗尽到 0 时自动转敌人阶段。某项动作不可支付时保留剩余能量，可改做另一项或手动结束。EndPlayerTurn 可在零次出牌时主动结束，剩余能量不跨回合保留。MaxEnergy 与 InitialEnergy 分开设置，当前能量可以高于上限；敌人行动后存活且战局继续则恢复到 MaxEnergy。怪物被击杀立即胜利，不再反击；玩家死亡失败；最后允许回合中玩家先行动、存活怪物反击，双方仍存活则按回合预算失败。CompletedRounds 在怪物行动或玩家击杀后递增，独立于 Scores.Count。每个出牌、付费弃牌、主动结束或怪物行动均写入 Actions；弃牌/手动结束不改变 Combat.Revision，但改变战局自己的 Revision。

此入口只接受宿主权威计分结果。ScoreEngine 的算术校验和顺序 ScoreId 能防止不一致/重复结算，无法证明玩家持有或合法打出了这些牌。客户端应提交牌 ID，由拥有牌区的宿主完成验证和计分，不能直接提交任意 ScoreResult。

## 存档与结果

`ScoredDuelSnapshotCodec` schema v4 保存规则指纹、初始玩家生命、单位状态、玩家能量、完成回合数、阶段、计分与行动账本；反序列化按冻结配置重放行动账本，核对生命/格挡/能量/回合数/阶段/Revision，支持在等待怪物行动时恢复。v4 新增付费弃牌账本、独立最大/初始能量与两类消耗的规则指纹，允许合法的超过上限能量，但仍拒绝伪造能量；规则版本为 combat.5 / scored-duel.4，v1–v3 旧档明确拒绝。combat.3 新增状态施加、消灭和类型化规则事件；combat.4 将已死亡目标改为跳过；combat.5 新增可配置状态行为、重施、消费、精确到期和事件 SequenceIndex。CombatState 存储形状未变；指纹包含 Combat 版本，因此旧 combat.2 / combat.3 / combat.4 快照也明确拒绝。规则或数值变化拒绝旧档；暂不提供迁移或磁盘 I/O。该快照仍不包含 CardDuel 的四牌区和随机状态。

`EncounterContext`、`EncounterProgress`、`EncounterResult` 由状态即时投影，不重复存储。生命战的通用参与者描述只包含槽位/阵营，InitialResources 为 null；生命模板与初始生命由 ScoredDuelDefinition / ScoredDuelState 持有。结果包含双方胜负、原因、回合数和最终 Revision；奖励与幂等写回由 Run/宿主管理。

## 场景 Demo

Composition 已新增 CardDuelEngine，把 CardTableState 四牌区与 ScoredDuelState 原子组合；Unity 宿主在独立的 Assets/Scripts/Gameplay/SingleBattle 中。场景角色使用抽象 BattleActor 以及派生的 PlayerBattleActor / EnemyBattleActor，通过 ParticipantId 读取宿主的当前单位，不另存生命值；玩家输入与怪物意图/行为各自独立。打开 SingleBattleDemo.unity 可以玩完一场战斗，详见 [Demo 结构](../../../../Gameplay/SingleBattle/README.md)。原 DrawBattleHost 仍为分数目标模式。CardDuel 的组合快照与完整 Run 接入尚未实现。

## 下一层接入边界

- Run 持有当前构筑；每场战斗实例化当前 Run.Deck（初始 52 张，随 Run 增减），卡牌 ID/OriginId 与四牌区继续沿用已有实现。CombatantState 不拥有牌组。
- 当前 DrawEngine/DrawBattleHost 仍是分数目标模式。接生命战时，由战局协调器统一拥有卡牌流程与 CombatState，复用现有 CardZone/View，不能简单在 Draw 分数胜利之后额外扣血。
- 当前 RunEngine/RunValidator 的胜负与历史节点校验都依赖 TargetScore。接入生命战需要显式的战局结果账本及对应快照升级，不能伪造分数来驱动胜利。本轮 Run v4、Draw v2、Match v5 保持现有行为。
- 玩家 Run 生命可以在创建战局时显式传入 InitialPlayerHealth；结果提交后的跨节点生命写回尚未接入。完整 AI、更多状态行为和角色 UI 留待后续。

回归覆盖公共伤害结算、批量回滚、死亡、生命单一所有权、单位不携带筹码/能量、真实牌型计分、能量消耗/恢复、主动/自动/零出牌结束回合、双方终局、重复提交、行动账本恢复和不一致快照拒绝。

CardDuelEngine.Create 只创建独立牌组与四区（Hand/Played/Discard 为 0），不发牌。首次 CardDuelAction.Start 免费补满 HandSize，HasStarted 置真、Table.Revision 加一，不改变 Duel、能量、动作账本、完成回合或弃牌次数；首回合前拒绝其他命令，重复 Start 拒绝。Unity 在空区绑定后执行 Start，以便开局发牌复用正常展示流程。

发牌策略以 ICardDuelDealPolicy.GetDrawCount(CardDuelDealContext) 注入 CardDuelEngine。四种触发点共用引擎的安全移牌/洗回路径，返回数量必须在 0..MissingCards；策略不修改牌区。CardDuelDealPolicy 允许出牌与弃牌分别选择 AfterAction / NextPlayerTurn，默认出牌延后、弃牌立刻补满。终局不再触发补牌；修改策略后需要创建新战局，CardDuel 完整存档仍未实现。
