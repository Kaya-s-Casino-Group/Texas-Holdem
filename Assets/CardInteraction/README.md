# 卡牌鼠标交互

`CardInteraction.Runtime` 是可选输入模块，依赖 `CardPresentation.Runtime`、`CardArt.Runtime`、`PokerEngine.Core` 和 Unity UGUI。展示模块不反向引用输入模块。运行时程序集与 link.xml 进入首包；编辑器验证入口留在 `CardPresentation.Editor`。

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

本地交互状态与 Core 规则卡牌关联，但不写入权威对局存档，不影响其他观察者。编辑器中的 `PreviewCard.IsHovered / IsSelected` 是实际状态来源，`CardZonePreviewModel` 实现数据接口；运行时 `CardZoneCardState` 持有逐牌数据，`CardZonePlayHost` 实现同样的接口；正式对局宿主也可使用自己的客户端模型。

## 当前行为

- 移入一张牌设置悬停，离开或换牌清除旧悬停。左键按下并在同一张牌松开，切换选中；可选多张，不执行拖拽。
- 选中表示待提交。离开牌面、离开窗口、失焦、解绑或 UI 遮挡会取消悬停与未完成点击，保留已选中状态。
- 默认悬停上浮为牌区局部 `+Z 0.01`，选中为 `+Z 0.02`；选中优先，两档不叠加。表现层使用 DOTween OutBack，上浮 0.12 秒、回落 0.14 秒，时长包含越过目标后的回稳；惯性强度默认 2.2；选中上浮额外乘 1.35（上限 3），默认约 25% 的越过量让第二段上浮更明显；基础强度设为 0 时全部关闭回弹。状态立即写回卡牌，动画独立追随。取消选中后，鼠标仍在牌上时回落到较低的悬停位置；两种状态都结束才回原位。选中时在第二段上浮过程中同步将该牌自身 local Y 摆动角度平滑归零，然后保持静止；取消后从实际角度恢复摆动，悬停仍可摆动。
- `CanSelect` 决定能否新增选中；已选中的牌总能通过再次点击取消。验证模型使用 Core `CanPlay`：禁用牌可悬停但不可新增选中，失效牌可以选中。正式宿主可补充回合、归属、数量等条件，提交时仍须由引擎验证。
- 鼠标控制器只管理选择，不修改 Zone 列表；弃牌按钮调用宿主命令。排序后状态跟随原牌；移除卡牌后不再返回其选中身份，也不会把旧按下事件应用到另一张牌。

## 射线命中

`CardZoneRaycaster.TryHit` 将世界射线变换到每张牌实际渲染 Mesh 的局部空间，与 bounds 相交。支持区域旋转、缩放、圆弧、翻面、上浮和摆动；使用距离最近的牌，等深度时后面的列表元素优先。美术 Prefab 的 Collider 保持关闭，不依赖 Physics.Raycast。

当前悬停牌按实际动画偏移同时保留未上浮时的命中范围，防止沿 Z 位移后在静止鼠标下反复进出。入列飞行/回稳中的牌、未渲染牌和停用牌区不参与命中；已有牌让位时按实际位置命中。命中范围是矩形包围盒，不逐像素裁切圆角；只处理绑定牌区，场景遮挡和多个牌区的输入仲裁由宿主负责。

## 运行时接入

`CardZonePlayHost` 通过基类引用消费具体 ZoneView；只有 `HandZoneView`（包括进一步派生的子类）会自动创建/绑定鼠标组件。通用牌区和 PlayedZoneView 不自动绑定输入；`DiscardSelected` 要求手牌表现，否则在修改数据前拒绝。命中与控制器仍依赖 CardZoneView 基类，正式宿主可自行绑定其他牌区。

项目当前使用旧 Input Manager。添加 `CardZoneMouseInput` 后由宿主绑定数据：

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

未指定相机时使用 Camera.main。组件仅读取鼠标左键；Unity 编辑器中只接受 Play 的 Game 视图输入，Scene 和其他窗口不响应；鼠标不在相机视口、应用失焦或 EventSystem 检测到 UI 时阻止牌区输入。宿主也可直接调用 `CardZonePointerController.ProcessPointer` 注入射线和左键边沿；`CancelPointer` 清理未完成手势。解绑时用 `Unbind`，并按宿主生命周期移除事件订阅。

## 固化牌区与 Play 验证

在验证窗口配置卡牌后，使用“固化并进入 Play”或 `Tools > Cards > Play Card Zone`，直接在 Game 视图悬停、左键选择。`Tools > Cards > Bake Card Zone For Play` 只固化；原 Preview Mouse Interaction 菜单作为该入口的兼容别名。Scene 不再提供交互回调。

固化将完整卡牌顺序、身份、定义、效果、禁用、可见性、Tint、初始选中和图集 ID 写入场景中的 `CardZonePlayHost`；瞬时悬停会清除。`CardZoneView` 序列化共用布局、摆动、相机和外观配置，`HandZoneView` 补充上浮、抽牌/弃牌及音效配置。根对象为普通 Untagged 场景对象，临时卡面仍使用 DontSave；Play 时由宿主根据数据重新生成并自动绑定鼠标，关闭验证窗口也可运行。

每次进入 Play 都复制初始卡牌数据。运行时的悬停/选中写入这份逐牌副本，不改写编辑时的初始快照；退出后再次进入可从相同状态验证。打开验证窗口时，可从宿主恢复原始卡牌身份和顺序。点击“固化当前牌区”或编辑器配置更新都会同步快照；工具不自动保存整个场景，保存场景即可将快照保留到磁盘。这里是本地场景启动数据，不是权威 Match 存档，也不实现完整对局规则。

