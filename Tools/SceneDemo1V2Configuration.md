# SceneDemo 1V2 配置

当前已在 `SceneDemo 1V2.unity` 的 `BattleHost` 接入 XML 桥接和 Actor 占位。已复用 SingleBattleDemoNew 的 3D 牌区和手牌操作面板，使用 FourBeasts 卡面。**战斗 HUD、模型和完整时间线尚未接入，自动开战关闭。** Host 与 Presenter 的运行时代码没有修改。

## 当前场景入口

`BattleHost` 包含 BattleSceneHost、SingleBattleSceneAdapter、CardMatchHost、CardMatchPresentationAdapter、ScenePresentationDirector 和 SceneCandidateFeedback。原最小 Host 实例已在本场景解包，移除它的 XmlBattleSessionSource 和 BattleTimelineSceneAdapter；原预制体资产不变。

CardMatchHost 已独立为通用数据/交互宿主，可供任意 2D/3D 前端复用，见 [CardMatch 接口](../Assets/Scripts/Presentation/CardMatch/README.md)。已在同一 BattleHost 对象上显式添加独立组件 CardMatchPresentationAdapter，绑定 StandardCardStateArt，供旧 3D 牌区接入时进行美术投影；Host 不直接依赖该适配器。SingleBattleSceneAdapter 的 match 引用仍指向唯一的 CardMatchHost，未创建第二份卡牌数据宿主。

```text
BattleHost
└─ Actors
   ├─ Player [player]       (PlayerBattleActor)
   │  └─ Visual            (BattleActorPresenter)
   ├─ Enemy 1 [enemy-1]     (EnemyBattleActor)
   │  └─ Visual            (BattleActorPresenter)
   └─ Enemy 2 [enemy-2]     (EnemyBattleActor)
      └─ Visual            (BattleActorPresenter)
```

三个 Visual 都为空，原 PokerCharacter、DragonGargoyle 保留原层级。玩家和第一名敌人的占位位于对应模型的位置；第二名敌人暂沿相机水平右方偏移 1.6 单位，后续可调整。

Director 已注册 `player`、`enemy`、`enemy-1`、`enemy-2`、`camera`、`scene`、`cards`；时间线和候选反馈路由为空。Actor 的 Presentation 已绑定基础 `BattleActorPresenter`，后续可替换为实现 `IBattleActorPresentation` / `IBattleActorDisplayPresentation` 的组件。完整时间线接入前不要手动开战或打开 Auto Start。

场景专用准备工具已移除。后续直接配置场景组件和引用；已保存的场景组件、XML、Actor 占位和桥接绑定独立于编辑器工具。

## 已接入的 3D 牌区和卡面

直接复制 `SingleBattleDemoNew.unity` 的牌区配置，不使用场景准备工具，也不修改共用预制体：

- `Card Table/Hand Zone`：`HandZoneView`（继承 `CardZoneView`）、`CardZonePlayHost`、`CardZoneMouseInput`、`WorldCardZonePresentation`。输入和绑定组件引用本场唯一 CardMatchHost 和 Main Camera。WorldCardZonePresentation 对外实现通用牌区表现能力，见 [接口约定](CardZonePresentationContract.md)。
- `Deck Anchor` / `Discard Anchor`：保留旧场景发牌、弃牌锚点和运动参数。牌区位置调整到当前相机前方下部，布局宽度改为 2.6、高度保持 0.5；朝向由 HandZoneView 对准当前相机。
- `HandActionPanel`：复用共享预制体及 HandActionPresenter，提供出牌、弃牌、按点数/花色排序；使用 Overlay Canvas 和独立 UI EventSystem。SingleBattleSceneAdapter.actions 已引用此 Presenter。
- `HandZoneView.cardPrefab` 引用 `Assets/GameAssets/CardArt/Prefabs/Card.prefab`；其 Surface 使用 `FourBeastsAtlas.asset`、`FourBeastsCards.mat` 和已有美术效果配置。`StandardCardStateArt` 负责状态效果映射，卡面主题由 Card.prefab 的图集决定。
- Director 的 `cards` 目标指向 WorldCardZonePresentation。开战后规则桥接按当前玩家动态绑定 hand/deck/discard 区域，不序列化第二份牌表。

自动开战关闭时牌区等待规则数据，不自行生成示例牌。此阶段完成牌区组件与资源接线，不代表完整战斗时间线已经可运行。

## 规则入口

- 内容包：`Assets/GameAssets/SingleBattle/Content/SceneDemo1V2.xml`
- Encounter ID：`scene-demo-1v2`
- 以 `External/BattleToolkit/Engine/Content/SingleBattleDemoNew.xml` 为基础的独立内容包。除包标识和 Encounters 外，规则定义保持一致；后续可单独调整本关配置。
- 可使用 `Tools/BattleContentEditor/start.command` 启动配置工具，导入本 XML，选择该编成试玩；导出后替换本 XML。

