# 项目脚本目录

自研 C# 代码统一放在 `Assets/Scripts`，按职责分组；模块内部沿用 `Runtime / Editor / Tests` 和既有程序集名。

| 目录 | 内容 |
| --- | --- |
| `Rules/PokerEngine` | 无 Unity 依赖的规则、会话、生命周期与公共表现协议 |
| `Rules/PokerScoring` | Unity 计分配置适配 |
| `Gameplay/SingleBattle` | 主线卡牌战斗的共享配置、Hosting、Scene3D 与 Minimal2D 前端 |
| `Gameplay/DrawBattle` | 独立分数挑战 |
| `Gameplay/RunFlow` | 关卡流程 |
| `Presentation/CardArt` | 卡牌美术协议、运行时组件和导出工具 |
| `Presentation/CardInteraction` | 卡牌输入、命令适配、牌桌镜像和 Presenter |
| `Presentation/CardPresentation` | 卡牌显示协议、World/Canvas 与反馈 |
| `Infrastructure/Rendering` | 渲染特效、VAT、独立 Forward2D Renderer |
| `Infrastructure/UIFoundation` | 公共 UI 基础组件 |
| `Tools/EditorTools` | 有独立程序集的公共编辑器工具与测试 |
| `Tools/Editor` | 构建、模型导入和资源校验工具；保持默认 Editor 程序集 |

## 资源与代码的边界

模块专用预制件、配置、材质、贴图、Shader 和 Resources 目录位于 [GameAssets](../GameAssets/README.md)。通用美术、音频、字体、场景、管线设置分别保留在 `Assets/Art / Audio / Fonts / Scenes / Settings`。第三方代码保留在 `Assets/Plugins` 或 Packages。

Shader 属于模块资源，与其 HLSL 文件一起迁移；`Resources` 保留目录名和内部路径，因此运行时资源加载键不变。序列化引用以 GUID 保持，工具中的路径常量、Shader include、离线测试脚本和文档链接同步更新。

不要在 Assets 一级新增自研代码模块，也不要改变程序集名来配合文件夹命名。两种战斗表现仍使用独立程序集，通用层不能反向引用具体前端。跨模块依赖通过 asmdef 显式声明。

## 战斗入口

- [生命周期与场景基类](Gameplay/SingleBattle/Hosting/README.md)
- [主线卡牌战斗](Gameplay/SingleBattle/README.md)
- [3D 场景表现](Gameplay/SingleBattle/Presentation/Scene3D/README.md)
- [最简 2D 验证](Gameplay/SingleBattle/Presentation/Minimal2D/README.md)
- [纯规则核心](Rules/PokerEngine/README.md)

`BattleHost` 管会话生命周期；`BattleSessionPlayback` 管命令与表现衔接；`BattleSession` 管权威规则；`CardMatchHost` 在战斗中管理前端牌桌镜像。2D 前端无需依赖 CardMatchHost。
