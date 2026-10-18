Gothic Chapel / 哥特教堂场景

本地集成测试：Assets/Scenes/CharacterValidation.unity（含其他工作中的角色/照明，不随此资产提交）
预制体：Prefabs/PF_GC_GothicChapel.prefab
源布局：Source/GothicChapel_Layout.json（米；保存对象位置、材质和摄像机）
本地 Blender 工作文件：output/gothic-scene/gothic-real-scale.blend（外部创作文件，按仓库策略保留本地）
可移植 Blender 导出脚本：Tools/GothicChapel/export_scene.py

资源规范
SM_ 网格，MAT_ 材质，TEX_ 贴图，PF_ 预制体，CAM_ 摄像机。
37 个网格、26 个材质；父组按建筑、道具、植物、旗帜、远景等来源分类。
Blender 坐标转换为 Unity (x,z,y)，翻转三角形绕序；旋转和缩放已烘入网格，实例缩放为 1。
世界布局保留；蜡烛与烛台保留父子关系。墙约 3.134 m、雕像约 1.966 m、祭坛约 1.044 m。
当前场景中使用 GothicChapel 层，新增摄像机只渲染该层；原有对象仍保留。

渲染
Unity 2022.3.62f3 / URP 14.0.12，当前 MainURP + MainDeferredRenderer 延迟管线。
Shader: Gothic Chapel/Stylized Deferred，参考本项目 ForestRuinsDeferred。
提供 GBuffer、Forward、ShadowCaster、DepthOnly、DepthNormals、Meta。
支持 BaseMap、Normal、Mask (R 金属度/G AO/A 光滑度)、双面、Alpha Clip、Lightmap/GI。
复杂 Blender 节点颜色已烘焙为反照率贴图，不含场景照明。
7 个远景面片使用 Unlit 显示，关闭 GI 贡献及阴影；不把显示亮度作为烘焙发光。
旗帜和植物使用裁切透明；保留原始 UV0。
不导入 Blender 灯光，不修改 Unity 现有灯光，不执行灯光烘焙。
Unity 截图继承当前管线像素化、色散等效果和当前场景照明，不保证与 Blender 像素一致。

UV1 / Lightmap
30 个实体网格生成第二套 UV（Unity mesh.uv2 / shader TEXCOORD1）。
展开角度 60，角度误差 8，面积误差 15，参考图集 1024 下边距 8 像素。
自动展开塌缩的微小三角形分离至预留 UV 条带，不改变几何或 UV0。
验证所有 UV 有效且在 0..1 内，退化 UV 三角形 0；512x512 内部覆盖采样重叠 0。
这是网格级 UV 检查，实际烘焙分辨率和物体在图集的缩放仍需按最终灯光设置确定。
30 个实体开启 ContributeGI；7 个远景不参与 LM，第二套 UV 复用显示 UV。

编辑器菜单：Tools > Scene Art > Gothic Chapel
Build Assets and Lightmap UV1：从 Source 重建材质、网格和预制体。
Place in Current Scene：备份当前场景并放入预制体（防重复）；不添加灯光。
Validate Meta Pixels：GPU 验证 Meta 的反照率、裁切与发光隔离。
Validate Placed Scene and Capture：验证布局/材质/UV/零灯光，输出预览。
Report Shader Errors：导出 Shader 编译消息。

验证报告：Reports/
预览：output/gothic-scene/unity-export/Unity-GothicChapel.png
导入前备份：output/gothic-scene/unity-export/BeforeGothicImport_CharacterValidation.unity
场景位置误差 0 m；Shader 编译消息为空；Meta GPU 测试通过。

旗帜与烛火动画
旗帜 Shader：Gothic Chapel/Animated Banner。
三面布旗共享 MAT_Banner_OxbloodGold；挂杆保留静态金属材质。
颜色通道 R 为从顶部到下摆的权重，G 为倒数高度，BA 为网格局部水平飘动方向。
顶边固定，两个不同频率波叠加，底部幅度约 3.8 cm；阴影/深度/法线使用同一变形。
Meta 始终使用静止网格，避免烘焙随时间变化。网格包围盒扩大以覆盖摆动。

烛火 Shader：Gothic Chapel/Candle Flame。
15 个灯芯位置挂接 FX_CandleFlame_01..15，全部共享 SM_Candle_Flame 和 MAT_Candle_Flame。
Shader 支持 GPU Instancing，材质已启用。随机种子由每实例位置得出，不需要独立材质或逐帧脚本。
暖橙色外焰/浅黄焰心，程序化透明边缘，朝向摄像机；根部固定、顶部摇曳。
闪烁通过平滑随机噪声驱动，强度默认 6、闪烁幅度 0.32、速度 7、顶端摆幅 0.009 m。
可在材质中调整这些参数。运行时自动播放；编辑模式需启用 Scene 视图的 Animated Materials。
烛火为 HDR 自发光表面，Bloom 效果取决于当前渲染器；不新增 Light 组件。
Meta 使用独立静态 BakeIntensity（默认 0）；动态闪烁不作为实时烘焙光源。

菜单 Set Up Banner and Candle Animation 为现有资源配置动画。
重建基础资源后再运行该菜单，可恢复顶点权重、材质分配、包围盒和烛火子物体。
菜单 Validate Animation and Instancing 检查布局接线、GPU 运动、Meta 稳定性及实例化绘制。
报告：Reports/animation-validation.txt。

版本控制交付
提交包含 Assets 下完整教堂预制体/材质/贴图/网格/Source 数据与所有 .meta、编辑器工具及导出脚本。
预制体可直接拖入其他场景，不依赖本地测试场景或烘焙缓存。
使用 Place in Current Scene 菜单时自动配置 GothicChapel 层，无需导入本地 TagManager 改动。
导出脚本按自身保存位置查找仓库根目录；在 Blender 打开源场景后运行此脚本。
Source JSON 是 Unity 重建所需的完整网格和材质数据，重建 Unity 资源无需 Blender 源文件。
