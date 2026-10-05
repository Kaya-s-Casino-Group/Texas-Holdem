# 牌区展示、手牌与出牌区

`CardPresentation.Runtime` 为 Unity 展示模块，引用 `PokerEngine.Core`、`CardArt.Runtime` 与已安装的 `DOTween.dll`，不引用 Holdem、Draw、Runs 或 Abilities。提供牌区展示和跨区出牌路径，消费外部卡牌状态而不采集鼠标输入；属性立即同步，悬停/选中驱动上浮动画，列表变化驱动补牌、让位与弃牌动画，可附加持续的视觉摆动。使用既有卡牌 Prefab 和 CardView，不修改规则状态。运行时不自动创建场景牌区；编辑器工具可显式创建测试牌区。

## 通用牌区与派生边界

- `CardZoneView`：通用基类，负责按 ZoneId/ViewId 同步有序视觉实例、外观、可见性、圆弧/直排布局、相机朝向与持续摆动。直接使用时，列表增删重排立即落位，不附带手牌飞入/弃牌/上浮和音效。
- `HandZoneView : CardZoneView`：手牌表现。负责两档上浮、抽牌及翻面/惯性、重排过渡、虚拟弃牌终点及离场动画、抽牌/弃牌/选中三组独立音效。类未封闭，可继续派生。
- `PlayedZoneView : CardZoneView`：出牌表现，最多五张卡牌，五个固定直排槽位，弧度与持续摆动强制关闭；不继承手牌行为。
- Core `CardZone` 继续共用有序数据模型，通过 `Kind` 区分含义；`DefaultZoneKind` 为验证工具提供默认 Kind（手牌为 `hand`、出牌区为 `played`），不替代引擎规则校验。
- 弃牌区只有 Core 列表与逐牌数据，无 `DiscardZoneView`。离场视觉由手牌临时持有，到达虚拟终点即释放。
- 输入模块继续独立；`CardZonePlayHost` 自动为 `HandZoneView` 及其子类绑定鼠标，普通牌区仅恢复/更新显示。

各类共享实例同步流程，派生类通过每次更新的表现策略补充效果。基类引用调用 `Present`、`SetLayout`、`Clear` 或推进时钟时会使用实际子类行为。自定义子类可覆写 `CreatePresentationUpdate`、`CalculateCardPoses`、`ApplyLayoutPose`、`OnMotionAdvanced` 及生命周期方法；覆写生命周期时保留 `base` 调用，清理自身动画和资源。

资产位于 `Runtime/CardZoneView.cs`、`Runtime/HandZoneView.cs`、`Runtime/HandZoneView.Audio.cs` 、`Runtime/HandZoneView.Discard.cs` 和 `Runtime/PlayedZoneView.cs`，全部属于原 `CardPresentation.Runtime` 首包程序集。原手牌脚本的 GUID 随 `.meta` 移至 `HandZoneView.cs`，已有场景/Prefab 自动识别为手牌组件；共用字段保留原序列化字段名，布局、Prefab、相机和手牌参数均可恢复。新基类使用独立 GUID。验证窗口的子类匹配位于 `Editor/CardZoneValidationWindow.Targets.cs`。

## 出牌区：五个固定槽位

`PlayedZoneView` 直接继承通用 CardZoneView，共用卡牌实例复用、属性外观、可见性、相机朝向及资源管理。它只负责展示提交后的列表，出牌合法性、牌型/计分以及从手牌区转移卡牌由上层宿主处理。

- `SlotCount = 5`，`CardCapacity = 5`。支持 0–5 张真实牌，空位不生成占位 CardInstance/CardVisualData；超过五张整批拒绝，原数据和视觉实例保持不变。
- 始终先计算五个固定槽位，再按有序列表从左向右占用前 N 个。补牌或清空不会因实际数量改变槽位间距；删除/重排后按新的列表顺序重新占位。`GetSlotPoses()` 可读取五个局部位置与朝向。
- 所有槽位位于同一水平直线上，局部 Z 为零、Y 相同、布局旋转为 identity。保留宽度、留白、期望间距和整列对齐配置；可压缩间距，但不能重叠。可用宽度不足五张完整卡牌时拒绝配置。
- `SupportsArc` / `SupportsSway` 固定 false，弧高归零、持续摆动关闭。Configure、SetLayout、SetMotionSettings、基类引用、Inspector/序列化恢复及 Prefab 导出均服从约束，不依赖使用者手动关闭开关。
- 继续显示真实牌面、观察者决定的正反面、Tint、强化/塑封/失效/禁用效果。保留输入投影中的交互数据，但没有手牌上浮、飞入/弃牌或音效；宿主不会自动为此类绑定鼠标。普通列表更新立即落位；显式跨区出牌由 CardZoneTransferPresenter 复用原视觉实例并飞入槽位。

