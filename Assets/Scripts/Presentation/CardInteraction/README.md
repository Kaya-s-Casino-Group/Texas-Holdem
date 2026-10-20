# 共用卡牌交互（CardInteraction）

本模块为主线 [卡牌战斗](../../Gameplay/SingleBattle/README.md) 与独立 [分数挑战](../../Gameplay/DrawBattle/README.md) 共用的输入和表现桥接层，不是第三种玩法。场景接入、界面预制体、输入诊断统一归 **工具 > 卡牌表现**；具体玩法接入分别位于 **工具 > 卡牌战斗** 和 **工具 > 分数挑战**。


`CardInteraction.Runtime` 是可选输入及本地交互适配模块，依赖 `CardPresentation.Runtime`、`CardArt.Runtime`、`PokerEngine.Core`、`PokerEngine.Scoring`、Unity UGUI、Input System 和 `UIFoundation.Runtime`。展示模块不反向引用输入模块。运行时程序集与 link.xml 进入首包；编辑器验证入口留在 `CardPresentation.Editor`。

Draw 实际玩法接入见 [Draw 战局桥接](../../Gameplay/DrawBattle/README.md)。外部权威模式下，CardMatchHost 仅保存展示镜像，ICardMatchCommands 将按钮提交给玩法；排序仅作用于本地显示，原编辑器/独立卡牌验证模式保持。

## 代码目录与程序集

卡牌数据与命令已独立到 [`CardMatch.Runtime`](../CardMatch/README.md)，命名空间保持 `CardInteraction`；本目录的具体牌区适配仍归 `CardInteraction.Runtime`。本模块适配场景牌区与 Canvas 界面；最简战斗 2D 验证可以使用自己的输入适配，无须引用本地牌桌宿主。

| Runtime 子目录 | 职责 |
| --- | --- |
| `Commands` | 独立牌桌使用的出牌/弃牌批次准备；不可变选牌请求与玩法命令/计分查询接口已移到 CardMatch.Runtime；正式玩法的规则推进仍由外部引擎负责 |
| `Input` | 鼠标/指针控制、射线命中、交互数据源与悬停数据源接口 |
| `Binding` | CardZonePlayHost、CardMatchPresentationAdapter 及旧牌区美术数据；负责具体视图绑定和美术投影 |
| `Presenters` | 手牌按钮、悬停提示、抽牌堆和牌库总览的界面桥接 |

`Runtime` 根目录的 asmdef 与 `link.xml` 保持原位。带 `.Editor.cs` 的宿主 partial 文件与各自运行时主体放在同一目录，仍通过 `UNITY_EDITOR` 隔离编辑器代码。旧牌区工具和测试继续位于 CardPresentation 的 Editor/Tests，通用宿主测试位于 CardMatch/Tests。移动脚本保留原 `.meta`；场景中原 Host 的美术字段已迁入同对象的 CardMatchPresentationAdapter。

## 命令与宿主边界

`ICardMatchCommands` 统一接收不可变的 `CardSelectionRequest`：`MatchId`、`ZoneId`、`ControllerId` 和有序卡牌 ID。请求复制输入，不持有 Host、View、Transform 或飞行路径；出牌和弃牌执行请求里的卡牌，不重新读取当时的 UI 选中集合。玩法接收方检查对局、玩家、源牌区、卡牌归属与现有规则，拒绝跨对局/跨玩家/已移走卡牌的请求。`animate` 只控制既有演出方式，不参与规则结果。

- `CardMatchHost` 管理整桌观察者状态、可见性、交互和原子提交。`CaptureSelection(zone)` 捕获输入；`Snapshot(zone)` / `ReadCards(zone)` 提供中立数据。美术、颜色和 `CardZoneViewData` 由 `CardMatchPresentationAdapter` 生成；旧 `GetCards` / `Project` 调用是该适配层的扩展方法。绑定外部 `Commands` 时，表状态是镜像，权威规则状态由玩法宿主持有；未绑定时保留独立牌桌的本地 Prepare/Commit 路径。
- `CardZonePlayHost` 管理单个 View 的绑定、输入生命周期与局部表现。`CaptureSelection()` 委托 Match，`PlaySelected/DiscardSelected` 转发数据请求。它不再把自己或目标 View 作为游戏命令参数。
- SingleBattle 和 DrawBattle 使用同一命令接口。SingleBattle 通过 `ICardZonePresentation` 播放牌区能力，场景时间线统一使用 `CardZoneTimelineAction`；DrawBattle 使用自身绑定的 `CardZoneTransferPresenter`，保留飞行、落位、计分和补牌顺序。显式 `PlaySelected(destination, route)` 参数仅用于独立牌桌，外部玩法统一使用自己的已绑定表现。

