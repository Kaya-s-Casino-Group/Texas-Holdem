# 通用 CardMatchHost

`CardMatch.Runtime` 仅引用 `PokerEngine.Core`，不引用 CardPresentation、CardArt、UGUI 或任何场景牌区实现。Unity 内的 2D、Canvas、3D 和自定义牌区可共享同一个 CardMatchHost。

## 职责与接口

- Host 维护一个观察者的卡牌/牌区镜像，以及共享的选中、悬停和本地排序。外部权威模式下，玩法仍由 BattleSession / ICardMatchCommands 决定。
- `ReadCards(zone)` 返回无美术依赖的逐牌状态；`Snapshot(zone)` 返回复制的 `CardMatchZoneData`，旧快照不会随后续输入变化。
- `SetInteraction(id, state)` 默认通知所有观察该牌区的视图。批量输入可传 `notify: false`，完成本地呈现后调用 `NotifyInteraction(zone, consumer)`；消费者 token 只用于跳过已呈现的视图。
- `CaptureSelection(zone)` 捕获有序卡牌 ID。出牌/弃牌交给 `Commands`；渲染组件不自行计算战斗规则。
- `Prepare…` 只准备更新；`Commit(update)` 提交镜像；`Notify(update, tokens…)` 通知其他视图。动画适配器可先验证/准备其表现，再提交和通知，保留原来的事务边界。
- `Restarted`、`Stopped`、`Changed`、`InteractionChanged` 支持不同前端同步生命周期。

一个 Host 表示一个观察者的数据与交互上下文。两个视图共享它时，选择和排序也共享；需要独立玩家视角或独立选择状态时，使用不同 Host 接收同一个规则会话的相应投影。这里是本地受信任适配接口，不是远程客户端的隐藏信息过滤协议。

## 接入任意牌区

实现 `ICardMatchZoneView`：

```csharp
public void Present(CardMatchZoneData snapshot) { /* 按 ID 更新自己的 UI / Sprite / Mesh */ }
public void Clear() { /* 清理本视图拥有的实例和动画 */ }
```

在自己的场景对象上添加 `CardMatchZoneBinding`，调用：

```csharp
binding.Configure(match, new ZoneId("实际牌区 ID"), viewComponent);
```

`viewComponent` 必须是实现上述接口的 MonoBehaviour。绑定会跟随状态、交互和重启更新；关闭一个绑定仅清理该视图，不会停止共享 Host。美术查找、布局、动画和输入反馈由自己的适配器提供。

也可直接订阅 Host 事件，编排更复杂的时间线。通用绑定只负责快照同步，不替代 Battle 的表现时间线和信号。

## 旧 3D Demo 兼容

`CardMatchPresentationAdapter` 位于 `CardInteraction.Runtime`，负责原牌区的 ArtId、Tint、CardStateArtBindings 和 CardZoneViewData 投影。原 `CardZonePlayHost`、手牌动画、牌库总览继续使用它。该层也可服务原有 Canvas 卡牌，但自定义前端无须引用它。

原 Host 上的美术配置和初始牌面装饰已经迁移至同对象的适配器；Host、命令接口及交互值类型移动时保留 GUID 和命名空间，已有场景引用保持有效。`CardInteractionState` 为源码兼容暂保留 `CardPresentation` 命名空间，但类型实际位于独立 CardMatch 程序集。

旧 `GetCards` / `Project` / 接收 `CardZoneCardState` 的初始化调用通过扩展方法转给适配器。新前端直接使用 `CardMatchCardState` / `CardMatchZoneData`，不会自动创建该美术适配器。

SceneDemo 1V2 的通用 Host 与 3D 牌区接入作为独立关卡改动提交。牌区动作能力另见 [表现能力接口](../../../../Tools/CardZonePresentationContract.md)，不属于 Host 的职责。
