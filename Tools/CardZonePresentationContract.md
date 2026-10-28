# 牌区表现能力接口

> **历史资料（2026-10-28 整理）**：本文保留旧版本的设计、配置或实施记录；下文“当前”“已完成”与测试结果均指记录当时。现行表现层接入统一以 [CardPresentation README](../Assets/Scripts/Presentation/CardPresentation/README.md) 为入口。旧 Scene3D / Minimal2D、CardMatchHost / CardZonePlayHost 及完整角色、HUD 配方不代表当前工程已恢复，不应直接按本文重建旧调用链。

`BattlePresentation.ICardZonePresentation` 定义渲染无关的牌区表现能力。它位于不引用 Unity、PokerEngine 或具体卡牌实现的 BattlePresentation.Runtime 程序集，接收展示数据，不推进规则。CardMatchHost 仍负责卡牌镜像和交互状态，不增加动画职责。

## 三个入口

- `Capabilities`：动作键、该动作可能发出的信号、占用的表现通道。
- `Play(CardZonePresentationRequest, IMarkerSink)`：执行一次表现，返回现有 `IPresentationOperation`；状态为 Pending / Completed / Cancelled / Failed。
- `Synchronize(CardZoneDisplayData)`：取消当前表现，立即恢复给定的最新展示快照。重开、跳过整段演出和恢复都通过此入口处理。

`IsPresenting` 报告当前牌区是否仍在表现；`Current` 是本视图当前接受的展示快照。请求包含动作键、执行 ID、作用域、同一牌区的 Before/After 快照、目标卡牌、参数和 Animated/Instant 模式。卡牌 ID 会在当前牌区的前后快照内解析为 ViewId；重复或不存在的卡牌被拒绝。隐牌使用展示 ID，不要求暴露规则卡牌 ID。

## 基础动作

| 动作键 | 信号 | 行为 |
| --- | --- | --- |
| cards.draw | AllArrived | 呈现 After，新增牌从发牌位置进入 |
| cards.arrange | Settled | 呈现 After 的顺序和布局 |
| cards.prepare | Prepared | 对指定牌增加临时准备姿态，不改变牌区成员 |
| cards.play | Released、AllArrived | 呈现移出后的 After；先报告释放，再等待离场动画结束 |
| cards.discard | Released、AllArrived | 呈现弃置后的 After，并等待弃牌动画结束 |
| cards.selection | Settled | 呈现新的选择状态与布局 |
| cards.state | Settled | 呈现可见性、禁用及状态美术等变化 |
| cards.settle | Settled | 等待当前牌区动作落定 |

Completed 是操作状态及时间线内置依赖，不需要额外发出同名信号。基础实现只声明实际支持的批次信号；不虚构逐牌 CardArrived 信号。

Play/Discard 的 After 必须已经不包含指定的移出牌。动作只应用展示快照，不提交规则命令。需要先蓄力但保留卡牌时使用 Prepare；不要求每次出牌都执行 Prepare。

## 时间线接入

添加通用 `CardZoneTimelineAction`，将 Source 设为实现 ICardZonePresentation 的组件，配置 Action Key 和可选 Zone Id。显式 Source 会提供自己的能力描述；脚本配置可调用 `Configure(frontend, key, zone, observe)`。动态目标也可通过 Target Key / cards 绑定解析，但需配置与能力一致的信号及通道。

Parameters 为自定义动作提供字符串键值；会合并规则展示节点已有的 Values，同名项由显式参数覆盖。参数仅影响表现，不写回规则。

例如：

```text
cards.play.Released → actor.attack
actor.attack.Impact → enemy.hit + camera.feedback
cards.play.Completed → cards.arrange
```

也可以把第一步换成 `cards.prepare.Prepared`，或者等出牌 Completed 再开始角色动作。牌区不持有玩家、敌人、镜头引用。

时间线 checkpoint 决定展示数据提交点。默认 Play 主动应用请求的 After；将这类节点配置在允许该快照呈现的位置，避免另一个绑定同时应用不同快照。3D 路径由规则桥接在 checkpoint 通过接口投影牌表时，可使用 Observe Projection 等待既有动画，避免重复发牌。Canvas 实现面向主动投影；不支持依赖外部牌区投影的 Observe Projection 模式。