基类新增 `CardCapacity`（默认不限制）与 `ValidateCardCount`，在 Present 修改视觉前检查；本地 CardZonePlayHost 在 Capture/BeginSession 替换原会话前检查，ReplaceCards 沿用 Present 的原子拒绝。该上限是出牌区展示契约，不改变通用 Core CardZone，也不替代引擎的出牌规则。

验证窗口“创建出牌区 · 五个固定槽位”或 `Tools > Cards > Create Played Zone In Current Scene` 新建独立出牌区和五张基础样例牌，保留现有手牌对象。窗口显示 `0–5 / 5` 容量，满槽禁用新增/复制，禁用 8/12 张预设，隐藏不适用的弧度/摆动设置。Scene 选中出牌区时用 Gizmo 标出全部五个槽位；空槽没有游戏内占位模型。可沿用“导出 View Prefab…”保存配置，实例化后再由宿主提供卡牌数据。

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

验证窗口增加独立 **出牌路径** 页。选择手牌与出牌区，或点“新建空出牌区并连接”；调节飞行时间/间隔/弧高/控制点，以及全员落位停顿和整理时间；在手牌列表勾选待提交，再“打出选中的牌”。Play 中在 Game 选牌后使用相同按钮；编辑模式支持两区数据一起撤销、暂停和单步。`Tools > Cards > Configure Hand To Played Route` 可为选定手牌创建空目标并连接；Scene 选中路径组件时，绿色 Gizmo 展示交叉飞行，黄色线段展示落位后的整理方向。工具只标记场景修改，不自动保存。

新增资产：`Runtime/CardZoneTransferPresenter.cs`、`CardZoneTransferSettings.cs`、`CardInstanceView.Transfer.cs`、`CardZoneView.Prepared.cs`；验证页位于 `Editor/CardZoneValidationWindow.Transfer.cs`，测试位于 `Tests/Editor/CardZoneTransferTests.cs`，各有独立 .meta。运行时延续首包程序集/link.xml。单区 View Prefab 导出仍只保存展示配置，跨区场景引用由路径组件保存。

## 视觉实例是什么

CardZoneView 维护有序的 `CardInstanceView`，以及 `ViewId → CardInstanceView` 映射。每个 CardInstanceView 是一个运行时 GameObject，包装一份既有卡牌 Prefab 实例：

```text
HandZoneView : CardZoneView             通用区域布局 + 手牌抽取/弃牌位置及节奏
├── CardInstance / CardInstanceView     展示身份、目标圆弧位置与朝向
│   └── Transfer                       跨区飞行的临时世界姿态偏移
│       └── Transition                 飞入、飞出、让位、落位惯性的临时偏移
│           └── Face                       牌面正向补偿、正反面、悬停/选中偏移
│               └── DealFlip               抽牌/弃牌途中翻面的临时旋转
│                   └── Sway               独立 local Y 持续摆动
│                       └── Card           现有卡牌 Prefab 的克隆
│                           └── Surface / CardView  共享渲染资源、逐牌 Shader 参数
└── CardInstance / CardInstanceView
    └── ...
```

`CardInstance` 是纯规则数据；`CardInstanceView` 是场景对象；已有 `CardView.CardId` 是美术图集条目 ID。这三者没有隐式同名映射。两张相同牌面的实例牌各有独立 GameObject，继续共享 Mesh/Material，通过 MaterialPropertyBlock 独立显示牌面、颜色和特效。

## 输入与可见性

