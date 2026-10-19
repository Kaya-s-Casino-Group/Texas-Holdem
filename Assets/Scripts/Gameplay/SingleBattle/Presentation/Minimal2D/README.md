# 卡牌战斗 2D 验证（Battle2DValidation）

本模块提供独立的最简 2D 战斗界面，用实际选牌、出牌、弃牌和敌方回合验证规则与表现的边界。它与 [主线卡牌战斗的 3D 表现](../Scene3D/README.md) 共用规则 Bridge、中性展示模型、`PresentationDirector` 和交互生命周期。这是架构验证入口，尚不作为正式产品界面。

## 目录与程序集

`Runtime/Hosting` 保存场景绑定和输入适配；`Runtime/Presentation` 保存基础卡牌与矩形表现；`Editor`、`Tests/Editor` 分别归工具与验证，风格资产位于 `Assets/GameAssets/SingleBattle/Presentation/Minimal2D/Art`。沿用 `Poker.Battle2DValidation.*` 程序集名。

每局使用通用 `BattleSceneHost`，其 Source 绑定 `XmlBattleSessionSource`，Adapter 绑定 `Battle2DSceneAdapter`。原 `Battle2DHost` 已移除，脚本 GUID 迁到适配器以保留场景引用；适配器不继承 Host、不持有另一份会话。Runtime 不再引用 `Poker.BattleConfiguration.Runtime` 或任何 3D 前端。Editor 与测试继续使用独立的 `Poker.Rendering.Forward2D.Runtime`。

## 打开与操作

- **工具 > 卡牌战斗 > 2D验证 > 创建或更新场景**：生成专用美术、渲染配置及 `Assets/Scenes/Battle2DValidation.unity`。更新前需关闭已打开的同名验证场景；生成过程使用临时附加场景并在结束后关闭。
- **工具 > 卡牌战斗 > 2D验证 > 接入 XML 与可配置表现时间线**：原位迁移并保存已打开的 2D 验证场景；补齐 Host、XML 来源、Director 与动作组件。重复执行保留已有非空时间线和 XML 配置。
- **工具 > 卡牌战斗 > 2D验证 > 打开验证场景**：打开验证场景，进入 Play 后自动开局。点击卡牌选择，再使用“出牌”“弃牌”“结束回合”；“重新开始”创建新会话。
- **工具 > 卡牌战斗 > 2D验证 > 验证运行时输入与表现**：在未暂停的 Play 状态下，从新一局执行真实射线选牌和按钮事件，检查出牌、弃牌、敌方回合及播放中禁用后的同步。
- **工具 > 卡牌战斗 > 2D验证 > 记录当前渲染配置**：记录当前 Graphics、Quality 管线和验证相机实际使用的 Renderer；编辑模式与 Play 分别保存结果。

默认场景与 3D 主场景共用 `Packages/com.kaya.battle-engine/Content/SingleBattleDemoNew.xml`，编成为 `single-battle-new`。在根对象“2D 卡牌战斗”的 `XmlBattleSessionSource` 修改 XML 或 Encounter Id，下一次开战生效；无效配置拒绝重开并保留当前会话。旧 Demo SO 规则绑定已经清理，默认数值以 XML 为准（玩家 800 HP、敌人 1200 HP、8 张手牌、3 能量）。当前界面只提供一名玩家的输入和手牌；默认场景为 1v1，也可接入一名玩家、多名敌人的 Session。多人玩家切换、目标选择、复杂角色演出和正式产品布局不在本验证界面的范围内。

## 共用规则与表现边界

```text
XML → XmlBattleSessionSource → BattleSceneHost → BattleSession
2D 卡片/按钮 → Battle2DSceneAdapter → BattleSessionPlayback → BattleSession
有序规则时间线 → BattlePresentationBridge → 中性数据 → Battle2DView
                                                    ↓
                                    ScenePresentationDirector
                                    ↙                      ↘
                           时间线动作与信号             DisplayStore
                                                          ↓
                                               卡牌列表、HUD、日志
```

`Battle2DSceneAdapter` 负责规则输入、查询与场景适配，不再收集 SO 配置或创建会话。它通过共享 `BattlePresentationBridge` 把规则结果投影到只读展示数据，通过 `BattleInteractionQueries` 生成候选模型。View 和 Card 不引用或接收 `CardDuelState`、`CardDuelTransition`、`CombatEvent` 等规则对象；卡牌身份为中性字符串，图集 key 由 Bridge 提供。规则引用只位于适配边界；View、Card 和时间线动作只消费中性数据。

