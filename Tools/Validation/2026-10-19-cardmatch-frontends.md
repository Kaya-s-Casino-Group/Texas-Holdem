# CardMatchHost 通用前端拆分验证

本记录保留通用 Host 拆分阶段的验证结果；后续牌区接口与旧流程清理见 [迁移验证](2026-10-19-card-zone-flow-migration.md)。SceneDemo 1V2 的关卡接入不包含在本次基建提交。

## 改造范围

- CardMatchHost、选牌请求、命令接口和交互值类型移入 `CardMatch.Runtime`，仅依赖 `PokerEngine.Core`；移动时保留脚本 GUID。
- Host 使用 `CardMatchCardState` / `CardMatchZoneData`，管理观察者镜像、交互、提交和通知；不持有美术 SO、颜色、牌区 View 或动画。
- 新增 `ICardMatchZoneView` / `CardMatchZoneBinding`，供自定义 Canvas、2D、3D 前端复用同一个 Host。
- 旧前端通过 `CardMatchPresentationAdapter` 保留美术配置、装饰对象与原有投影 API；原 3D 手牌的批量输入发布、原子提交和动画通知时机保持。
- 5 个已有场景的 Host 美术配置迁至同对象的新适配器：SingleBattleDemo、SingleBattleDemoNew、TeamBattleDemo、ForestRuins、CardRenderingValidation。没有调整这些场景的模型、布局或规则配置。
- SceneDemo 1V2 的占位使用通用 Host，仍未接牌区/UI，Auto Start 仍关闭。

## 验证结果

Unity 编译：通过，0 warnings。

| 验证 | 结果 |
| --- | --- |
| 表现时间线 EditMode 回归（含新 CardMatch 测试程序集） | 428 passed / 0 failed |
| 表现时间线 PlayMode 场景回归 | 8 passed / 0 failed |
| DrawBattle EditMode 回归 | 16 passed / 0 failed |

新增测试覆盖：独立程序集依赖边界；RectTransform 和世界节点观察者共享 Host；已发布快照不随交互变化；提交前不刷新；仅跳过指定消费者；禁用一个前端不影响另一前端；停止/重启清理与恢复；非法恢复不改状态；原 HandZoneView 与中立 RectTransform 前端双向同步选择。

检查了移动前后的 GUID 一致、场景 fileID 唯一且有效、原初始牌面装饰迁移，以及已有 PlayMode 流程中的出牌、弃牌、回合、预选和取消恢复。

## 边界

这次交付通用 Host 和接入协议，未将 Battle2DValidation 或 SceneDemo 1V2 整体迁入这套牌区。既有 SingleBattleSceneAdapter 仍适配原 HandZoneView；新牌区的具体渲染、输入和战斗桥接按自己的前端接入。共享同一个 Host 的视图共享选择和排序，独立观察者需要独立 Host 镜像。