`CardVisualData` 包含 ViewId、可空 CardId、可空 FaceArtId 和 Tint。ViewId 在同一展示上下文内稳定，用于复用视觉对象；未知牌可以仅有不透明的展示 ID，不携带真实 CardInstanceId 或牌面。FaceArtId 为空时显示牌背，并将正面图集区域也重置为 card_back，避免复用已知牌时残留旧正面。揭示时可保持同一 ViewId，从而复用对象。

`CardZoneViewData` 是一个 ZoneId 加有序的 CardVisualData 列表，防御性复制输入并检查重复身份。`Present(CardZone, project)` 是本地/权威宿主的便捷适配入口：按区域顺序调用投影函数。投影负责解析图集 ID 和观察者权限；远端客户端应直接接收过滤后的展示数据。展示层不实现联网协议或可见性授权。

## 卡牌属性与本地状态

| 数据 | 来源 | 当前表现 |
| --- | --- | --- |
| 花色、点数 | Core 卡牌定义，经宿主映射到图集 | 对应牌面 |
| 强化 | CardInstance.Effects.Enhanced | 开启火漆及样式配置的发光 |
| 塑封 | CardInstance.Effects.Laminated | 开启镭射，支持 HDR 颜色 |
| 失效 | CardInstance.Effects.Ineffective | 压暗牌面、关闭火漆发光并减弱镭射；保留强化/塑封标识 |
| 禁用 | CardInstance.IsDisabled | 显示红叉；不改变选中状态 |
| 观察者可见性 | 宿主提供给 FromCard 的 isVisible | 可见=正面，不可见=牌背 |
| 悬停、选中（待提交） | 本地 CardInteractionState | 悬停 +Z 0.01；选中 +Z 0.02，选中优先 |
| Tint、上浮偏移 | 展示配置 | 颜色仅为美术参数，不反推规则状态 |

`CardVisualData` 同时携带 Effects、IsDisabled 和 Interaction。`CardVisualData.FromCard(card, viewId, isVisible, resolveArt, interaction, tint)` 从规则卡牌和本地状态构建投影；隐藏时不调用牌面解析器，并清除真实 CardId、FaceArtId、Effects 和 IsDisabled，保留不透明 ViewId 与本地交互状态。解除隐藏时重新从权威卡牌读取属性。展示数据不是操作合法性的事实来源；即使直接构造含效果的隐藏 CardVisualData，外观解析器仍输出白色 Tint 和全部关闭的正面特效，统一显示普通牌背。

`CardInteractionState` 为独立不可变值，不属于 Core 或对局存档。WithHovered / WithSelected 返回新状态；IsRaised = IsHovered || IsSelected，不增加 Dragging 或另一套 Pending 状态。上层提交交互可用同一选中集合驱动打出与丢弃，独立 CardInteraction 模块通过卡牌数据接口采集悬停/点击，本模块继续只消费投影，不提交玩法命令。验证模型禁止新增选中禁用牌；已选中的禁用牌可取消，失效牌仍可选中。

`HandZoneView.SetInteractionOffsets(hovered, selected)` 分别配置两档上浮偏移，默认悬停局部 +Z 0.01、选中 +Z 0.02；两种状态同时成立时使用选中偏移，不叠加。根 Transform 保持牌区布局位置，Face 子节点应用位置偏移和翻面旋转，反复刷新、重排、布局更新不会累积位移。偏移始终使用牌区局部坐标，卡牌绕 Y 朝向圆心时也不改变偏移方向。偏移允许超出基础布局矩形；区域矩形约束的是未上浮的牌列。由 DOTween OutBack 过渡，默认上浮 0.12 秒、回落 0.14 秒，均包含越过目标后的回稳。`CardZoneLiftSettings` 可配置开关、时长与 `Overshoot` 惯性强度（0–3，默认 2.2，最大越过量约为本次位移的 15%）；设为 0 时退化为无回弹的 OutCubic。上浮、回落和两档切换均使用 OutBack；选中上浮强度额外乘 1.35（上限 3），默认最大越过量约为本次位移的 25%，越过选中高度后回稳固定。基础强度为 0 时全部关闭回弹。悬停转选中继续上浮，取消选中且仍悬停则使用回落时间退到较低档；选中期间移入/移出鼠标不会改变目标或重播动画；中途反向从实际偏移接续，布局/翻面变化不改变 +Z 方向。首次生成或 `animate: false` 直接恢复目标位置。

