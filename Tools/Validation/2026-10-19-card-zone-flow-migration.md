# 牌区旧流程清理与新接口迁移验证

日期：2026-10-19。

## 变更边界

战斗牌区统一由 `CardZoneTimelineAction → ICardZonePresentation` 调用。规则命令、XML 规则内容、费用和伤害结算逻辑未修改。

- 删除 `HandPreparationTimelineAction`、`HandSettleTimelineAction`、`Battle2DCardTimelineAction` 以及旧 `ICardActionView` / `PresentActionCommitted` 协议，不保留回退路径。
- 准备信号统一为 `Prepared`；准备和落定动作分别使用 `cards.prepare`、`cards.settle`。
- 3D 战斗显示检查点经 `CardZonePlayHost.PresentProjection` 将中立快照送入新接口，接受后提交 CardMatchHost 镜像。`WorldCardZonePresentation` 封装已有 HandZoneView 动画；原生渲染方法作为实现内部能力保留。
- Battle2DValidation 通过 `Battle2DCardZonePresentation` 实现同一协议，保留原来的平面卡牌和上下准备动画，由通用时间线动作调用。
- CanvasCardZonePresentation 是独立的 UI 牌区实现，不依赖世界空间 HandZoneView。
- SceneDemo 1V2 的场景接入、阵容 XML 和对应关卡说明属于独立改动，留在工作区，未包含在本次基建提交。

## 场景引用核对

| 场景 | 已连接的牌区能力实现 | 通用牌区时间线动作 |
| --- | --- | --- |
| SingleBattleDemoNew | WorldCardZonePresentation | 准备、落定 |
| Battle2DValidation | Battle2DCardZonePresentation | 出牌准备、弃牌准备 |
| SingleBattleDemo / TeamBattleDemo / ForestRuins / CardRenderingValidation | WorldCardZonePresentation | 原场景没有旧牌区时间线动作 |

静态检查覆盖全部 Assets 场景、预制体和资产：已删除的 3D 时间线脚本 GUID 无残留。保存场景中所有 CardZoneTimelineAction 的 Source 都指向新接口实现。

## 回归结果

- EditMode：**445 通过，0 失败，0 跳过**。覆盖规则桥接、Host、通用时间线、3D/2D 适配器、CardMatch 与牌区能力测试。
- PlayMode：**8 通过，0 失败，0 跳过**。覆盖 3D 与 2D 保存场景中的开局、选牌切换、出牌、弃牌、敌人队列、取消、重开及配置依赖顺序。
- 新增回归：已销毁的 World/Canvas 提供者不会被时间线再次解析；2D 提供者位于时间线子对象时能正确找到展示视图。
- 运行回归中发现并修复了 2D 提供者查找位置、销毁对象经接口引用仍被解析、以及 3D 输入桥接销毁时的访问问题，随后完整 PlayMode 通过。
- EditMode 控制台中的 `ActionFailed: Test presentation failure` 来自预期失败恢复测试；测试使用 LogAssert 验证该日志。

验证通过编辑器测试入口执行，未使用已撤销的 SceneDemo 1V2 场景准备工具。工作区其他美术和渲染改动未纳入本次修改。

接口约定见 [牌区表现能力接口](../CardZonePresentationContract.md)，场景配置见 [表现时间线配置](../BattlePresentationTimelineConfiguration.md)。
