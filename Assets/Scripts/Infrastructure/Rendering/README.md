# 渲染模块

渲染支持按功能分目录，2D 前向 Renderer 使用独立程序集，供不同战斗表现选择。卡牌规则与存档不引用此模块。当前 3D 主场景使用 Deferred Renderer，先以最多 540 行绘制场景并应用复古 Feature，再用 Point 放大；UI Camera 在场景放大后按输出分辨率叠加 UI。

| 目录 | 职责 | 程序集 |
| --- | --- | --- |
| `Runtime/Forward2D` | `Simple2DRenderer` 与其设置类型，绘制无光照卡牌、精灵和 Overlay UI | `Poker.Rendering.Forward2D.Runtime` |
| `Runtime/Features` | SSAO、高度雾、体积光、Bloom、复古后处理及高度曲线缓存 | `RetroRendering.Runtime` |
| `Runtime/Cameras` | 3D 场景与原生分辨率 UI 的相机叠加 | `RetroRendering.Runtime` |
| `Runtime/VAT` | 刚体顶点动画播放 | `RetroRendering.Runtime` |
| `Runtime/Environment` | 体积灯组件与萤火虫区域 | `RetroRendering.Runtime` |
| `Runtime/Infrastructure` | 应用帧率初始化与开发环境采样 | `RetroRendering.Runtime` |

两个运行时程序集都只依赖 Unity Core/Universal Render Pipeline，彼此没有引用。已有 `RetroRendering.Runtime.asmdef` 保留在 `Runtime` 根目录；`Forward2D` 内的 asmdef 划出独立编译边界。现有类型仍使用 `RetroRendering` 命名空间，移动脚本保持 `.meta` GUID；`Simple2DRendererData` 标记原程序集迁移信息，现有 Settings 资产继续引用原脚本。

## 独立 UI Camera

`Runtime/Cameras/UICameraStack.cs` 挂在 UI Camera 上，`Configure(sceneCamera)` 指定同一观察者的 Base 摄像机。组件复用原有 Camera Stack，将自身追加到最后；不会清空其他 Overlay。主相机排除内置 UI Layer，UI Camera 只绘制 UI Layer。

UI Camera 为 Overlay，开启项目自定义 URP 的 `renderOverlayAtNativeResolution`，清除输出目标的深度并保留已绘制的场景颜色；关闭阴影、后处理、HDR、MSAA、抗锯齿、NaN 处理、抖动、XR、遮挡剔除、动态分辨率以及深度/颜色纹理请求。Volume Layer Mask 为 0，Volume 更新采用 Via Scripting。UI 不再借用场景的低分辨率颜色/深度附件。

组件在编辑器和 Play 同步主相机位置、旋转、透视/正交参数、视口、Display 与投影矩阵，在主相机开始渲染前再次同步。非 XR 管线在每次分析 Camera Stack 前，将开启原生分辨率且无后处理的 Overlay 稳定移到尾部，保留普通 Overlay 与原生 Overlay 各自的相对顺序；运行时追加普通 Overlay 后的第一帧，UI 仍使用原生分辨率。这一步早于 `beginCameraRendering`，避免调整列表后当帧的最终解析标记仍使用旧顺序。Screen Space Camera 按钮与 World Space 悬停提示可共用这台相机；提示的位置仍按场景相机和卡牌几何计算。销毁 UI Camera 时移除自己的 Stack 引用。

## Renderer 和资产

`Assets/Settings/Battle2DRenderer.asset` 使用 `Runtime/Forward2D/Simple2DRendererData.cs`，固定注册到 `MainURP` 的 Renderer 列表（当前 index 2）。2D 验证相机直接选择该槽；默认 index 0 与 UI 的 index 1 保持原样。编辑和运行都使用同一配置，不切换 Graphics / Quality 管线。创建或检查此 Renderer 的代码引用 `Poker.Rendering.Forward2D.Runtime`，无需依赖 3D 效果、VAT 或相机叠加代码。

`Simple2DRenderer` 按 [URP 14 的 ScriptableRenderer 接口](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/api/UnityEngine.Rendering.Universal.ScriptableRenderer.html) 实现，复用项目内官方 `Runtime/Passes/DrawObjectsPass.cs` 绘制不透明和透明物体，再按官方 `DrawScreenSpaceUIPass.cs` 的方式绘制 Overlay UI。直接写入相机目标，保留透明排序、深度与 MPB 实例化；卡牌优先走 `UniversalForwardOnly`，也接受 `UniversalForward`、`SRPDefaultUnlit` 和 `Universal2D` 标签。不创建 GBuffer、光照、阴影、后处理或中间纹理。