验证窗口“牌区样式 → 动画”提供上浮/回落时长、惯性强度和“应用快速惯性”按钮；也可选中手牌区后使用 `Tools > Cards > Preview Snappy Lift`。旧场景保留已有时长，未序列化的惯性强度为 0，可用该预设一次应用新参数；操作支持撤销，场景保存由编辑器常规流程处理。

```csharp
var interaction = new CardInteractionState(isHovered: true, isSelected: false);
interaction = interaction.WithSelected(true).WithHovered(false); // 离开后保持上浮
var visual = CardVisualData.FromCard(card, stableViewId, isVisible,
    definitionId => artByDefinition[definitionId], interaction);
```

## 属性驱动 Shader

数据流为 `CardInstance → CardVisualData → CardAppearanceResolver → CardShaderAppearance → CardView / MaterialPropertyBlock`。CardZoneView 只维护顺序、实例和展示配置；效果开关完全由每张牌的属性解析。CardView 不判断强化、禁用或计分规则。

`CardAppearanceSettings` 只保存美术样式，不进入 Core 或引擎存档。默认镭射颜色白色、强度 1.2；火漆颜色 RGB(0.65, 0.08, 0.04)、发光 0.2；失效时牌面 RGB 乘 0.45，镭射强度乘 0.2，火漆发光归零。效果颜色忽略 Alpha，镭射 RGB 保留 HDR；强度与系数在入口检查范围并拒绝非有限值。压暗使用现有 Tint 乘色，不是真正的去色/灰度。

强化、塑封、失效与禁用可以叠加。失效不移除火漆或镭射标识，禁用红叉叠加在最上层。每次 Present 都重新生成完整 Shader 输入，包括关闭的开关和归零的参数，避免移除属性、翻面、实例复用或重复刷新后残留特效、累积压暗。外部直接调用 CardView 做的临时设置会被下一次 Present 覆盖。

```csharp
var style = CardAppearanceSettings.Default;
style.FoilColor = new Color(1.2f, 3.2f, 4f);
zoneView.SetAppearanceSettings(style); // 立即按当前卡牌属性刷新；不改共享材质
```

在更改任何视觉实例前，牌区会检查所有卡牌需要的 Shader 能力及贴图。要求强化时必须支持火漆并配置火漆贴图，非零发光需要发光接口；塑封需要镭射支持；禁用需要红叉支持及贴图。缺失时整次提交报错并保留原显示。火漆位置、尺寸、旋转、法线强度和镭射动画速度仍属于共享材质配置，此模块不修改它们，也不克隆逐牌材质。

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
- `SetAppearanceSettings(settings)`：按新样式重新解析当前牌区属性，刷新所有逐牌 Shader 参数；`CardInstanceView.Appearance` 可读取解析结果。
- `Clear()`：释放本组件创建的实例并解除绑定；不删除其他场景子对象。
- `Configure(prefab, settings)`：初始配置或 Clear 后更换 Prefab。已有绑定时用 SetLayout 调整布局。

Prefab 应有一个已配置的 CardView 和对应 MeshFilter/MeshRenderer，不含 Rigidbody。测量使用真实 Mesh bounds 和子级变换，保留 Prefab 的根缩放；场景实例会将模型中心归零。隐藏牌使用真实翻面，同时清除未知正面的图集信息。原 Prefab 资源保持不变，克隆中的 Collider 关闭。

## 手牌：弃牌与虚拟弃牌堆

本次更新的 `discardedViewIds` 只说明移除原因，必须是旧快照中存在、新快照中消失的唯一身份。无关身份、重复身份、仍留在手牌中的身份或跨 ZoneId 的弃牌会在修改视觉前拒绝。普通删牌、清空、出牌等操作不会被误当作弃牌。实际命令由 `CardZonePlayHost.DiscardSelected()` 或玩法宿主执行，View 不读选择来决定删除谁，也不修改 Card/Core 数据。