`Changed`/`Notify` 传递 `CardTableChange` 与不透明的已呈现 consumer token。Host 不检查 token 的类型；旧适配器继续使用 `CardZoneViewData` 作 token，通用绑定使用其 View 实例。牌区仅在自己的 `View.Data` 正是已应用快照时跳过重复刷新；另一个观察同一 ZoneId 的 View 仍会更新。这样既保留正在播放的动画，也不把同牌区的其他观察者一并跳过。

## 状态归属与数据流

```text
Play 中的鼠标 → CardZonePointerController → ICardInteractionSource
                                              ↓ 写回每张本地卡牌
                                          CardInteractionState
                                              ↓ 宿主投影
                         CardZoneView.Present → CardInstanceView → CardView
```

悬停、选中由每张本地卡牌持有。`ICardInteractionSource` 按稳定 ViewId 读取和更新这份状态，提供 `CanSelect` 权限判断。控制器只记录按下位置和当前命中身份，不另存一份选中集合，也不直接操作 Transform 或 CardView。`StateChanged` 在一次鼠标事件完成后通知宿主重新投影，`GetSelectedViewIds()` 按当前牌区顺序从卡牌读取待提交选择。

成功选中后，展示层比较前后卡牌快照，在 `IsSelected` 从 false 变为 true 时播放一次选中音效；取消选中、悬停和没有成功选中的点击不播放。`HandZoneView.SetSelectionSound` 独立配置开关、音量与音频，验证窗口“牌区样式 → 音效 → 选中音效”提供对应设置。鼠标控制器不直接播放音效，保持卡牌数据驱动表现。

手牌默认最多选中 **5 张**，由 `HandZoneView.MaxSelectedCards / SetMaxSelectedCards` 配置。达到上限后拒绝新增选择，已有选择仍可取消；降低上限不强制清除已有选择。鼠标控制器对任意手牌数据源检查上限，CardZonePlayHost 的权限查询与直接新增选中也遵守相同边界。

完整点击但未能选中新牌时，HandZoneView 统一触发 `CardSelectionDenied`：超限、禁用牌、权威阶段锁定及可见的入列飞行卡牌都使用同一套小幅上升/回弹动画。默认局部 +Z 0.004、上升 0.08 秒、回弹 0.18 秒。效果与正常选中/悬停独立，重复点击从当前位移续接，不累加高度；成功选中、收牌、重开或禁用会清理。UI 遮挡/失焦、跨牌松开、已移除或不可见卡牌属于未完成点击，不触发反馈。`SetSelectionDeniedSound` 配置独立的拒绝音效，默认空 Clip 为静默，不加载占位资源；Inspector 和实战牌区工具均可配置。

本地交互状态与 Core 规则卡牌关联，但不写入权威对局存档，不影响其他观察者。编辑器中的 `PreviewCard.IsHovered / IsSelected` 是实际状态来源，`CardZonePreviewModel` 实现数据接口；运行时 `CardZoneCardState` 持有逐牌数据，`CardZonePlayHost` 实现同样的接口；正式对局宿主也可使用自己的客户端模型。

## 当前行为

- 移入一张牌设置悬停，离开或换牌清除旧悬停。左键按下并在同一张牌松开，切换选中；可选多张，不执行拖拽。
- 选中表示待提交。离开牌面、离开窗口、失焦、解绑或 UI 遮挡会取消悬停与未完成点击，保留已选中状态。
- 悬停面积扩大 20%（等比缩放 √1.2 ≈ 1.0954），不再上浮；选中仍默认沿局部 +Z 0.02 上浮，取消选中回落至原位。悬停缩放和选中位移由两个独立 DOTween OutBack 驱动，可同时成立；鼠标移入/移出不重播选中动画。默认放大/上浮 0.12 秒、缩小/回落 0.14 秒，惯性强度 2.2；选中上浮额外乘 1.35（上限 3）。状态立即写回卡牌，快速反向从实际姿态接续。选中时摆角随上浮平滑归零并保持静止，取消后恢复摆动。
- `CanSelect` 决定能否新增选中；已选中的牌总能通过再次点击取消。验证模型使用 Core `CanPlay`：禁用牌可悬停但不可新增选中，失效牌可以选中。正式宿主可补充回合、归属、数量等条件，提交时仍须由引擎验证。
- 鼠标控制器只管理选择，不修改 Zone 列表；弃牌按钮调用宿主命令。排序后状态跟随原牌；移除卡牌后不再返回其选中身份，也不会把旧按下事件应用到另一张牌。

