# SceneDemo 1V2 配置

> **历史资料（2026-10-28 整理）**：本文保留旧版本的设计、配置或实施记录；下文“当前”“已完成”与测试结果均指记录当时。现行表现层接入统一以 [CardPresentation README](../Assets/Scripts/Presentation/CardPresentation/README.md) 为入口。旧 Scene3D / Minimal2D、CardMatchHost / CardZonePlayHost 及完整角色、HUD 配方不代表当前工程已恢复，不应直接按本文重建旧调用链。

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

- `Card Table/Hand Zone`：`HandZoneView`（继承 `CardZoneView`）、`CardZonePlayHost`、`CardZoneMouseInput`、`WorldCardZonePresentation`。输入和绑定组件引用本场唯一 CardMatchHost 和独立的 Card Camera。WorldCardZonePresentation 对外实现通用牌区表现能力，见 [接口约定](CardZonePresentationContract.md)。
- `Deck Anchor` / `Discard Anchor`：锚点改为手牌局部坐标 `(0.55, 0.035, 0)` / `(-0.55, 0.035, 0)`，配合独立牌区空间；运动时长、曲线和牌区现有 `0.95 × 0.38` 布局保留。HandZoneView 使用 Card Camera 的「平行相机画面」朝向。
- `HandActionPanel`：复用共享预制体及 HandActionPresenter，提供出牌、弃牌、按点数/花色排序；使用 Screen Space Camera Canvas，绑定 UI Camera，并保留独立 UI EventSystem。SingleBattleSceneAdapter.actions 已引用此 Presenter。
- `HandZoneView.cardPrefab` 引用 `Assets/GameAssets/CardArt/Prefabs/Card.prefab`；其 Surface 使用 `FourBeastsAtlas.asset`、`FourBeastsCards.mat` 和已有美术效果配置。`StandardCardStateArt` 负责状态效果映射，卡面主题由 Card.prefab 的图集决定。
- Director 的 `cards` 目标指向 WorldCardZonePresentation。开战后规则桥接按当前玩家动态绑定 hand/deck/discard 区域，不序列化第二份牌表。

自动开战关闭时牌区等待规则数据，不自行生成示例牌。此阶段完成牌区组件与资源接线，不代表完整战斗时间线已经可运行。

## 场景、牌区、UI 三相机

当前场景的相机栈为：

```text
Main Camera（场景，Base / CharacterRenderer，原透视投影和场景效果）
  → Card Camera（牌区，Overlay / CardForwardRenderer，原生分辨率透视投影，FOV 30°）
  → UI Camera（UI，Overlay / UIRenderer，原生分辨率）
  → CRT 最终合成并上屏
```

- `Cards` 是第 11 层（Mask 2048）。Card Table、Hand Zone 和两个牌堆锚点都使用该层；CardInstanceView 已将生成卡牌的全部子对象设置为所属牌区的 Layer。
- Main Camera 排除 UI 和 Cards；Card Camera 仅绘制 Cards；UI Camera 仅绘制 UI。两个 Overlay 都清理深度，牌区不会被场景几何遮挡，UI 在牌区上方。
- UI Camera 复用 UICameraStack，跟随场景相机的投影与输出。`UIRenderer.asset` 的 CRT 参数和最终合成机制未改动：包含场景、牌区和 UI 后才执行 CRT，不是只处理 UI 像素。
- Card Camera 使用独立 `CardForwardRenderer.asset`（MainURP renderer index 4）。它是无 Renderer Feature 的 Forward Renderer，禁用低分辨率、深度预通道、透明阴影；相机关闭阴影、后处理、深度/颜色纹理请求、MSAA、遮挡剔除和逐帧 Volume 更新。没有为牌区新增 RenderTexture 合成步骤；最终 CRT 继续使用原有共享合成目标。
- 现有 Card Deferred Shader 已有 `UniversalForward` Pass，牌区直接使用该 Pass。FourBeasts 材质、图集和卡面效果协议不变；保留 Shader 的 GBuffer Pass 供其他场景使用，不代表牌区会执行 GBuffer。当前仍是前向光照材质，可受主灯、环境和附加灯影响；不是新增 Unlit 材质，也没有承诺移除所有光照成本。

