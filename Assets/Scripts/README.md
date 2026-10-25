# Palamedes 脚本模块

当前 Unity 工程只保留表现重构需要的基础模块：

| 目录 | 职责 |
| --- | --- |
| `Infrastructure/Rendering` | 定制 URP 的 Renderer、后处理、相机、VAT 与环境渲染组件，以及编辑器工具和测试 |
| `Presentation/CardArt` | 共享网格/材质、MPB 卡牌实例参数、图集、外观配置与导出工具 |
| `Presentation/BattlePresentation` | 中性显示数据、DisplayStore、动作调度、交互协议与 Unity 时间线接入 |
| `Gameplay/SingleBattle/Hosting` | 会话生命周期、XML 来源与通用场景宿主 |
| `Gameplay/SingleBattle/Presentation/Shared` | 规则结果投影、候选查询和通用时间线适配器 |
| `Tools/EditorTools` | 卡牌编辑器使用的公共窗口工具 |

玩法、权威状态与结算继续由 `External/BattleToolkit/Engine` 提供。Unity 不新增规则副本。旧 Scene3D / Minimal2D 前端、CardMatch / CardInteraction / CardPresentation、DrawBattle / RunFlow 及其集成代码已从本分支清理。

Runtime、Editor、Tests 继续通过 asmdef 分离。新增前端应实现现有显示与输入接口；保留模块的程序集名和资产 GUID 沿用原值。