## 射线命中

`CardZoneRaycaster.TryHit` 将世界射线变换到每张牌实际渲染 Mesh 的局部空间，与 bounds 相交。支持区域旋转、缩放、圆弧、翻面、上浮和摆动；使用距离最近的牌，等深度时后面的列表元素优先。美术 Prefab 的 Collider 保持关闭，不依赖 Physics.Raycast。

命中使用实际缩放后的卡牌几何。当前悬停牌若处于选中上浮，还按实际动画偏移保留未上浮时的命中范围，防止沿 Z 位移后在静止鼠标下反复进出。默认命中排除入列飞行卡牌；鼠标控制器通过 includeEntering 识别可见飞行牌的拒绝点击，不允许其选中/悬停。未渲染牌和停用牌区不参与命中；已有牌让位时按实际位置命中。命中范围是矩形包围盒，不逐像素裁切圆角；只处理绑定牌区，场景遮挡和多个牌区的输入仲裁由宿主负责。

## 运行时接入

`CardZonePlayHost` 通过基类引用消费具体 ZoneView；只有 `HandZoneView`（包括进一步派生的子类）会自动创建/绑定鼠标组件。通用牌区和 PlayedZoneView 不自动绑定输入；`DiscardSelected` 要求手牌表现，否则在修改数据前拒绝。命中与控制器仍依赖 CardZoneView 基类，正式宿主可自行绑定其他牌区。

手牌与 UI 优先使用 Unity Input System。添加 `CardZoneMouseInput` 后由宿主绑定数据：

```csharp
// cards 实现 ICardInteractionSource，并把 Interaction 写回自己的每张本地卡牌。
mouseInput.Bind(zoneView, cards, playerCamera);
mouseInput.StateChanged += RefreshZone;

void RefreshZone()
{
    // projectCard 从卡牌本身读取 Interaction，构建 CardVisualData。
    zoneView.Present(handZone, projectCard);
}

// 正式玩法可读取这一组身份构造命令；本地验证宿主已提供 DiscardSelected / PlaySelected。
var pending = mouseInput.Controller.GetSelectedViewIds();
```

未指定相机时使用 Camera.main。组件读取 Input System 的 Mouse 和 Touchscreen；手机按住显示悬停，松开才选中/取消，滑动、取消、失焦和 UI 遮挡会取消未完成手势。一次手势只跟踪第一根手指，触摸期间不再读取鼠标，避免重复点击；Unity 编辑器中只接受 Play 的 Game 视图输入，Scene 和其他窗口不响应；鼠标不在相机视口、应用失焦或 EventSystem 检测到 UI 时阻止牌区输入。宿主也可直接调用 `CardZonePointerController.ProcessPointer` 注入射线和左键边沿；`CancelPointer` 清理未完成手势。解绑时用 `Unbind`，并按宿主生命周期移除事件订阅。

## Game 中的手牌操作按钮

场景 `HandActionPresenter` 绑定一个手牌 `CardZonePlayHost`、`HandActionPanelView` 及既有 `CardZoneTransferPresenter` 路径；展示 Prefab 为 `Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab`。四个按钮分别出牌、弃掉选中、点数理牌、花色理牌；没有主动抽牌按钮，不显示牌堆数字和“优先”状态行。当前使用 Noto Sans CJK SC 字体和占位色块，默认缩小为左下角两行；位置、尺寸及颜色可在 Prefab 调整。按钮面板为 Screen Space Camera Canvas，独立 UI Camera 叠加到主相机 Stack；World Space 单牌提示和右下角抽牌堆图标共用 UI Camera 绘制，数据与 Presenter 所有权仍各自独立。相机及 Renderer 接入见 [渲染模块](../../Infrastructure/Rendering/README.md)。

```text
UGUI Button → HandActionPanelView 请求 → HandActionPresenter
                                           ↓
                     CardZonePlayHost.PlaySelected / DiscardSelected / RequestSortAfterLanding
                                           ↓ CardMatchHost → CardTableSession 原子更新归属/顺序
                             HandZoneView / PlayedZoneView 消费变化
```

