# 渲染模块

当前工程使用 Unity 2022.3.62f3 自带的官方 URP 14.0.12。`Packages/manifest.json` 声明版本，由 Package Manager 解析；不再携带嵌入式 URP 修改或自研 Renderer。

Graphics 与所有 Quality 等级统一使用 `Assets/Settings/BattleURP.asset`，只有一个 `BattleForwardRenderer.asset` 槽位，采用标准 `UniversalRendererData`，Rendering Path 为 Forward，Renderer Features 为空，Render Scale 为 1。

`Assets/Scenes/BattleDemo.unity` 是唯一保存的场景。牌区使用场景主相机，HUD 使用 Screen Space Overlay。卡牌 Mesh 和 Canvas Shader 均不依赖法线或场景光照；镭射、火漆颜色/自发光、红叉、实例颜色和图集采样继续保留。

`Runtime/Cameras` 保留通用 URP 相机叠加、视口同步桥接；`Runtime/VAT` 保留可选的动画播放桥接；`Runtime/Infrastructure` 保留帧率支持。历史命名空间 `RetroRendering` 为兼容已有脚本引用保留，不代表继续使用旧管线。旧环境组件不在 BattleDemo 中启用。

旧 Deferred、降分辨率、SSAO、雾、体积光、Bloom、CRT、自研 Simple2DRenderer 及相关美术资产见本地归档分支 `legacy-3d-scenes`。
