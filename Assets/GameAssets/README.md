# 模块资源

这里保存自研模块使用的预制件、配置、Shader、材质和贴图。对应 C# 代码位于 [Scripts](../Scripts/README.md)，不同表现风格继续分开存放。

| 目录 | 内容 |
| --- | --- |
| `SingleBattle/Content` | 原有示例场景的 XML 规则与编成（保留原数值） |
| `SingleBattle/Presentation/Scene3D` | 3D 盾牌与 Shader |
| `SingleBattle/Presentation/Minimal2D/Art` | 最简 2D 卡牌与矩形角色资源 |
| `CardArt` | 卡牌预制件、配置、Shader 与通用贴图 |
| `CardPresentation` | 卡牌 UI、预制件、音效 Resources |
| `PokerScoring/Profiles` | 计分配置 |
| `RunFlow/Configs` | 流程配置 |
| `Rendering` | 渲染 Shader、材质、贴图与预制件 |
| `Editor/Shaders` | 编辑器资源验证 Shader |

迁移保留原 `.meta` / GUID，不重新生成资源。通用美术与原始牌面仍在 `Assets/Art`，场景仍在 `Assets/Scenes`，固定管线和 Renderer 仍在 `Assets/Settings`。