`Configure(hand, panel, route)` 绑定本地命令。按钮不保存选中集合、不操作 CardView 或 Transform：出牌要求活动的两区会话与匹配路径、非空选择、每张 CanPlay、可见视觉实例和目标剩余容量；上一批跨区飞行期间不能再提交出牌。无选中或选中含禁用牌时不能出/弃，失效牌仍能出/弃。出牌经既有 PlaySelected 更新两区 Card 列表，复用交叉飞行和落位整理动画；点击时再次检查权限，过期按钮事件不绕过检查。亮色文字保留当前排序选择，不额外写“优先”。完整回合、抽弃次数、PVP 权限仍由正式玩法宿主提供。

`CardMatchHost` 是局内卡牌数据的唯一入口，持有纯 C# `CardTableSession` 和当前观察者的逐牌表现/交互投影；手牌、出牌区、抽牌堆、弃牌堆全部注册在同一张 `CardTableState` 中。`CardZonePlayHost` 只持有 Match 和三个 ZoneId 的绑定，以及该牌区输入/排序偏好。多个手牌区可以绑定同一个抽牌堆和弃牌堆；所有身份在整局中唯一，任一牌只能处于一个牌区。

场景启动快照保存在 Match。玩法可以直接调用 Player 可用的 `StartMatch(CardTableState, IEnumerable<CardZoneCardState>)` 初始化或恢复引擎存档，并提供当前观察者的美术 ID、可见性、Tint 和本地状态；引擎数据覆盖投影里的定义、效果及禁用，悬停在启动时清除。`Bind(match, zoneId, deckId, discardId, camera)` 绑定各牌区，`BeginSession` 只建立表现与输入订阅，重复绑定或停用牌区不会补牌或重置对局。明确调用 `match.RestartMatch()` 才统一恢复所有牌区的启动快照。

`InitialDeckCards / DeckCards / DeckZone` 是转发 Match 的查询，列表末尾是牌顶。`DrawCards`、`PlaySelected`、`DiscardSelected` 和排序使用 Match 的 Prepare/Commit 命令：先计算新的完整归属和顺序，校验权限、目标容量及美术表现，再提交 Match 并消费变化。过期/跨对局提交、重复身份、禁用牌或坏牌面不消耗牌堆；成功转移保留身份、定义、效果、来源和观察者属性，出/弃清除本地待提交状态。View 仍负责飞行、翻面、惯性和音效，抽牌耗尽不自动补牌。

验证窗口只观察外部玩法权威绑定的实战 Match，不提交卡牌数据；Edit 仅调表现参数。Capture、ReplaceCardsAndDeck、ConfigureInitial、PrepareDebugEdit、预览出牌和迁移 API 仍通过 #if UNITY_EDITOR 留给旧资源迁移与隔离测试；窗口不调用这些 API，也不持有卡牌编辑缓冲。工具程序集仍限定 Editor 平台。

旧场景可执行 `工具 > 卡牌表现 > 场景接入 > 迁移牌桌状态`：验证全局身份后迁移所有牌区，合并弃牌堆，单个旧抽牌堆作为共享堆；若原来有多个非空堆则保留各自归属。迁移支持 Undo、清空旧 Host 列表，只标记场景 dirty。运行时 Match/Core 脚本使用既有 Runtime 程序集与 link.xml 进入首包；需保存场景以持久化 Match 引用。

抽牌由玩法流程调用 `DrawCards`，不由玩家按钮触发。`Runtime/Presenters/DrawPilePresenter.cs` 保存本地宿主与 `CardPresentation.DrawPileView` 引用，每帧将当前 DeckCards 数量投影为牌背叠层；Edit 显式刷新读取 InitialDeckCards，Play 不读取编辑器窗口。图标不拥有牌堆、不发抽牌命令；悬停显示紧凑牌组统计表，点击打开并保持完整卡牌总览，返回关闭，全部属于本地展示状态。空堆保留一张暗色牌背；右下角数字显示剩余牌数（含 0），复用 Noto Sans CJK SC 字体，不接收射线。`Editor/DrawPileSetup.cs` 将独立 `DrawPileIcon.prefab` 放在右下角并绑定同一 UI Camera；采用 GraphicRaycaster 和稳定的 Icon 射线区域；Deck 子节点的 CardUIGraphic 仅负责图集渲染与惯性缩放，不接收射线。

