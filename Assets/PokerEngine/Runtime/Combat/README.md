# Combat：战斗单位与公共结算

`PokerEngine.Combat` 是纯 C# 模块，只引用 Core、Abilities。玩家和怪物共用 `CombatantState`；当前不建立 Player/Monster 继承树。区别来自模板、阵营和上层控制方式。

## 所有权

| 对象 | 持有 / 负责 | 不负责 |
| --- | --- | --- |
| CombatantDefinition | 模板 ID、最大生命、初始格挡 | AI、手牌、奖励 |
| CombatantState | 实例 ID、模板 ID、阵营、当前/最大生命、格挡 | 出牌、回合推进、Run 成长 |
| CombatState | 本场 ID、Revision、全部单位、集中保存的 StatusInstance | 牌组、胜负、状态效果执行 |
| CombatEngine | 伤害、治疗、获得/清除格挡，原子批量结算和事件 | 选目标、费用、分数换算、自动反击 |
| ScoredDuelEngine（Composition） | 最小单玩家/单怪物流程、牌型分数换伤害、玩家能量/结束回合、固定攻击、胜负投影 | 卡牌合法性、洗牌/抽牌、完整 Run、Unity 表现 |

生命值直接存于 `CombatantState.Health`，类型为必填 `int`，范围 0..MaxHealth；`IsAlive` 从生命值派生。单位不再持有 ResourceState，也不携带筹码或能量。不要同时持有另一份可变 AbilityWorld 作为生命真相；未来接能力执行器时，需要由模式投影并原子回写同一权威状态。

ScoredDuelState 组合持有独立的 PlayerTurnState（MaxEnergy / Energy），定义配置每回合能量和出牌消耗，费用与恢复时机由玩法控制；怪物行动由其行为规则决定，无需创建能量字段。既有 ResourceState 保留给 Draw/Holdem 及其能力执行流程，后续按具体玩法需要独立迁移。

状态效果复用 `StatusInstance`，集中放在 CombatState，检查唯一 ID、持续时间与归属。当前只允许指向本场或本场单位；CombatEngine 保留状态但不执行触发、过期或伤害修正。最小 ScoredDuel 明确拒绝非空状态集合，避免把尚未实现的中毒等效果当成已生效。

## 结算契约

- `CombatEffect` 是上层完成合法性判断后的效果，不是玩家提交的命令。模式负责费用、阵营、目标和数值来源。
- Damage 先扣格挡再扣生命；生命最低为 0，事件单独报告吸收、实际生命损失、溢出伤害和死亡。
- Heal 最高恢复到 MaxHealth，不支持复活。来源与目标都必须是本场存活单位；死亡后的效果拒绝。
- 格挡不会自动在回合边界消失；模式需要时显式提交 ClearBlock。最小示例中格挡持续至消耗完毕。
- 每批 1–64 个效果，共用一次 Revision；未知单位、死亡单位、溢出或任一步失败都回滚整批，不发事件。旧 Revision 拒绝，输入状态不修改。
- 公共层不判胜负，支持多个阵营/单位；一对一限制只在 ScoredDuel 示例中。

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

`ScoredDuelSnapshotCodec` schema v4 保存规则指纹、初始玩家生命、单位状态、玩家能量、完成回合数、阶段、计分与行动账本；反序列化按冻结配置重放行动账本，核对生命/格挡/能量/回合数/阶段/Revision，支持在等待怪物行动时恢复。v4 新增付费弃牌账本、独立最大/初始能量与两类消耗的规则指纹，允许合法的超过上限能量，但仍拒绝伪造能量；规则版本为 combat.2 / scored-duel.4，v1–v3 旧档明确拒绝。规则或数值变化拒绝旧档；暂不提供迁移或磁盘 I/O。该快照仍不包含 CardDuel 的四牌区和随机状态。

`EncounterContext`、`EncounterProgress`、`EncounterResult` 由状态即时投影，不重复存储。生命战的通用参与者描述只包含槽位/阵营，InitialResources 为 null；生命模板与初始生命由 ScoredDuelDefinition / ScoredDuelState 持有。结果包含双方胜负、原因、回合数和最终 Revision；奖励与幂等写回由 Run/宿主管理。

## 场景 Demo

Composition 已新增 CardDuelEngine，把 CardTableState 四牌区与 ScoredDuelState 原子组合；Unity 宿主在独立的 Assets/SingleBattle 中。场景角色使用抽象 BattleActor 以及派生的 PlayerBattleActor / EnemyBattleActor，通过 ParticipantId 读取宿主的当前单位，不另存生命值；玩家输入与怪物意图/行为各自独立。打开 SingleBattleDemo.unity 可以玩完一场战斗，详见 [Demo 结构](../../../SingleBattle/README.md)。原 DrawBattleHost 仍为分数目标模式。CardDuel 的组合快照与完整 Run 接入尚未实现。

## 下一层接入边界

- Run 持有当前构筑；每场战斗实例化当前 Run.Deck（初始 52 张，随 Run 增减），卡牌 ID/OriginId 与四牌区继续沿用已有实现。CombatantState 不拥有牌组。
- 当前 DrawEngine/DrawBattleHost 仍是分数目标模式。接生命战时，由战局协调器统一拥有卡牌流程与 CombatState，复用现有 CardZone/View，不能简单在 Draw 分数胜利之后额外扣血。
- 当前 RunEngine/RunValidator 的胜负与历史节点校验都依赖 TargetScore。接入生命战需要显式的战局结果账本及对应快照升级，不能伪造分数来驱动胜利。本轮 Run v4、Draw v2、Match v5 保持现有行为。
- 玩家 Run 生命可以在创建战局时显式传入 InitialPlayerHealth；结果提交后的跨节点生命写回尚未接入。完整 AI、状态效果调度和角色 UI 留待后续。

回归覆盖公共伤害结算、批量回滚、死亡、生命单一所有权、单位不携带筹码/能量、真实牌型计分、能量消耗/恢复、主动/自动/零出牌结束回合、双方终局、重复提交、行动账本恢复和不一致快照拒绝。

CardDuelEngine.Create 只创建独立牌组与四区（Hand/Played/Discard 为 0），不发牌。首次 CardDuelAction.Start 免费补满 HandSize，HasStarted 置真、Table.Revision 加一，不改变 Duel、能量、动作账本、完成回合或弃牌次数；首回合前拒绝其他命令，重复 Start 拒绝。Unity 在空区绑定后执行 Start，以便开局发牌复用正常展示流程。

发牌策略以 ICardDuelDealPolicy.GetDrawCount(CardDuelDealContext) 注入 CardDuelEngine。四种触发点共用引擎的安全移牌/洗回路径，返回数量必须在 0..MissingCards；策略不修改牌区。CardDuelDealPolicy 允许出牌与弃牌分别选择 AfterAction / NextPlayerTurn，默认出牌延后、弃牌立刻补满。终局不再触发补牌；修改策略后需要创建新战局，CardDuel 完整存档仍未实现。