### 调整牌区位置、大小

在 **Card Camera / ViewportOverlayCamera** 配置：

| 字段 | 当前值 | 含义 |
| --- | --- | --- |
| Scene Camera | Main Camera | 输出视口、宽高比来源，不跟随它的位姿/震动 |
| Before Camera | UI Camera | 将牌区插入 UI 之前 |
| Content Layers | Cards | 本相机绘制层，同时从 Base 排除 |
| Content Root | Card Table | 独立的场景根容器 |
| Content Viewport | `(0.56, 0.06, 0.40, 0.24)` | 输出画面右下方的适配区域 |
| Content Size | `(0.95, 0.38)` | 未缩放的牌区参考宽高，与 Hand Zone 布局对应 |
| Content Distance | `5` | 容器距牌区相机的深度 |

牌区相机使用全输出视口，当前为透视、垂直 FOV 30°、Content Distance 5。ViewportOverlayCamera 保留 Camera 自身的 Projection 设置；可切回正交（Size 保留为 1）。组件通过 ViewportToWorldPoint 测量指定深度处的可见宽高，将 Content Size 等比例放入 Content Viewport 并居中定位，因此两种投影、FOV、正交 Size 和输出比例变化都使用同一套布局。Content Viewport 是布局范围，不是裁切区域；手牌动画、选中抬升和牌堆进出允许越界。修改 Hand Zone 布局宽高时同步调整 Content Size。不同屏幕比例会重新适配，在编辑器中也生效。

Content Root 应保持为场景根对象。组件拥有它的位置、旋转和缩放；时间线应驱动 Hand Zone 或其下卡牌的动画层，不要同时动画 Card Table，以免与屏幕锚定竞争。

### 牌区朝向模式

Hand Zone 的 Inspector 和通用牌区验证窗口均提供三个模式：KeepAuthored（保持配置朝向）、LookAtCameraPosition（朝向相机位置）、ParallelCameraPlane（平行相机画面）。模式独立于相机投影类型。返回 KeepAuthored 或停用牌区时，恢复跟随前缓存的局部旋转；卡牌子级的扇形、翻牌、抬升等动画不受模式切换重置。

旧场景 faceCamera=true 自动迁移为 LookAtCameraPosition，false 为 KeepAuthored；旧 SetCameraFacing(bool, Camera) 调用仍兼容。通用相机组件重命名时保留原脚本 GUID，既有场景引用继续有效。

### 输入与表现边界

`HandZoneView.facingCamera`、`CardZonePlayHost.inputCamera`、`CardZoneMouseInput.inputCamera` 均引用 Card Camera。HandZoneView.FacingMode 设置为 ParallelCameraPlane（平行相机画面），在透视和正交下均按 `-camera.forward` 确定牌面法线，+Z 沿相机上方；牌区移到角落不会改变基准旋转。规则桥接的 BindBattleHand 会继续读取 HandZoneView.FacingCamera，重绑或重开对局不会退回 Main Camera。鼠标/触摸通过该相机产生射线，原有 GraphicRaycaster 阻挡和选择规则保留。

Director 的 `cards` 目标仍指向原 WorldCardZonePresentation；抽牌、理牌、出牌准备、弃牌、自定义动作和 Prepared 等信号沿用原能力接口。镜头目标仍属于场景表现，移动场景相机不会带动独立牌区。跨相机动画若需要从牌区飞向角色，必须通过屏幕/视口坐标转换目标，不能直接复用角色世界坐标。

本次只拆分渲染和屏幕布局、改绑既有接口。XML、CardMatchHost、规则数据和玩法命令未修改；原本未完成的战斗 HUD、完整时间线和关闭 Auto Start 的状态保留。

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

三相机拆分阶段：按要求未运行测试、未进入 Play Mode，未做 GPU 或输入效果验证。

透视/正交通用化阶段：SceneDemo 1V2 牌区改为透视 + ParallelCameraPlane，右下角 Content Viewport 与输入/表现绑定保留；按要求未运行测试或进入 Play Mode。
