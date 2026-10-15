# 牌区展示、手牌与出牌区

`CardPresentation.Runtime` 为 Unity 展示模块，引用 `PokerEngine.Core`、`CardArt.Runtime` 与已安装的 `DOTween.dll`，不引用 Holdem、Draw、Runs 或 Abilities。提供牌区展示和跨区出牌路径，消费外部卡牌状态而不采集鼠标输入；属性立即同步，悬停驱动缩放、选中驱动上浮动画，列表变化驱动补牌、让位与弃牌动画，可附加持续的视觉摆动。使用既有卡牌 Prefab 和 CardView，不修改规则状态。运行时不自动创建场景牌区；编辑器工具只观察实战牌局并调整现有 View 的布局、动画和音效。

## 通用牌区与派生边界

- `CardZoneView`：通用基类，负责按 ZoneId/ViewId 同步有序视觉实例、属性投影、可见性、圆弧/直排布局、相机朝向与持续摆动。直接使用时，列表增删重排立即落位，不附带手牌飞入/弃牌/上浮和音效。
- `HandZoneView : CardZoneView`：手牌表现。负责悬停缩放与选中上浮、抽牌及翻面/惯性、重排过渡、虚拟弃牌终点及离场动画、抽牌/弃牌/选中三组独立音效。类未封闭，可继续派生。
- `PlayedZoneView : CardZoneView`：出牌表现，最多五张卡牌，五个固定直排槽位，弧度与持续摆动强制关闭；不继承手牌行为。
- Core `CardZone` 继续共用有序数据模型，通过 `Kind` 区分含义；`DefaultZoneKind` 为验证工具提供默认 Kind（手牌为 `hand`、出牌区为 `played`），不替代引擎规则校验。
- 弃牌区只有 Core 列表与逐牌数据，无 `DiscardZoneView`。离场视觉由手牌临时持有，到达虚拟终点即释放。
- 输入模块继续独立；`CardZonePlayHost` 自动为 `HandZoneView` 及其子类绑定鼠标，普通牌区仅恢复/更新显示。

各类共享实例同步流程，派生类通过每次更新的表现策略补充效果。基类引用调用 `Present`、`SetLayout`、`Clear` 或推进时钟时会使用实际子类行为。自定义子类可覆写 `CreatePresentationUpdate`、`CalculateCardPoses`、`ApplyLayoutPose`、`OnMotionAdvanced` 及生命周期方法；覆写生命周期时保留 `base` 调用，清理自身动画和资源。

资产位于 `Runtime/CardZoneView.cs`、`Runtime/HandZoneView.cs`、`Runtime/HandZoneView.Audio.cs` 、`Runtime/HandZoneView.Discard.cs` 和 `Runtime/PlayedZoneView.cs`，全部属于原 `CardPresentation.Runtime` 首包程序集。原手牌脚本的 GUID 随 `.meta` 移至 `HandZoneView.cs`，已有场景/Prefab 自动识别为手牌组件；共用字段保留原序列化字段名，布局、Prefab、相机和手牌参数均可恢复。新基类使用独立 GUID。验证窗口的子类匹配位于 `Editor/CardZoneValidationWindow.Targets.cs`。

## 卡牌悬停信息 Prefab

`Prefabs/CardSuitChipsTooltip.prefab` 提供 World Space 的两栏卡牌提示：上栏“花色符号 + 花色文字 + 点数”（如 `♣ 梅花 J`），下栏蓝色点数加“筹码”；背景为深色底板，两行各有独立的浅米色矩形背景（均无 Sprite），文字与原生花色符号都使用 Fusion Pixel 12px 比例简体中文的 TMP 位图字体。`CardTooltipView.SetContent(suit, rank, chips)` 消费调用方提供的数据，红桃/方块整个上栏变红，切回黑桃/梅花恢复深色，可在 Inspector 预览；不查询计分或更改卡牌属性。Runtime 新增 UGUI/TMP 依赖，纯 C# 引擎保持独立，鼠标射线拦截关闭。

