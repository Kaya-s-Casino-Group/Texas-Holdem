# BattleSceneHost 拆分验证

> **历史验证记录**：测试结果仅对应当时版本，不作为当前场景集成完成的依据。现行接入统一见 [CardPresentation README](../../Assets/Scripts/Presentation/CardPresentation/README.md)；本文旧组件和场景引用仅供追溯。

日期：2026-10-18；Unity 2022.3.62f3；接入场景：`Assets/Scenes/SingleBattleDemoNew.unity`。

## 交付与边界

- `BattleSceneHost : BaseBattleHost` 作为每局统一生命周期入口，使用 `IBattleSessionSource / IBattleSceneAdapter` 组合服务；不同 Battle 使用独立实例。
- 原 `SingleBattleHost` 更名为 `SingleBattleSceneAdapter`，保留脚本 GUID 和场景引用。现有 Actor、牌区、盾牌、HUD 和输入接入该适配器。场景只增加 Host 组件和绑定，未修改时间线组、动作参数、对象布局或玩法数值。
- 提供一个根对象、四个组件的 `BattleSceneHostMinimal.prefab`，绑定 XML 来源、中性适配器与空 Director，无相机、UI、Actor 或 CardZoneView 依赖。
- 编辑器工具支持一键添加、重复执行定位已有入口、显式新增独立 Battle、迁移旧入口及 Undo；不自动保存用户场景。
- 通用 Host 程序集不引用 Scene3D 或 CardPresentation。规则子模块没有修改；旧 Minimal2D 验证宿主保留。

用法见 [入口配置教程](../BattleSceneHostSetup.md)。

## 实际验证

| 检查 | 结果 |
| --- | --- |
| Unity 脚本编译 | 无编译错误或警告 |
| 新增 Host EditMode 用例 | 7/7 通过 |
| 扩展 EditMode 回归 | 403/408 通过；5 项失败见下文 |
| PlayMode 回归 | 5/5 通过；恢复场景原有姿态缓存后重新运行仍通过 |
| PlayMode 退出后的 Console | 无错误 |
| 场景序列化结构比较 | 原 411 个对象块全部保留，新增 1 个 Host；原块只修改 Card Match 组件列表和适配器的 Host 引用 |

新增 EditMode 覆盖最小预制体独立开局、两局会话/显示/选择/命令隔离、接入不自动开局、同步不执行规则、来源失败保留旧会话、拒绝跨 Battle 服务绑定、Host 停用门禁、添加工具幂等和显式多局。

PlayMode 使用实际关卡验证开局、预选切换与清空、出牌、弃牌、敌人演出期间同步、重开和启停恢复；新增空场景中两个最小预制体自动开局与独立操作验证。另保留三项盾牌动画与取消回归。

EditMode 运行时间：23:11:55–23:12:08（Asia/Shanghai）。最终 PlayMode：23:16:42–23:16:51。
本地原始结果位于 `Library/BattlePresentationValidation/host-split-editmode.xml` 和 `host-split-playmode.xml`（Library 不入库）。

## 未通过的扩展检查

四个 CardPresentation 用例与上次表现时间线验证中的失败同名，本次未修改这些实现或测试：

1. `CardHoverPresenterTests.TooltipFacesTheSharedCameraAndClampsWhileFollowingCardScale`：视口边界断言得到 -0.0819056034，预期 0–1。
2. `CardUICameraTests.NativeUIStackDrawsSinglePixelStripesAfterSceneResolveAndDisabledUILeavesScene`：预期 960×540，实际 853×480。
3. `CardUICameraTests.OverlaysAppendedAfterUIStayBeforeNativeUIOnTheirFirstRender`：相同分辨率差异。
4. `CardUICameraTests.RendererIsForwardUIOnlyAndDoesNotInheritRetroOrLowResolutionFeatures`：UI Renderer 带有已有 CRT Feature。

本次额外纳入集成程序集检查后，`PackagingTests.EngineAssetsHaveImportedUniqueMetadata` 检测到本地 `External/BattleToolkit/Engine/.DS_Store` 没有 `.meta`，因此失败。没有为了通过检查修改规则仓库或该本地文件。其余集成检查，包括通用 Host 与场景适配器的程序集边界，通过。

本次未进行完整 Player/IL2CPP 构建，也未重复运行规则独立 .NET 测试。
