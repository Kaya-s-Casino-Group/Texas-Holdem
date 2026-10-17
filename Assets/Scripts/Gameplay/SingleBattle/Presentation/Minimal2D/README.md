# 卡牌战斗 2D 验证（Battle2DValidation）

本模块提供独立的最简 2D 战斗界面，用实际选牌、出牌、弃牌和敌方回合验证规则与表现的边界。它与 [主线卡牌战斗的 3D 表现](../Scene3D/README.md) 共用 `BattleSession`、`BattleSessionPlayback` 和 `IBattleSessionPresentation`。这是架构验证入口，尚不作为正式产品界面。

## 目录与程序集

`Runtime/Hosting` 保存配置收集、场景绑定和输入适配；`Runtime/Presentation` 保存基础卡牌与矩形表现；`Editor`、`Tests/Editor` 分别归工具与验证，风格资产位于 `Assets/GameAssets/SingleBattle/Presentation/Minimal2D/Art`。沿用 `Poker.Battle2DValidation.*` 程序集名。

`Battle2DHost` 继承 `BaseBattleHost`，共用纯 C# `BattleHost` 管理会话创建、接入、重开和同步。Runtime 直接引用 `Poker.BattleHosting.Runtime` 和 `Poker.BattleConfiguration.Runtime`，不引用 `Poker.SingleBattle.Runtime`；Editor 与测试使用独立的 `Poker.Rendering.Forward2D.Runtime`，不引入 3D 渲染特效程序集。共用 Profile 位于 `../../Configuration`。

## 打开与操作

- **工具 > 卡牌战斗 > 2D验证 > 创建或更新场景**：生成专用美术、渲染配置及 `Assets/Scenes/Battle2DValidation.unity`。更新前需关闭已打开的同名验证场景；生成过程使用临时附加场景并在结束后关闭。
- **工具 > 卡牌战斗 > 2D验证 > 打开验证场景**：打开验证场景，进入 Play 后自动开局。点击卡牌选择，再使用“出牌”“弃牌”“结束回合”；“重新开始”创建新会话。
- **工具 > 卡牌战斗 > 2D验证 > 验证运行时输入与表现**：在未暂停的 Play 状态下，从新一局执行真实射线选牌和按钮事件，检查出牌、弃牌、敌方回合及播放中禁用后的同步。
- **工具 > 卡牌战斗 > 2D验证 > 记录当前渲染配置**：记录当前 Graphics、Quality 管线和验证相机实际使用的 Renderer；编辑模式与 Play 分别保存结果。

默认场景使用现有 `DemoBattleRules`、`DemoPlayer` 和 `DemoEnemy` 配置。修改配置在下一场开战时形成规则快照。当前界面只提供一名玩家的输入和手牌；默认场景为 1v1，也可接入一名玩家、多名敌人的 Session。多人玩家切换、目标选择、复杂角色演出和正式产品布局不在本验证界面的范围内。

## 共用规则与表现边界

```text
2D 卡片/按钮 → Battle2DHost → BattleSessionPlayback → BattleSession
                                   ↓                    ↓
                      IBattleSessionPresentation ← 已结算步骤批次
                                   ↓
                             Battle2DView
```

`Battle2DHost` 负责收集场景配置和适配本地输入。`StartBattle()` 将阵容 Setup 交给共用 `BattleSessionFactory`，绑定 View 后通过 `Playback.Start` 开局；`AttachSession(session)` 接入已有会话，也接受已经开局的状态。出牌、弃牌和结束回合统一通过 `Playback.Submit` 提交；行动门禁与完整预览分别通过共用 `CanAct` 和 `PreviewPlay/PreviewDiscard` 查询。Host 只捕获当前选择和展示拒绝提示，不再组装引擎或拼装 expectedRevision。重新 Attach 时先停用旧命令入口，初始化新表现后刷新按钮，避免查询到上一场的状态。

`Battle2DView` 实现与 `SingleBattleHost` 相同的表现接口：

- `Initialize` 显示传入快照并清空选择和日志。
- `Present` 在 Play 且请求动画时逐步显示已结算快照：出牌向上移动、弃牌向下移动，玩家和敌方矩形在攻击时前进再归位；每段移动约 0.18 秒。使用普通协程和 Transform，不依赖 DOTween。
- `Synchronize` 终止当前表现，显示给定状态、清空本地选择并让矩形归位，不推进规则。播放中禁用 View 也会追上已提交状态。
- `IsPresenting` 在步骤播放期间禁止重复输入；编辑模式或 `animate: false` 立即显示最终状态。

一次命令返回前，Session 已完成自动敌人行动、收牌和下一决策点的推进。2D 使用卡牌与矩形的基础移动；现有 3D 使用攻击、命中和补牌演出。两者读取同一类步骤快照，都不在动画完成回调中执行战斗规则。播放速度和取消只影响展示进度。`CardInstanceId`、牌区、生命、格挡、能量和随机状态由 Session 持有；选中哪些卡、颜色和布局属于前端。

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

`Battle2DValidationTests` 使用预览场景和真实卡片/按钮事件，覆盖编辑模式即时结果、重复绑定、无效选择拒绝、胜负与重启、固定种子命令与无界面规则执行等价、共享图集/材质与独立 MPB，以及固定管线中的相机 Renderer 选择和原槽位保护。`BattleSessionTests` 和 `BattleSessionPlaybackTests` 继续验证纯规则及立即/逐步表现的一致性。

同一测试文件中的 `Simple2DRenderingTests` 通过 URP 的 `SingleCameraRequest` 实际渲染到独立 RenderTexture，检查卡牌朝向与位置、深度遮挡，以及前后透明矩形的排序和混合。

`Battle2DPlayValidation` 在实际 PlayerLoop 中通过 `EventSystem.RaycastAll` 检查卡片与按钮的首个命中，再派发指针事件。它等待每批表现结束，检查权威状态在播放期间不变、展示追上 Session、卡牌与双方矩形发生位移、矩形最终归位；另在敌方移动期间禁用再启用 View，检查同步没有重复结算。运行会重开当前验证战斗，只影响运行时状态。

结果输出至 `Library/Battle2DValidation/play.json`，游戏画面保存为 `game-screen.png`。运行中持续检查 Graphics、Quality 与生效管线均为 MainURP，验证相机的 Renderer 实例为 `Simple2DRenderer`。JSON 记录这些管线路径，以及相机 Renderer 的类型、索引和资产路径。“记录当前渲染配置”在编辑模式输出 `rendering-edit.json`，在 Play 输出 `rendering-play.json`，用于核对两种状态采用相同管线和相机 Renderer。