场景 `CardInteraction.CardHoverPresenter` 持有提示 Prefab，并为本地相机复用一个实例；`ICardHoverSource` 提供观察者可读的数据与卡牌视觉锚点。提示跟随卡牌上方、朝向场景相机且可限制在视口内，牌背及飞入/离场不显示。验证宿主按卡牌定义查询基础点数；固化手牌会自动注册，也可用 `Tools > Cards > Configure Card Hover Tooltip` 接入已有手牌。CardZoneView 不持有提示引用、不采集鼠标、不查询计分。资源结构见 [UI README](UI/README.md)，数据边界与配置见 [交互 README](../CardInteraction/README.md#场景共用的悬停提示)。

## 直接调整牌区尺寸

选中 CardZoneView / HandZoneView / PlayedZoneView，在 Inspector 顶部打开「卡牌随区域缩放」，再调整「区域大小（宽 / 高）」；Scene 中的青色边缘手柄也可拖动调整，支持 Undo 和 Prefab override。旧配置默认关闭，继续使用 Prefab 原始尺寸。

开启后按扣除 Padding 的高度等比缩放卡牌，预留弧高和摆动空间；宽度不足时进一步缩小。手牌数量变化只重排，不改变卡牌大小；出牌区始终按五个槽位适配。编辑模式和运行时都会刷新已有实例，无需修改 Card Prefab。代码通过 `layout.FitCardsToZone = true` 配合 `SetLayout` 使用；实战牌区工具的表现参数页也有同名开关。

DOTween 的距离也随卡牌等比变化：选中上浮、拒绝选中的弹跳、发牌抬升与落位回弹、弃牌抬升、跨区出牌弧高与整理抬升。时长、间隔、旋转角度、悬停面积倍率和缓动曲线不变，Inspector 中的动画参数保持原始值，不累计乘算。无锚点时，牌堆/弃牌的局部位置一起缩放；指定场景锚点或出牌控制点仍使用其实际世界位置。跨区距离按目标牌区尺寸计算，并计入该牌区的 Transform 缩放；关闭自动尺寸时沿用旧的世界距离。动画中的选择反馈与出牌路径也能响应 `SetLayout` 尺寸变化。

## 出牌区：五个固定槽位

`PlayedZoneView` 直接继承通用 CardZoneView，共用卡牌实例复用、属性外观、可见性、相机朝向及资源管理。它只负责展示提交后的列表，出牌合法性、牌型/计分以及从手牌区转移卡牌由上层宿主处理。

- `SlotCount = 5`，`CardCapacity = 5`。支持 0–5 张真实牌，空位不生成占位 CardInstance/CardVisualData；超过五张整批拒绝，原数据和视觉实例保持不变。
- 始终先计算五个固定槽位，再按有序列表从左向右占用前 N 个。补牌或清空不会因实际数量改变槽位间距；删除/重排后按新的列表顺序重新占位。`GetSlotPoses()` 可读取五个局部位置与朝向。
- 所有槽位位于同一水平直线上，局部 Z 为零、Y 相同、布局旋转为 identity。保留宽度、留白、期望间距和整列对齐配置；可压缩间距，但不能重叠。可用宽度不足五张完整卡牌时拒绝配置。
- `SupportsArc` / `SupportsSway` 固定 false，弧高归零、持续摆动关闭。Configure、SetLayout、SetMotionSettings、基类引用、Inspector/序列化恢复及 Prefab 导出均服从约束，不依赖使用者手动关闭开关。
- 继续显示真实牌面、观察者决定的正反面、Tint、强化/塑封/失效/禁用效果。保留输入投影中的交互数据，但没有手牌上浮、飞入/弃牌或音效；宿主不会自动为此类绑定鼠标。普通列表更新立即落位；显式跨区出牌由 CardZoneTransferPresenter 复用原视觉实例并飞入槽位。

基类新增 `CardCapacity`（默认不限制）与 `ValidateCardCount`，在 Present 修改视觉前检查；本地 CardZonePlayHost 在 Capture/BeginSession 替换原会话前检查，ReplaceCards 沿用 Present 的原子拒绝。该上限是出牌区展示契约，不改变通用 Core CardZone，也不替代引擎的出牌规则。

出牌区在实际玩法场景中配置，窗口不再创建出牌区或样例牌。工具读取当前 PlayedZoneView 的容量与实战数据；Scene Gizmo 保留五个槽位，空槽不创建占位模型。可用“导出 View Prefab…”保存表现配置，实例化后由玩法宿主提供数据。

## 手牌到出牌区的路径

场景组件 `CardZoneTransferPresenter` 引用源 `HandZoneView`、目标 `PlayedZoneView`、`CardZoneTransferSettings` 和可选的曲线控制点 Transform。它消费两区转移后的投影与本批 ViewId，不读取规则卡牌或鼠标；本地宿主的 `PlaySelected` 负责命令校验和两份列表的提交。

- 每批可打出 1–5 张，受目标剩余容量限制。数据仍按原手牌列表顺序追加，目标已有牌保留、手牌剩余牌自然重排。开始时记录实际视觉世界姿态，包含正在进行的上浮、弧形朝向、抽牌翻面和摆动。
- 同一个 `CardInstanceView` 立即转交目标牌区管理。数据已在目标列表中，`Transfer` 子节点暂时保持起点并逐帧飞入；不销毁重建、不克隆替身，也不会误触发抽牌、弃牌或选中音效。
- 第一段从左到右依次起飞，目标按本批 N 张的空槽逆序映射：第 i 张暂时飞向第 N−1−i 张的逻辑落点，形成交叉（A、B、C 暂时落成 C、B、A）。飞行默认 InOutSine 0.4 秒、间隔 0.06 秒，曲线中点沿目标 +Y 抬高 0.02 世界单位；间隔期间保持源世界姿态。指定 Transform 后以其提交时位置作为二次贝塞尔控制点。
- 第二段等待本批最后一张到达，停顿 `LandingPause`（默认 0.08 秒），全员用 `ReorderSeconds`（默认 0.3 秒）一起恢复真实列表位置。对换卡牌带轻微、错开的高度，避免同平面交叠；奇数批次中间牌原地等待。单张直接落位。旋转/缩放在飞行段恢复到目标姿态，可见性不因动画改变。
- 每张牌使用一条手动推进的 DOTween 时间轴覆盖起飞、等待和整理；逐张延迟与等待互补，使同批整理同时开始，大帧也可跨过所有阶段。旧场景缺少 `ReorderSeconds` 时，0 回退到 0.3 秒；旧场景的 `LandingPause=0` 表示全员到齐后立即整理。
- 目标负责时钟；重复投影、追加下一批不会重启已有飞行。重新布局/移动目标时交叉落点和最终落点都跟随当前槽位；布局先更新全部逻辑根，再解析视觉偏移，避免依赖后序卡牌的旧位置。`IsTransferring` 同时计入 `IsEntering` / `IsTransitioning`，飞行牌不接受鼠标命中。
- 两区需使用同一张牌 Prefab、正向等比世界缩放且无父级剪切；不同等比缩放和旋转可连续衔接。尚未渲染的排队抽牌不能出牌。无效配置/容量/投影在提交卡牌列表或修改原视觉前拒绝。
- `CompleteTransfers`、停用/销毁路径组件会直接落位；清空目标会释放它持有的飞行实例。中断动画不回滚已提交的卡牌归属。`animate:false` 或禁用出牌动画直接落位。

底层 `CardZoneView.Prepared` 在修改任一牌区前预校验两区布局、外观、材质及更新策略，暂存待创建实例；源更新排除本批转交实例的释放，目标导入相同实例。两区提交后才通知表现事件。`PresentTransfer(sourceAfter, targetAfter, ids, animate, commitData)` 的可选回调仅用于赋值已验证的宿主状态，不得调用外部事件或修改 View；正式对局可先在引擎中提交，再提供投影驱动动画。

验证窗口的 **出牌路径** 页只查看实战已有的手牌/出牌区绑定，调整飞行时间、间隔、弧高、控制点、落位停顿及整理时间，不创建目标、不重连路径、不提交出牌或手动推进飞行。实际出牌使用 Game UI；Scene Gizmo 继续显示交叉和整理路径。

新增资产：`Runtime/CardZoneTransferPresenter.cs`、`CardZoneTransferSettings.cs`、`CardInstanceView.Transfer.cs`、`CardZoneView.Prepared.cs`；验证页位于 `Editor/CardZoneValidationWindow.Transfer.cs`，测试位于 `Tests/Editor/CardZoneTransferTests.cs`，各有独立 .meta。运行时延续首包程序集/link.xml。单区 View Prefab 导出仍只保存展示配置，跨区场景引用由路径组件保存。

## 视觉实例是什么

CardZoneView 维护有序的 `CardInstanceView`，以及 `ViewId → CardInstanceView` 映射。每个 CardInstanceView 是一个运行时 GameObject，包装一份既有卡牌 Prefab 实例：

```text
HandZoneView : CardZoneView             通用区域布局 + 手牌抽取/弃牌位置及节奏
├── CardInstance / CardInstanceView     展示身份、目标圆弧位置与朝向
│   └── Transfer                       跨区飞行的临时世界姿态偏移
│       └── Transition                 飞入、飞出、让位、落位惯性的临时偏移
│           └── Face                   牌面正向补偿、正反面、选中偏移
│               └── Hover              悬停面积放大 20%
│                   └── DealFlip       抽牌/弃牌途中翻面的临时旋转
│                       └── Sway       独立 local Y 持续摆动
│                           └── Card   现有卡牌 Prefab 的克隆
│                               └── Surface / CardView  共享渲染资源、逐牌 Shader 参数
└── CardInstance / CardInstanceView
    └── ...
```

`CardInstance` 是纯规则数据；`CardInstanceView` 是场景对象；已有 `CardView.CardId` 是美术图集条目 ID。这三者没有隐式同名映射。两张相同牌面的实例牌各有独立 GameObject，继续共享 Mesh/Material，通过 MaterialPropertyBlock 独立显示牌面、颜色和特效。

## 输入与可见性

`CardVisualData` 包含 ViewId、可空 CardId、可空 FaceArtId 和 Tint。ViewId 在同一展示上下文内稳定，用于复用视觉对象；未知牌可以仅有不透明的展示 ID，不携带真实 CardInstanceId 或牌面。FaceArtId 为空时显示牌背，并将正面图集区域也重置为 card_back，避免复用已知牌时残留旧正面。揭示时可保持同一 ViewId，从而复用对象。

`CardZoneViewData` 是一个 ZoneId 加有序的 CardVisualData 列表，防御性复制输入并检查重复身份。`Present(CardZone, project)` 是本地/权威宿主的便捷适配入口：按区域顺序调用投影函数。投影负责解析图集 ID 和观察者权限；远端客户端应直接接收过滤后的展示数据。展示层不实现联网协议或可见性授权。

## 卡牌属性与本地状态

| 数据 | 来源 | 当前默认配置的表现 |
| --- | --- | --- |
| 花色、点数 | Core 卡牌定义，经宿主映射到图集 | 对应牌面 |
| 强化 | CardInstance.Effects.Enhanced | 开启火漆及样式配置的发光 |
| 塑封 | CardInstance.Effects.Laminated | 开启镭射，支持 HDR 颜色 |
| 失效 | CardInstance.Effects.Ineffective | 压暗牌面、关闭火漆发光并减弱镭射；保留强化/塑封标识 |
| 禁用 | CardInstance.IsDisabled | 显示红叉；不改变选中状态 |
| 观察者可见性 | 宿主提供给 FromCard 的 isVisible | 可见=正面，不可见=牌背 |
| 悬停、选中（待提交） | 本地 CardInteractionState | 悬停面积 +20%，无位移；选中 +Z 0.02，可叠加 |
| Tint、上浮偏移 | 展示配置 | 颜色仅为美术参数，不反推规则状态 |

`CardVisualData` 同时携带 Effects、IsDisabled 和 Interaction。`CardVisualData.FromCard(card, viewId, isVisible, resolveArt, interaction, tint)` 从规则卡牌和本地状态构建投影；隐藏时不调用牌面解析器，并清除真实 CardId、FaceArtId、Effects 和 IsDisabled，保留不透明 ViewId 与本地交互状态。解除隐藏时重新从权威卡牌读取属性。展示数据不是操作合法性的事实来源；即使直接构造含效果的隐藏 CardVisualData，外观解析器仍输出白色 Tint 和全部关闭的正面特效，统一显示普通牌背。

`CardInteractionState` 为独立不可变值，不属于 Core 或对局存档。WithHovered / WithSelected 返回新状态；IsRaised = IsHovered || IsSelected 保留为兼容的交互聚合标志，不再表示一定有位移；实际平移只由 IsSelected 决定，不增加 Dragging 或另一套 Pending 状态。上层提交交互可用同一选中集合驱动打出与丢弃，独立 CardInteraction 模块通过卡牌数据接口采集悬停/点击，本模块继续只消费投影，不提交玩法命令。验证模型禁止新增选中禁用牌；已选中的禁用牌可取消，失效牌仍可选中。

悬停通过独立 `Hover` 子节点将卡牌等比缩放至 `√1.2 ≈ 1.0954`，牌面面积扩大 20%，不移动布局根节点或 Face 节点。选中仍在 Face 节点沿牌区局部 +Z 上浮（默认 0.02），取消选中回到原位；两种状态独立叠加。鼠标离开只将大小恢复为 1，已选中牌保持上浮。旧 `SetInteractionOffsets(hovered, selected)` 接口继续校验参数，但 hovered 不再产生位移，`HoveredOffset` 返回零；旧场景保存的悬停偏移不会重新启用上浮，`SetRaisedOffset` 仅影响选中偏移。

两种交互共用 `CardZoneLiftSettings` 时长与惯性配置，各自持有 DOTween OutBack：默认放大/上浮 0.12 秒、缩小/回落 0.14 秒，时长包含回弹和回稳。`Overshoot` 范围 0–3，默认 2.2；选中上浮额外乘 1.35（上限 3），摆角与位移同步回稳归零。零强度关闭回弹。重复快照不重播，悬停变化不中断选中动画，快速反向从实际大小或位移接续。首次生成、静默恢复、动画关闭和停用精确落到目标。缩放不改变牌区顺序、布局间距或规则卡牌属性；射线命中跟随实际几何。普通 CardZoneView 和 PlayedZoneView 不应用手牌悬停缩放。

实现位于 `Runtime/CardInstanceView.Hover.cs` 及独立 .meta，沿用首包运行时程序集。层级为 `Face → Hover → DealFlip → Sway → Card`，缩放、选中位移、翻面和自转各自组合；跨区转移仍捕获完整世界姿态，弃牌保留起飞时的实际大小。

验证窗口“表现参数 → 动画”提供上浮/回落时长、惯性强度和快速惯性预设。Edit 参数支持 Undo；Play 只在实战允许交互且落位时修改，退出后 Unity 恢复。

```csharp
var interaction = new CardInteractionState(isHovered: true, isSelected: false);
interaction = interaction.WithSelected(true).WithHovered(false); // 离开后保持上浮
var visual = CardVisualData.FromCard(card, stableViewId, isVisible,
    definitionId => artByDefinition[definitionId], interaction);
```

## 美术组合与玩法映射

美术侧只提供 Shader 参数组合，玩法侧约定卡牌状态对应哪些组合：

- `CardArtProfile`（`Assets/CardArt/Profiles/StandardCardArt.asset`）：图集、基础组合 ID、命名的 `CardShaderCombination` 列表。每项参数只有名字、Float/Color/Vector 类型、值和覆盖/乘算方式，不包含强化、失效等游戏状态。
- `CardStateArtBindings`（`Assets/CardPresentation/Resources/StandardCardStateArt.asset`）：玩法配置。匹配所需/排除的 Effects 与禁用条件，所有匹配规则按列表顺序输出组合 ID。可以为某个组合状态指定独立美术组合，或叠加多个组合。它不定义 Shader 参数名和值。
- `CardMatchHost.ArtBindings`：战局配置引用；绑定/投影时传给观察者数据，交互和跨区转移保留该引用。Demo 已绑定默认映射；未显式选择的适配器使用默认资源。Run 用 `FromDeckEntry(..., artBindings: bindings)` 选择同类映射。

当前默认映射为强化 → `wax`、塑封 → `foil`、失效 → `dim`、禁用 → `cross`，依次叠加。这只是可编辑的默认资源：把强化映射为 `foil` 不需要修改美术配置、渲染器或 Shader。美术配置保留原手牌的 HDR 镭射颜色；基础组合将效果关闭，`dim` 通过乘算压暗和减弱强度。

```text
DeckEntry / CardInstance
  → CardVisualData（观察者可见的状态 + 玩法映射引用）
  → CardStateArtBindings（按顺序选择组合 ID）
  → CardArtProfile（合成通用 Shader 参数）
  → CardView / MaterialPropertyBlock，或 CardUIGraphic / 顶点通道
```

每次投影从基础组合重新合成，清空旧 MPB 后写入本次参数；移除状态、隐藏、复用和跨区不会累积上次效果。隐藏牌不匹配玩法状态，只使用基础组合。通用渲染器不判断效果或权限，Zone 只负责实例、顺序、布局和动画。

Shader 沿用原先的 Atlas UV、表面标识、颜色及 Instancing 最小采样约定，不强制火漆/镭射/红叉扩展。自定义参数必须在 Shader 中存在且类型正确，声明实例 buffer 并通过实例读取（PerRendererData 可作为 Inspector 标记）；组合编辑和导出检查这些条件。通用组合支持数值、HDR 颜色、向量；贴图与 Keyword 属于共享 Shader/Material 配置。Atlas 的 BaseMap、FrontUVRect、BackUVRect 由采样路径维护，不能由组合覆盖。共享 Mesh/Material 不按卡牌克隆。

当前 `CardUIGraphic` 是现有 Canvas Shader 的适配器，支持该 Shader 已定义的颜色、镭射、火漆、红叉顶点通道；其他参数会明确报错，接新 Canvas Shader 时扩展其通道适配器。场景 Shader 的自由实例参数不受这组 Canvas 通道限制。

美术入口 `Tools > 卡牌美术 > Shader 参数组合`（旧“属性表现”入口保留）：折叠区编辑参数名、类型、值及组合；“应用组合修改”校验并更新预览，“保存美术配置”保存资产。预览勾选组合 ID，按选择顺序叠加；左侧基础组合、右侧所选组合。独立相机渲染真实实例，支持主光/补光/环境亮度及主光方向，不依赖 Inspector Prefab 缩略图，关闭时清理预览场景。

玩法入口 `Tools > 玩法配置 > 卡牌状态美术映射`：编辑默认规则，检查任意效果/禁用组合得到的 ID 顺序。各玩法可以创建独立映射资产并给 CardMatchHost 配置。`Tools > 卡牌美术 > 接入当前玩法场景` 绑定现有美术及默认玩法配置；Prefab 导出保存美术配置引用。

`Tools > 测试面板` 提供“卡牌实例预览与光源”和“卡牌属性与共享美术”专项；不运行全量离线规则测试。完整 Run 牌组界面仍由 Run 宿主按需接入，Unity 配置引用不进入 Core 存档或远端协议。

## 使用

在目标 GameObject 添加 CardZoneView；需要手牌行为时添加 HandZoneView，给 Card Prefab 指定 `Assets/CardArt/Prefabs/Card.prefab` 或兼容的美术 Prefab，也可在代码中配置：

```csharp
// 初始化一次。Prefab 根应采用桌面卡牌约定：局部 XZ 平面，正面朝 +Y。
zoneView.Configure(cardPrefab, CardZoneLayoutSettings.Default);

// artByInstance 由玩法宿主生成，只含观察者获准看到的图集牌面。
zoneView.Present(cardZone, id =>
    new CardVisualData(id.Value, artByInstance[id], id));

// 后续接收新状态时继续 Present；相同 ViewId 会复用原 GameObject。
zoneView.Present(new CardZoneViewData(new ZoneId("opponent/hand"), new[] {
    new CardVisualData("opaque-slot-0"),
    new CardVisualData("opaque-slot-1")
}));
```

公共接口（标注“手牌”的成员仅在 HandZoneView 上提供）：

- `Present(data, animate = true)` / `Present(zone, project, animate = true)`：基类立即同步；手牌按列表变化编排过渡。手牌额外支持 `Present(data, discardedViewIds, animate = true)`、`Present(data, animate, discardedIds)` 和 `Present(zone, project, animate, discardedViewIds)`，显式标记本批弃牌。按稳定 ViewId 比较完整列表，新增牌依次飞入、已有牌平滑让位；本次移除中标记为弃牌的身份飞向弃牌堆，普通删除立即清理。数据/顺序/牌面/Shader 属性立即同步，不同 ZoneId 重新建立展示上下文。`animate: false` 用于首次恢复、读档、重连或撤销时直接落位。
- **手牌**：`DepartingViews`：已经从数据列表移除、仅等待离场动画结束的视觉实例；不参与布局或鼠标命中。
- **手牌**：`SetDiscardDestination(localPosition, anchor = null)` / `SetDiscardSettings(settings)`：设置弃牌终点与飞行时间、逐张间隔和抬高；可读取 `DiscardWorldPosition` / `DiscardSettings`。
- `Views`：当前列表顺序的只读视觉实例集合；`TryGetView(viewId, out view)` 查询当前实例。
- `SetLayout(settings)` / `RefreshLayout()`：重新计算位置与基础朝向，保留已有对象。
- `SetLayout(settings, motion, animate = true)`：一起校验布局与摆动需要的空间，再应用配置并调整动画目标；false 立即落位。
- `SetMotionSettings(settings)`：配置持续摆动；相同配置不会重启 Tween。
- **手牌**：`SetTransitionSettings(settings)`：设置补牌/让位参数；关闭时立即完成当前过渡，持续摆动独立。
- **手牌**：`SetDeckSource(localPosition, anchor = null)`：维护右侧牌堆局部位置，可选场景 Transform 优先。`DeckWorldPosition` 读取实际来源。
- `CompleteTransitions()` / `IsTransitioning`：立即完成全部过渡（释放飞出视觉），或查询是否仍有飞入/让位/飞出。单牌可读取 IsTransitioning / IsEntering / IsExiting / DisplayPose；DisplayPose 是牌区局部空间中的实际显示位置与未翻面的美术朝向，不含上浮/发牌翻面/持续摆动。单牌 `DealFlipAngle` 读取当前飞行翻面附加角度（抽牌 180° → 0°，弃牌 0° → 180°），`DealFlipRoot` 为独立视觉节点。
- `SetCameraFacing(enabled, camera = null)`：切换整区 +Y 朝向相机，未指定时使用 Camera.main；关闭恢复启用前的局部旋转。
- `RefreshCameraFacing()`：立即同步相机朝向；运行时 LateUpdate 自动调用，编辑器预览独立于摆动播放状态调用。
- `AdvanceMotion(deltaTime)`：只推进本牌区拥有的 Tween。运行时 Update 自动调用；编辑器预览按自己的时钟调用，运行时宿主无需额外调用。
- **手牌**：`SetLiftSettings(settings)` / `LiftSettings`：设置/读取上浮与回落动画；关闭时立即到达目标。单牌 `InteractionOffset` 返回实际牌区局部偏移，`IsLifting` 表示是否仍在过渡。
- **手牌**：`SetInteractionOffsets(hovered, selected)`：分别设置悬停/选中偏移；`HoveredOffset` / `SelectedOffset` 可读取。输入全部验证后统一应用，不修改顺序或卡牌数据。
- **手牌**：`SetRaisedOffset(offset)`：保留兼容便捷入口，把两档设为相同偏移；旧 `RaisedOffset` 属性对应选中偏移。
- `CardInstanceView.Appearance` 只读当前单牌渲染器的解析结果；美术设置通过 `CardArtProfile` 调整，跨区保留卡牌自己的配置。
- `Clear()`：释放本组件创建的实例并解除绑定；不删除其他场景子对象。
- `Configure(prefab, settings)`：初始配置或 Clear 后更换 Prefab。已有绑定时用 SetLayout 调整布局。

Prefab 应有一个已配置的 CardView 和对应 MeshFilter/MeshRenderer，不含 Rigidbody。测量使用真实 Mesh bounds 和子级变换，保留 Prefab 的根缩放；场景实例会将模型中心归零。隐藏牌使用真实翻面，同时清除未知正面的图集信息。原 Prefab 资源保持不变，克隆中的 Collider 关闭。

## 手牌：弃牌与虚拟弃牌堆

本次更新的 `discardedViewIds` 只说明移除原因，必须是旧快照中存在、新快照中消失的唯一身份。无关身份、重复身份、仍留在手牌中的身份或跨 ZoneId 的弃牌会在修改视觉前拒绝。普通删牌、清空、出牌等操作不会被误当作弃牌。实际命令由 `CardZonePlayHost.DiscardSelected()` 或玩法宿主执行，View 不读选择来决定删除谁，也不修改 Card/Core 数据。

弃牌提交后，`Data`、`Views` 和 `TryGetView` 立即反映新手牌列表；原视觉移到 `DepartingViews`，不再参与重排、Shader 刷新或鼠标命中。它从当时的实际位置、上浮高度、摆角与翻面角度接续 DOTween：按原手牌顺序错开起飞，InCubic 加速离开并沿抬高轨迹飞到弃牌点；可见牌使用 SmoothStep 翻到 180°，不可见牌保持牌背。上浮偏移与残余摆动在飞行中退到零。终点精确落在弃牌位置，到达即销毁，没有落地惯性或叠牌模型；每张开始飞出时播放独立配置的弃牌音效，默认使用与抽牌相同的占位资源；卡牌原有可见性、牌面、Tint 和规则效果不变。

默认终点为牌区局部 `(-0.38, 0.035, 0)`，粉红色 Gizmo 标识。可指定 `DiscardAnchor`，优先于局部位置；每批提交时采样终点，轨迹随整个牌区移动，锚点之后的移动影响下一批。`CardZoneDiscardSettings.Default` 为飞出 0.45 秒、间隔 0.08 秒、抬高 0.025；与抽牌共用“启用列表变化动画”总开关。

重复发布剩余列表不会重播或取消已有离场；连续弃牌可重叠，整手弃空后仍推进并清理动画。尚未起飞、不可见的排队入列视觉直接取消；已经飞入中的视觉从当前翻面角度接续飞出。重新加入同身份、静默恢复、换 ZoneId、CompleteTransitions、Clear、停用和销毁会清理相应离场视觉与私有 Tween，不影响其他牌区的动画。

弃牌使用 Game 内实际按钮，经玩法权威校验禁用牌、能量和次数。验证窗口观察结果；“表现参数 → 动画 → 弃牌 / 离场动画”调整终点与参数，“视觉诊断”查看飞出实例，不提交数据或生成编辑器副本。

## 手牌：从牌堆增加牌与列表同步

调用方使用 Core 的 Insert / InsertRange / Remove / RemoveRange / TransferTo 等接口得到新的不可变 CardZone，再提交 Present。CardZoneView 不抽牌、不修改源牌堆、也不生成实例牌身份；它按相同 ZoneId 下的稳定 ViewId 判断成员变化。相同牌面但不同身份仍是两张独立牌，牌背也按获准的投影显示。视觉实例被意外销毁后重新同步，只恢复该实例，不当作一次新增补牌。

```csharp
zoneView.SetDeckSource(new Vector3(0.38f, 0.035f, 0)); // 牌区右侧
// 或指定场景中的牌堆锚点：zoneView.SetDeckSource(Vector3.zero, deckTransform);
zoneView.Present(hand, project, animate: false);     // 恢复已有手牌
hand = hand.InsertRange(hand.Cards.Count, drawnIds); // 玩法层已确认抽到的身份
zoneView.Present(hand, project);                    // 只为新增身份生成飞入
hand = hand.Remove(discardedId);
zoneView.Present(hand, project, discardedViewIds: new[] { discardedId.Value }); // 弃牌飞出，其余牌补位
```

默认牌堆来源为局部 `(0.38, 0.035, 0)`；指定 DeckAnchor 时，在收到新增快照时把锚点世界位置转换为牌区坐标并采样。飞行轨迹随后随牌区整体移动/朝向；锚点后续移动作用于下一批新增牌。牌堆这里只是来源位置与橙色 Gizmo，不包含剩余牌数、牌堆规则或实际叠牌模型。

CardZoneTransitionSettings 默认启用：飞入 0.45 秒、逐张间隔 0.12 秒、惯性回稳 0.65 秒、已有牌让位 0.25 秒。飞行抬高 0.025，落点过冲 0.006，惯性 Y 摆角 3°。同批按新列表顺序错开发射；尚未轮到的牌暂不渲染，避免重合闪烁。新牌沿抬高轨迹减速入列，稍微越过落点后做一次主要回摆与微弱余摆，最终精确回到圆弧布局。飞行抬高采用正弦平方轨迹，回稳使用首尾斜率为零的衰减包络，飞入/回稳交界及结束时速度连续归零，避免突然收住或反向。

新增牌从发牌区背面朝上飞出。观察者可见的牌从生成时就绑定最终牌面、Tint 和 Shader 特效，独立 DealFlip 节点绕局部 Z 从 180° 平滑转到 0°；整个 FlightSeconds 期间使用 SmoothStep，发射和到达时角速度归零，落位惯性开始前翻正。不可见牌由原有 Face 节点维持牌背，DealFlip 始终为 0°，不会翻开；隐藏投影也不会解析真实牌面。

这里的背面朝上是临时视觉姿态，不修改 CardInstance、CardVisualData.FaceUp、效果、禁用、Tint 或本地交互状态，也不通过先隐藏后揭示来做动画。飞行中完整列表刷新不重播；追加、重排和布局变化从当前翻面角度接续，在剩余飞行时间内完成，排队牌等待时保持背面朝上。animate: false、CompleteTransitions、停用和销毁都会清理翻面偏移并恢复数据决定的最终朝向；恢复已有牌、普通排序/让位不重新翻牌。

数据与目标布局立即更新，Transition 子节点表现从旧位置到新位置的过渡。所有已有牌向新列表的最终槽位让位；动画期间不会缩放卡牌。飞行/惯性期间允许越出区域边界，落稳后恢复既有布局约束。普通编辑删除立即销毁对应实例；明确标记的弃牌先从当前集合移除，再完成下述离场动画。

相同列表重复 Present、改牌面/属性/翻面不会重新发牌或重启当前时间轴。目标变化时从实际显示位置接续，等待中的牌保留发射时刻；已在飞行的牌使用剩余飞行时间重新趋向目标，已在回稳的牌转为让位。连续多次补牌共享逐张间隔，Clear/切换 ZoneId/跳过动画会重置补牌排程。过渡按最新快照调整，不缓存过时的布局或命令。

过渡、上浮与持续摆动各有私有 DOTween ID，共用牌区 AdvanceMotion 时钟，只手动推进自有 Tween。暂停编辑器预览冻结三类动画；关闭持续摆动仍可飞入。停用组件/对象时立即完成过渡和上浮并清理 Tween，重新启用不会重播补牌；Clear、移除与销毁取消所属动画。运行时用 Time.deltaTime，编辑器预览用编辑器时钟。动画状态不进入 Core 或存档；读取存档使用 animate: false。

## 手牌：抽牌 / 弃牌 / 选中音效

默认音效为 `Resources/CardPresentation/Audio/CardDraw.wav`，来自用户指定的 Epic Stock Media - Card Game 2 / Card / `ESM_FX_card_deck_discard_dump_remove_reject_slide_flip_02.wav`。原始 WAV 按字节复制并重命名（双声道、96 kHz、24 bit，约 0.533 秒）；Unity 导入为 48 kHz PCM，Decompress On Load 并预加载，适合频繁播放的短音效。Resources 保证默认音效进入首包，不依赖 Editor 或 Downloads 路径。

`HandZoneView` 在每张新增牌飞入结束、进入牌列并开始惯性回稳时，消费一次入列信号，发出 `CardEnteredZone` 并播放抽牌音效；不等待整段回稳结束。默认首张在起飞 0.45 秒后触发，后续按各自飞行进度触发。原 `CardDealStarted` 保留起飞通知语义，但不再播放声音。正面牌与隐藏牌使用同一音效。刷新属性、重排、飞行中追加或改布局不会重播；入列前移除/弃掉、立即完成、停用或 `Present(..., animate: false)` 恢复不会补播。只关闭列表动画时，新增牌立即入列并逐张触发；音效可独立关闭。大帧跨过飞入与回稳仍保留一次入列提示。

弃牌在每张结束等待、实际开始飞出时发出一次 `CardDiscardStarted`，与抽牌的 `CardEnteredZone` 分开计数；两者默认使用同一占位 Clip，替换 Clip、音量、开关和播放通道各自独立。提示在手牌 Data/Views 更新后、离场实例销毁前消费；即使一帧跨过起飞和到达也按顺序触发。重复快照不重播，隐藏牌照常播放；等待中被取消、普通列表删除、立即完成或静默恢复不补播。仅关闭列表动画时，即时弃牌仍逐张触发。

选中音效由同一 ZoneId、同一 ViewId 的前后卡牌快照比较驱动：`Interaction.IsSelected` 从 false 变为 true 时，完整快照提交后发出一次 `CardSelected` 并播放选中音效。悬停、取消选中、排序、重复刷新、视觉修复不会重播；首次生成、带初始选中的新卡、换 ZoneId、清空后恢复及 `animate: false` 静默恢复不播放。仅关闭上浮或列表动画仍能播放；禁用点击未成功改变选中状态时无提示。选择音效消费卡牌状态，不直接监听鼠标事件，不增加卡牌或存档字段。

Play / Player 中每个牌区按需分别创建抽牌、弃牌与选中的临时 2D AudioSource，使用 `PlayOneShot` 允许连续操作的尾音重叠，不修改共享 AudioClip。关闭某一种音效或将其音量归零，只停止该通道，不截断其他音效；清空、停用牌区停止全部三个通道，销毁时一起清理。音源仍受 Unity 的 AudioListener、总音量与编辑器静音控制。音效及时间轴属于表现层，不新增卡牌或存档属性。

`SetDrawSound(clip, volume, enabled)` 仅配置抽牌，`SetDiscardSound(clip, volume, enabled)` 仅配置弃牌，`SetSelectionSound(clip, volume, enabled)` 仅配置选中。三组都提供 Enabled / Override / Sound / Volume 只读属性，例如 `SelectionSoundEnabled`、`SelectionSoundOverride`、`SelectionSound`、`SelectionSoundVolume`。音量为有限的 0–1，空 clip 使用默认资源，静音使用 enabled=false；三组配置互不覆盖，分别随 HandZoneView 场景组件序列化。“牌区样式 → 音效”分为“抽牌音效”、“弃牌音效”和“选中音效”，各自设置开关、替换音频和音量；进入 Play 后，“抽牌实验”页可指定牌面抽牌或从测试牌堆抽取；编辑模式仅预览动画。测试牌堆使用独立运行时副本，手牌通过 CardZonePlayHost.ReplaceCards 更新，鼠标交互继续读取同一份运行时数据，退出 Play 恢复原始手牌与测试牌堆。

## 轻弧形布局与持续摆动

Size.x / Size.y 分别对应局部 X / Z 的宽高，Transform 决定整组在世界中的位置与朝向，局部 +Y 是牌面朝向。Padding 为每侧留白，PreferredGap 为理想边缘间距，Alignment 为左/中/右对齐。卡牌尺寸来自 Prefab；开启摆动时会按最大角度保守预留旋转的 XZ 投影空间，区域至少须容纳一张牌及这份旋转空间。

`ArcHeight` 指定圆弧期望弓高，默认 0.008，正值让中间向 +Z 突出，负值反向，0 恢复直排。中心点在局部 XZ 中沿同一圆等角排列，每张牌的基础 local +Z 指向圆心，+Y 保持牌面法线。正弧高的圆心位于 −Z 一侧，因此中间牌布局根节点旋转约为 Y 180°；负弧高的圆心位于 +Z 一侧，中间牌约为 Y 0°。Face 子节点补偿布局带来的半圈旋转，牌面与直排保持相同阅读方向；弧高正负、牌数和可见性变化不会将牌面倒置。圆心朝向属于布局根节点，牌面朝上方向属于美术子节点。逐牌 Y 摆动继续独立叠加。

布局同时预留扇形旋转与最大摆动的完整卡面投影，空间不足时收窄排列、减小弧度，不扩大区域、不缩放卡牌。实际弓高受宽深、旋转空间和采样影响；曲线限制在半圆以内。空/单张及没有可用弧度空间时使用直排；两张牌的 Z 相同，但各自朝向同一圆心。`CardZoneRowLayout.CalculatePoses` 返回位置/旋转，旧 Calculate 仍返回对应位置。

牌数增加时先缩短间距，再重叠；完整卡面投影仍在可用矩形内，不自动缩放或裁剪。重叠时后面的列表元素略高，至少错开一个卡牌厚度，避免相交。选中组件可见外框及留白框 Gizmo。运行时修改配置后调用 SetLayout/RefreshLayout，首版不轮询 Inspector 或引擎变化。

`CardZoneMotionSettings` 默认开启 local Y 摆动，最大角度 1.5°，完整往返周期在 3.5–6.5 秒之间。每张牌按稳定 ViewId 与表现随机种子独立获得最大角度 65%–100% 的幅度、周期和初始相位；不使用 Unity 全局随机或引擎随机，不写入 CardInstance 或存档。选中时停止持续摆动，并在悬停 → 选中的选中上浮中，将 Sway 节点 local Y 角度平滑收回到零；高度与角度共用一个 DOTween OutBack 进度，轻微越过目标后同步回稳到选中高度和零角度，然后保持静止。取消选中后从实际角度恢复摆动，保留该身份对应的幅度/周期；中途反向也不跳角度。悬停本身不暂停摆动，上浮/回落和布局过渡照常推进。初始即选中的牌同样保持零角度，取消后才从零启动摆动。相同数据反复 Present、换牌面、重排和布局刷新不会重新随机或重启动画；改变摆动配置后重建，设 Enabled=false 或最大角度 0 停止并归零。

持续摆动使用 DOTween InOutSine / Yoyo，旋转仅写入 Sway 节点，Face 仍负责翻面与上浮，因此三种表现不会争写同一个 Transform。牌背也可保持同样的装饰摆动。运行时由牌区 Update 以 Time.deltaTime 推进，受游戏时间缩放影响；编辑器窗口按编辑器时钟推进自己的牌区，不使用全局 ManualUpdate/KillAll，不影响其他 Tween。

每张牌以私有 Tween ID 清理自己的动画，覆盖停用、移除、销毁、Clear、换配置和编辑器重载；恢复启用后按稳定初始相位重新启动。此处使用过滤 ID 的 DOTween.Kill，兼容已安装 1.3.030 中运行时初始化之前 Tween.Kill 在编辑模式下不执行清理的情况。

## 可选相机朝向

`FaceCamera` 默认关闭，`FacingCamera` 可指定 Camera；为空时使用带 MainCamera 标签的活动相机。开启后，牌区 Transform 的 +Y 指向相机位置，+Z 尽量沿相机屏幕上方；相机上方向与观察方向平行时改用相机 forward 作为参考。相机缺失或与牌区重合时保留当前旋转，后续有效时自动恢复跟随。

朝向在 LateUpdate 更新，编辑器验证窗口也会持续更新；暂停/关闭逐牌摆动、空牌区均不阻止朝向跟随。只改变整区旋转，保持位置、缩放、卡牌顺序、翻面、属性与 Tween 相位；关闭选项或停用组件/对象后恢复启用前的局部旋转。基础旋转作为隐藏序列化配置保留，可随组件重载恢复。父级建议采用正的均匀缩放，避免层级非均匀缩放造成 Mesh 剪切。

```csharp
zoneView.SetCameraFacing(true, playerCamera); // 对准本观察者的相机
zoneView.SetCameraFacing(false);             // 恢复原先的牌区朝向
```

## 生命周期与范围

相同 ZoneId/ViewId 的刷新复用 GameObject；离开的对象停用后销毁。关闭再开启区域 GameObject 会保留对象；Clear 或销毁组件会清理其拥有的对象。暂不维护已移除对象的缓存池，也不协调跨区对象移交。

HandZoneView 消费外部悬停/选中状态；独立 [CardInteraction](../CardInteraction/README.md) 模块提供鼠标射线命中与点击选中。交互状态写回每张本地卡牌后重新投影，当前仅准备待提交选择，不直接出牌或自动绑定对局；不设计拖拽。已有补牌飞入、落位惯性、让位/重排及持续摆动；上浮/回落也由独立 DOTween 处理；新增牌的翻面沿用飞入时间轴，其他外部可见性变更仍立即应用。调用者负责状态到达顺序。Core 的 CardZone 仍为权威成员/顺序来源，展示数据和场景对象不进入引擎存档。

## 手牌操作面板

`Prefabs/HandActionPanel.prefab` 是四按钮 Screen Space Camera UI：出牌、弃牌、点数理牌、花色理牌；当前占位色块和 Fusion Pixel 字体，默认位于左下角、两行排列、缩放 0.75。`Runtime/HandActionPanelView.cs` 只发送按钮请求和指示当前排序选择，不显示牌堆数字或“优先”状态行；`CardInteraction.HandActionPresenter` 持有手牌数据宿主和出牌路径引用并调用命令。UI 不更改视觉牌或持有规则数据，增减牌/理牌/音效仍由既有列表变化驱动。

`Editor/HandActionPanelSetup.cs` 的 Prepare Hand Action Panel Prefab 准备缺失 Prefab，重复执行保留美术调整；Configure Hand Action Buttons 固化当前手牌并绑定出牌路径与右下角抽牌堆图标。固化手牌时也自动安装，缺少 EventSystem 才创建，已有活动输入系统复用。仅按钮 Image 拦截射线，背景和文字不拦截。工具不自动保存当前场景。Prefab、字体、运行脚本与独立 .meta 沿用首包程序集及 link.xml；功能及牌堆接口见 [交互 README](../CardInteraction/README.md#game-中的手牌操作按钮)。

`Editor/CardUICameraSetup.cs` 为同场景 Base 相机创建或复用子对象 UI Camera，配置 `Assets/Settings/UIRenderer.asset` 并注册到当前 URP 的 Renderer 列表；原 Stack 的其他相机与主 Renderer 保留。按钮 Canvas 绑定该相机，悬停 Presenter 也设置 RenderingCamera，场景相机排除 UI 层。`Tools > Cards > Use Compact Hand Action UI Camera` 显式应用左下角紧凑布局并完成上述场景接入，保留 Prefab GUID 和美术配色。运行时投影同步、场景先放大及原生 UI 分辨率见 [渲染模块](../Rendering/README.md)。

右下角 `Prefabs/DrawPileIcon.prefab` 为独立 Screen Space Camera Canvas，八层 CardUIGraphic 复用当前牌组图集与共享材质，错位形成厚度；只有固定 Icon 命中面接收射线，不含文字。`Runtime/DrawPileView.SetRemainingCards` 只更新叠层和空堆暗色，`CardInteraction.DrawPilePresenter` 从局内 DeckCards 读取数量；编辑器仅配置启动快照，抽牌由玩法调用 DrawCards，不从图标发起抽牌；图标只负责悬停预览和点击固定牌组总览。图标与按钮共用 UI Camera，位置和资源说明见 [UI 资源](UI/README.md#右下角抽牌堆)。`Tools > Cards > Update Player Hand UI` 升级已有面板，保留 GUID 和颜色，用 FormerlySerializedAs 迁移原 Draw 引用并移除 Status 节点。

## 验证与首包

### 编辑器窗口

默认创建 `HandZoneView`。窗口顶部的“目标牌区”接受 CardZoneView 的任意场景子类，并显示实际类型；“跟随场景选择”自动匹配当前所选对象或其祖先牌区，支持非激活对象及嵌套牌区，不依赖固定对象名。关闭跟随后可固定检查当前目标。

手牌及其进一步派生类自动显示抽牌、弃牌、悬停缩放与选中上浮和三组音效；普通牌区保留卡牌列表、布局、相机、摆动、外观和诊断。切换目标读取各自的 CardZonePlayHost 卡牌快照及样式，保留 ZoneId、Kind 与 ControllerId，牌堆来源由绑定 Match/ZoneId 决定，共享堆按同一数据读取；进入/退出 Play 仍定位同一目标。未提供卡牌源且已绑定外部展示数据的牌区只读，不能从隐藏牌投影重建或覆盖真实卡牌。

`Tools > Cards > Card Zone Validator` 打开实战牌区验证窗口，跟随场景选择现有 CardZoneView。工具不创建牌区、不维护测试牌、不固化初始快照，也不提交出牌、弃牌、抽牌或排序。牌区、卡牌身份及归属由玩法权威管理。

| 页面 | 职责 |
| --- | --- |
| 实战卡牌 | Play 中直接读取外部玩法权威绑定的 CardZonePlayHost，查看当前区、抽牌堆与弃牌堆；身份、牌面、效果、选择与顺序只读 |
| 表现参数 | 修改现有 View 的布局、相机朝向、交互上浮、发牌/弃牌动画、摆动、音效及外观 |
| 视觉诊断 | 对照当前实例牌和对应视觉，查看实例 ID、位置、翻面与过渡状态，不重新投影或推进动画 |
| 出牌路径 | 查看既有源/目标并调整 TransferPresenter 的表现参数；不创建/重连路径或强制完成飞行 |

编辑模式只调展示组件，卡牌列表为空；不读取 initialZones 或旧窗口缓存来造牌。Play 需同时存在运行中的宿主与外部 ICardMatchCommands 权威，再直接查询当前牌局，不保存第二套牌列表。出牌、弃牌、排序用 Game 内 UI，发牌由玩法策略触发。窗口以 10 Hz 刷新观察结果，动画时间完全由运行时推进；关闭窗口、切换目标、Undo 或进入/退出 Play 均不清除视觉、不重建牌区、不改变牌局。

Edit 的参数修改支持 Undo、标记场景 dirty；Play 中只在允许交互且当前过渡结束后调整参数，退出后由 Unity 恢复。工具不自动保存场景。`Card Zone Animation Lab` 现在直接打开表现参数的动画页。旧造牌、指定抽牌、编辑测试牌堆、固化/预览出牌、手动时钟和创建牌区菜单已移除。

`Tools > 测试面板 > 实战验证 > 实战牌区窗口` 专项检查真实牌局读取、参数修改不触及卡牌状态、切换目标和关闭窗口保持视觉身份，报告 `Library/CardPresentationTests/live-validator.json`。直接打开 SingleBattleDemo 并进入 Play 即可，无需编辑器预置卡牌。

### 导出 View 设置为 Prefab

验证窗口顶部的“导出 View Prefab…”将当前目标牌区的展示配置保存为普通 Prefab，默认目录为 `Assets/CardPresentation/Prefabs`，默认名称使用实际子类名。适用于 CardZoneView、HandZoneView 及其进一步派生类；也可导出 Play 中调好的组件参数。导出读取组件当前值，不依赖或重写任何卡牌列表。完成后可点“定位 Prefab”。

- 保留实际组件类型、所有可序列化的展示设置、卡牌 Prefab 引用、布局/摆动/外观、上浮/飞入/弃牌参数，以及三组音效。资源继续共享，不复制材质或音频。
- 导出仅包含牌区展示组件。CardZonePlayHost、鼠标组件、测试手牌/弃牌、临时 CardInstanceView、音源及场景中的其他子对象不随之导出。实例化后由宿主调用 Present 提供真实数据。
- 抽牌与弃牌锚点按导出时的世界位置换算成牌区局部坐标，清空场景锚点引用。FaceCamera 开关保留，相机引用清空，实例使用 MainCamera，也可由新场景重新绑定。
- 根位置归零，保留本地缩放、Layer、启用状态及原本的本地旋转；如果相机正在驱动朝向，导出旋转使用驱动前缓存的原朝向，不保存当前跟随帧。
- 子类的资源引用与自身组件/根对象/Transform 引用保留或重映射；其他场景引用及未保存的临时资源在写入前报错，避免生成断链配置。
- 默认建议未占用的名称；覆盖需要确认，只接受相同具体子类的普通牌区 Prefab。覆盖会替换资源中的组件和子对象，但保留资源 GUID。单张卡牌、不同牌区子类、Prefab Variant 和其他资产不能被此入口覆盖。

实现位于 `Editor/CardZoneViewPrefabExporter.cs` 与 `Editor/CardZoneValidationWindow.Prefab.cs`。导出在隔离 Preview Scene 中复制组件并校验，不连接或替换原场景实例，不保存整个场景。导出的 Prefab 只依赖运行时组件；由实际场景或资源加载配置引用后纳入构建，导出器本身留在 Editor。

### 自动测试与构建

运行时由独立 asmdef 和 link.xml 纳入 Player，asmdef 显式引用 DOTween.dll；`CardPresentation.Editor` 与测试程序集仅在编辑器中使用，不进入 Player。Editor 测试主要使用隔离 Preview Scene 和现有美术 Prefab；生命周期测试使用当前场景内临时 DontSave 根对象验证真实停用/销毁回调，完成后销毁，不保存或替换当前场景。

`Tools > 测试面板 > 编辑模式测试 > 更多模块 / 全量回归 > 卡牌表现全模块` 执行本模块同步 NUnit 测试，报告写入 `Library/CardPresentationTests/status.json` 与 `results.xml`。该入口复用已有轻量运行器，完整 Unity Test Runner 也能发现此测试程序集。共 162 项测试，覆盖实例身份、增删重排、未知牌揭示/恢复牌背、资产共享、布局约束、生命周期、打包元数据，以及属性投影、本地上浮状态转换、无累积位移和编辑器样例序列化。Shader 测试读取真实 Prefab 实例的 MaterialPropertyBlock，检查全部 16 种效果/禁用组合、完整重置、HDR 参数、样式刷新、非法配置与缺失材质能力/贴图的原子拒绝，以及共享资产未被改写。编辑器属性测试另覆盖同牌面的独立身份修改、重排后编辑、隐藏时修改再揭示、清除状态、非法属性拒绝、序列化保留，以及全部 52 张标准定义与真实图集的映射。

动效测试覆盖牌面阅读方向在正负弧高/直排/单牌/翻面间保持一致、正负弧高、同圆等半径及 +Z 朝圆心、旋转后对齐边界、奇偶/空/单牌/密集牌列、极限角度下实际 Mesh 顶点边界、随机数隔离、刷新与重排相位连续、翻面/选中组合、停用/销毁/Tween 清理及外部手动 Tween 不受影响。相机测试覆盖空牌区/关闭摆动时跟随、父级变换、恢复原朝向、退化观察方向、禁用/启用，以及牌面/属性/实例/Tween 相位不受影响。测试按帧手动推进同一运行时动效入口，未执行完整 Player/IL2CPP 构建。

补牌测试覆盖逐张时序、落位过冲与衰减回稳、飞入/回稳交界及结束时的位移与转动速度、重复快照不重播、飞行中重排/追加/删除、已有牌让位、场景锚点坐标转换、Core 列表更新与隐藏牌投影、立即恢复、无效输入不消耗补牌时隙、停用/销毁/清理及外部 Tween 隔离。原有即时布局测试显式关闭过渡，继续验证目标布局与属性契约。

动画实验另有 4 项测试，覆盖 Core 牌顶顺序与身份/属性保留、两个 Zone 和抽牌记录的序列化恢复、非法抽牌/重建的原子拒绝，以及暂停抽牌 → Present → 单步推进 → 飞行中追加 → 完成立即落位的完整链路。推进动画期间，Core 列表保持不变。

排序测试覆盖固有定义到有序列表、同牌面稳定性、王牌美术样例归尾、隐藏/选中/效果保留及序列化；表现测试验证两种排序复用原有视觉实例，重排中再次排序保留当前姿态且不重新飞入。

鼠标交互新增 7 项测试，验证卡牌状态 → 投影 → +Z 偏移的链路、悬停命中稳定性、重叠优先级、禁用/失效选择、跨牌松开/失焦取消、排序后选择保留、移除后的旧点击、旋转缩放/牌背、入列屏蔽和运行时解绑。首包检查覆盖独立 CardInteraction.Runtime 与 link.xml，以及两个模块的资源元数据。

上浮与 Play 固化另有 8 项测试，覆盖上浮/回落的 OutBack 惯性与精确终点、回弹阶段反向、悬停缩放与选中位移独立叠加、快速反向、选中高度保持和重复目标不重播、中途反向、弧形/翻面/变换下局部 Z 保持、立即恢复、Tween 生命周期与外部动画隔离，以及初始卡牌序列化、运行时副本独立、重复启动、非法快照原子拒绝和验证窗口重新载入。

选中归零回归逐帧对照高度与摆角的相同插值进度，验证选中上浮起点不跳变、重复快照不重播、高度与摆角同步越过目标并回稳到选中高度/零角度，以及中途取消/再选中的姿态连续性。

发牌翻面新增 2 项回归，验证实际卡面法线从背向正、飞行中点/终点、不可见牌全程朝背、卡牌/投影引用与 Shader 属性全程一致，以及飞行中重排/追加/换弧度的角度连续性和排队时序。既有恢复/停用/销毁回归同时检查翻面偏移清理。

抽牌音效另有 3 项回归，覆盖飞行中静默、飞入结束逐张触发、开始回稳即提示与隐藏牌、快照提交顺序、重复刷新/重排/飞行中追加不重播、取消与生命周期、静默恢复、关闭动画的即时入列提示，以及首包默认音频加载、序列化配置与非法音量拒绝。

Play 抽牌另有 1 项集成回归，验证列表新增驱动三次音效提示、保留已有视觉身份和鼠标状态、非法重复身份拒绝，以及重启恢复初始手牌时不补播。

列表编辑与任意抽牌新增 6 项回归：任意插入/复制/移动后的身份与属性独立、非法编辑原子拒绝、运行时悬停/来源保留、从牌堆任意身份向任意位置转移、空牌堆指定生成及历史记录序列化，以及非法指定抽牌保持两个列表和既有记录不变。

弃牌新增 9 项回归覆盖：有序跨区转移与逐牌数据保留、禁用整批拒绝、失效允许、空选择、实际上浮/摆动连续性、翻面与隐藏牌、到达锚点即释放、连续弃牌和整手弃空、发牌途中取消、防重播、生命周期清理、非法配置和初始弃牌快照恢复。

弃牌音效新增 2 项回归，覆盖逐张延迟、隐藏牌、数据提交后触发、长帧跨过起飞/到达、重复刷新防重播、取消/静默恢复、关闭动画仍提示、禁用拒绝和编辑模式不创建音源；发牌途中弃牌的既有回归补验独立音效次数。

独立音效配置另有 1 项回归，覆盖默认占位资源共用、独立替换/开关/音量、两组配置的序列化恢复、单组恢复默认不覆盖另一组，以及非法弃牌音量的原子拒绝。

选中音效新增 3 项回归，覆盖鼠标成功选中/取消/重选、禁用与失效、点击中断、数据快照的选择边沿、防刷新/重排/恢复重播，以及第三组音效配置的独立性、序列化与非法值拒绝。

牌区派生新增 7 项回归：通用层同步与原子拒绝、基类调用的手牌动态分派与清理、宿主按类型绑定输入、任意命名/嵌套/非激活子类匹配、多目标卡牌/样式/归属隔离、外部隐藏投影保护，以及原 GUID 与继承字段序列化恢复。`Tests/Support` 仅提供可挂载的测试派生组件，使用 TestAssemblies 标记，普通 Player 构建不包含测试资产。

Prefab 导出新增 5 项回归，覆盖完整手牌参数/共享资源/虚拟锚点转换/原朝向、原场景与进行中动画不变、空牌区实例化后重新绑定数据、任意子类与自身引用、覆盖保留 GUID，以及非法路径/其他资产/场景依赖/无效参数写入前拒绝。测试资产在临时目录创建并于结束清理。

出牌区新增 6 项回归，覆盖 0–5 张时固定槽位与实例复用、基类/序列化入口强制直排静止及卡牌属性显示、超容量/过窄布局原子拒绝、宿主快照与会话保护、编辑器自动匹配/手牌隔离，以及出牌区 Prefab 导出后约束恢复。

抽牌入列时机回归同时覆盖飞行中和回稳中的目标重排、运行时批量新增遇到大帧仍每张一次，以及飞入途中直接弃牌只触发弃牌音效。起飞通知 CardDealStarted 保留给外部订阅方，三组音效配置保持独立。

测试牌堆延续新增 3 项回归：编辑配置到 Play 副本的顺序/身份/属性保留，抽牌消耗与编辑状态隔离，序列化后继续消耗、重复会话重置，以及多牌区/空堆和窗口目标引用恢复。

牌顶显示新增 3 项回归：Edit 与 Play 副本中连续单抽和批量抽取均对应显示列表的前 N 张，并保留身份/属性；上下移动、指定位置、复制、插入牌顶/牌底、删除及稳定排序均正确映射显示序号，手牌保持自然顺序。

悬停缩放回归覆盖面积精确为 1.2、无悬停位移、放大/缩小回弹、中途反向与重复投影、选中上浮不被鼠标进出重启、旧偏移兼容、牌背/重排无累积缩放、普通牌区不缩放，以及扩大后的边缘命中与离开还原。

手牌操作共 7 项回归，覆盖出/弃/两种理牌 Button 事件到卡牌数据与跨区动画、自动抽牌后按最后选择排序、牌堆耗尽与重启恢复、坏牌面/重复身份原子拒绝、禁用/失效权限、反复绑定/停用/缺少路径、五张容量及过期调用，还有 Prefab 输入边界与场景绑定复用。抽牌堆另有 3 项回归，覆盖右下角图集叠层/固定输入区域及剩余数字、运行副本消耗/重启/注册复用，以及空堆暗色/叠层和非法数量拒绝；另覆盖封面与惯性缩放、预览离开图标即关闭、点击保持和返回关闭。

UI Camera 新增 5 项回归，覆盖独立 Forward Renderer 的精简配置、重复注册和保留其他 Overlay、透视/正交投影同步及销毁清理、左下角两行 Prefab、主场景复古后处理先于 UI 执行，以及主相机排除 UI 后悬停提示仍由 UI Camera 显示。

`Tools > 测试面板 > 实战验证 > 旧验证场景 > 旧场景手牌按钮` 可在已有手牌和出牌路径的场景中检查 Play 反序列化后的出/弃/两种理牌事件绑定，报告写入 `Library/CardPresentationTests/hand-actions-play.json`；检查后完成转移并恢复两区启动数据与牌堆。此检查不注入系统鼠标，真实鼠标输入需在 Game 手动验证。

所有 Editor 工具源码显式使用 `#if UNITY_EDITOR`；运行目录内的 Editor partial（启动配置/迁移/Debug 编辑）也整文件限定。Player 的抽牌、出/弃、排序调用正式 Match 命令。`CardZoneView.PresentCommitted` / `HandZoneView.PresentDiscardCommitted` 允许先验证并准备视觉，再回调提交 Match，最后提交并通知视觉；跨区动画继续复用既有 TransferPresenter，不要求引擎提供动画时长。

## Canvas 牌组总览

原生 UI 卡牌由 CardUIGraphic / CardUIItemView 消费 CardVisualData，DeckOverviewView 组合四行与剩余统计。它是另一种表现适配器，不继承带 MeshRenderer 的场景 CardZoneView；四行也不创建逻辑 Zone。共享规则属性、CardStateArtBindings、参数组合、图集及美术纹理；Shader 的采样/镭射/火漆坐标数学共用 HLSL，渲染分别使用 Canvas 与场景 Deferred。行宽固定、间距随牌数变化，浅圆弧直接共用手牌 CardZoneRowLayout 并将 XZ 转为 XY，保持正立与绘制顺序重叠；左侧分类/逐花色/逐点数统计独立于牌区排列。四行共同挂在 SuitRows RectTransform 下，stretch anchors 分配四个等高槽位，默认纵向重叠 12%，VerticalOverlap / SetVerticalOverlap 可调；调整共同父级大小会在 Canvas 渲染前自动重排与适配卡牌尺寸，Editor 和 Play 都保留视觉身份/悬停，不重新投影 Match。

逐牌 idle 保留独立 local Y 摆动并附加轻微平面摆角；悬停时所在行一起置顶，退出恢复花色顺序。UI Camera 保留 Stack，在场景 540p 解析放大之后按输出分辨率绘制，不再继承低分辨率附件。

总览悬停复用场景 CardHoverPresenter 与既有 CardSuitChipsTooltip，以 Canvas 卡牌为锚点放在左侧。DeckOverviewPresenter 实现 ICardHoverSource / ICardHoverScope，按观察者投影提供花色、点数、基础筹码；页面打开时独占提示，关闭恢复手牌。同一实例切换 Canvas / World Space 父级，不复制 Prefab 或计算最终分数。

DeckOverviewData 明确区分 IsRemaining 与规则效果，按规则定义计算花色及数字/JQK/A 统计，同牌面多份按身份分别展示。CardUIItemView 持有本页逐牌悬停，不从原投影携带手牌选中。可见性、未知定义与美术缺失边界均在投影/提交时校验；坏牌面不替换已有视图。详见 [UI 资源与接入](UI/README.md#原生-canvas-牌组总览)。

新增回归验证原生 Canvas/共享资源、全局余量与规则状态、完整图集区域/HDR 顶点参数、隐藏信息过滤、独立悬停/摆动/复用、坏牌面原子拒绝、重载孤儿清理及 Match/成员范围同步；另验证固定行宽的多牌数圆弧边界、正立/重叠、无效布局原子拒绝、分类与点数余量及图标导入。Play 菜单 `Tools > 测试面板 > 实战验证 > 旧验证场景 > 旧场景牌库交互` 验证事件、抽牌后的统计/身份复用和返回关闭，恢复测试前对局，报告写入 Library/DeckOverviewValidation/play.json；不注入系统鼠标。

牌组总览补充回归覆盖纵向重叠、悬停跨行置顶与恢复、逐牌独立持续摆动，以及 1280×720 / 1920×1080 / 800×450 原生 UI 输出、普通 Overlay 共存、单像素条纹与 UI 停用后的场景保留。

牌堆图标、牌组面板和共用筹码提示都使用独立 DOTween：图标惯性缩放，总览 Frame 从下方快速上移回稳，提示以显隐倍率放大出现/缩小消失。详细参数、当前图集/指定顶层牌和预览固定行为见 UI README；播放验证覆盖实际事件、上移回弹、反向与原对局不变。

牌堆悬停改用 DeckSummaryView 原生统计片段，上方十三列点数余量，下方四行花色交叉余量及花色总数；0 压暗，隐藏/未知仅汇总。点击仍进入完整 DeckOverview。两者共用 DeckOverviewData 与 Presenter.ReadData，PixelPanelGraphic 生成阶梯边框并沿用 Canvas 默认材质，无新增纹理。详见 UI README 的“牌堆悬停统计表”；全部运行脚本、Prefab、字体引用和 .meta 沿现有 Runtime/link.xml 进入首包。


### 选牌上限与无法选中反馈

HandZoneView 默认最多选中 5 张，由 `SetMaxSelectedCards` 调整；此限制属于本地交互，正式提交仍使用玩法的权威校验。无法选中新牌共用独立 DOTween 反馈：默认沿局部 +Z 上升 0.004，0.08 秒到峰值、0.18 秒回弹归零，期间不改变选中状态，也不锁定正常手牌操作。`SetSelectionDeniedAnimation` 可调整偏移和时长，重复点击不累加高度，悬停/重排刷新不中断，成功选中或生命周期清理后归位。

`SetSelectionDeniedSound(clip, volume, enabled)` 与成功选中音效独立，默认空 Clip 静默。拒绝事件为 `CardSelectionDenied`；音效只在 Play/Player 播放，空槽位不创建声源或加载占位音效。实战牌区工具的交互/音效页及 Inspector 均可编辑，View Prefab 导出保留并校验全部新设置。

取消选牌音效通过 `SetDeselectionSound(clip, volume, enabled)` 独立配置，默认空 Clip、音量 1、开关开启；空 Clip 静默，不使用占位音效。实战牌区工具「表现参数 > 音效」与 Inspector 均提供「取消选牌音效」，支持配置读回、序列化和 View Prefab 导出。仅同一牌区内保留的卡牌由选中变为未选中时，在快照提交后逐牌触发 `CardDeselected`；重复刷新、悬停、重排、出牌、弃牌、静默恢复及重启会话不触发。实际音频只在 Play/Player 播放，使用独立 AudioSource，停用/清理时与其他音效一起停止。

「表现参数 > 动画」单独展示「理牌 / 让位动画」：`TransitionSettings.ReflowSeconds` 默认 0.25 秒，为包含过冲及回稳的总时长；`ReflowOvershoot` 为 0–3 的惯性强度，默认 1.25，使用 OutBack，0 则保持原有 OutCubic 减速。点数理牌、花色理牌及增减牌后的让位共用此配置，位移和转动按同一进度过冲，幅度随实际移动距离等比变化；重复快照不重播，中途重排从当前姿态继续。已有配置若强度为 0，可点击「恢复默认理牌动画」同时恢复时长与惯性。补牌的飞入、逐张间隔和回稳参数独立。补牌、理牌、弃牌共用页首的列表动画总开关，上浮/悬停使用独立交互动画配置。

理牌 / 让位音效通过 `SetReflowSound(clip, volume, enabled)` 独立配置，默认空 Clip、音量 1、开关开启，空 Clip 静默。动画区和音效页共用设置，支持 Inspector、序列化及 View Prefab 导出。快照提交后，已有卡牌实际开始理牌 / 让位时整批触发一次 `ReflowStarted`；重复快照、静默恢复、只有新牌飞入、关闭列表动画时不触发。实际音频仅在 Play/Player 播放。

### 手机与手柄触觉反馈

`HandZoneView.HapticSettings` 与表现编辑器「音效 > 手机 / 手柄震动」配置设备震动，默认开启，手柄时长 0.045 秒、双马达强度 0.25。配置会序列化并导出至 View Prefab。

原则是一次 `AudioSource.PlayOneShot` 对应一次触觉请求：抽牌入列、弃牌、选中、取消选中、无法选中、理牌 / 让位共用入口。Clip 为空、音效关闭、音效音量为 0、编辑模式、静默恢复及未触发音效的重复快照不震动。理牌音效整批一次，因此震动也整批一次；不同音效即使同帧也各自发请求。连续的手柄请求从当前时刻重新发送短脉冲，不排队延迟；手柄硬件可能把重叠脉冲感知为连续触感。

项目已安装 `com.unity.inputsystem` 1.6.1，Active Input Handling 使用 Both，保留现有旧输入。优先使用 `Gamepad.current` 的双马达反馈；没有当前手柄时，Android 使用 `View.performHapticFeedback(KEYBOARD_TAP)`，iOS 使用轻触 `UIImpactFeedbackGenerator`。手机反馈的时长和强度由系统控制，Android 尊重系统触觉开关且不需要 VIBRATE 权限。无设备支持时不阻断卡牌操作。手柄计时使用实际时间，Time.timeScale 为 0 也会停止；牌区清理、停用、应用暂停、失焦和退出会停止本模块发出的持续震动。编辑器只能验证已连接手柄，手机触感须真机验证。

参考：[Unity Input System 安装](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.6/manual/Installation.html)、[Gamepad 双马达反馈](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.6/manual/Gamepad.html)、[Android 系统触觉反馈](https://developer.android.com/develop/ui/views/haptics/haptic-feedback)、[Apple UIImpactFeedbackGenerator](https://developer.apple.com/documentation/uikit/uiimpactfeedbackgenerator)。