出牌区的五张上限由展示契约声明；Capture 与 BeginSession 会在替换初始数据或停止旧会话前验证容量，超限保持原会话。ReplaceCards 在展示提交前同样拒绝超限输入。

手动接入时可使用 `host.Capture(zone, cards, camera)` 保存启动快照，`BeginSession()` 重建显示并绑定输入，`StopSession()` 解绑。会话中通过 `ReplaceCards(cards, animate)` 提交新的有序卡牌列表，先复制/验证并投影，再更新交互数据源；不会改写初始快照，新增身份驱动入列动画和逐张音效，animate=false 可静默恢复。验证窗口在 Play 中通过同一入口编辑完整列表，初始快照不受影响。`Cards` 是当前运行时逐牌数据，`InitialCards` 是初始快照；读取 `card.Interaction` 或控制器的 `GetSelectedViewIds()` 获取选择。

测试归入 `CardPresentation.Tests.Editor`，使用轻量入口 `Poker > Tests > Run Card Presentation In Current Scene`。9 项鼠标测试覆盖数据写回、Z 偏移、稳定悬停、重叠优先级、禁用/失效、取消手势、排序/移除、变换/牌背、入列屏蔽及运行时解绑，并验证点击选中的上浮惯性、回稳与鼠标离开不中断；另有 8 项上浮/固化测试覆盖 Tween 连续性与清理、场景快照序列化、运行时副本、重启恢复及编辑器重新载入。

## 弃牌命令

本地 `CardZonePlayHost.DiscardSelected(animate = true)` 返回实际弃牌张数。它通过 `CardZoneDiscardBatch` 从每张卡牌的 Interaction 读取选中，按当前手牌顺序验证 Core `CanDiscard`，使用 `TransferRangeTo` 生成手牌与弃牌区的新有序列表。空选择返回 0；选中集合中有禁用牌时整次拒绝，原卡牌/列表/视觉保持原样；失效牌允许弃掉。

`Cards` / `Zone` 是剩余手牌，`DiscardedCards` / `DiscardZone` 保留已弃牌的原身份、定义、来源、规则效果、可见性和 Tint。逻辑弃牌区 ID 为原 ZoneId 加 `/discard`，Kind 为 `CardZoneKinds.Discard`；移入时只清除悬停和选中。再次 ReplaceCards 不能把同身份同时放在手牌和弃牌区；未来回收操作应明确进行跨区转移。

提交新手牌投影时附带本批 `discardedViewIds`，展示层据此保留短暂的飞出实例，数据不等待动画。按钮也可在飞行期间再次提交，已弃牌不再命中或出现在选中集合。初始快照保持不变，重启会话恢复初始手牌与弃牌数据；`Capture(..., discarded: cards)` 可固化初始弃牌区，`InitialDiscardedCards` 可读。这只是场景验证宿主，正式对局的回合、归属、次数限制仍由玩法引擎确认，不新增权威存档字段。

```csharp
int count = host.DiscardSelected();
var remainingHand = host.Zone;
var discardPile = host.DiscardZone;
```

验证窗口“手牌列表 / 抽牌实验”页均提供“弃掉选中”，也可用 `Tools > Cards > Discard Selected Cards`。动画和音效属于 HandZoneView；每张弃牌开始飞出时播放独立配置的弃牌音效，默认使用与抽牌相同的占位资源，详见 CardPresentation README。


## 出牌命令与两区数据

`CardZonePlayHost.PlaySelected(destinationHost, transferPresenter, animate = true)` 返回本批张数，要求两区运行中的宿主与路径引用一一对应。选中状态仍由逐牌数据提供；`CardZonePlayBatch` 验证两区完整身份顺序、互不重复、每张 Core `CanPlay` 和目标五张容量，通过 `CardZone.TransferRangeTo` 生成两份新列表。禁用牌或容量不足整批拒绝，失效牌允许出牌；空选择返回 0。按手牌原顺序追加，清除移入牌的悬停/选中，身份、定义、来源、效果、禁用、可见性和 Tint 保留。两区的弃牌档案不变。

提交前要求两端视图对应当前卡牌列表，再预校验双方展示。确认可展示后一次赋值两端运行数据并交接相同视觉实例；不会等待动画完成再修改 Zone。初始快照不受运行时命令影响，重复点击不会重复打出已移走的牌。目标已接收但尚在飞行的牌照常占用容量；可继续提交新的合法批次。 每批可少于五张；反向落位和到齐后的整理都是视觉临时偏移，两段动画中 Zone 顺序始终保持提交后的手牌顺序，不为表现反转卡牌数据。

```csharp
route.Configure(handView, playedView, CardZoneTransferSettings.Default);
int playedCount = handHost.PlaySelected(playedHost, route);
// handHost.Zone / playedHost.Zone 此时已经是提交后的列表。
```

`PlaySelectedPreview` 是编辑器验证的初始快照版本，仅限非 Play 且两个宿主均未开始会话；直接更新两份序列化卡牌数据，验证窗口负责 Undo 和场景 dirty 标记。它不绑定鼠标、不从 CardView 反推数据。路径参数和验证操作见 CardPresentation README。新增 `Runtime/CardZonePlayBatch.cs` 与 `Runtime/CardZonePlayHost.Transfer.cs`，沿用首包运行时程序集；完整回合/归属/资源消耗与计分仍交给玩法引擎，此命令只验证本地牌区转移。
