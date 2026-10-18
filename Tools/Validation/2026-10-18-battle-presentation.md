# 单局战斗表现架构验证记录

日期：2026-10-18；Unity 2022.3.62f3。实施说明见 [BattlePresentationImplementation.md](../BattlePresentationImplementation.md)，设计约定见 [BattlePresentationDesign.md](../BattlePresentationDesign.md)。

## 结果

| 范围 | 结果 | 说明 |
| --- | --- | --- |
| 全部纯规则测试 | **1259 / 1259 通过** | `Tools/test-poker-engine.sh`；包含新增 12 项事实时间线回归 |
| Unity 脚本编译 | **0 错误、0 警告** | 最后代码修改后重新编译 |
| Unity EditMode | **386 / 390 通过，4 项失败** | 六个相关程序集；失败详情如下，不应表述为全量通过 |
| Unity PlayMode | **4 / 4 通过** | 真实当前场景流程与原有盾牌播放；退出播放后 Console 无错误 |
| 代码与文档 diff-check | **通过** | 本次脚本、文档与规则子模块；未重排 Unity 场景自动生成的 YAML 空值 |

最终 EditMode 执行时间为 19:39:34–19:39:41，PlayMode 为 19:40:01–19:40:12（Asia/Shanghai）。原始结果保存在本地 `Library/BattlePresentationValidation/configured-editmode.xml`、`configured-playmode.xml`；默认菜单仍将最近一次结果写入 `results.xml` 与 `status.txt`。

| EditMode 程序集 | 通过 | 失败 |
| --- | ---: | ---: |
| BattlePresentation.Tests.Editor | 16 | 0 |
| BattlePresentation.Unity.Tests.Editor | 20 | 0 |
| Poker.BattlePresentation.Shared.Tests | 9 | 0 |
| Poker.SingleBattle.Tests.Editor | 93 | 0 |
| Poker.Battle2DValidation.Tests.Editor | 15 | 0 |
| CardPresentation.Tests.Editor | 233 | 4 |

## 每一步的关键检查

1. **规则时间线**：开战、阶段进入/正常结束/提前关闭、行动开始/跳过/结算、逐效果、状态与遗物、实际移牌/发牌、终局按真实执行顺序记录。末项快照与权威结果一致；拒绝请求不发布时间线；开局即致死不伪造阶段结束。旧规则全套用例继续通过。
2. **数据边界**：`BattlePresentation.Runtime` 的 asmdef 不引用规则、Unity 或具体场景，`noEngineReferences=true`；`BattlePresentation.Unity` 引用中性核心、TMP 和 UGUI，不引用规则或具体 Scene3D 实现。共享 Bridge 承担规则适配和可见性投影；隐藏牌无实体牌 ID/牌面/效果泄露。未知游标不能退回最终状态，重复 revision 仍使用独立事实游标。
3. **调度协议**：同步 marker 锁存、真实中间节点、Started/Marker/Completed 与 AllOf、阻塞尾部、非阻塞尾部、缺失 marker、循环依赖、取消 epoch 与失败恢复均有覆盖。Instant 只同步最终快照，不播放动作或伪造事件。
4. **候选交互**：同型更新、跨型替换、清空、提交冻结/接受关闭、过时结果丢弃和作用域归属验证；重复卡牌不能被查询层自动去重后误判为合法。反馈恢复只清理自己的临时贡献，不覆盖新布局。
5. **当前场景**：`SingleBattleDemoNew` 持久化 Director、候选反馈路由、Host 绑定与现有对象 targets。真实场景测试覆盖开局 8 张牌、1 张到 2 张预选及取消、等待 Impact 才揭示伤害、出牌补牌、弃牌得盾、敌人阶段中途同步、过期回调无效、重开、整体禁用再启用。既有角色/牌区/美术继续使用。
6. **恢复边界**：新增回归覆盖 Instant 与早期取消后的摘要同步、禁用 Director 后命令即时完成、无变化 Heal 夹在得盾与受击之间仍保留正确盾牌归属。实际发现并修复停用顺序问题：CardMatch 镜像停止后恢复流程不再向它提交投影，重新启用时按最新显示快照重新绑定。
7. **任意对象接入**：Inspector 配方可绑定任意 `IPresentationAction`；Transform 示例、角色适配器和手牌准备适配器具有实际 marker、取消和同步路径。适配测试验证动态目标解析、规则 ID 到 ViewId 的映射、scope 释放及恢复；没有将 Unity 对象或动作时长下沉到规则层。