`Battle2DView` 实现中性的 `IBattlePresentationFrontend`：

- `Initialize` 初始化 `DisplayStore`，取消旧播放及反馈，清空日志。
- `Present` 将有序批次交给公共 Director。出牌配方的卡牌移动发出 `ReadyToAttack`，启动玩家矩形动作；矩形的 `Impact` 推进展示检查点，收招继续运行。弃牌向下移动，敌人真正行动时矩形向前再归位；敌人动作完成后推进检查点，避免多个敌人同时占用同一矩形；整次准备或攻击约 0.18 秒。眩晕跳过窗口不播放攻击。
- `Synchronize` 取消 Director 的当前作用域并显示指定快照，清除选择，由各动作的取消/同步协议恢复矩形；不执行规则。播放中禁用 View 会追上已提交的最终展示快照。
- `IsPresenting` 由公共 Director 统一提供；它会等待阻塞动作及收招全部完成。编辑模式或 Instant 模式原子发布最终展示快照，不伪造动画信号。
- 由 `ScenePresentationDirector.Update` 统一 Tick，View 不再另建 Director 或生成 BuildPlan。卡牌、矩形和 UI 读取同一个显示游标，动作上下文固定为所属事实的数据。

候选使用共享 `InteractionSession`。同类牌型更新复用反馈作用域，替换、清空或提交释放旧反馈；临时选中偏移和颜色移除后，根据卡牌当前布局 slot 重建外观，不恢复捕获的旧坐标。提交前冻结卡牌 ID，进入 Committing；Bridge 在启动已接受批次之前关闭候选。拒绝提交重新查询当前合法候选。候选显示识别结果与提交资格分别提供，不影响权威规则状态。

卡牌列表由当前展示快照对账，已有实例按稳定 ID 复用，列表重排只改变布局；UI 使用同一份生命、能量、牌堆和预算数据。一次命令返回前规则已完成结算，动画及取消只控制这些结果的展示进度。


## 时间线配置入口

根对象“2D 卡牌战斗”的 `ScenePresentationDirector.timeline` 保存动作顺序，子物体 `Presentation Timeline` 保存动作组件。运行时没有默认配方回退；缺少有效配置会在开战前报错。Editor 中的 `Battle2DTimelineSetup` 只负责生成初始配方。

| 规则事实 | 当前配方 | 显示检查点 |
| --- | --- | --- |
| ActionStarted / Play | cards → `ReadyToAttack` → attack | attack 的 `Impact` |
| ActionStarted / Discard | cards 向下准备，发出 `Released` | cards 完成 |
| ActionStarted / EnemyAction | 非 skipped 时 enemy attack | attack 完成（共用矩形的多个敌人依次收招） |

`cards` 绑定 Battle2DView，`player / enemy` 绑定矩形 Transform。卡牌使用 `Battle2DCardTimelineAction`，位移、时长、信号名及信号进度可配置；矩形使用公共 `TransformTimelineAction`，前冲段发出 Impact，第二段回位。所有动作派生 `PresentationActionBehaviour`。例如把 attack.after 从 Marker/ReadyToAttack 改成 Completed/cards，即可改成牌区完全准备结束才攻击，无需改 C#。

规则其余节点继续有序经过同一 Director；没有配置动作的事实立即公开显示快照，HUD 和日志随显示游标更新。可以继续为开战、阶段、效果、移牌和终局添加动作组，使用公共的效果/目标筛选与 checkpoint。当前只有一组敌人矩形，显示和输入仍是最简验证布局。

卡牌布局、预选偏移、已提交动作偏移分别维护。表现取消只释放自己的动作偏移；选牌切换与取消继续由 InteractionSession 释放反馈，并根据最新 slot 重建位置。动作不会修改 CardList，成员变化仍以显示快照为准。

## 卡牌美术复用

生成链为：

```text
Assets/GameAssets/CardArt/Prefabs/Card.prefab → 共享 Mesh
Assets/Art/Cards/StandardClassic 牌面 → 现有 CardAtlases/Classic.asset 图集
    ↓ 两项资源交给导出器
Battle2DCardArt.asset：Unlit 支持的颜色组合
    → CardPrefabExporter + CardArt/Validation Unlit
    → Art/Battle2DCard.prefab 与专用材质
    → CardView：卡面 ID、图集 UV 和每实例 MaterialPropertyBlock
```