固化手牌时自动创建按钮和抽牌堆图标并绑定；`工具 > 卡牌表现 > 场景接入 > 配置手牌操作按钮` 可手动接入。出牌路径从该手牌上的 CardZoneTransferPresenter 匹配；没有路径时仅禁用出牌按钮，配置路径后重新接入。`Update Player Hand UI` 可升级旧 Prefab：原 Draw 字段通过 FormerlySerializedAs 保留按钮/文字引用，改为 Play 并移除 Status 节点，保留 GUID/配色。缺少 EventSystem 时创建 InputSystemUIInputModule；运行时复用已有 EventSystem，仅使用 InputSystemUIInputModule；旧场景中残留的 StandaloneInputModule 会停用。项目 Active Input Handling 固定为 Input System Package (New)，修改该设置后必须重启 Unity，脚本重编译不能启动原生设备后端。鼠标、触摸与键盘不再读取旧输入 API。四个按钮 Graphic 与牌堆 Icon 固定区域拦截射线，背景/文字及图集牌堆叠层不拦截；CardZoneMouseInput 沿用 UI 遮挡检查。准备工具支持 Undo、保留已有 Prefab 样式，仅标记场景 dirty；需保存场景以保留引用。Prefab、运行脚本、字体与 .meta 沿现有程序集和 link.xml 进入首包，不依赖打开验证窗口。

## 抽牌落位后排序

本地手牌宿主的 `SortOrder` 默认为 `CardZoneSortOrder.RankFirst`，也支持 `SuitFirst`，沿用 Core 的点数/花色排序。抽牌命令先向 Match 提交原始转移顺序，再调用 `RequestSortAfterLanding(order)`；`TryCompletePendingSort()` 在 Play 的 LateUpdate 检查整区飞入、回稳、让位及离场过渡是否完成，之后更新 Card 层列表，触发已有重排动画。它不按固定时长估算、不修改 View 内部顺序；进入完成时的单牌音效通知早于回稳结束，不能用该通知提前排序。

```csharp
host.DrawCards(count); // 自动排队落位后按当前偏好理牌
```

连续抽牌保留请求并延后执行；新的排序请求覆盖选择，手动改序不触发自动排序。无动画时下次刷新完成，已排序/空手牌消耗请求而不重播。排序读取当前卡牌定义，保留逐牌身份、可见性、效果、Tint 与交互，未知定义稳定归尾；初始快照不被 Play 排序改写。SetSortOrder 只保存偏好；请求要求运行中的 HandZoneView，StopSession、停用和重启清理等待。固化时保存此牌区的排序选择，编辑器两个原有按钮沿用此选择；牌堆和出牌区不自动排序。

## 场景共用的悬停提示

`CardHoverPresenter` 由场景持有，每个本地观察者/相机配置一份。它保存 `CardSuitChipsTooltip` Prefab 引用和来源列表，第一次有效悬停时生成一个提示实例，换牌或换区继续复用；手牌使用 World Space，总览卡牌将同一实例临时挂到页面 Canvas。无悬停时隐藏，组件停用/销毁时释放。CardZoneView 和每张 CardView 不持有提示 Prefab。

```text
逐牌 Interaction → 宿主投影 → CardInstanceView
       ↓
ICardHoverSource → CardHoverTarget（视觉锚点、花色、点数、筹码）
       ↓
场景 CardHoverPresenter → 共用 CardTooltipView.SetContent
```

`ICardHoverSource.TryGetHoveredCard` 由客户端玩法适配器实现：先确认观察者权限，再解析允许展示的数据，返回当前视觉实例作为锚点。Presenter 不持有权威卡牌，不解析 ArtId，也不计算最终得分；普通来源按 Sources 顺序取第一个有效目标；实现 ICardHoverScope 的活动页面可通过 CapturesHover 独占提示来源，避免读到背后手牌的悬停。输入仲裁仍由宿主处理。

CardHoverTarget 支持场景 CardInstanceView 和原生 Canvas CardUIItemView 两种视觉锚点。DeckOverviewPresenter 复用该接口，从只读 DeckOverviewData 的规则花色/点数及 CardScoreRules.GetBasePoints 提供基础筹码；已抽出、禁用、失效牌仍能查看，隐藏/未知定义不提供信息。总览打开时独占提示层；鼠标移出卡牌即隐藏；触摸按住卡牌显示信息，松开或移出即隐藏。关闭后恢复手牌提示。

