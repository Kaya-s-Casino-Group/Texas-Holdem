森林遗迹 / Forest Ruins — Unity 导入资源

适用：Unity 2022.3、URP 14；已在当前项目验证。
不含两个雕像，也不含旧草丛面片、测试台、灯光或实际相机。

入口
  Prefabs/PF_FR_EnvironmentLayout.prefab：完整环境，76 个网格实例。
  Prefabs/PF_FR_*.prefab：31 个可复用单体，包括 9 种草丛。
  完整环境按 Ground / Architecture / Nature / Foliage / Backdrop / References 分组。
  References 里的三个空节点记录原镜头的位置、前方和上方；不参与渲染。
  原工程中额外复制的一组近景草丛也保留了，共 15 组草丛实例。

命名和资源
  SM_FR_：FBX 模型；PF_FR_：预制件；M_FR_：外部材质；T_FR_：贴图。
  模型使用米制、Unity Y-up，单体根变换为单位缩放和零旋转。
  保留修复后的完整地台拓扑。模块、实例和旧名称对应见 ForestRuins_Manifest.json。
  18 个共用材质、31 张贴图。旧 PBR 金属/粗糙度贴图未重复导入；本套场景统一使用低光泽石材。
  草丛共用 512×256 图集；顶点颜色 R = 手绘明暗 / 2，不可清除顶点色。
  UV0 为美术贴图；UV1（Mesh.uv2）用于光照贴图。完整场景从 Editor/LightmapUV 缓存导入优化 UV，单体由 Unity 导入器生成。
  背景是独立面片，保留透明裁切和原始贴图尺寸。

专用 Shader
  Forest Ruins/Hand Painted Deferred
  包含 GBuffer / Forward / ShadowCaster / DepthOnly / DepthNormals / Meta。
  支持静态与方向性光照贴图、动态光照贴图、Shadowmask 和光探针。
  Shader 按 URP 的 GBuffer 与 Mixed GI 路径写入；支持 SRP Batcher 和 GPU Instancing。
  为保证草丛局部空间风动，禁用普通网格合批；重复网格仍可 GPU Instancing。
  Cutoff 控制叶片裁切，所有 Pass（包括 Meta）使用同一纹理和阈值。
  Hand Painted Fill 是显示层的手绘提亮，不自动成为烘焙光源。
  Physical Emission for Baking 是独立的真实发光；默认 0，通过材质面板修改时同步 GI 标记。
  默认双面显示、双面 GI；普通石材 Normal Strength = 0.18，Smoothness = 0.08。

烘焙准备
  石台、建筑、树干、树根与岩石：Contribute GI 开启，Receive GI = Lightmaps。
  草丛：默认风动，Receive GI = Light Probes，不加入静态烘焙。请在正式关卡布置光探针。
  如果需要将草丛也烘焙进光照贴图，先复制其材质并将 Wind Strength 设为 0，再把对应物体设为 Contribute GI / Receive GI = Lightmaps。
  Meta 始终使用静止模型，不随时间摇晃；包含叶片顶点色与透明裁切。
  背景面片：不投影、不参与 GI，避免把远景画片当作巨大发光墙。
  ForestRuins 和 SingleBattleDemo 已分别配置 Lighting Settings 与烘焙数据，见 Assets/Scenes 下同名目录。
  使用新相机取景、移动边缘资产或调大风动后，需要重新检查外围遮挡和草丛间距。

工具菜单
  Tools > Art Assets > Forest Ruins > Configure Materials and Prefabs
  Tools > Art Assets > Forest Ruins > Validate Import
  Tools > Art Assets > Forest Ruins > Validate Meta Pass Pixels
  Tools > Art Assets > Forest Ruins > Capture Import Preview
  配置命令会重建本文件夹的材质与预制件；手动调整前可先复制到自己的关卡文件夹。

源文件与记录
  Blender/ForestRuins_UnityExport/ForestRuins_UnityReady.blend：干净、分组、重命名后的交付文件。
  Blender/ForestRuins_Layout/ForestRuins_Layout.blend：保留可用草丛插件的原始编辑场景。
  Blender/ForestRuins_UnityExport/unity_import_report.txt：导入与 Shader 变体验证。
  Blender/ForestRuins_UnityExport/meta_pixel_validation.txt：Meta 像素级验证。
  Blender/ForestRuins_UnityExport/ForestRuins_UnityPreview.png：Unity 实际导入预览。

UV1 复检与重生成
  菜单 Tools > Art Assets > Forest Ruins > Generate Lightmap UV1 可重新导入环境 Lightmap UV；有缓存的完整场景沿用优化后的 UV1。
  Audit Lightmap UV1 将详细检查数据写入 Library/ForestRuinsLightmapUV。
  保留原贴图 UV0，跳过独立背景资源与雕像 VAT 网格；不自动开始烘焙。
  StoneTile 使用 60 度分片角和 8 像素打包间距，修复原先的小范围 UV 重叠。

场景光照归档
  Assets/Scenes/ForestRuins.unity：森林遗迹场景；光照配置与烘焙结果位于 Assets/Scenes/ForestRuins/。
  Assets/Scenes/SingleBattleDemo.unity：战斗演示场景；配置与烘焙结果位于 Assets/Scenes/SingleBattleDemo/。
  每个场景使用自己的 <场景名>_LightingSettings.lighting、LightingData.asset 和 Lightmap 文件，避免复制场景后共用烘焙数据。
  当前参数为 512 图集上限、44 texels/unit；每个场景烘焙得到一张 512×512 方向性 lightmap。
  Optimize Lightmap Allocation and Bake 针对当前已保存的森林场景，配置其同名文件夹下的光照设置后重烘焙。
  模型 UV 与背景保留检查已通过；独立背景和雕像 VAT 通道不会被重生成。
  光照 UV 缓存生成脚本与说明已纳入 Git：Tools/ForestRuins/。
  Blender、临时检查截图、Library 和本地备份不随 Unity 项目提交。
