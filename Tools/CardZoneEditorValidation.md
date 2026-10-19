# 通用牌区编辑器验证

## 打开与选目标

菜单：`工具 → 卡牌表现 → 通用牌区验证`，原“实战牌区验证”也打开同一窗口。拖入场景中的 `ICardZonePresentation` 组件，或开启跟随场景选择。World / Canvas / Battle2D 都走同一调用入口。

在“表现参数”页配置实现。World 使用 HandZoneView 的卡牌预制体、布局和动画参数；Canvas 需要 Card Template；Battle2D 需要场景 View 的卡牌预制体及卡牌根节点。窗口不创建或绑定 BattleHost。

## 编辑器直接看动画

1. 从窗口顶部“已配置的牌区表现”列表直接选择组件。列表列出所有已加载场景中的实现，包含场景、对象路径、类型和启用状态。
2. 点击下方“已配置的表现动作”即可播放。首次点击自动载入八张随机牌，不需要填写 Art Key、ID 或 JSON。固定从标准 52 张牌中抽取八张不重复牌面，使用独立随机源。
3. “换一手八张随机牌”刷新样例；用牌名按钮勾选动作目标，默认选中前两张。
4. 点击抽牌、理牌、出牌准备、出牌、弃牌等按钮，工具自动构造前后展示快照。抽牌会从空牌区发入八张新随机牌。
5. 选择不同目标后点击“选牌 / 切换”可验证预选切换；“取消所有选牌”验证选择撤销。“取消并恢复”则结束整个预览并恢复原展示。
6. 支持暂停、单步和继续，日志显示实际信号与完成状态。需要自定义快照和参数时展开“高级”。

动作列表读取该实现声明的 Capabilities，预览工具排除 cards.state 状态刷新。例如 Battle2D 当前提供出牌准备和弃牌准备；窗口不把它伪装成完整 Canvas 牌区。

## 自定义能力

提供者实现 `ICardZonePresentation`，可选实现 `ICardZonePresentationPreview.AdvancePreview(float deltaTime)` 推进自己拥有的动画。PreservePreviewState 返回 IDisposable 恢复句柄，释放时取消预览并恢复入口展示，包括实现特有的颜色和美术绑定。窗口不引用具体渲染类进行驱动。无编辑器时钟的提供者仍支持数据观察和参数编辑。

注册在 CardZonePresentationBehaviour 的自定义动作，如需要手动时间，实现 ICardZonePresentationPreviewClock；World、Canvas、Battle2D 的预览时钟会转发给已注册的自定义动作。自定义代码应只推进自己的活动操作，临时资源通过 request.Scope.Own 注册释放。

After JSON 表示同一牌区的目标展示卡牌，每条支持 viewId、cardId、artKey、hidden、selected、hovered。目标列表以逗号分隔，使用当前 Before/After 中的 ViewId。自定义参数 JSON 形如：

```json
{"items":[{"key":"style","value":"burst"}]}
```

## 数据与生命周期

窗口只调用 Synchronize / Play，不调用规则命令，不修改 CardMatchHost 的权威状态、牌组或初始配置。编辑器样例数据只用于展示。

一次预览保存入口展示快照并独占当前提供者。停止、切换目标、关闭、Undo、保存场景、脚本重载和进入 Play 时，取消 Scope 并恢复入口快照。临时视觉不保存。运行中不允许启动编辑器预览，以免覆盖战斗调度器展示。

本工具验证牌区能力和真实信号；完整的玩家攻击、敌人受击、镜头、UI 时间线仍由 ScenePresentationDirector 配置并通过战斗场景回归验证。

## 阶段验证记录（2026-10-19，移除特殊状态入口之前）

- EditMode：455 通过，0 失败，0 跳过。
- PlayMode：8 通过，0 失败，0 跳过；包含既有 3D 和 2D 实战场景。
- 新增 10 项测试：World / Canvas 手动预览的暂停与单步、真实信号、抽牌和移出、关闭/切换/保存时恢复、World 原生颜色及展示数据恢复、不写入 CardMatchHost 初始状态、2D 预览时钟与取消恢复。
- 运行回归结束后返回 SceneDemo 1V2，场景未被预览工具修改。

易用性更新：默认固定八张不重复随机牌，提供者列表与动作按钮直接选择；World / Canvas 一键准备、弃牌、换牌和恢复回归通过；本轮编辑器预览专项 11/11 通过。

本工具不接入卡牌特殊效果或禁用状态，保留普通牌动作和选牌交互；特殊状态字段及状态刷新入口已移除。本次按要求未再运行测试。