弃牌提交后，`Data`、`Views` 和 `TryGetView` 立即反映新手牌列表；原视觉移到 `DepartingViews`，不再参与重排、Shader 刷新或鼠标命中。它从当时的实际位置、上浮高度、摆角与翻面角度接续 DOTween：按原手牌顺序错开起飞，InCubic 加速离开并沿抬高轨迹飞到弃牌点；可见牌使用 SmoothStep 翻到 180°，不可见牌保持牌背。上浮偏移与残余摆动在飞行中退到零。终点精确落在弃牌位置，到达即销毁，没有落地惯性或叠牌模型；每张开始飞出时播放独立配置的弃牌音效，默认使用与抽牌相同的占位资源；卡牌原有可见性、牌面、Tint 和规则效果不变。

默认终点为牌区局部 `(-0.38, 0.035, 0)`，粉红色 Gizmo 标识。可指定 `DiscardAnchor`，优先于局部位置；每批提交时采样终点，轨迹随整个牌区移动，锚点之后的移动影响下一批。`CardZoneDiscardSettings.Default` 为飞出 0.45 秒、间隔 0.08 秒、抬高 0.025；与抽牌共用“启用列表变化动画”总开关。

重复发布剩余列表不会重播或取消已有离场；连续弃牌可重叠，整手弃空后仍推进并清理动画。尚未起飞、不可见的排队入列视觉直接取消；已经飞入中的视觉从当前翻面角度接续飞出。重新加入同身份、静默恢复、换 ZoneId、CompleteTransitions、Clear、停用和销毁会清理相应离场视觉与私有 Tween，不影响其他牌区的动画。

验证窗口“手牌列表”和“抽牌实验”页提供“弃掉选中”按钮；Play 中先在 Game 选牌，或在单牌数据面板设置待提交，再点按钮。禁用牌阻止整批提交，失效牌允许弃牌。编辑模式支持撤销/重做，Play 使用独立会话副本；“牌区样式 → 动画 → 弃牌 / 离场动画”配置终点与参数，“诊断”显示逻辑弃牌身份和飞出实例。

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

`CardZoneMotionSettings` 默认开启 local Y 摆动，最大角度 1.5°，完整往返周期在 3.5–6.5 秒之间。每张牌按稳定 ViewId 与表现随机种子独立获得最大角度 65%–100% 的幅度、周期和初始相位；不使用 Unity 全局随机或引擎随机，不写入 CardInstance 或存档。选中时停止持续摆动，并在悬停 → 选中的第二段上浮中，将 Sway 节点 local Y 角度平滑收回到零；高度与角度共用一个 DOTween OutBack 进度，轻微越过目标后同步回稳到选中高度和零角度，然后保持静止。取消选中后从实际角度恢复摆动，保留该身份对应的幅度/周期；中途反向也不跳角度。悬停本身不暂停摆动，上浮/回落和布局过渡照常推进。初始即选中的牌同样保持零角度，取消后才从零启动摆动。相同数据反复 Present、换牌面、重排和布局刷新不会重新随机或重启动画；改变摆动配置后重建，设 Enabled=false 或最大角度 0 停止并归零。

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

## 验证与首包

### 编辑器窗口

默认创建 `HandZoneView`。窗口顶部的“目标牌区”接受 CardZoneView 的任意场景子类，并显示实际类型；“跟随场景选择”自动匹配当前所选对象或其祖先牌区，支持非激活对象及嵌套牌区，不依赖固定对象名。关闭跟随后可固定检查当前目标。

手牌及其进一步派生类自动显示抽牌、弃牌、两档上浮和三组音效；普通牌区保留卡牌列表、布局、相机、摆动、外观和诊断。切换目标读取各自的 CardZonePlayHost 卡牌快照及样式，保留 ZoneId、Kind 与 ControllerId，测试牌堆按牌区保留；进入/退出 Play 仍定位同一目标。未提供卡牌源且已绑定外部展示数据的牌区只读，不能从隐藏牌投影重建或覆盖真实卡牌。

`Tools > Cards > Card Zone Validator` 打开统一验证窗口，`Create Card Zone In Current Scene` 在当前场景创建测试牌区。顶部固定显示编辑 / Play 状态、手牌/牌堆/视觉数量、定位与进入/退出 Play；四个页面可在小窗口折成两行，正文独立滚动。

