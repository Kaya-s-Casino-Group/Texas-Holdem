# Fox Statue

来自用户提供的 Tripo FBX / FBM 文件。源资产已复制并规范为独立 FoxStatue 目录。
`FoxStatue.fbx` 保留 2,976 个原始三角面，单材质，原 UV0 / 法线 / 切线。
Blender 尺寸 XYZ 约 0.442493 / 0.442493 / 0.996098 m，Unity 为 XZY，底部枢轴落地。

原始 BaseColor / Normal / Metallic / Roughness 均为 512×512；线性 MetallicSmoothness
使用 R=metallic、A=1-roughness。原材质使用 `Rendering/Gargoyle Cracks Deferred`，
裂痕使用共享的 128×128 线性纹理、静止局部空间三平面投影。

## 使用

直接拖入 `VAT/FoxStatue_Collapse_VAT.prefab`。它包含准备好的 mesh、一个材质和
`RigidVATPlayer`，原始 FBX 仅作源参考。默认裂痕程度和播放进度为 0，不自动播放。
材质 `_CrackProgress` 控制裂痕，播放器 Progress 控制崩塌；运行时调用 Play()，
3.633 秒后保持末姿态，可 Pause() / ResetPose() / SetProgress()。
死亡事件由游戏调用者接入。

Blender 工程：`Blender/FoxStatue/Fracture/FoxStatue_Fracture_VAT.blend`。
N → Destruction → 返回破碎模拟场景 → 导出刚体 VAT + 验证。
工程保存了相对工作/Unity目录及 Unity 源材质路径，再次导出自动更新已有资源。
Unity 菜单：Tools → Art Assets → Fox Statue；通用导入：选中 manifest，
Tools → Art Assets → Statue VAT → Configure Selected Manifest。

## 验证结果

- 36 块，110 帧，30 fps；两张 64×128 RGBAHalf、线性、point / clamp、无 mipmap。
- 5,304 个外表面三角形 + 3,428 个断面；原三角面归属无丢失。
- UV / 角法线插值误差为 0；源 mesh 未体素化，物理参考体与显示面独立。
- COLOR.r：外表面 0 / 断面 1，每个三角形恒定，单 submesh / 单材质。
- Unity 渲染顶点 16,928；Blender EXR 量化最大位置误差约 0.463 mm。
- Metal GPU / CPU 解码最大位置差约 1.24e-7 m；所有帧均在动画包围盒内。
- GBuffer、ShadowCaster、DepthOnly、DepthNormals 四个 pass 编译通过。
- 裂痕静止和崩塌中间态可见；真实 GPU 顶点流与新 mesh 对照一致。
- 再打开 / 导出 / 自动导入保留 GUID、材质调节值和已有实例播放进度。

报告、原始/静止/裂痕/崩塌截图：`Library/FoxStatueValidation/`。
完整 Blender 工作包与 bind JSON：`Blender/FoxStatue/Fracture/VAT/FoxStatue_Collapse/`。
插件源码和安装包：`Tools/Blender/`，本机已更新为 Statue Fracture v1.5.0。
