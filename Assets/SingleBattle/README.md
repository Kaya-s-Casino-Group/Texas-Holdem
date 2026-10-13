# 单场战斗：1v1 与 n v n

`SingleBattleDemo.unity` 保留 1v1；`TeamBattleDemo.unity` 提供 2 位玩家对 3 个怪物。打开后 Play 即可，也可用 `Tools > Poker > Single Battle > 打开多人战局 Demo（2v3）`。这里的一场战斗是 Run 中的一个节点，不是完整 Run。

## 配置归属

Host 不再保存生命、攻击、能量、牌数等数值，仅持有玩家/怪物 Actor 数组、公共规则引用、UI/表现绑定与运行时流程。数组顺序定义阵容席位与怪物行动顺序，双方人数可不同，总单位数受 CombatState 的 64 上限约束。

| 位置 | 内容 | 默认 Demo 资产 |
| --- | --- | --- |
| PlayerBattleActor → 玩家配置 | 单位 ID、最大生命、初始格挡、能量上限/初始值、出牌/弃牌费用、手牌上限、弃牌次数、两种补牌时机 | `Assets/SingleBattle/Profiles/DemoPlayer.asset` |
| EnemyBattleActor → 怪物配置 | 单位 ID、最大生命、初始格挡、基础攻击伤害；没有能量或牌组字段 | `Assets/SingleBattle/Profiles/DemoEnemy.asset` |
| SingleBattleHost → 公共战局规则 | 规则版本、回合上限、分数到伤害换算 | `Assets/SingleBattle/Profiles/DemoBattleRules.asset` |

使用 Assets/Create/玩法/战局 创建不同角色/怪物的配置，分别拖到对应 Actor。多个 Actor 可复用同一模板，但需要不同数值时应复制或另建配置再绑定。共享资产不是角色当前状态：开战时转成不可变的 BattlePlayerSetup / BattleEnemySetup / BattlePlayerRules，当前 HP、格挡、能量、手牌仅由战局状态持有；修改配置应用于下一场战斗。

已迁移原场景数值，Demo 公共配置的回合上限保留原值 **445**，没有重置为代码默认的 8。Player 40 HP、怪物 75 HP/攻击 7、默认 3 能量，出牌和弃牌各耗 1。初始能量独立设置，允许超过上限；后续回合恢复到该玩家的上限。

## 回合与牌组

1. Create 时每位玩家分别创建标准无 Joker 的 52 张牌，拥有自己的抽牌、手牌、出牌、弃牌四区，起手为 0。Start 首次进入玩家阶段，为所有玩家按各自策略免费发牌。
2. 存活玩家共享行动阶段，可交错出牌/弃牌，每人独立扣费。能量为 0 自动标记该玩家结束；也可手动结束。初始能量为 0 时仍可手动空过，费用不足但非 0 时不强行结束。
3. 所有存活玩家均结束，才进入怪物阶段。存活怪物按阵容列表顺序行动，一次命令只结算一个怪物；表现结束才调用下一个。
4. 全部怪物完成后算一整轮。仍在战斗时，所有存活玩家恢复各自能量并按各自策略补牌。死亡玩家不再参与行动/等待条件，也不恢复能量或补牌。
5. 怪物全灭胜利、玩家全灭失败；达到回合上限在完整怪物阶段后判负。部分队友/怪物死亡不提前终止整场。

选择 1–5 张真实手牌，由 PokerHandClassifier / ScoreEngine 计算牌型，总分经公共 ScoreDamagePolicy 转为伤害，CombatEngine 处理格挡与 HP。默认出牌延迟至下一玩家阶段补满，弃牌立即补满；每位玩家可单独配置。抽牌堆不足只洗回该玩家的弃牌堆。

本地 Demo 用一套 CardZone/UI 观察当前玩家，点击“切换玩家”或 Tab 切换。结束后自动转到仍可行动的队友。切换只重新绑定观察者牌区和命令接收者，不移动逻辑卡牌、不重开战局；牌库总览也仅包含当前玩家的完整构筑。表现/发牌过程中禁止切换与重复输入。

这不是联网多人功能：目前共享阶段中的命令由同一权威按 Revision 串行结算，本地输入一次控制一名玩家。规则层没有固定的玩家行动游标。

## 数据与接口边界

- `CardDuelState` 拥有全部卡牌、每位玩家四区、弃牌预算及洗牌状态；保留类名以兼容原调用，已支持多对多。新代码使用 `Zone(kind, playerId)` / `DiscardsFor(playerId)`，不使用默认第一玩家的便捷别名。
- `TeamBattleState` 拥有共同 CombatState、各玩家能量与结束标记、怪物队列和战斗阶段。`TeamBattleEngine` 负责阵营回合、费用、伤害与胜负，`BattleRules` 只含公共规则。
- 角色根节点持有 PlayerBattleActor / EnemyBattleActor，通过 ParticipantId 查询最新快照；Model 子物体继续承载 VAT、DOTween、裂纹与音效。角色绑定要求在 Host 阵容中具有对应席位，不能冒用同队另一个角色的身份。
- `IBattleTargetPolicy.SelectTargets(BattleTargetContext)` 由权威侧注入，接收源单位、当前状态、可选请求和存活对手集合，返回一个或多个目标。默认选第一个存活对手（或校验显式单目标）；引擎拒绝未知、死亡、友方和重复目标。接口支持多个伤害事件，但当前 Demo 不提供选目标或 AOE 操作 UI，也未定义范围/友方治疗规则。
- `PlayerBattleActor.CreateSetup(id, deck, initialHealth)` / `PlayerBattleProfile.BuildSetup` 可消费 Run 当前构筑和生命；缺省牌组为独立标准 52 张。Core 不依赖 ScriptableObject 或 Unity。
- `ICardDuelDealPolicy` 上下文含 PlayerId，只返回抽牌数量，不能移动或创建卡牌。每位玩家可独立注入策略。
- `ScoredDuelEngine` / 旧快照协议保留给原有独立 1v1 规则调用；新 Demo 使用 TeamBattleEngine。完整多人战局存档、Run 奖励写回、复活和增援尚未实现。

## 可等待的表现协议

逻辑角色绑定任意实现 `IBattleActorPresentation` 的子物体组件，宿主通过角色调用协议，不依赖具体 Presenter 类型、动画名称或持续时间。协议只接收已结算的只读 `BattlePresentationCue`（PrepareAttack / Attack / Hit / Death、ActorId、可选 CombatEvent）；表现不获得战局命令权限。

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