此简版用于原生分辨率的无光照 2D 验证，相机关闭 HDR、HDR 显示输出、MSAA 和 XR，不提供相机堆叠或 Renderer Feature 注入。Screen Space Overlay Canvas 在最后绘制；Camera 的 RenderTexture 目标也可使用。正式 3D 渲染继续走原 Deferred / UI 配置。

`Assets/Settings/UIRenderer.asset` 为独立 UniversalRendererData，使用 Forward；Opaque Layer Mask 为 0、Transparent Layer Mask 为 UI，无 Renderer Feature，无 PostProcessData，关闭 Depth Priming 与透明阴影接收，Intermediate Texture 为 Auto，自身 Low Resolution 开关关闭。保留 URP 内置 Shader 资源引用，避免丢失渲染所需 Shader。主 Deferred Renderer 的设置和 Feature 保留。

`CardPresentation.Editor/CardUICameraSetup.cs` 在当前 URP Asset 的 Renderer 列表追加或复用 UI Renderer（当前 MainURP 的 index 1，默认 Renderer 仍为 index 0），在同场景主相机下创建或复用 UI Camera，绑定 Renderer 并接入 Stack。场景保存后，Camera 和 Renderer 的引用随场景/URP Asset 进入首包；运行脚本及新增资源均带独立 .meta。工具只标记场景 dirty，不自动保存场景。

嵌入式 URP 的 `UniversalAdditionalCameraData`、`CameraData`、`UniversalRenderPipeline`、`UniversalRenderer`、`ScriptableRenderer` 与 `LowResolutionSettings` 增加原生分辨率 Overlay 路径。Stack 末尾连续开启该选项且无后处理的相机在场景最终解析之后绘制；前面的 Base/普通 Overlay 保持低分辨率共享附件，最后一个场景相机先完成解析/放大。原生 UI 直接加载输出目标，不重建场景缓冲、不再次放大 UI，Screen Space Overlay Canvas 只在最后阶段绘制。

例如输出 1920 × 1080 时，场景仍为 960 × 540，UI 为 1920 × 1080。输出到 Base Camera 的 RenderTexture 时使用该目标的尺寸；UI 停用后回到原有场景解析流程。选项默认关闭，普通相机保持原行为；此路径用于当前非 XR、无光照/无后处理的 UI Renderer，XR Stack 沿原路径。运行组件保持 UI Camera 位于 Stack 尾部，并保留其他 Overlay 顺序。

`RetroPostProcessFeature` 在 Base 相机执行，即使 Base 不是 Stack 最终解析者；不在 Overlay 执行。场景先完成复古处理，再叠加 UI，可避免增加 UI Camera 后主场景后处理被跳过，也避免把 UI 文字一起调色。

## 验证

`工具 > 测试面板 > 编辑模式测试 > 更多模块 / 全量回归 > 卡牌表现全模块` 覆盖 UI Renderer、Stack 注册/复用/销毁、投影、按钮 Prefab、悬停提示和后处理执行顺序；原生 UI 检查使用不同尺寸输出与普通 Overlay，验证实际单像素条纹、场景颜色保留及关闭 UI。`工具 > 卡牌表现 > 截图 > 运行时界面相机叠加` 在 UI Camera 完成渲染后记录真实目标尺寸和主场景复古输入尺寸，输出 `Library/CardUICameraValidation/frame.json` 与 `game.png`，用于检查实际 Game 布局及叠加结果。

## 低分辨率 SSAO

`LowResolutionSSAOFeature` 已安装在 `MainDeferredRenderer`，Feature 列表中位于复古后处理之前。两者都在 `BeforeUpscaling` 执行，同事件按列表顺序执行：低分辨率场景 → SSAO → 高度雾 → 体积光 → Bloom → 复古调色 → Point 放大 → 原生 UI。关闭复古 Feature 不会关闭 SSAO；关闭 Renderer 降分后，SSAO 使用管线当前内部尺寸。

AO 和滤波缓冲直接使用 `cameraTargetDescriptor`，无额外半分辨率步骤。只请求深度，从深度重建几何法线，适配项目自定义 Deferred 材质，不依赖额外 DepthNormals Pass。采用固定屏幕采样旋转与 8/16/32 次半球采样，双向深度引导滤波后乘到场景 RGB；合成保留颜色目标与 alpha，不申请 Opaque Color 拷贝。共三个全屏 Pass、两张线性 R8 缓冲（设备不支持时回退 RGBA8），无跨帧历史。