当前验证宿主 `CardZonePlayHost` 已实现该接口：读取运行副本的逐牌悬停状态，按 CardInstance.DefinitionId 查询标准 52 张牌定义及 `CardScoreRules.GetBasePoints`（2–10、J/Q/K=10、A=11）。禁用/失效牌仍可查看印刷基础点数；未知定义或纯美术牌不提供提示。正式玩法应实现自己的来源，通过能力解析后的有效牌面、`ScoringCard.PointsPerTrigger` 等提供所需数值，并在适配器中处理权限；基础/名义筹码不等于最终结算。

Presenter 在 Play 的 LateUpdate 消费投影，要求目标正面可见、悬停、处于活动渲染状态且没有飞入/离场；隐藏牌、移出/弃掉、失焦后清除悬停或来源停用即隐藏。编辑模式不自动处理鼠标；`RefreshPresentation()` 供宿主显式刷新和确定性测试。

提示的位置、朝向及视口限制使用指定 SceneCamera（为空时 Camera.main），按卡牌实际 Mesh 投影的上边缘放置，不继承卡牌翻面、摆动和悬停缩放。框、文字、Gap、Camera Plane Offset 和 Toward Camera 跟随 CardZoneView 的自动卡牌尺寸及牌区世界缩放等比变化；共用父级缩放不会重复计算。显隐 DOTween 使用同一个尺寸基准，悬停中调整大小不会重新播放显隐动画；Viewport Padding 保持屏幕像素单位。可通过 `SetRenderingCamera(uiCamera)` 单独指定绘制相机，Canvas.worldCamera 及活动/Culling Mask 检查使用该相机；留空则回退到场景相机。安装按钮工具会自动接入同场景/同观察者的 UI Camera，后续注册提示也会识别已有 Stack。主相机排除 UI 层，UI Camera 只绘制 UI 并同步主相机投影。Inspector 提供 Size Multiplier（默认 0.55）、Gap（0.008）、Camera Plane Offset、Toward Camera（0.002）、Clamp To Viewport 与 Viewport Padding（12 像素）。所有提示 Graphic 的 Raycast Target 关闭，不拦截鼠标。

Canvas 卡牌使用实际 Graphic 的四角（包括放大/摆动）定位，同一提示实例临时挂在其 rootCanvas 下、排序高于页面，显示于卡牌左侧并垂直居中。Canvas Size Multiplier 默认 0.4（448 × 320 模板为 179.2 × 128 Canvas 单位），Canvas Gap 默认 8；Inspector 可调。位置跟随 CanvasScaler，边界限制仍由 Clamp To Viewport / Viewport Padding 控制。提示不接收射线，移回手牌时恢复 World Space 父级、尺寸和排序；不创建另一份 Tooltip Prefab。

```csharp
// sourceComponent 是实现 ICardHoverSource 的 MonoBehaviour。
presenter.Configure(tooltipPrefab, playerCamera, new[] { sourceComponent });
presenter.RegisterSource(otherZoneSource); // 去重追加
presenter.RemoveSource(otherZoneSource);
```

真实手牌场景可执行 `工具 > 卡牌表现 > 场景接入 > 配置悬停提示` 配置悬停 UI；验证窗口不再自动固化或注册卡牌。工具保留已设置的尺寸和偏移，支持 Undo，只标记场景 dirty，不自动保存。保存场景后，其 Prefab → 背景/字体/材质引用随场景进入首包。

新增 6 项提示回归覆盖：鼠标控制器到逐牌状态再到 UI 的完整链路、基础点数与红黑整行颜色恢复、复用和排序身份、隐藏牌/弃牌/飞入过滤、定义与美术解耦、禁用失效牌数值保留、相机跟随/屏幕边界、多个来源及停用/重启清理。

## 固化牌区与 Play 验证

进入实际玩法场景的 Play，通过 Game 视图悬停与左键选择；玩法创建牌组与四区，并绑定场景的 View / 输入宿主。牌区验证窗口在 Play 中只观察当前牌局；编辑模式可通过通用表现接口预览临时展示数据，旧 Bake / Play Card Zone / Preview Mouse Interaction 菜单已移除。

卡牌顺序、身份、定义和效果由玩法权威提供；场景保存表现资源、输入引用与宿主配置。CardZoneView 序列化布局、摆动、相机和外观，HandZoneView 补充上浮、发牌/弃牌与音效参数。临时卡面使用 DontSave；关闭验证工具不影响实战输入或动画。

