# 渲染模块

`Runtime` 归属 `RetroRendering.Runtime`，依赖 Unity Core/Universal Render Pipeline。卡牌规则与存档不引用此模块。当前主场景使用 Deferred Renderer，先以最多 540 行绘制场景并应用复古 Feature，再用 Point 放大；UI Camera 在场景放大后按输出分辨率叠加 UI。

## 独立 UI Camera

`Runtime/UICameraStack.cs` 挂在 UI Camera 上，`Configure(sceneCamera)` 指定同一观察者的 Base 摄像机。组件复用原有 Camera Stack，将自身追加到最后；不会清空其他 Overlay。主相机排除内置 UI Layer，UI Camera 只绘制 UI Layer。

UI Camera 为 Overlay，开启项目自定义 URP 的 `renderOverlayAtNativeResolution`，清除输出目标的深度并保留已绘制的场景颜色；关闭阴影、后处理、HDR、MSAA、抗锯齿、NaN 处理、抖动、XR、遮挡剔除、动态分辨率以及深度/颜色纹理请求。Volume Layer Mask 为 0，Volume 更新采用 Via Scripting。UI 不再借用场景的低分辨率颜色/深度附件。

组件在编辑器和 Play 同步主相机位置、旋转、透视/正交参数、视口、Display 与投影矩阵，在主相机开始渲染前再次同步。Screen Space Camera 按钮与 World Space 悬停提示可共用这台相机；提示的位置仍按场景相机和卡牌几何计算。销毁 UI Camera 时移除自己的 Stack 引用。

## Renderer 和资产

`Assets/Settings/UIRenderer.asset` 为独立 UniversalRendererData，使用 Forward；Opaque Layer Mask 为 0、Transparent Layer Mask 为 UI，无 Renderer Feature，无 PostProcessData，关闭 Depth Priming 与透明阴影接收，Intermediate Texture 为 Auto，自身 Low Resolution 开关关闭。保留 URP 内置 Shader 资源引用，避免丢失渲染所需 Shader。主 Deferred Renderer 的设置和 Feature 保留。

`CardPresentation.Editor/CardUICameraSetup.cs` 在当前 URP Asset 的 Renderer 列表追加或复用 UI Renderer（当前 MainURP 的 index 1，默认 Renderer 仍为 index 0），在同场景主相机下创建或复用 UI Camera，绑定 Renderer 并接入 Stack。场景保存后，Camera 和 Renderer 的引用随场景/URP Asset 进入首包；运行脚本及新增资源均带独立 .meta。工具只标记场景 dirty，不自动保存场景。

嵌入式 URP 的 `UniversalAdditionalCameraData`、`CameraData`、`UniversalRenderPipeline`、`UniversalRenderer`、`ScriptableRenderer` 与 `LowResolutionSettings` 增加原生分辨率 Overlay 路径。Stack 末尾连续开启该选项且无后处理的相机在场景最终解析之后绘制；前面的 Base/普通 Overlay 保持低分辨率共享附件，最后一个场景相机先完成解析/放大。原生 UI 直接加载输出目标，不重建场景缓冲、不再次放大 UI，Screen Space Overlay Canvas 只在最后阶段绘制。

例如输出 1920 × 1080 时，场景仍为 960 × 540，UI 为 1920 × 1080。输出到 Base Camera 的 RenderTexture 时使用该目标的尺寸；UI 停用后回到原有场景解析流程。选项默认关闭，普通相机保持原行为；此路径用于当前非 XR、无光照/无后处理的 UI Renderer，XR Stack 沿原路径。运行组件保持 UI Camera 位于 Stack 尾部，并保留其他 Overlay 顺序。

`RetroPostProcessFeature` 在 Base 相机执行，即使 Base 不是 Stack 最终解析者；不在 Overlay 执行。场景先完成复古处理，再叠加 UI，可避免增加 UI Camera 后主场景后处理被跳过，也避免把 UI 文字一起调色。

## 验证

