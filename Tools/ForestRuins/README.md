# 森林遗迹光照 UV

Unity 2022.3 / URP 14。运行项目不需要 Python：优化后的 UV1 已保存在 `Assets/Scripts/Tools/Editor/ForestRuins/LightmapUV/SM_FR_EnvironmentLayout.json`，导入器按模型 SHA-256 与有向三角形位置匹配，写入 Mesh.uv2；保留原几何、法线、UV0、材质和背景通道。只有临时展开网格会被平滑。

## 重新生成缓存

1. 若源 FBX 几何变化，先将旧缓存移出 Assets，避免旧缓存的几何校验阻止导入。
2. 在 Unity 执行 `工具 > 场景美术 > 森林遗迹 > 生成光照贴图 UV1`，并在目标森林场景执行 `Audit Lightmap Allocation`。输入报告位于 `Library/ForestRuinsLightmapUV/`。
3. 在独立 Python 环境安装 `numpy`、`xatlas==0.0.11`，执行 `python Tools/ForestRuins/pack_lightmap_uv.py`。
4. 执行 `Generate Lightmap UV1` 导入新缓存，再执行 `Optimize Lightmap Allocation and Bake`。核对烘焙完成后的 UV Overlap 告警与实际画面。

脚本处理组合环境 FBX 的 72 个非背景网格，保留 4 个背景网格的 UV1。单体模块沿用各自 Unity 自动生成的 UV1。微小退化展开面会分配独立 UV 岛，不删除原模型三角形。

打包间距以当前场景 512 图集的对象占用为参考；大幅缩小对象或降低光照密度后，需重新检查间距。不要用该流程覆盖雕像的 VAT 数据通道。

## 场景文件

- `Assets/Scenes/ForestRuins/ForestRuins_LightingSettings.lighting`
- `Assets/Scenes/SingleBattleDemo/SingleBattleDemo_LightingSettings.lighting`

每个场景的 LightingData、光照贴图与方向贴图保存在同名文件夹中，独立烘焙。场景配置和已有烘焙结果都需要提交 Git；Library 中的中间报告无需提交。