| 页面 | 职责 |
| --- | --- |
| 手牌列表 | 从图集中指定牌面、数量和插入位置；有序列表内直接编辑每张卡牌的全部属性 |
| 抽牌实验 | 指定新牌直接抽取；或管理测试牌堆、从牌顶/任意实例牌抽取；预览动画时钟 |
| 牌区样式 | 布局 / 动画 / 音效 / 外观四个子页；包含相机朝向、两档上浮、飞入惯性、摆动和 Shader 样式 |
| 诊断 | Core → 投影 → Shader 对照、实例复用检查、最近一次抽牌列表及逐牌视觉状态 |

手牌与测试牌堆复用同一套列表编辑器，支持任意位置插入、修改牌面、复制、删除、上下移动、直接指定最终序号、反转、打乱、花色/点数排序、全部正面/牌背及清空。每行直接提供牌面下拉、正面（观察者可见）、强化、塑封、失效、禁用、悬停、选中和 Tint，行尾提供最终序号、上下移动、复制、清除效果、抽出（测试牌堆）及删除；无需先点选再到下方修改。表头与控件共用列坐标，窄窗口通过横向滚动查看完整一行。点击序号只标记当前编辑行，悬停可查看实例 ID，不等于游戏里的“选中待提交”。禁用牌不能新选中，但允许取消已有选中。所有修改仍经过既有 `Mutate` 与 `SetCardProperties → CardInstance` 数据链路，继续支持 Undo/Redo 和 Play 副本，不直接写 CardView。复制保留效果、禁用、可见性、Tint 和来源信息，生成新身份并清空悬停/待提交。

插入位置可选末尾或任意现有卡之前，移动使用从 1 开始的最终序号；底层继续用 Core 的零基索引。手牌和牌堆分别最多 64 张，同牌面允许多张。花色/点数排序使用 Core 规则，非标准美术样例归尾。5 张基础示例、12 张重叠示例、8 张 Shader 对照收进手牌页的“示例预设”折叠区，只有点击才替换列表。

抽牌页分为两种来源：

- **指定牌面**：选择图集中的任意牌、数量、可见性与手牌插入位置，生成独立实例，经临时来源 Zone 和 Core 转移操作进入手牌，不消耗测试牌堆；牌堆为空也可以使用。动画和音效仍由新增身份触发。
- **从测试牌堆抽取**：列表从牌顶到牌底显示，第一行标记“牌顶（下一张）”；抽 1 张取第一行，抽 N 张依次取前 N 行。也可点击任意列表行的“抽出”将该原始实例转移到指定手牌位置，保留其属性和身份。牌堆可任意编辑，支持重建 12 张测试牌或完整标准牌堆，不会自动补满。 新窗口、动画实验菜单默认打开此来源；“指定牌面”仍是显式生成新牌，不消耗测试牌堆。

测试牌堆通过 `Editor/CardZonePreviewList.cs`（含独立 .meta）映射显示序号与底层序号；上下移动、“移动到第几张”、“复制到后面”和插入位置都按屏幕顺序解释。花色/点数排序也按显示顺序生效，相同牌面及无点数图案保持稳定顺序。Core 与序列化仍以列表末尾为牌顶，不迁移既有牌堆数据；手牌仍按自然列表顺序显示。该适配器仅属于 Editor 程序集。

最近一次抽牌记录移到诊断页；牌堆之前/之后统一按牌顶到牌底显示。区分“指定生成 / 测试牌堆”，指定生成时牌堆前后列表保持不变。记录序列化后仍保留，后续编辑不会覆盖之前的抽牌对照。新增/复制/抽牌只通过数据列表驱动 View，重排复用原有视觉身份；期间仍可继续增删、移动及改属性。