`Poker > Tests > Run Card Presentation In Current Scene` 覆盖 UI Renderer、Stack 注册/复用/销毁、投影、按钮 Prefab、悬停提示和后处理执行顺序；原生 UI 检查使用不同尺寸输出与普通 Overlay，验证实际单像素条纹、场景颜色保留及关闭 UI。`Tools > Cards > Capture UI Camera Stack In Play` 在 UI Camera 完成渲染后记录真实目标尺寸和主场景复古输入尺寸，输出 `Library/CardUICameraValidation/frame.json` 与 `game.png`，用于检查实际 Game 布局及叠加结果。

## 雕像裂痕与刚体 VAT

`Shaders/RigidVAT.hlsl` 匹配 `Tools/Blender/statue_fracture/rigid_vat.py` 的 `statue-rigid-vat-v1` 格式：位置/旋转两张线性 RGBA EXR，UV1 按碎片定位第一帧像素，UV2/UV3 保存 Blender 局部枢轴。位置使用 min/range 解码，旋转是 XYZW 归一化编码；相邻帧手工插值位置和最短路径 nlerp 四元数。时间夹到最后一帧。Blender XYZ 映射到 Unity XZY；反射后的四元数轴向量使用 -XZY。

`GargoyleCracks` 的 GBuffer、ShadowCaster、DepthOnly、DepthNormals 都调用同一顶点 include。颜色和法线随碎片移动，三平面裂痕始终使用变形前的局部坐标。UV0 继续采样原材质；`COLOR.r` 为 0 时使用外表面，为 1 时使用断面的粗糙石材参数，断面跳过原贴图和裂痕采样。导入工具先烘进 FBX 节点的单位/轴变换，生成 `DragonGargoyle_Collapse_Unity.asset`，保留四个 UV 通道、二值顶点色、法线和角切线，并设置整段动画的包围盒。

当前场景 `DragonGargoyle` 使用这个 mesh、一个 submesh 和单个 `DragonGargoyle_Collapse_Surface` 材质。`Runtime/RigidVATPlayer.cs` 用一个 Renderer PropertyBlock 同步全部材质槽的时间；Inspector 的 Progress 滑杆可预览，运行时 `Play()` 从头播放、`Pause()` 停止、`ResetPose()` 回到静止，约 3.633 秒后保持最后一帧。默认不自动播放，战斗死亡事件可调用 `Play()`。`Tools > Art Assets > Dragon Gargoyle VAT` 提供导入、替换、预览和验证菜单。

GPU 验证在 Metal 上读取全部 15,564 个导入顶点的变形位置、法线和切线，对照 CPU Blender 空间解码，覆盖整数、半帧和首尾夹取；还检查所有顶点在 110 帧内的包围盒、EXR 的独立 alpha、FBX 导入前后的二值断面标记和实际 GPU COLOR 通道。报告及原模型/静止/崩塌截图保存到 `Library/GargoyleVATValidation/`。

`Tools > Art Assets > Dragon Gargoyle Cracks > Validate Rendering` 对当前场景实际使用的 mesh 做 GPU 片元读取，并与新复制的 mesh 对照，检查外表面标记、裂痕遮罩及静止/崩塌时的 0→1 裂痕画面差异。捕获使用临时材质，恢复当前材质、PropertyBlock 和场景 dirty 状态。`Configure Assets` 通过原生 Mesh setters 重建已有 mesh 的顶点流并上传到 GPU，保持 GUID；避免 `CopySerialized` 增加 COLOR 通道后 CPU 数据已更新、GPU 却保留旧布局的情况。报告与截图在 `Library/GargoyleCrackValidation/`。

Blender 插件与导出流程见 `Tools/Blender/README.md`。安装 `statue_fracture.zip` 后在 Destruction 面板导出，验证通过的四个输入文件发布到雕像 VAT 目录；Unity 检测 manifest 后自动更新 mesh 和材质，保留 GUID、材质调节值和场景播放设置。工作包、bind JSON 与检查报告留在 Assets 外。
