# 完整表现回归的 5 项失败修复

## 原因与改动

1. **世界空间 Tooltip 越界**：完整 EditMode 中复现系统屏幕为 541×2548、相机输出为 2532×1170，代码直接把系统安全区像素用于相机裁剪，造成左边界 -0.00866752863。新增统一显示尺寸换算，Canvas 与世界空间 Tooltip 共用；相机输出到 RenderTexture 时不套用设备安全区。原测试继续检查所有四角均在 viewport 内，并增加尺寸不一致、分屏和离屏测试。
2. **两个原生 UI 像素测试**：测试写死 540 行，而已保存的主 Renderer 使用可配置的 480 行。现在从 Renderer 的实际高度配置计算期望值，仍验证原生 UI 分辨率、逐像素黑白条纹、场景颜色保留，以及动态追加 Overlay 的首帧顺序。
3. **UI Renderer Feature 断言过时**：项目已明确将 CRT 安装在最终 UI Renderer，在 UI 绘制后作用于完整画面。测试改为严格要求唯一 Feature 为 `CRTFeature / ICameraStackOutputFeature`，继续禁止 UI 自身低分辨率和场景后处理。逐像素测试临时关闭 CRT 曲率/扫描线影响并在 finally 恢复；另执行 CRT GPU 验证，确认最终输出行为。没有改变作者的 480p 或 CRT 配置。
4. **资源元数据检查误报**：Unity 不导入点号开头的隐藏文件/目录，`.DS_Store` 不应要求 meta。扫描仅排除这些隐藏路径；普通 XML/代码资源仍必须有 meta、可导入 GUID 且 GUID 唯一。新增 6 项路径分类用例，明确普通缺失 metadata 的资源不能被过滤。没有删除本地 `.DS_Store` 或修改规则子模块。

## 验证结果

- Unity 编译通过，0 warning。
- 完整表现 EditMode：**423/423 通过**（原 415 项 + 新增 8 项）；包含原来失败的 tooltip、两个实际像素渲染、UI Renderer 配置及包元数据检查。
- CRT GPU 验证通过：横屏、竖屏、非整数放大、原生尺寸、零强度绕过、颜色/alpha、曲率边框、扫描线、原生 UI 接受 CRT、最终 UI 相机执行。
- 2D/3D 联合 PlayMode：**8/8 通过**，开局、预选切换/取消、出弃牌、镜头/UI、敌人队列、同步与重开通过。
- 本次代码与文档 diff 检查通过；Renderer/管线资产及规则子模块没有修改。

本地证据（Library 不入库）：`Library/BattlePresentationValidation/regression-fixes-editmode.xml`、`regression-fixes-playmode.xml`、`Library/CRTValidation/report.txt` 与 GPU 对照图。

本次未执行 Player/IL2CPP 构建。其他未提交的美术、字体、环境与场景改动不属于本次修复。