中文 Inspector 可调遮蔽强度、世界空间半径、深度偏移、采样数及深度边缘锐度。初始强度 1.2、半径 0.06、偏移 0.001、16 次采样，按项目桌面尺度设置。强度为 0 时完全跳过。RenderTexture 默认开启，Scene View 默认关闭；只在 Base 执行，Overlay、Preview、Reflection、XR、深度专用目标不执行。普通 Overlay 后绘制的物体不参与该次 AO。

这是升采样前的颜色合成效果，会同时压暗已绘制的直接光、间接光和自发光；不向 URP 光照注入环境遮蔽关键字。透明物体不贡献深度遮蔽，但已经混合的透明颜色可能被后方深度产生的 AO 压暗。只覆盖当前常规非 XR 路径，不支持实验性 Render Graph。

- `工具/渲染/环境光遮蔽/安装低分辨率效果`：重复执行会复用已有 Feature、保留参数并恢复推荐顺序，只保存主 Renderer。
- `工具/渲染/环境光遮蔽/验证低分辨率效果`：独立 Preview Scene 中验证透视/正交、1080p/竖屏/小窗口，检查 AO 尺寸、接触暗化、alpha、强度归零等价于禁用，以及 2×2 Point 像素块。结束恢复 Renderer 参数和 Feature 开关；报告、开关对照 PNG 位于 `Library/LowResolutionSSAOValidation/`。
- `LowResolutionSSAOTests`：验证资产顺序、Shader 持久引用、深度输入和 Base/Overlay/零强度/离屏范围。

## 高度雾与依赖它的体积光