| 编成顺序 | 参与者 ID | 定义引用 | 初始数值 | 后续场景绑定 |
| --- | --- | --- | --- | --- |
| 玩家 0 | `player` | `hero` / `baseline` | 800 生命、3 能量、8 张手牌、无遗物 | `playerActors[0]` 占位已绑定，待放入 PokerCharacter |
| 敌人 0 | `enemy-1` | `guardian` | 1200 生命、240 伤害 | `enemyActors[0]` 占位已绑定，待放入第一名守卫 |
| 敌人 1 | `enemy-2` | `guardian` | 1200 生命、240 伤害 | `enemyActors[1]` 占位已绑定，待放入第二名守卫 |

两名敌人共享同一规则定义，拥有不同参与者 ID，各自维护生命、状态和行动。XML 中的槽位顺序与场景 Actor 数组顺序必须一致，不能让两个槽位绑定同一个 Actor。

规则沿用 20 回合上限；出牌、弃牌各消耗 1 能量并立即补满手牌；整场 3 次弃牌，每张弃牌提供 8 格挡。单体伤害使用 `FirstLiving`，首个敌人死亡后自动转向下一存活敌人；三条、四条和皇家同花顺使用 `AllLiving`。本轮没有增加手动选敌。

单体数值未重新平衡：两敌都存活且未受控制时，一轮合计造成 480 原始伤害。

## 后续完整接入

已保留一个 `BattleSceneHost`，`SingleBattleSceneAdapter` 同时作为其 SessionSource 和 SceneAdapter；ContentXml 已指向本 XML，Encounter ID 为 `scene-demo-1v2`。Adapter 已实现 XML 编译、阵容校验和规则到表现的数据桥接，无须派生新的 Host。

牌区及 HandActionPresenter 已绑定本场 CardMatchHost，由规则快照维护 card list。后续 HUD 读取表现快照，显示两名敌人的生命和各自意图，并按本场景世界尺寸调整角色动作幅度和意图 UI 大小。

复用 `BattlePresentationSceneSetup` 的可配置时间线。以下是后续写入 ScenePresentationDirector 的接线约定，**不是已落盘生效的场景配置**：

| 规则节点 | 表现顺序 / 信号 | 目标 |
| --- | --- | --- |
| `BattleStarted` / `PhaseStarted` | 开场 / 阶段 UI，`Revealed` 推进展示快照 | `ui.feedback` |
| `ActionStarted` + `Play` | `cards` 发出 `Prepared` → `attack`；`attack.Impact` 允许后续效果展示 | cards → 当前 ActorId |
| `EffectSettled` | `effect` 受击/格挡发出 `Revealed` → 伤害 UI、敌方受击镜头；死亡在 `effect.Completed` 后 | 当前 Effect.TargetId；camera；ui.feedback |
| `CardsMoved` / `CardsDealt` / `DeckRecycled` | 手牌整理与补牌 | cards |
| `ActionStarted` + `EnemyAction` | `prepare.Completed` → `attack.Impact` | 当前行动的敌人 ActorId |
| `PhaseEnding` / `BattleEnded` | 回合结束 / 结算 UI | ui.feedback |

攻击和受击动作保持动态目标解析，不能把两名敌人的动作都固定绑定为 `enemy`。通用的 `enemy` 别名仅代表第一名敌人；实际战斗目标依赖参与者 ID 解析。UI 的 HP、能量等数据由表现快照在时间线 checkpoint 更新，不在动画中重新计算规则。

预选、切换与取消继续复用已有候选会话：仅更新预览；取消撤销该会话的视觉效果，不改变权威状态。

## 验证记录

XML 阶段使用 BattleToolkit 自带 .NET 10 宿主验证：

- XML 规则编译成功，diagnostics 为空。
- 对比基础包：Encounters 之外所有规则定义一致。
- 固定种子 `104729` 开战：1 名玩家、2 名独立敌人、8 张手牌、3 能量。
- 直接结束玩家回合：两名敌人均执行行动，玩家生命从 800 变为 320，完成回合数为 1。

占位接入阶段：Unity 编译通过；检查保存的场景确认只有一个 Host/Adapter、1+2 Actor 均绑定 Visual/Presenter、XML 和目标引用完整、Auto Start 关闭；进入 Play Mode 未发现错误。完整战斗、输入与动画联调留待实际接入后验证。

牌区接入阶段：检查唯一 Host/美术桥接、HandZoneView/输入相机、HandActionPresenter 和 cards 目标引用；确认 Card.prefab 指向 FourBeasts 图集及材质。进入 Play Mode（自动开战关闭）无控制台错误，随后退出。此检查不包含尚未接入的完整战斗播放。