## 两种实现

### WorldCardZonePresentation

挂在现有 HandZoneView 对象上，封装发牌、整理、准备抬升和离场动画。复用原卡牌预制体、FourBeasts 图集、状态美术和运动参数；不重写渲染器。默认 Play 和 Discard 都复用已有离场飞行能力及弃牌目的地，需要独立出牌目标或不同特效时扩展自定义动作。

已有 3D 场景已接入此组件。SceneDemo 1V2 的牌区接入属于独立关卡改动，未包含在本次基建提交中。

### CanvasCardZonePresentation

独立 RectTransform 实现，不引用 HandZoneView 或 CardZonePlayHost。配置 Card Template（CardUIGraphic）、牌尺寸、间距、动画时长、准备进度、抽牌/离场/准备偏移。可使用共享 CardUIItem.prefab 的 CardUIGraphic 作为模板来源，实例化其图形对象。

这层只实现表现，不内置鼠标选牌或战斗按钮。输入应通过 CardMatchHost / 命令适配器处理，再将选择状态作为新展示快照传入。Battle2DValidation 使用独立的 Battle2DCardZonePresentation 实现同一协议，保持原平面卡牌视觉；它通过通用 CardZoneTimelineAction 提供 cards.prepare / cards.prepare-discard。Canvas 实现仍供新的 Canvas 牌区直接接入，不要求改变现有平面渲染。

展示属性使用已有 CardDisplayData：ArtKey 决定卡面，Hidden 隐藏牌面，DisabledReason 表示禁用；Properties 的 effects 为效果位，selected / hovered 为布尔文本。Canvas 用中立展示 ID 管理实例，不存第二份规则牌表。

## 自定义动作和取消

自定义 MonoBehaviour 实现 `ICardZonePresentationAction`，在 World/Canvas 组件的 Custom Actions 注册，声明唯一动作键、信号及通道。它接收相同请求和信号接口；临时位移、材质、协程等必须通过 request.Scope.Own 注册释放，异步异常必须使自己的操作进入 Failed。

每次调用创建独立子作用域。取消操作、关闭组件、同步或释放父作用域都会清理该次工作的资源；不会取消父作用域中的其他动作。信号必须提前声明，每次执行中同名信号只接受一次；取消或结束后发出的迟到信号被拒绝。

已完成的 Prepare 可以保留临时姿态直到作用域释放。旧作用域的释放不能撤销后来动作的姿态。Selection 动作支持动画未结束时被下一次 Selection 替换，从当前姿态续接；撤销选择/候选应传入最新展示快照，不回放预选开始时的旧牌表。Instant 模式直接落到目标表现并返回完成；整段时间线的跳过沿用调度器 Synchronize。

## 当前唯一调用流程

所有战斗牌区时间线使用 CardZoneTimelineAction → ICardZonePresentation。新场景和自定义牌区同样只接这条入口，不增加旧接口回退。原 3D 准备/落定动作、2D 专用时间线动作及 ICardActionView 提交接口已删除，保存场景和配置代码已迁移。准备信号统一为 Prepared，不再保留旧别名。

3D 显示检查点通过 CardZonePlayHost.PresentProjection 将中立快照送入接口；表现接受后才提交 CardMatchHost 镜像。用于等待检查点动画的 cards.settle 采取 Observe Projection，允许与正在应用快照的表现同时存在。发生同步/取消时释放对应作用域，保持最新已接受的镜像。

CardZonePlayHost 仍是世界空间牌区的输入适配器，内部持有 HandZoneView；这不是第二条战斗时间线。HandZoneView 的原生飞行、抬升与事务性弃牌方法仅作为 World 实现和本地牌桌渲染的底层能力，不能再作为战斗的对外动作协议。

CardZoneCapabilityTests 覆盖 World/Canvas 共同协议、抽牌/出牌、准备节点、取消、Instant 理牌/弃牌、自定义动作、迟到信号屏蔽、同一时间线适配器、旧作用域清理隔离及公共程序集依赖。完整迁移验证记录见 Tools/Validation/2026-10-19-card-zone-flow-migration.md。
