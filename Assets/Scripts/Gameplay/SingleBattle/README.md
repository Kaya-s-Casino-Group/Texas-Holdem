# 主线卡牌战斗（SingleBattle）

SingleBattle 是主线卡牌战斗，支持 1v1 与多对多；DrawBattle 是独立的[分数挑战](../DrawBattle/README.md)。同一套战斗规则可以接入不同表现风格。

统一表现系统见[单局卡牌战斗表现系统设计](../../../../Tools/BattlePresentationDesign.md)。规则时间线、公共显示模型、动作与信号调度、预选切换和取消恢复已接入当前 3D 与最简 2D 前端；通用协议与 Unity 适配组件见 [BattlePresentation](../../Presentation/BattlePresentation/README.md)。下文描述当前实现与仍保留的兼容入口。

## 目录归属

| 目录 | 职责 | 程序集 |
| --- | --- | --- |
| [Hosting](Hosting/README.md) | 每局通用入口、服务接口、最小预制体与编辑器工具 | `Poker.BattleHosting.Runtime` |
| [Presentation/Shared](Presentation/Shared/Runtime) | Bridge、公开数据投影、候选查询及不依赖场景对象的 Unity 适配器 | `Poker.BattlePresentation.Shared` / `.Unity` |
| [../../Presentation/BattlePresentation](../../Presentation/BattlePresentation/README.md) | 无规则依赖的显示存储、动作调度、交互会话及可选 Unity 组件 | `BattlePresentation.Runtime` / `.Unity` |
| [Presentation/Scene3D](Presentation/Scene3D/README.md) | 现有场景适配器、Actor、VAT、盾牌、HUD 与接入工具 | `Poker.SingleBattle.Runtime` / `.Editor` / `.Tests.*` |
| [Presentation/Minimal2D](Presentation/Minimal2D/README.md) | 最简卡牌与矩形界面、验证场景和测试 | `Poker.Battle2DValidation.Runtime` / `.Editor` / `.Tests.Editor` |
| [../PokerEngine](../../Rules/PokerEngine/README.md) | 不依赖 Unity 的规则、BattleSession、播放协调与公共表现接口 | `PokerEngine.*` |
| [../Rendering/Runtime/Forward2D](../../Infrastructure/Rendering/README.md) | 小型 URP 前向 Renderer | `Poker.Rendering.Forward2D.Runtime` |

每个模块内部按 `Runtime / Editor / Tests` 区分编译范围；Runtime 内按职责分文件夹，不为每个 Presenter 新建程序集。`Scene3D` 与 `Minimal2D` 不互相引用。命名空间和前端程序集名保留。原 `SingleBattleHost` 更名为 `SingleBattleSceneAdapter`，保留脚本 `.meta` GUID 和场景引用；生命周期迁至通用 Host。

## 依赖边界

```text
Scene3D ──→ XML 内容编译与编成 ──→ PokerEngine
Minimal2D ──→ XML Session Source ──→ PokerEngine

BattleSceneHost ──继承──→ BaseBattleHost
                          │ 持有
                          ↓
                      BattleHost
                          │ 管理
                          ↓
                 BattleSessionPlayback ──→ BattleSession
                          │
                          ↓
              IBattleSessionPresentation
                          ↓
              BattlePresentationBridge
                          ↓ 中性快照与有序事实
              IBattlePresentationFrontend
                  ↙                ↘
   ScenePresentationDirector ← Battle2DView
                  └── DisplayStore + PresentationDirector ──┘
```

旧战斗 SO 配置层与资产已清理。两个前端均通过 `BattleContentCompiler / BattleContentFactory` 读取 XML 并生成不可变会话。`SingleBattleDemoNew` / `Battle2DValidation` 使用规则包的 `SingleBattleDemoNew.xml`；其他原有场景使用 `Assets/GameAssets/SingleBattle/Content/SceneBattles.xml`，保留原数值。配置修改只在下一场开战生效。

`BattleSession` 原子完成规则推进，同时记录带中间状态的有序 `Timeline`。`BattleSessionPlayback` 通过 `PokerEngine.Composition` 中保留的 `IBattleSessionPresentation` 提交整批结果；共享 Bridge 按观察者过滤并转换为 `BattleDisplaySnapshot / BattleDisplayChange`。两种前端再使用同一套 `DisplayStore / PresentationDirector / InteractionSession`，普通动作不接收规则命令或规则对象。两层入口都保留 `Initialize / Present / Synchronize / IsPresenting` 语义，播放时长、信号与取消不会改变已提交规则结果。

规则节点包括开战、阶段开始、行动开始、判型、费用与效果结算、移牌/发牌、行动完成、阶段结束及战斗结束。Director 依序消费事实；每组动作可以按 `Started / Marker / Completed` 依赖并发或顺序执行，在配置的 checkpoint 发布显示状态。发布可早于动作收招完成，但输入仍等全部阻塞动作结束。HUD 只读已发布状态，避免提前显示整批最终生命、牌区或胜负。`Instant` 校验整批后一次发布最终状态，不调用普通动作或伪造信号。

