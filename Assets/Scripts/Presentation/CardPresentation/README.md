# World card presentation (Palamedes)

## 现行入口与文档状态

本页是当前表现层接入的统一入口（整理日期：2026-10-28）。当前场景为 `Assets/Scene/InTest/SingleBattle.unity`；已接入世界空间手牌动效、输入和操作按钮，尚未接入完整角色、正式 HUD、镜头及终局演出。接口支持某项能力不代表当前场景已配置该动作。

当前链路为：规则事实 → `BattlePresentationBridge` → `ScenePresentationDirector / DisplayStore` → `WorldCardZoneDisplayBinding` → `WorldCardZonePresentation` → `HandZoneView` → `CardArt.CardView`。战斗牌区能力边界是 `ICardZonePresentation`，CardView 负责末端卡面渲染；当前链路不使用旧 `CardMatchHost / CardZonePlayHost`。

当前场景将 `CardsDealt / CardsMoved / DeckRecycled / CardsCreated / AdminEdited / ActionResolved` 配置为观察投影的 `cards.settle`，以空的显式 checkpoint 发布快照，并等待手牌运动。`prepare → attack → Impact` 等角色联动尚未在当前场景配置。

现行职责约定：DisplayStore 发布持久事实；悬停、选择和本地排序不改写事实；同一牌区避免重复投影；Marker、checkpoint 与 Completed 分别表示中途信号、显示提交点和动作终态；取消与 Synchronize 清理所属资源并对齐快照。具体协议见 [通用表现框架](../BattlePresentation/README.md)，会话生命周期见 [Hosting README](../../Gameplay/SingleBattle/Hosting/README.md)。

### 历史资料索引

以下资料保留原路径和历史正文，供追溯设计与迁移；其中“当前”“已完成”和测试结果均指记录当时，不作为当前场景的接入或验收依据。

| 历史资料 | 使用范围 |
| --- | --- |
| [表现系统设计](../../../../Tools/BattlePresentationDesign.md) | 旧场景的整体目标与迁移设计 |
| [实施记录](../../../../Tools/BattlePresentationImplementation.md) | 旧 Scene3D / Minimal2D 完整接入记录 |
| [时间线配置教程](../../../../Tools/BattlePresentationTimelineConfiguration.md) | 旧 SingleBattleDemoNew 配方与 Inspector 示例 |
| [牌区接口约定](../../../../Tools/CardZonePresentationContract.md) | 旧 World / Canvas / 2D 接口迁移记录；旧宿主流程不再适用 |
| [表现层保留清单](../../../../Tools/UnityPresentationRetention/README.md) | 清理时的模块与依赖快照；后续恢复以本页为准 |
| [SceneDemo 1V2 配置](../../../../Tools/SceneDemo1V2Configuration.md) | 旧关卡接入状态 |

本页后续验证计数也是各次迁移的历史结果，不表示本次文档整理重新运行了测试。

## 恢复来源

从 `origin/legacy-3d-scenes` 恢复世界空间卡牌动效，保留原始默认曲线、分层 Transform 和独立手动 Tween 时钟：发牌飞行与翻面、弧形排布、重排、闲置摇摆、悬停放大、选中抬升与回弹、拒绝反馈、准备抬牌、离场及跨牌区移动。

## 组件与接入

1. 在牌区对象添加 `HandZoneView`，将 `Card Prefab` 绑定为 `Assets/GameAssets/CardArt/Prefabs/Card.prefab`。布局、摇摆、发牌、抬升、离场参数沿用旧组件。
2. 添加 `WorldCardZonePresentation`。这是战斗时间线唯一的牌区能力入口，实现当前 `ICardZonePresentation`。
3. 在时间线节点使用现有 `CardZoneTimelineAction`，Source 绑定该组件，设置动作键和牌区 ID。
4. 如需在显示检查点自动呈现牌区，添加 `WorldCardZoneDisplayBinding`，绑定 `ScenePresentationDirector`。Zone Id 留空时自动跟随当前观察者的手牌区，重开后重新解析；也可指定固定牌区 ID。代码配置入口为 `Configure(director, zoneId)`。

渲染实例仍使用 `CardArt.CardView`；其卡面、材质和美术配置不被旧分支覆盖。默认状态美术映射恢复在 `Assets/GameAssets/CardPresentation/Resources/StandardCardStateArt.asset`。DOTween 恢复运行时 DLL、原始导入设置、许可说明，以及启动所需的 `DOTweenModuleUtils` 和它依赖的 `DOTweenModulePhysics / DOTweenModulePhysics2D`；模块与 DLL 均来自仓库 `0c98182` 的同一版本。不要仅保留 DLL：首次创建 Tween 时会反射调用模块初始化，缺失会报 `Couldn't load Modules system`。同时恢复默认发牌音效与 iOS 震动桥接。