来源为 [LifeSugar/RayMarchingGodRay](https://github.com/LifeSugar/RayMarchingGodRay)，迁移基线 `9bcc726dc3fc3f2f6e87af2b1e11e5606edc7d43`。原仓库完整克隆在项目根目录的 `.cache/RayMarchingGodRay`（不纳入 Git，也不导入 Unity）；MIT 授权保留在 `RayMarchingGodRay-LICENSE.txt`。

两个独立 Renderer Feature 已安装在 `MainDeferredRenderer.asset`：**SSAO → 高度雾 → 体积光 → Bloom → 复古后处理 → Point 放大 → 原生 UI**。全部使用定制 URP 的 `BeforeUpscaling`，同事件按 Feature 列表排序。保留原 Renderer 的 Deferred、540 行、SSAO 和复古设置，没有替换 Renderer 或修改嵌入式 URP。两个效果均只请求深度，使用 `cameraTargetDescriptor` 的当前内部尺寸；不再对 540 行画面额外减半，也不导入原仓库的 Pipeline Asset、Volume 或场景。

### 高度雾：HeightFogFeature

直接在 Renderer Feature 的 Settings 调节，无需额外 Global Volume。

- `baseHeight`：世界空间完整密度的最高高度；更低处密度保持恒定。
- `maximumHeight`：从基础高度按所选曲线减淡，达到此高度为零，必须高于基础高度。
- `heightFalloff`：选择 Linear（线性）、Smooth（平滑 S 形）、Ground Hugging（贴地浓雾，快速衰减）、High Fill（较高位置仍保留密度）或 Custom（自定义）。默认 Linear 保持旧画面。
- `heightCurve`：Custom 时生效，X=基础高度到最大高度的归一化高度，Y=密度倍率，输出限制在 0–1。端点固定 `(0,1)`、`(1,0)`，基础高度以下仍为完整密度，最大高度以上仍为零；地面裁切规则不变。可调整中间控制点和切线形成更厚的贴地层或分层雾。
- `enableGround / groundHeight`：独立的下边界裁切。关闭时完全忽略地面高度，不使用极小浮点数参与密度运算。
- `density / attenuationDistance`：密度与消光距离；密度为 1 时，经过 attenuationDistance 后透射率为 1/e。`distance` 是从近裁剪面起的最大积分长度，场景深度会进一步截断它。
- `color`：均匀背景雾色。只在此处合成一次 `scene × T + fogColor × (1 − T)`，保留原 alpha。

线性模式沿视线对“恒定密度 + 线性高度衰减”作解析积分。其他高度曲线缓存为 257×1 RGFloat 查找表（约 2 KiB，R=密度，G=累计积分），仅曲线/预设变化时重建和上传；积分与分段线性密度匹配，极短区间用中点值避免累计值相减的精度损失。无 3D 纹理时，高度雾仍无需逐步 ray march，且透射率不受体积光步数影响。高度雾、3D 噪声调制及体积光共用这一高度分布。透视、正交和非对称投影都从深度与逆 VP 重建近裁剪面及表面端点。无额外颜色拷贝、无雾专用 RT。

高度雾 Settings 的 **Use Noise Texture** 可启用动态 `Texture3D` 密度调制。读取线性纹理 R 通道，实际密度为 `heightDensity × lerp(1, 2 × R, Noise Strength)`：R=0.5 不改变密度，强度为 1 时 R=0/1 对应零/双倍密度。默认接入离线生成的 `FogNoise32.asset`；其均值并非精确 0.5，不进行自动均值校正。开关关闭、纹理为空或强度为零时恢复原解析路径。

**Noise World Size** 是各轴重复一个纹理周期的世界尺寸，**Noise Offset** 是世界偏移，**Noise Velocity** 是世界单位/秒的平移速度（正速度沿正轴移动）；速度为零时完全静止。坐标固定在世界空间，不跟随相机。Shader 使用独立的三轴 Repeat + 空间线性 sampler 和显式 LOD 0；偏移用 CPU double 计算并按周期回绕，Shader 不切换纹理、不逐帧上传噪声，也没有逐帧随机抖动。默认速度 `(0.03, 0, 0.015)`、世界尺寸 `(2, 1, 2)`，建议使用生成工具产生的周期性低频纹理。这里是噪声体的连续平移，未实现 4D 形态演化。

**Noise Steps** 默认 32，统一控制纹理雾及体积光积分。每段保留精确高度/地面裁切积分，只在段中点采一次 3D 纹理。无参与灯光时由高度雾独立求透射率；有体积光时高度雾将本帧积分委托给后续体积光 Pass，一次步进同时累计 RGB 散射和 alpha 透射率，最后按 `scene × T + fogColor × (1-T) + scattering` 合成。沿用原来的两张 RGBA16F 缓冲，不增加密度 RT 或第二遍 3D 采样。双边滤波只滤散射 RGB，透射率逐像素保留，避免开关体积光/模糊时改变雾的遮蔽；目标颜色 alpha 保持不变。纹理模式下体积光自身 Steps / Jitter 不生效。Frame Debugger 中独立 Height Fog 绘制可能合并到 Volumetric Light。

`工具/渲染/高度雾与体积光/启用默认三维噪声` 可接入默认纹理，保留现有其他参数。`工具/渲染/三维噪声/验证雾噪声` 验证常量密度、空纹理/零强度回退、独立与共享积分一致性、三轴周期/接缝、平移、正交视角和地面裁切，并对五种高度曲线验证水平射线的密度、竖直射线的累计衰减以及与 3D 噪声共用积分的结果；报告在 `Library/HeightFogNoiseValidation/report.json`。CPU 测试另覆盖长时间运动偏移的周期连续性，以及曲线积分、边界、数值限制和缓存更新。细节空间频率仍需匹配 Noise Steps，缩小平铺尺寸时应增加步数以减少欠采样。

### 体积光：VolumetricLightFeature

`heightFog` 必须引用同一 Renderer 中排在它前面的高度雾。高度雾停用、密度或距离为零、参数无效、相机不在适用范围时，体积光不入队；前一相机和前一帧的数据不会作为依赖依据。单独关闭体积光仍保留高度雾。

解析模式复用同一高度密度函数和解析透射率，沿视线做单次散射积分，仅叠加散射 RGB。纹理模式共用上述密度采样与透射积分，并一起合成，不重复衰减场景。两条路径都保留 Cornette–Shanks 相位函数、主光与额外光、阴影、Light Cookie、近灯中心抑制。主光直接按体积采样点查阴影图，不把表面的屏幕空间阴影当成雾中的遮挡。

主光由 Feature 的 `mainLight / mainScattering / mainAnisotropy` 控制，不需要挂脚本。额外灯光必须挂载并启用 `VolumetricLight` 才参与体积光，散射强度、各向异性和近灯抑制半径都取自该组件；未挂载、禁用组件或 scattering 为零的灯不会产生体积散射，普通表面照明不受影响。最多选取 `maxAdditionalLights` 盏可见且贡献非零的额外灯（默认 16，上限 64，沿 URP 可见灯顺序），与 URP 的全局灯光缓冲索引对齐，不受某个 Mesh 的 per-object 灯光列表限制。

默认 48 步、固定屏幕噪声、1 次双向深度引导滤波。两张内部尺寸的 RGBA16F 临时 RT 在 Feature 释放时回收；固定噪声避免无 TAA 的像素风画面逐帧闪动。关闭滤波可检查原始采样，增加步数可改善细阴影和窄光束。设置按当前桌面约米级场景给出温和起点，艺术效果仍需按灯光位置和强度调整。

### 范围与验证

默认处理 Game Base Camera 和离屏 RenderTexture；Scene View 默认关闭，跟随高度雾开关。Overlay、Preview、Reflection、XR 和深度专用目标跳过。UI Renderer 未添加这两个 Feature。当前实现适配本项目常规 Deferred / 非 Render Graph 路径，不包含多次散射、时间累积或局部体积盒。没有深度写入的透明物体不能提供独立雾距离；效果按它背后的不透明深度合成。灯光遮挡范围受 URP Shadow Distance 和阴影图分辨率限制。

- `工具/渲染/高度雾与体积光/安装效果`：安装或复用两个 Feature，保留参数、修复依赖和推荐排序，只保存主 Renderer。
- `工具/渲染/高度雾与体积光/验证效果`：独立 Preview Scene 中做真实 GPU 验证，包含横屏/竖屏/正交、原生尺寸、Point 放大、alpha、依赖关闭、零密度、主光/点光/聚光及阴影。执行后恢复 Feature 和低分辨率设置。报告及截图在 `Library/HeightFogValidation/`。
- `工具/渲染/高度雾与体积光/捕获当前场景`：使用当前 Main Camera 与 Camera Stack 捕获关雾、仅雾、雾加体积光三张对照，不保存当前场景。
- `HeightFogTests`：覆盖安装顺序、持久 Shader 和依赖引用、地面关闭、无效高度、Base/Overlay、零密度以及跨 Renderer 的依赖隔离。

## Bloom

`BloomFeature` 已安装在主 Renderer 中体积光之后、复古后处理之前，同样在 `BeforeUpscaling` 执行。独立于高度雾和体积光，无需 Volume 或开启 URP 内置后处理，也不修改定制 URP。场景 HDR 高光及体积散射先产生光晕，再接受现有复古调色和 Point 放大；原生 UI Camera 不执行 Bloom。

采用常规柔和阈值提取、半尺寸起始的多级降采样、Tent 升采样与 RGB 加法合成。缓冲为线性 RGBA16F；每级加权归一化，避免层数增加时均匀区域亮度成倍增长。保留场景 alpha，不读取同一目标正在写入的颜色，不请求深度或 Opaque Color Copy。临时 RT 在尺寸和层数变化时复用或释放，销毁 Feature 时全部释放。

- `intensity`：光晕强度，默认 0.3，0 时完全跳过。
- `threshold`：Gamma 空间阈值，传给 Shader 前转为线性；默认 1，适合提取 HDR 高光。
- `softKnee`：阈值附近过渡比例，默认 0.5；0 为硬阈值。
- `scatter`：向更粗层扩散的比例，默认 0.7；越大光晕越宽。
- `maxIterations`：最多 1–8 层，默认 6；小尺寸自动停止，最小支持 1×1。
- `clamp`：进入 Bloom 的线性 HDR 亮度上限，默认 65472，可降低以压制极端亮点。
- `tint`：光晕颜色，默认白色。

默认处理 Game Base Camera 和 RenderTexture，Scene View 默认关闭；Overlay、Preview、Reflection、XR 和深度专用目标跳过。按本项目 Linear 色彩空间、常规非 Render Graph 路径实现。不包含镜头污渍、镜头眩光或时间累积。要让发光材质明显溢光，需要启用相机 HDR 并使高光超过阈值；低动态范围画面可降低阈值，但会让更多普通亮色参与 Bloom。

- `工具/渲染/泛光/安装效果`：安装或复用 Feature，保留已有参数与开关；持久保存 Shader 引用和排序。雾与 SSAO 安装菜单也会保留推荐顺序。
- `工具/渲染/泛光/验证效果`：Preview Scene 中验证横屏、竖屏、奇数尺寸、1×1、关闭低分辨率后的原生尺寸、HDR 光晕、阈值过滤、零强度、颜色、alpha、单层和 Point 像素块。报告与对照图在 `Library/BloomValidation/`。
- `工具/渲染/泛光/捕获当前场景`：保留其他效果，以当前 Main Camera 捕获开关对照，不保存场景。

体积光相关类型和 Shader 已统一命名为 `VolumetricLightFeature`、`VolumetricLight`、`VolumetricLight.shader`。脚本及 Shader 保留原 `.meta` GUID，类型带 `MovedFrom`，已有 Renderer 子资产、灯光组件和参数可继续使用；高度雾的 `HeightFogFeature` 及依赖引用保持原名。

## 离线 3D 雾噪声生成器

`工具/渲染/三维噪声/噪声生成器` 打开编辑器工具，可调随机种子、基础格子数、噪声层数和每层强度比例，生成后可拖动 Z 滑杆预览 XY 切片。输出固定为 32³、R8 单通道线性 `Texture3D`，三轴 Repeat、空间线性过滤、无 mip。体素数据 32 KiB；为了离线预览保留一份可读 CPU 数据，Unity 文本序列化文件约 65 KiB。

默认资产为 `Assets/GameAssets/Rendering/Textures/FogNoise32.asset`：seed 1337、基础 2 格、3 层、强度递减 0.5，即把 2/4/8 格的周期性三维 Value Noise 离线合成。每层使用五次平滑插值，整数周期哈希让三个轴自然平铺；采样点位于体素中心，不复制末端切片。输出是 0–1 噪声值，不承诺均值恰好为 0.5，后续密度调制应单独定义强度和均值语义。最高频率限制为每轴 16 格，避免 32³ 数据欠采样。

`工具/渲染/三维噪声/生成默认雾噪声` 可直接按默认参数生成。更新相同路径时保留资产 GUID 和 file ID；生成参数存入资产 `.meta` 的 userData，重新打开工具可恢复。目标若是其他类型/规格的资产，工具会要求另选路径。生成代码仅存在于 Editor 程序集，运行时无需生成或逐帧上传噪声；纹理可供上述高度雾/体积光的共享积分动态采样。

`VolumeNoiseGeneratorTests` 验证同种子复现、不同种子差异、三轴周期连续、频率限制、R8 线性格式及重烘焙后的引用/数据持久性。默认体积的八张切片预览在 `Library/VolumeNoiseValidation/FogNoise32Slices.png`。

## 萤火虫区域

`工具/渲染/创建萤火虫区域` 在主相机前创建一个区域并选中，默认 12 只；也可以拖入 `Assets/GameAssets/Rendering/Prefabs/FireflyVolume.prefab`。`FireflyVolume` 的 Center / Size 定义局部立方体范围，支持移动、旋转及缩放，选中时显示线框。编辑器可直接预览，关闭 Preview In Editor 后仅运行时生成；子物体自动生成且不写入场景，禁用组件时清理。

每只是具有真实深度的、不透明 URP Unlit 小球，正常写入深度与 GBuffer，受前景遮挡，并参与现有高度雾和 Bloom。Emission 是 HDR 亮点颜色，Diameter 使用世界单位；点光源颜色、强度和范围独立控制，亮点与灯光共用缓慢且错相的亮度脉动。创建菜单会开启主相机 Allow HDR，让亮度超过 1 的数据进入 Bloom；新增区域及相机变更留在当前场景，需正常保存场景。

每只自动挂载 `Light(Point)` 和 `VolumetricLight`，Volumetric Light Enabled 默认开启，可统一开关体积散射，关闭后仍保留实体亮点和普通点光照明；支持在 Inspector 或运行时代码中切换。Scattering / Anisotropy / Light Radius 在区域上统一调整。点光源默认不投射阴影，可按需要开启。体积散射仍依赖现有 Height Fog + Volumetric Light Features，所有区域与场景其他体积灯共享 Feature 的 Max Additional Lights 上限（默认 16），增加数量并不会自动提高预算。

随机种子控制初始分布；Speed / Turn Speed / Wander Interval 控制平滑游动。首版避障针对同一 Scene 内、Obstacle Layers 命中的 MeshRenderer / SkinnedMeshRenderer 世界包围盒，按 Clearance 和球体半径扩张；无需 Collider，出生位置、目标和整段位移均检测。已缓存 bounds 每帧更新，新增/删除模型按 Obstacle Refresh Interval 重新发现。包围盒是保守近似，树冠与镂空模型空隙也会避让；不解析 Shader/VAT 变形后的实际三角形。空间不足时减少生成数量并提示，不强行放进 Mesh。障碍物移入当前位置时会在区域内重新安置，找不到位置则暂时隐藏。

`FireflyVolumeTests` 覆盖旋转/非均匀缩放边界、无 Collider 薄墙防穿透、随机复现、统一灯光参数和清理；GPU 测试确认 HDR 亮度、前景遮挡及亮点写深度后遮住后绘制的背景几何。

## 雕像裂痕与刚体 VAT

`Shaders/RigidVAT.hlsl` 匹配 `Tools/Blender/statue_fracture/rigid_vat.py` 的 `statue-rigid-vat-v1` 格式：位置/旋转两张线性 RGBA EXR，UV1 按碎片定位第一帧像素，UV2/UV3 保存 Blender 局部枢轴。位置使用 min/range 解码，旋转是 XYZW 归一化编码；相邻帧手工插值位置和最短路径 nlerp 四元数。时间夹到最后一帧。Blender XYZ 映射到 Unity XZY；反射后的四元数轴向量使用 -XZY。

`GargoyleCracks` 的 GBuffer、ShadowCaster、DepthOnly、DepthNormals 都调用同一顶点 include。颜色和法线随碎片移动，三平面裂痕始终使用变形前的局部坐标。UV0 继续采样原材质；`COLOR.r` 为 0 时使用外表面，为 1 时使用断面的粗糙石材参数，断面跳过原贴图和裂痕采样。导入工具先烘进 FBX 节点的单位/轴变换，生成 `DragonGargoyle_Collapse_Unity.asset`，保留四个 UV 通道、二值顶点色、法线和角切线，并设置整段动画的包围盒。

当前场景 `DragonGargoyle` 使用这个 mesh、一个 submesh 和单个 `DragonGargoyle_Collapse_Surface` 材质。`Runtime/VAT/RigidVATPlayer.cs` 用一个 Renderer PropertyBlock 同步全部材质槽的时间；Inspector 的 Progress 滑杆可预览，运行时 `Play()` 从头播放、`Pause()` 停止、`ResetPose()` 回到静止，约 3.633 秒后保持最后一帧。默认不自动播放，战斗死亡事件可调用 `Play()`。`工具 > 场景美术 > 石像鬼 > 顶点动画` 提供导入、替换、预览和验证菜单。

GPU 验证在 Metal 上读取全部 15,564 个导入顶点的变形位置、法线和切线，对照 CPU Blender 空间解码，覆盖整数、半帧和首尾夹取；还检查所有顶点在 110 帧内的包围盒、EXR 的独立 alpha、FBX 导入前后的二值断面标记和实际 GPU COLOR 通道。报告及原模型/静止/崩塌截图保存到 `Library/GargoyleVATValidation/`。

`工具 > 场景美术 > 石像鬼 > 验证裂纹渲染` 对当前场景实际使用的 mesh 做 GPU 片元读取，并与新复制的 mesh 对照，检查外表面标记、裂痕遮罩及静止/崩塌时的 0→1 裂痕画面差异。捕获使用临时材质，恢复当前材质、PropertyBlock 和场景 dirty 状态。`Configure Assets` 通过原生 Mesh setters 重建已有 mesh 的顶点流并上传到 GPU，保持 GUID；避免 `CopySerialized` 增加 COLOR 通道后 CPU 数据已更新、GPU 却保留旧布局的情况。报告与截图在 `Library/GargoyleCrackValidation/`。

Blender 插件与导出流程见 `Tools/Blender/README.md`。安装 `statue_fracture.zip` 后在 Destruction 面板导出，验证通过的四个输入文件发布到雕像 VAT 目录；Unity 检测 manifest 后自动更新 mesh 和材质，保留 GUID、材质调节值和场景播放设置。工作包、bind JSON 与检查报告留在 Assets 外。

雕像 VAT 导入现已共用 `Assets/Scripts/Tools/Editor/RigidVATAssetImport.cs`：按 `_vat.json` 识别文件和源材质，
生成保持 GUID 的准备 mesh、单材质以及带播放组件的 Prefab。额外原始 UV 集移至 UV4+，
Unity UV1/2/3 始终用于 VAT；原 UV0 和法线保留。选中 manifest 后可在 `工具 > 场景美术 > 雕像顶点动画`
配置资源、验证 GPU 或切换自动导入。旧 Gargoyle 菜单继续使用同一实现。

FoxStatue 的完整闭环报告在 `Library/FoxStatueValidation`。
36 块、110 帧、64×128 EXR16，外表面归属、原 UV / 角法线、二值 COLOR.r、四个 pass、
整数与半帧 GPU 解码、末帧夹取、实际裂痕渲染均通过。保存后重新打开再次导出，也保留了
mesh / 材质 / Prefab GUID、已加载 mesh 实例、材质裂痕值和实例播放进度；实际 GPU 片元读回
与新复制 mesh 一致。使用 `Assets/Art/Models/Creatures/FoxStatue/VAT/FoxStatue_Collapse_VAT.prefab`。


## 老电视 / CRT（最终相机栈输出）

CRT 已独立为 `CRTFeature` / `CRT.shader`，安装在 `Assets/Settings/UIRenderer.asset`，不属于场景的复古调色 Shader。
最终相机使用的 Renderer 决定整条相机栈是否启用 CRT；当前安装在 UI Renderer，所以没有 UI 相机的场景不会自动执行 CRT。
当前强度 0.35、曲率 0.025、扫描周期 4 个输出像素，Scene View 默认关闭。复古后处理原来的调色、暗角、色差和场景 Bloom 保持独立。

执行顺序：低分辨率场景与调色 → Point 放大 → 单侧细描边 → 原生 UI 相机与 Overlay Canvas → CRT → 屏幕。
显像管曲率、扫描线、RGB 荧光栅格与屏幕边缘柔化统一作用于场景、描边和 UI，扫描纹理按输出像素生成。
定制 URP 通过 `ICameraStackOutputFeature` 在栈开始前查询最后一台有效相机的 Renderer，分配原生尺寸 RGBA16F 颜色目标。
Base 和所有 Overlay 共享这个颜色目标；原生 UI 使用独立深度目标，避免颜色采样读到深度。
最终相机在 `AfterRenderingUI` 处理已完成的合成画面，随后上屏。不修改 Camera.targetTexture、不读取 backbuffer，原先的输出纹理与色彩编码语义保留。
仅支持普通非 XR / SDR 相机栈；HDR 显示输出跳过。每条栈只由最后相机的 Feature 处理一次。
关闭 Feature、关闭启用开关或总强度为 0 时走原有直接输出路径。开启时增加原生颜色纹理与原生 UI 深度纹理，以及一次最终全屏绘制。
曲率会改变 UI 的视觉位置；当前没有修改 EventSystem 的指针坐标映射。

`工具/渲染/老电视 CRT/安装到最终 UI Renderer` 安装或复用独立 Feature。
`工具/渲染/老电视 CRT/验证 CRT` 检查横屏、竖屏、非整数放大和原生尺寸、零强度回退、颜色/方向/alpha、边框、扫描线、UI 接受 CRT 及最终 UI 相机执行。
`工具/渲染/老电视 CRT/捕获当前 CRT 对照` 输出当前主相机完整 Camera Stack 的开关对比到 `Library/CRTValidation/`，不保存当前场景。


## 屏幕像素描边

`DepthNormalOutlineFeature` 使用场景深度和法线生成单侧边缘，Point 放大后再按输出像素宽度合成。
支持 0..4 像素宽度、颜色、深度/法线阈值和排除层；当前安装到 MainDeferredRenderer，默认关闭。
默认排除位为 layer 8；各场景可按自身层定义调整，不依赖角色资产或角色 Renderer。
Overlay 相机跳过描边，描边在原生 UI 之前合成；CRT 在最终 UI 之后处理整幅画面。
`工具/渲染/描边/验证屏幕像素宽度` 检查 1/2/4 像素宽度在 1x/2x/3x/2.5x 输出中的稳定性。

## 独立视口表现相机

`Runtime/Cameras/ViewportOverlayCamera.cs` 用于独立于场景透视的 3D 展示区域：相机作为原生分辨率 Overlay 插到指定相机之前，保留 Camera 的透视/正交设置，在 Content Distance 处测量实际投影宽高，内容按归一化视口矩形等比适配。它只依赖 Camera、Transform 和 URP，不依赖 Battle、卡牌数据或表现调度器。场景根 Content Root 专用于布局，动画在其子对象上进行；该组件不替代输入相机和对象 Layer 的显式配置。

SceneDemo 1V2 使用 Scene → Cards → UI → 最终 CRT；Cards 当前为 FOV 30° 的透视投影、牌区平行相机画面，使用无 Feature 的 `CardForwardRenderer` 和现有卡牌 Shader 的 Forward Pass。相机、Layer、输入和调参说明见 `Tools/SceneDemo1V2Configuration.md`。本次未运行测试。