编辑模式数据变更支持 Unity Undo/Redo，恢复时静默重建视觉并同步初始快照。进入 Play 后四页仍可操作：手牌来自 CardZonePlayHost 的运行时副本，测试牌堆由 `CardZoneDrawExperimentStore` 按牌区保留，进入 Play 时从编辑配置深复制顺序、实例身份、牌面及全部逐牌属性。Play 内抽取、改牌和清空只修改该副本，切换牌区后回来保留剩余牌，耗尽或主动清空不自动补牌；退出并再次进入 Play 重新复制编辑配置。编辑与 Play 两份牌堆及当前牌区标识均参与窗口序列化，场景引用短暂缺失时仍能找回对应状态。手牌统一经 `ReplaceCards` 提交，后续鼠标操作不会用旧列表覆盖新卡牌。实时导入保留悬停/选择和来源信息。Play 内编辑不写入初始快照，退出恢复编辑配置；运行时操作不加入编辑器 Undo。窗口关闭不影响正在运行的牌区。 测试牌堆存储位于 `Editor/CardZoneDrawExperimentStore.cs`（含独立 .meta），仅服务验证窗口，不向 Core、运行时宿主或 View Prefab 增加测试状态。

编辑模式可播放/暂停、调速及单步推进自己的动画；Play 跟随游戏时间，暂停使用 Unity 工具栏。音效只在 Play 中播放。立即完成过渡不会补播排队音效。所有样式集中在样式页，诊断不再混入常用操作。

`Tools > Cards > Card Zone Animation Lab` 打开抽牌页；`Deal Three Cards To Zone` 从测试牌顶抽取 3 张；`Preview Gentle Row Motion` 和 `Preview Soft Landing` 转到样式/动画页应用对应预设。`Bake Card Zone For Play` / `Preview Mouse Interaction` 兼容原菜单，固化当前编辑数据后打开手牌页；`Play Card Zone` 固化并进入 Play。

场景中的测试根对象为普通 Untagged 对象，保存具体 ZoneView 子类配置和 CardZonePlayHost 初始卡牌数据，临时卡面使用 DontSave。工具不自动保存整个场景；需保存场景以持久化配置。此处使用本地样例数据，不引入 Match 或网络存档契约。`CardZonePreviewCatalog` 显式映射 Core 的 52 张标准定义与图集，王等额外牌面仍标记为美术样例。

### 导出 View 设置为 Prefab

验证窗口顶部的“导出 View Prefab…”将当前目标牌区的展示配置保存为普通 Prefab，默认目录为 `Assets/CardPresentation/Prefabs`，默认名称使用实际子类名。适用于 CardZoneView、HandZoneView 及其进一步派生类；也可导出 Play 中调好的组件参数。导出读取组件当前值，不依赖或重写窗口里的卡牌列表。完成后可点“定位 Prefab”。

- 保留实际组件类型、所有可序列化的展示设置、卡牌 Prefab 引用、布局/摆动/外观、上浮/飞入/弃牌参数，以及三组音效。资源继续共享，不复制材质或音频。
- 导出仅包含牌区展示组件。CardZonePlayHost、鼠标组件、测试手牌/弃牌、临时 CardInstanceView、音源及场景中的其他子对象不随之导出。实例化后由宿主调用 Present 提供真实数据。
- 抽牌与弃牌锚点按导出时的世界位置换算成牌区局部坐标，清空场景锚点引用。FaceCamera 开关保留，相机引用清空，实例使用 MainCamera，也可由新场景重新绑定。
- 根位置归零，保留本地缩放、Layer、启用状态及原本的本地旋转；如果相机正在驱动朝向，导出旋转使用驱动前缓存的原朝向，不保存当前跟随帧。
- 子类的资源引用与自身组件/根对象/Transform 引用保留或重映射；其他场景引用及未保存的临时资源在写入前报错，避免生成断链配置。
- 默认建议未占用的名称；覆盖需要确认，只接受相同具体子类的普通牌区 Prefab。覆盖会替换资源中的组件和子对象，但保留资源 GUID。单张卡牌、不同牌区子类、Prefab Variant 和其他资产不能被此入口覆盖。

实现位于 `Editor/CardZoneViewPrefabExporter.cs` 与 `Editor/CardZoneValidationWindow.Prefab.cs`。导出在隔离 Preview Scene 中复制组件并校验，不连接或替换原场景实例，不保存整个场景。导出的 Prefab 只依赖运行时组件；由实际场景或资源加载配置引用后纳入构建，导出器本身留在 Editor。

### 自动测试与构建