Runtime Managed 场景每场由玩法生成或恢复状态，验证窗口不读取旧 initialZones 或自身缓存、不写入运行时牌列表。悬停/选择仍属于本地逐牌交互数据；权威牌区状态另由玩法引擎维护。旧启动快照只保留给资源迁移与隔离测试。

出牌区的五张上限由展示契约声明；Capture 与 BeginSession 会在替换初始数据或停止旧会话前验证容量，超限保持原会话。ReplaceCards 在展示提交前同样拒绝超限输入。

正式实战接入使用 `match.StartExternalMatch(authority, state, observerCards)`、`host.Bind(...)` 和 `BeginSession()`，`StopSession()` 仅解绑。验证工具只读取 `Cards` 与相关牌区，不调用 Capture / ReplaceCards；这些 Editor API 留给迁移和隔离测试。新增身份仍驱动入列动画及音效。当前选择来自 `card.Interaction` 或控制器的 `GetSelectedViewIds()`。

测试归入 `CardPresentation.Tests.Editor`，使用轻量入口 `工具 > 测试面板 > 编辑模式测试 > 更多模块 / 全量回归 > 卡牌表现全模块`。9 项鼠标测试覆盖数据写回、Z 偏移、稳定悬停、重叠优先级、禁用/失效、取消手势、排序/移除、变换/牌背、入列屏蔽及运行时解绑，并验证点击选中的上浮惯性、回稳与鼠标离开不中断；另有 8 项上浮/固化测试覆盖 Tween 连续性与清理、场景快照序列化、运行时副本、重启恢复及编辑器重新载入。

## 弃牌命令

本地 `CardZonePlayHost.DiscardSelected(animate = true)` 返回实际弃牌张数。它从每张卡牌的 Interaction 读取选中并提交到 Match.PrepareDiscard，按当前手牌顺序验证 Core `CanDiscard`，使用 `TransferRangeTo` 生成手牌与弃牌区的新有序列表。空选择返回 0；选中集合中有禁用牌时整次拒绝，原卡牌/列表/视觉保持原样；失效牌允许弃掉。

`Cards` / `Zone` 是剩余手牌，`DiscardedCards` / `DiscardZone` 保留已弃牌的原身份、定义、来源、规则效果、可见性和 Tint。逻辑弃牌区 ID 由宿主绑定决定，可由多个手牌共享，Kind 为 `CardZoneKinds.Discard`；移入时只清除悬停和选中。再次 ReplaceCards 不能把同身份同时放在手牌和弃牌区；未来回收操作应明确进行跨区转移。

提交新手牌投影时附带本批 `discardedViewIds`，展示层据此保留短暂的飞出实例，数据不等待动画。按钮也可在飞行期间再次提交，已弃牌不再命中或出现在选中集合。初始快照保持不变，重启 Match 恢复所有牌区启动数据；`Capture(..., discarded: cards)` 可固化初始弃牌区，`InitialDiscardedCards` 可读。全局归属由 CardTableSession 验证；正式对局的回合、归属权限和次数限制仍由各玩法引擎确认。

```csharp
int count = host.DiscardSelected();
var remainingHand = host.Zone;
var discardPile = host.DiscardZone;
```

弃牌通过 Game 内真实 UI 和玩法权威执行，验证工具只观察结果。动画和音效属于 HandZoneView；每张弃牌开始飞出时播放独立配置的弃牌音效，详见 CardPresentation README。


## 出牌命令与两区数据

`CardZonePlayHost.PlaySelected(destinationHost, transferPresenter, animate = true)` 返回本批张数，要求两区运行中的宿主与路径引用一一对应。选中状态仍由逐牌数据提供；Match.PreparePlay 验证全局身份归属、每张 Core `CanPlay` 和目标五张容量，通过 `CardZone.TransferRangeTo` 生成两份新列表。禁用牌或容量不足整批拒绝，失效牌允许出牌；空选择返回 0。按手牌原顺序追加，清除移入牌的悬停/选中，身份、定义、来源、效果、禁用、可见性和 Tint 保留。未受影响的牌堆/弃牌区不变。

