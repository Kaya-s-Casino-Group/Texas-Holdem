# 主线卡牌战斗（SingleBattle）

SingleBattle 是主线卡牌战斗，支持 1v1 与多对多；DrawBattle 是独立的[分数挑战](../DrawBattle/README.md)。同一套战斗规则可以接入不同表现风格。

## 目录归属

| 目录 | 职责 | 程序集 |
| --- | --- | --- |
| [Hosting](Hosting/README.md) | 共享 Unity 场景基类，转接纯 C# 生命周期 | `Poker.BattleHosting.Runtime` |
| [Configuration](Configuration/README.md) | 共享规则与角色配置、配置工厂、默认资产 | `Poker.BattleConfiguration.Runtime` / `.Editor` |
| [Presentation/Scene3D](Presentation/Scene3D/README.md) | 现有场景 Host、Actor、VAT、盾牌、HUD 与接入工具 | `Poker.SingleBattle.Runtime` / `.Editor` / `.Tests.*` |
| [Presentation/Minimal2D](Presentation/Minimal2D/README.md) | 最简卡牌与矩形界面、验证场景和测试 | `Poker.Battle2DValidation.Runtime` / `.Editor` / `.Tests.Editor` |
| [../PokerEngine](../../Rules/PokerEngine/README.md) | 不依赖 Unity 的规则、BattleSession、播放协调与公共表现接口 | `PokerEngine.*` |
| [../Rendering/Runtime/Forward2D](../../Infrastructure/Rendering/README.md) | 小型 URP 前向 Renderer | `Poker.Rendering.Forward2D.Runtime` |

每个模块内部按 `Runtime / Editor / Tests` 区分编译范围；Runtime 内按职责分文件夹，不为每个 Presenter 新建程序集。`Scene3D` 与 `Minimal2D` 不互相引用。现有类名、命名空间和前端程序集名保持兼容，文件移动时保留 `.meta` 与 GUID。

## 依赖边界

```text
Scene3D ─────┐
            ├── Configuration ──→ PokerEngine + Poker.Scoring
Minimal2D ───┘

两个场景 Host ──继承──→ BaseBattleHost
                          │ 持有
                          ↓
                      BattleHost
                          │ 管理
                          ↓
                 BattleSessionPlayback ──→ BattleSession
                          │
                          ↓
              IBattleSessionPresentation
                  ↙                ↘
          SingleBattleHost      Battle2DView
```

`Configuration` 只将 ScriptableObject 作者数据构建成纯规则快照，不持有 Host、场景对象、动画或美术资源。配置资产归在 `Assets/GameAssets/SingleBattle/Configuration/Profiles`；同一资产可被两种前端引用，修改在下一场开战生效。

`BattleSession` 完成规则推进并产出已结算步骤；`BattleSessionPlayback` 向公共表现接口提交快照。两种风格都遵守 `Initialize / Present / Synchronize / IsPresenting`，播放时长和取消不会改变规则结果。接口仍位于 `PokerEngine.Composition`，不依赖 Unity。

3D 的卡牌桌面交互继续使用 [CardInteraction](../../Presentation/CardInteraction/README.md) 与 [CardPresentation](../../Presentation/CardPresentation/README.md)，这些模块也服务分数挑战。它们包含场景牌区、Canvas 与动画适配，不是所有表现风格必须依赖的公共接口。Minimal2D 只复用 [CardArt](../../../../Tools/CardAtlas.md) 的卡牌资源协议，不依赖 3D Host、Actor、VAT、CardZoneView 或 DOTween。

2D Renderer 代码属于 Rendering 基础设施。固定资产仍为 `Assets/Settings/Battle2DRenderer.asset`，场景相机从 `MainURP` 选择它；3D 主 Renderer 和 UI Renderer 的槽位保持不变。

## 两种 Host 的职责

两边只负责输入适配、场景绑定和表现，共同使用以下入口：

| 共用入口 | 职责 |
| --- | --- |
| `BattleHost.StartBattle / RestartBattle / AttachSession / Synchronize`（Composition） | 创建、接入、重开和同步会话；持有唯一 Session 与 Playback |
| `BaseBattleHost`（Hosting） | 两种场景 Host 的 MonoBehaviour 基类，转发公共生命周期并调用场景扩展点 |
| `BattleSessionFactory.Create`（Configuration） | 从规则配置与显式阵容 Setup 构建引擎、初始状态和未开局 Session；允许传入战局 ID 与随机状态 |
| `BattleSessionPlayback.Start / Submit`（Composition） | 统一开局和当前版本命令提交；读取表现门禁，交给 Session 原子结算，再派发表现 |
| `BattleSessionPlayback.CanAct / CanPlay / CanDiscard` | 共用行动阶段、牌区、费用、张数和弃牌预算查询 |
| `BattleSessionPlayback.PreviewPlay / PreviewDiscard` | 对当前权威快照只读验算，不提交状态或驱动表现 |

`SingleBattleHost` 从场景 Actor 收集阵容，校验输入来源、切换观察玩家、绑定 CardMatch/Hand，并在 `SingleBattleHost.Presentation.cs` 投影 3D 步骤。`Battle2DHost` 从 Inspector 配置收集阵容，把本地选牌和按钮转换为相同命令，绑定 `Battle2DView` 并显示拒绝提示。Host 不构造规则引擎、不维护命令版本号，也不自行推进敌人回合。

`SingleBattleHost` 与 `Battle2DHost` 均继承 `BaseBattleHost`。基类组合普通 C# `BattleHost`，由它先建立未开局 Session，再绑定表现，最后调用 `Playback.Start`。`RestartBattle` 重新读取当前配置创建新会话；`SynchronizeBattle` 取消表现并显示权威快照，不执行规则。3D 保留开局发牌动画，2D 保留即时开局；`AttachSession` 只接入已有状态，不自动重新开局。参与者 ID、阵容顺序与自定义牌堆/初始生命仍由输入配置确定。3D 的快速按钮检查使用共用轻量查询，2D 保留完整预览驱动按钮；规则算法都在共用层，前端只决定交互反馈方式。

## 工具与场景

- **工具 > 卡牌战斗 > 数值配置**：编辑当前 3D 场景 Host 绑定的共享配置；运行时只读。
- **工具 > 卡牌战斗 > 默认配置**：打开公共配置资产。
- **工具 > 卡牌战斗 > 场景接入 / 示例场景 / 验算**：现有 3D 工具。
- **工具 > 卡牌战斗 > 2D验证**：创建、打开与验证最简 2D 场景。
- **工具 > 测试面板**：规则、表现与打包边界回归。

场景继续放在 `Assets/Scenes`：`SingleBattleDemo`、`SingleBattleDemoNew`、`TeamBattleDemo` 与 `Battle2DValidation`。目录迁移不重新生成场景，也不修改配置数值或玩法。3D 的配置用法和表现细节见 [Scene3D 说明](Presentation/Scene3D/README.md)，2D 的操作与验证见 [Minimal2D 说明](Presentation/Minimal2D/README.md)。