3D 的卡牌桌面交互继续使用 [CardInteraction](../../Presentation/CardInteraction/README.md) 与 [CardPresentation](../../Presentation/CardPresentation/README.md)，这些模块也服务分数挑战。它们包含场景牌区、Canvas 与动画适配，不是所有表现风格必须依赖的公共接口。Minimal2D 只复用 [CardArt](../../../../Tools/CardAtlas.md) 的卡牌资源协议，不依赖 3D Host、Actor、VAT、CardZoneView 或 DOTween。

2D Renderer 代码属于 Rendering 基础设施。固定资产仍为 `Assets/Settings/Battle2DRenderer.asset`，场景相机从 `MainURP` 选择它；3D 主 Renderer 和 UI Renderer 的槽位保持不变。

## 通用 Host 与场景适配器

每个 Battle 的生命周期由通用 Host 管理，场景适配器负责输入、绑定和表现。最小预制体与一键添加方式见 [入口配置教程](../../../../Tools/BattleSceneHostSetup.md)。共用入口如下：

| 共用入口 | 职责 |
| --- | --- |
| `BattleHost.StartBattle / RestartBattle / AttachSession / Synchronize`（Composition） | 创建、接入、重开和同步会话；持有唯一 Session 与 Playback |
| `BaseBattleHost`（Hosting） | 通用入口的 MonoBehaviour 基类，转发公共生命周期并调用场景扩展点 |
| `BattleContentCompiler / BattleContentFactory`（Composition） | 校验 XML 并按编成创建未开局 Session；可指定战局 ID 与随机状态 |
| `BattleSessionPlayback.Start / Submit`（Composition） | 统一开局和当前版本命令提交；读取表现门禁，交给 Session 原子结算，再派发表现 |
| `BattleSessionPlayback.CanAct / CanPlay / CanDiscard` | 共用行动阶段、牌区、费用、张数和弃牌预算查询 |
| `BattleSessionPlayback.PreviewPlay / PreviewDiscard` | 对当前权威快照只读验算，不提交状态或驱动表现 |

`SingleBattleSceneAdapter` 从场景 Actor 收集阵容，校验输入来源、切换观察玩家、绑定 CardMatch/Hand；`SingleBattleSceneAdapter.Presentation.cs` 将显示游标交回 CardMatch 适配入口，动作顺序来自场景时间线。`Battle2DSceneAdapter` 将本地选牌和按钮转换为相同命令，会话由独立的 XML 来源创建；`Battle2DView` 仅消费中性显示、候选和动作协议。两种前端均通过共享 Bridge 接入，不维护命令版本号，也不自行推进敌人回合。

3D 场景适配器的 `BuildConfiguredSession` 调用唯一的 XML 编译组装入口；XML 编成拥有属性、牌组、牌型、状态和遗物，场景 Actor 仅对应阵容席位。新场景及 HUD 文字反馈用法见 [Scene3D 说明](Presentation/Scene3D/README.md)。

`BattleSceneHost` 继承 `BaseBattleHost`，两种场景分别通过 `SingleBattleSceneAdapter` 和 `Battle2DSceneAdapter` 接入通用 Host。基类组合普通 C# `BattleHost`，由它先建立未开局 Session，再绑定表现，最后调用 `Playback.Start`。`RestartBattle` 重新读取当前配置创建新会话；`SynchronizeBattle` 取消旧动作和预选作用域，并显示权威快照，不执行规则。3D 保留开局发牌动画，2D 默认开局节点无动作时即时公开显示快照；`AttachSession` 只接入已有状态，不自动重新开局。参与者 ID、阵容顺序与自定义牌堆/初始生命仍由输入配置确定。场景适配器通过共享 `BattleInteractionQueries` 生成 `CandidatePresentationModel`，`InteractionSession` 校验选择版本并管理切换、清空和提交；UI 不自行调用规则预览。3D 的输入按钮仍可使用轻量规则资格查询，正式提交仍由 Session 校验。

## 工具与场景

- **工具 > 卡牌战斗 > Battle入口**：添加、定位或迁移通用入口，以及新增同场景独立 Battle。
- **工具 > 卡牌战斗 > 数值配置**：定位当前 XML 与编成，校验后用内容编辑工具修改。
- **工具 > 卡牌战斗 > 场景接入 / 示例场景 / 验算**：现有 3D 工具。
- **工具 > 卡牌战斗 > 2D验证**：创建、打开与验证最简 2D 场景。
- **工具 > 测试面板**：规则、表现与打包边界回归。

场景继续放在 `Assets/Scenes`：`SingleBattleDemo`、`SingleBattleDemoNew`、`TeamBattleDemo` 与 `Battle2DValidation`。目录迁移不重新生成场景，也不修改配置数值或玩法。3D 的配置用法和表现细节见 [Scene3D 说明](Presentation/Scene3D/README.md)，2D 的操作与验证见 [Minimal2D 说明](Presentation/Minimal2D/README.md)。