运行时由独立 asmdef 和 link.xml 纳入 Player，asmdef 显式引用 DOTween.dll；`CardPresentation.Editor` 与测试程序集仅在编辑器中使用，不进入 Player。Editor 测试主要使用隔离 Preview Scene 和现有美术 Prefab；生命周期测试使用当前场景内临时 DontSave 根对象验证真实停用/销毁回调，完成后销毁，不保存或替换当前场景。

`Poker > Tests > Run Card Presentation In Current Scene` 执行本模块同步 NUnit 测试，报告写入 `Library/CardPresentationTests/status.json` 与 `results.xml`。该入口复用已有轻量运行器，完整 Unity Test Runner 也能发现此测试程序集。共 134 项测试，覆盖实例身份、增删重排、未知牌揭示/恢复牌背、资产共享、布局约束、生命周期、打包元数据，以及属性投影、本地上浮状态转换、无累积位移和编辑器样例序列化。Shader 测试读取真实 Prefab 实例的 MaterialPropertyBlock，检查全部 16 种效果/禁用组合、完整重置、HDR 参数、样式刷新、非法配置与缺失材质能力/贴图的原子拒绝，以及共享资产未被改写。编辑器属性测试另覆盖同牌面的独立身份修改、重排后编辑、隐藏时修改再揭示、清除状态、非法属性拒绝、序列化保留，以及全部 52 张标准定义与真实图集的映射。

动效测试覆盖牌面阅读方向在正负弧高/直排/单牌/翻面间保持一致、正负弧高、同圆等半径及 +Z 朝圆心、旋转后对齐边界、奇偶/空/单牌/密集牌列、极限角度下实际 Mesh 顶点边界、随机数隔离、刷新与重排相位连续、翻面/选中组合、停用/销毁/Tween 清理及外部手动 Tween 不受影响。相机测试覆盖空牌区/关闭摆动时跟随、父级变换、恢复原朝向、退化观察方向、禁用/启用，以及牌面/属性/实例/Tween 相位不受影响。测试按帧手动推进同一运行时动效入口，未执行完整 Player/IL2CPP 构建。

补牌测试覆盖逐张时序、落位过冲与衰减回稳、飞入/回稳交界及结束时的位移与转动速度、重复快照不重播、飞行中重排/追加/删除、已有牌让位、场景锚点坐标转换、Core 列表更新与隐藏牌投影、立即恢复、无效输入不消耗补牌时隙、停用/销毁/清理及外部 Tween 隔离。原有即时布局测试显式关闭过渡，继续验证目标布局与属性契约。

动画实验另有 4 项测试，覆盖 Core 牌顶顺序与身份/属性保留、两个 Zone 和抽牌记录的序列化恢复、非法抽牌/重建的原子拒绝，以及暂停抽牌 → Present → 单步推进 → 飞行中追加 → 完成立即落位的完整链路。推进动画期间，Core 列表保持不变。

排序测试覆盖固有定义到有序列表、同牌面稳定性、王牌美术样例归尾、隐藏/选中/效果保留及序列化；表现测试验证两种排序复用原有视觉实例，重排中再次排序保留当前姿态且不重新飞入。

鼠标交互新增 7 项测试，验证卡牌状态 → 投影 → +Z 偏移的链路、悬停命中稳定性、重叠优先级、禁用/失效选择、跨牌松开/失焦取消、排序后选择保留、移除后的旧点击、旋转缩放/牌背、入列屏蔽和运行时解绑。首包检查覆盖独立 CardInteraction.Runtime 与 link.xml，以及两个模块的资源元数据。

上浮与 Play 固化另有 8 项测试，覆盖上浮/回落的 OutBack 惯性与精确终点、回弹阶段反向、悬停低于选中、两档间平滑转换、选中优先和重复目标不重播、中途反向、弧形/翻面/变换下局部 Z 保持、立即恢复、Tween 生命周期与外部动画隔离，以及初始卡牌序列化、运行时副本独立、重复启动、非法快照原子拒绝和验证窗口重新载入。

选中归零回归逐帧对照高度与摆角的相同插值进度，验证第二段上浮起点不跳变、重复快照不重播、高度与摆角同步越过目标并回稳到选中高度/零角度，以及中途取消/再选中的姿态连续性。

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