提交前要求两端视图对应当前卡牌列表，再预校验双方展示。确认可展示后原子提交共享 Match 并交接相同视觉实例；不会等待动画完成再修改 Zone。初始快照不受运行时命令影响，重复点击不会重复打出已移走的牌。目标已接收但尚在飞行的牌照常占用容量；可继续提交新的合法批次。 每批可少于五张；反向落位和到齐后的整理都是视觉临时偏移，两段动画中 Zone 顺序始终保持提交后的手牌顺序，不为表现反转卡牌数据。

```csharp
route.Configure(handView, playedView, CardZoneTransferSettings.Default);
int playedCount = handHost.PlaySelected(playedHost, route);
// handHost.Zone / playedHost.Zone 此时已经是提交后的列表。
```

`PlaySelectedPreview` 是旧启动快照 API，仅保留给资源迁移/隔离测试，实战验证窗口不再调用。运行时出牌通过玩法权威和既有 TransferPresenter；规则与计分属于玩法引擎。

## Canvas 牌组检查

DeckOverviewPresenter 持有 Match、抽牌区 ID、明确的牌组成员和 DeckOverviewView 引用。Configure / SetRoster 配置身份集合，Refresh 从对局读取每张当前卡牌及观察者获准查看的投影，按 DeckZone 成员关系标记剩余。只读操作不提交命令、不改变顺序或规则效果；新增到本抽牌堆的身份纳入成员，正式构筑的永久增删由 SetRoster 更新。工具用与手牌直接关联的启动牌区引导成员，不遍历注册表推断其他玩家牌组。

Open / Close 控制 Canvas 页，逐牌悬停属于页面的 CardUIItemView，不写回 Match 的手牌交互。运行时只读 State 变化即可刷新，关闭页面不停止对局；所有 UI/效果参数由展示层消费。牌堆图标由 DrawPilePresenter 绑定，悬停预览、左键保持打开、返回关闭；共享悬停提示和页面的惯性显隐均由各自拥有的 DOTween 驱动。Editor/DeckOverviewSetup 用 UNITY_EDITOR 限定，场景引用支持 Undo、只标记 dirty。原生 Canvas 资源与 Shader 说明见 [UI README](../../../GameAssets/CardPresentation/UI/README.md#原生-canvas-牌组总览)。

## 牌堆预览与 UI 动画

DrawPilePresenter 只管理本地预览、固定显示与关闭意图：DrawPileView 发出悬停/左键事件，DeckOverviewPresenter 继续提供同一 Match 的只读牌组投影；悬停的 DeckSummary 统计表固定居中于根 Canvas，随屏幕尺寸保持居中，仅由图标控制，离开图标立即收起；点击固定显示完整 DeckOverview 卡牌页，返回取消固定并抑制仍在图标上的重复开启。默认进入 Play 关闭总览，玩法可通过 SetPinned / Open 显式打开。图标跟随手牌 CardView 的当前图集，顶层 CoverCardId 是美术配置，不揭示权威牌顶。

CardHoverPresenter 仍拥有唯一提示实例，新增独立 DOTween 显隐倍率。有效目标出现时 OutBack 放大，离开时 InBack 缩小结束再停用；重复刷新不重播，快速反向接续当前倍率。IsVisible 表示当前有有效展示目标，退场阶段实例可暂时保持活动；Hide(false) 与停用立即回收自身动画。Canvas/World Space 切换继续复用模板、数据内容和实例，不改变权威卡牌或手牌交互。

DrawPilePresenter 持有 DeckSummary Prefab 和唯一局部实例；通过 DeckOverviewPresenter.ReadData 消费同一只读牌组投影，缓存按 Match State 更新，完整页面关闭时也可读取。不创建另一份逻辑牌堆，统计表不写入卡牌交互且不抢占悬停来源，只有点击打开的完整页沿用 ICardHoverScope。悬停统计表、点击完整页和返回关闭的意图独立，切换只改变本地展示状态。

实战场景可将 CardMatchHost 配置为 Runtime Managed（编辑器 API ConfigureRuntimeManaged），并用 CardZonePlayHost.ConfigureRuntimeBinding 保留宿主引用、清空验证用牌区 ID。此模式不从 initialZones 自动启动，未绑定时为无牌无区的展示占位；只接受 StartExternalMatch 提供的权威状态，拒绝编辑器固化、预览出牌和本地重开。牌区验证工具在 Play 中观察实战与调整表现参数；编辑模式的临时表现预览不维护规则牌组、不固化卡牌快照。SingleBattleDemo 使用此模式；旧快照 API 仅保留给旧资源迁移与隔离测试。
