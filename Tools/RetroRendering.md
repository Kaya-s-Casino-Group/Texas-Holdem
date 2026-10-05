# Renderer 降分渲染与独立后处理

Unity 2022.3.62f3 / 内嵌 URP 14.0.12 / Deferred。复古效果是根据《邪恶冥刻》的视觉方向做的可调近似，没有提取原游戏 Shader。

## 配置入口

选择 `Assets/Settings/MainDeferredRenderer.asset`，有两个独立入口。

### Low Resolution Rendering：Renderer 固有能力

- **Enabled**：启用内部降分渲染和 Point 输出。与 Renderer Features 列表无关，删除所有 Feature 仍可工作。
- **Maximum Render Height**：可配置整数，默认 540；也可设为 360、720 等，没有写死 540 上限。宽度按相机原生输出宽高比四舍五入；低于设定高度的输出保持自身尺寸。
- **Apply To Render Textures**：默认开，离屏游戏相机和 URP Render Request 同样使用该能力。深度专用目标跳过。
- **Apply To Scene View**：默认关，Scene View 保持清晰。Preview、Reflection 和 XR 相机跳过。

开启时按原始输出尺寸计算内部尺寸，覆盖该相机本次渲染的 URP Render Scale；不改写共享 URP Asset。关闭时恢复 URP 自身的 Render Scale 和采样流程，所以不一定是内部原生分辨率。

| 输出 | 配置高度 | 实际颜色 / 深度 / GBuffer |
| --- | --- | --- |
| 1920×1080 | 540 | 960×540 |
| 2560×1080 | 540 | 1280×540 |
| 1080×1920 | 540 | 304×540 |
| 800×450 | 540 | 800×450 |
| 1920×1080 | 360 | 640×360 |
| 1920×1080 | 720 | 1280×720 |

保持相机原始投影、Viewport 和输出尺寸。Point 放大到原生输出，不固定为 16:9、不额外留黑边。非整数倍率会出现宽度不一致的像素块；像素尺寸取整导致的比例误差最多半个内部像素。Screen Space Overlay UI 在最终输出后绘制，保持原生清晰度；Camera/World Space Canvas 参与场景低分辨率渲染。

### 复古后处理：独立 Renderer Feature

实现为 `Assets/Rendering/Runtime/RetroPostProcessFeature.cs`，Shader 为 `Assets/Rendering/Shaders/RetroPost.shader`。Feature 只负责后处理，不设置分辨率、相机 AA 或最终放大采样；可独立开关、移除，也可在 Renderer 降分能力关闭时使用。原有 Shader 和 Feature 的资产 GUID 与美术参数已保留。

中文 Inspector 的「执行时机 → 执行事件」可选透明物体渲染后、内置后处理前/后、场景渲染结束时、升采样前（默认推荐）。事件在每次加入渲染队列时读取，修改后下一次渲染即生效。

Inspector 使用中文分组、标签和悬停说明，支持撤销和多对象编辑。色阶化强度为 0 时，对应从属参数置灰但保留数值。

参数包括曝光、对比度、饱和度、暖色调、暗角、按通道色阶、固定 Bayer 抖动和色差。默认色阶 64 级、低混合量；色差范围为 0–5 个内部像素。

所有效果仍在选定事件执行，默认升采样前。Feature 的 Apply To Render Textures / Scene View 只控制效果作用范围，与 Renderer 的作用范围各自独立。

## 实现边界

1. `UniversalRendererData.lowResolution` 保存配置，`UniversalRenderer.ConfigureCameraRendering` 在 culling、TAA history、RTHandle reference size 和 GBuffer 分配之前应用。
2. Base Camera 决定尺寸，Overlay 继承。低分辨率 Point 模式同步内部 descriptor / scaled dimensions，禁用该次 CameraData 的 MSAA、动态分辨率、FXAA/TAA、jitter 和 URP Volume 后处理，避免模糊像素。阴影图集保持项目原配置。
3. 管线新增 `RenderPassEvent.BeforeUpscaling = 1001`，普通 FinalBlit 与可能执行升采样的 FinalPost 都排在 1002，原生 Overlay UI 排在 1003。存在晚于内置后处理的自定义 Pass 时，UberPost 保持内部尺寸的中间颜色缓冲，不能提前放大并输出。没有尺寸缩放时，该事件仍会在最终输出之前执行。

   Renderer 分配 Point 颜色缓冲。URP `FinalBlitPass` 在该模式下明确选择 Point Pass，不依赖后处理是否执行；保留平台翻转和 sRGB 编码。
4. 后处理默认在新增的 `BeforeUpscaling`（1001，升采样前）事件执行，使用同尺寸临时 RT 安全地读写。通过通用 `ScriptableRenderPass.requiresIntermediateTexture` 声明颜色缓冲需求，不申请多余的 Opaque Color 拷贝，也不依赖降分配置。
5. 诊断尺寸由 Renderer 的 `lastCameraRenderTargetInfo` 记录，无需 Feature 回调。

原先添加到 `ScriptableRendererFeature` 的分辨率配置回调已经移除。内嵌包改动集中在 `LowResolutionSettings.cs`、UniversalRendererData/Editor、UniversalRenderer、ScriptableRenderer、UniversalRenderPipeline/Core、FinalBlitPass 和 ScriptableRenderPass。升级 URP 时需保留这些扩展。当前使用 URP 14 常规渲染路径；实验性 Render Graph 与 XR 不在本版支持范围内。

## 验证与工具

`Assets/Scenes/CardRenderingValidation.unity` 使用直接相机输出，保留原来的镜头与卡牌布局；固定 RenderTexture / 输出相机 / RawImage 已从该场景移除。

- `Tools/Rendering/Install Retro 540p and Migrate Card Scene`：安装独立后处理并开启 Renderer 降分能力，保留已配置高度。
- `Tools/Rendering/Capture Retro 540p Validation`：四种输出尺寸实际渲染，验证 Deferred 颜色、深度、GBuffer 及 Shader 编译；整数倍率逐像素检查 Point 放大。
- `Tools/Rendering/Validate Before Upscaling Event`：验证五个 Inspector 事件，并覆盖 Point、URP 后处理、最终后处理/FXAA 三条输出路径；记录实际后处理输入尺寸与最终输出/UI 的事件顺序。
- `Tools/Rendering/Validate Renderer and Post Independence`：验证关闭/移除 Feature、360/720 高度、关闭降分后独立使用后处理六种组合；关闭与移除 Feature 的截图必须完全一致。验证结束自动恢复配置。
- `Tools/Rendering/Capture Retro Game Output`：Play 模式捕获实际屏幕。

结果位于 `ArtSource/Cards/Retro540Validation/`，其中 `independence.json` 记录独立开关验证。`RetroRendering.Tests.Retro540Tests` 的 9 个 EditMode 用例验证可配置高度、比例、窗口上限和相机配置。也可用 `python3 Tools/validate_retro_point.py`（Pillow）复查默认 540p 下的 2×2 像素块。