## 两种时间线方式

**动作主动呈现**：配置 `cards.draw / cards.arrange / cards.play / cards.discard / cards.selection / cards.state`，关闭 Observe Projection。动作应用请求的 After，等待真实运动结束。不要同时让其他组件向底层 HandZoneView 写入不同快照。

**检查点驱动呈现**：绑定 `WorldCardZoneDisplayBinding`；配置 `cards.settle` 并开启 Observe Projection。组使用 Explicit Checkpoint，依赖列表留空，使快照先发布，settle 再等待投影后的真实动画落定。不能把同一个等待投影的 settle 完成信号作为该投影的前置条件，否则会形成等待环。

绑定器只有在存在观察操作时才播放动画；没有观察操作时即时投影，因此 Instant 批次不会偷偷启动 Tween。已被主动动作应用的同一快照不会重复呈现。排序、可见性、牌面和属性变化均参与观察匹配。

## 动作与信号

| 动作 | 信号 | 行为 |
| --- | --- | --- |
| cards.draw | AllArrived | 从牌堆位置分批飞入并翻面 |
| cards.arrange | Settled | 平滑重排 |
| cards.prepare | Prepared | 真实抬牌进度到达准备点，保持牌区成员不变 |
| cards.play / cards.discard | Released、AllArrived | 指定牌离场，等待全部离场与布局落定 |
| cards.selection / cards.state | Settled | 更新显示状态并等待抬升、缩放和布局落定 |
| cards.settle | Settled | 等待当前运动；观察模式还要求 After 已被投影 |

Play 和 Discard 当前共用旧版离场轨迹和弃牌目的地。`CardZoneTransferPresenter` 保留为跨牌区的底层视觉能力，尚未包装成独立的多牌区时间线动作。

## 生命周期与数据

- 请求只消费显示 DTO，不执行规则命令。`Current` 保留已接受投影的 Zone/Owner/Kind 与卡牌顺序。
- Prepare 发出 Prepared 时不删除手牌；后续 CardsMoved 投影负责牌区成员变化。
- 操作取消或 Scope 释放只清理该次动作；旧准备操作不能覆盖后续动作的姿态。
- Synchronize 取消等待和运动，立即呈现最新快照；迟到信号不会穿过旧 Scope。
- Selection 可被下一次 Selection 替换，从当前姿态继续，不恢复过期牌表。
- 隐藏牌复用实例时清除原卡面和规则身份。
- `AdvancePreview(deltaTime)` 仅在 EditMode 使用，逐实例推进拥有的 Tween，不更新全局 DOTween 时钟。

## 世界空间交互

`Presentation/CardInteraction` 保留旧版 `CardZoneMouseInput → CardZonePointerController → CardZoneRaycaster`：鼠标／单指触摸、按下与释放命中同一张牌才切换选择、悬停稳定命中、滑动取消点选、UI 遮挡／失焦／离开视口取消手势。几何命中直接使用卡面网格，无需 Collider。

`Gameplay/SingleBattle/Presentation/World/Runtime/BattleHandInteraction` 实现 `ICardInteractionSource / ICardInteractionGate`，挂在宿主下的 HandZone 上，通过 `Configure(adapter, camera)` 或场景序列化绑定：

- 悬停只保存为前端状态；选择映射 ViewId → 公开 CardId，交给 `BattleTimelineSceneAdapter.SelectCards` 查询候选。
- `WorldCardZonePresentation.PresentInteraction` 只更新实例的抬升／缩放反馈，不改 `Current`、DisplayStore、牌区成员或飞行动画。
- 是否可选受桥接 `CanAct`、规则窗口、卡牌禁用状态、进场状态和手牌选择上限约束；规则提交仍受桥接限制；玩家对牌的短按、长按和拖动统一受桥接玩家回合与播放门控限制；牌区正在进出牌或有未完成事实动作时暂缓理牌。
- 同步、重开、提交和组件停用会清理旧手势／选择反馈；候选反馈动画本身不锁住规则输入。

`Assets/Scene/InTest/SingleBattle.unity` 已配置 `Battle/HandZone` 输入、屏幕操作区 `BattleActions` 以及 Input System EventSystem。操作按钮包括 Draw、Skip Draw、Play、Discard、End Turn，可用状态来自桥接和规则窗口。选牌后显示候选牌型与分值。当前场景沿用单章节 XML 及其开局发牌配置；使用手动摸牌配置时，Draw 按规则允许数量摸牌。

按钮通过 `BattleHandActionPanel` 调用现有桥接，不直接改规则状态或手牌实例。规则事实仍由 DisplayStore 检查点投影到牌区。本次未恢复旧 CardMatchHost / CardZonePlayHost。