## 本轮完整配置化验证

- Host 中的 `BuildPlan` 及固定动作链已移除，缺少序列化 timeline 时明确报配置错误。场景保留 **41 个配置组、6 个静态对象绑定**；运行时另外绑定实际 Actor ID。
- 新增 **26 项 EditMode 用例全部通过**：13 项筛选/调度配置、9 项场景配方、4 项 UI 动画。覆盖实际目标优先、同目标联合筛选、效果条件、致死/零效果、缺失依赖、真实信号、替换配方保留及取消恢复。
- 新增 `PresentationActionBehaviour`；牌区准备/收尾、Actor、Transform 和 UI 动作均派生该基础组件。UI 只消费中性 DTO，独立实例和槽位防止并发、旧 scope 互相清理。修复了 Unity `CanvasGroup` fake-null 判断，测试已覆盖无预置 CanvasGroup 的克隆模板。
- 实际场景 PlayMode 增加断言：配置确实存在、Host 无 BuildPlan、伤害 UI 真正出现、敌方受击镜头实际运动且恢复；同步后所有临时 UI 消失，晚到回调不再推进展示游标。原有出牌补牌、弃牌得盾、重开与整体禁用/启用仍通过。
- 实际截图已查看：`Library/BattlePresentationValidation/configured-damage.png`。出牌、牌型与伤害文字位置可区分；伤害使用当前像素字体可见的 ASCII 减号。反馈根与模板继承 HUD 的 UI layer 5，不拦截射线。
- 相对于本轮迁移前保存的场景，原有 **322 个序列化对象全部保留**；只修改既有 Director 配方及两个父级的子对象列表，新增动作和 UI 对象。未调整现有角色、相机基准、规则 XML 或渲染设置。
- 当前敌方伤害 checkpoint 等待 `effect.Revealed` 与短镜头 `Completed`，避免连续效果并发写镜头；基础 HUD 因此在约 0.145 秒镜头结束后发布。该行为由配方控制，配置边界详见 [配置教程](../BattlePresentationTimelineConfiguration.md)。

## 仍失败的 4 项既有测试

| 测试 | 失败内容 | 核对结果 |
| --- | --- | --- |
| `CardUICameraTests.NativeUIStackDrawsSinglePixelStripesAfterSceneResolveAndDisabledUILeavesScene` | 期待 960×540，实际 853×480 | 当前与 HEAD 的 `MainDeferredRenderer.asset` 均配置 `maximumHeight: 480`；测试仍期待 540。与本次表现代码改动无关的配置契约差异。 |
| `CardUICameraTests.OverlaysAppendedAfterUIStayBeforeNativeUIOnTheirFirstRender` | 同上 | 同一 480/540 配置差异。 |
| `CardUICameraTests.RendererIsForwardUIOnlyAndDoesNotInheritRetroOrLowResolutionFeatures` | 期待 renderer features 为空，实际包含 CRTFeature | 当前与 HEAD 的 `UIRenderer.asset` 均包含最终输出 CRT；原断言不再符合已有配置。 |
| `CardHoverPresenterTests.TooltipFacesTheSharedCameraAndClampsWhileFollowingCardScale` | 期待 viewport 在 [0,1]，实际 -0.00866752863 | 该测试、HoverPresenter、相关 tooltip/prefab 路径未被本次修改；本次准备偏移不在该用例中启用。但没有在隔离的旧版本完整复跑，尚不能断言其基线归因已证明；保留为独立待定位问题。 |

未为通过上述测试调整现有渲染资产或收窄断言。已存在的场景、美术与其他任务的本地修改予以保留，不属于本次表现改造的验证结论。

## 复跑入口与范围

- Unity：`工具 > 卡牌战斗 > 表现时间线 > 验证 EditMode / 验证 PlayMode`。
- 场景接入菜单：`工具 > 卡牌战斗 > 表现时间线 > 接入当前场景`，可重复执行且保留已有自定义配方。
- 本次完成 Inspector/代码配方、完整事实数据流和当前场景接入。没有提供可视化轨道编辑器；新对象的具体动画仍需实现 `IPresentationAction` 或配置已有组件。
- 验证范围为规则全套及上述六个 EditMode / 一个 PlayMode 程序集；未宣称覆盖项目所有渲染/资源测试或所有可能的新表现实现。