2D 卡牌指定使用 `Assets/Art/Cards/StandardClassic` 牌面，通过已有 `Assets/Art/CardAtlases/Classic.asset` 图集显示。网格从原 `Card.prefab` 读取，继续使用 `CardView`、平面排布与正交相机。专用美术配置仅定义 `base`、`selected`、`disabled` 的 `_ColorTint`；所有卡牌实例共享 Classic 图集、导出的材质和原网格。`Battle2DCard` 把逻辑实例 ID 绑定到卡面美术 ID，通过 `CardView.SetCard` 选择图集区域，再用 `PresentArtwork` 和 MPB 更新单牌颜色；选择不会修改共享材质或原 3D 美术资产。卡片上的点击接口只向 View 提交选择意图。

`CardArt/Validation Unlit` 通过前向 Unlit Pass 绘制，保留现有卡面 UV、正反面和透明裁切协议。新材质与配置位于本模块 `Art`，原 `Card.prefab` 及其材质继续供 3D 使用。

## 固定 Renderer 与共用管线

`Assets/Settings/Battle2DRenderer.asset` 使用项目自有的 `RetroRendering.Simple2DRendererData`，创建小型前向绘制实现 `Simple2DRenderer`。它直接继承 URP 的 `ScriptableRendererData` / `ScriptableRenderer`，Renderer Feature 列表为空，关闭 Native Render Pass。验证场景采用正交相机、Unlit 卡牌与矩形、卡牌射线点击及独立 Canvas。

项目始终使用 `Assets/Settings/MainURP.asset`。创建器将上述 Renderer 追加注册到 MainURP，并按资产引用找到索引后对验证相机调用 `SetRenderer`；不会重排现有槽位。3D 主 Renderer 保持槽 0，UI Renderer 保持槽 1，验证 Renderer 当前位于槽 2。Graphics、Quality 的管线引用在编辑模式与 Play 中保持一致，只由各相机选择不同 Renderer。

创建器的 `EnsureAssets()` 更新本模块美术、固定 Settings 下的 Renderer 及其 MainURP 注册项，不打开场景。验证模块不再拥有私有 Pipeline 或 Play 期间切换管线的作用域组件，退出运行无需恢复管线。现有 3D 和 UI Renderer 的配置、默认索引及场景绑定保持原样。

## 验证范围

`Battle2DValidationTests` 使用预览场景和真实卡片/按钮事件，覆盖中性接口不暴露规则类型、候选更新与清空、纯展示快照重排保持实例、编辑模式即时结果、重复绑定、无效选择拒绝、胜负与重启、固定种子命令与无界面规则执行等价、共享图集/材质与独立 MPB，以及固定管线中的相机 Renderer 选择和原槽位保护。`BattleSessionTests` 和 `BattleSessionPlaybackTests` 继续验证纯规则及立即/逐步表现的一致性。

同一测试文件中的 `Simple2DRenderingTests` 通过 URP 的 `SingleCameraRequest` 实际渲染到独立 RenderTexture，检查卡牌朝向与位置、深度遮挡，以及前后透明矩形的排序和混合。

`Battle2DPlayValidation` 在实际 PlayerLoop 中通过 `EventSystem.RaycastAll` 检查卡片与按钮的首个命中，再派发指针事件。它等待每批表现结束，检查权威状态在播放期间不变、展示追上 Session、卡牌与双方矩形发生位移、矩形最终归位；另在敌方移动期间禁用再启用 View，检查同步没有重复结算。运行会重开当前验证战斗，只影响运行时状态。

结果输出至 `Library/Battle2DValidation/play.json`，游戏画面保存为 `game-screen.png`。运行中持续检查 Graphics、Quality 与生效管线均为 MainURP，验证相机的 Renderer 实例为 `Simple2DRenderer`。JSON 记录这些管线路径，以及相机 Renderer 的类型、索引和资产路径。“记录当前渲染配置”在编辑模式输出 `rendering-edit.json`，在 Play 输出 `rendering-play.json`，用于核对两种状态采用相同管线和相机 Renderer。

新增回归覆盖 XML 与无界面规则执行一致性、无效 XML 编成保留会话、配方生成幂等、卡牌动作取消后按最新布局恢复，以及真实场景中修改依赖后攻击启动时点随之改变。统一入口 `工具 > 卡牌战斗 > 表现时间线 > 验证 EditMode / 验证 PlayMode` 同时覆盖 2D 与 3D。

角色位移恢复由 TransformTimelineAction 的作用域与 restoreOnComplete 配置负责，View 不再捕获或强制还原角色起点。