## 验证

保留 `WorldCardZoneContractTests` 的 16 项核心回归：真实信号、准备后离场、观察排序与状态、Instant、取消同步、隐藏牌复用、当前 Director 的检查点接入、连续检查点观察以及本地候选反馈隔离。归档动效曲线、布局几何及音效配置的 41 项旧测试已在迁移验证后移除。

迁移时曾在 Unity 2022.3.62f3 独立验证工程中通过 89/89 项测试；精简后的测试范围为卡牌契约 12 项与通用表现层 36 项。视觉参数仍可在当前场景手牌组件中调整。

清理后验证：独立 Unity 工程 EditMode **48/48 通过**，结果位于 `Library/CardMotionValidationProject/cleanup-results.xml`。

交互迁移验证：独立 Unity 工程 **50/50** 卡牌／通用表现层回归通过（`interaction-regression.xml`）；加载当前 SingleBattle 场景进入 PlayMode 的端到端校验通过（`input-tests.xml`），覆盖开局发牌、射线悬停／点选／取消、选择上限、停用清理、候选不修改事实、出牌／弃牌及规则补牌、重开、结束回合。验证脚本只位于 `Library/CardMotionValidationProject`，不加入产品脚本。鼠标／触摸设备手感仍需在 Game 视图或真机上体验。

## 本地理牌与长按拖动

`WorldCardZonePresentation` 提供 `SortByRank()`、`SortBySuit()`、`MoveToIndex(viewId, index)` 和 `VisualOrder`。点数从大到小（A 最大），花色顺序为黑桃、红桃、梅花、方片；花色理牌在同花色内按点数降序。公开 DTO 携带 `rank / suit`，排序不读取隐藏牌身份，也不依赖卡面美术命名。

这些接口不提交规则命令，不改变 `Current` / DisplayStore / 规则牌序。表现层保留独立的 ViewId 顺序，剩余牌维持相对位置，新进牌追加；切换对局牌区时重置。`SingleBattle/Battle/BattleActions/RightControls` 集中放置临时点数／花色按钮，左侧操作区保持原样。

`CardZoneMouseInput` 的 Long Press Seconds 默认 0.35 秒，Press Move Tolerance 默认 12 屏幕像素。玩家对卡牌的操作只在当前玩家回合且桥接 `CanAct` 为 true 时有效。鼠标与触摸共用同一手势状态机：短按在松开后选中／取消；达到长按阈值后消耗点击，进入拖动，松开只确认顺序。阈值前移动超过容差取消点击与长按；失焦、UI 遮挡或触摸取消回到拖动前顺序。拖动期间保留原选择状态。离开玩家回合、开始规则批次播放或桥接停用时，立即取消按压／长按计时／拖动，恢复拖动前顺序；迟到的松开不选牌、不落位，重新开放输入后必须重新按下。

拖动牌拥有独立 Drag Transform，沿视线向镜头浮起（Drag Lift Distance 默认 0.08 世界单位，随牌大小调整，并限制为当前射线距离的 15%，避免贴近镜头后过度放大）。其布局槽位仍参与排布，指针经过槽位时其余牌平滑让位，松开后落回空位。真实牌区投影／同步会取消旧拖动；卡牌实例由新快照正常接管。底层程序化理牌接口和右侧理牌按钮仍不依赖规则阶段；玩家直接对卡牌的点击／长按／拖动通过 `BattleHandInteraction` 额外检查玩家回合门控。底层只在正在进出牌或牌区事实动画未结束时返回 false，避免与实例增删争用。

理牌迁移验证：Unity 2022.3.62f3 独立验证工程 **63/63 通过**（`Library/CardMotionValidationProject/order-regression.xml`），覆盖原出牌流程、规则投影、点数／花色排序、本地顺序随成员变化重整、短按／长按分流、拖动占位、取消与落位、非可行动状态下理牌和重开。场景测试隔离了无关的角色布料模拟；拖动与 GUI 已通过离屏渲染检查。

## 手牌最大间距

`CardZoneView → Layout → 最大中心间距`（`MaxCardSpacing`）限制牌区平面内相邻卡牌中心距离，使用牌区本地单位。设为 0 保留原排布；设为正数后，少量卡牌也紧凑排列，可小于牌宽形成重叠。卡牌尺寸不变，保留 Alignment 对齐方式；牌多或区域不足时允许进一步压紧。弧形排布按相邻中心的弦长限制，不会在中间位置超出上限。SingleBattle 的 HandZone 当前设为 `0.08`。出牌区仍保持五个固定槽位且不允许重叠，其正数上限不能小于牌宽。
