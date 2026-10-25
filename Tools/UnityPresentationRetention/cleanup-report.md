# Palamedes 清理结果

日期：2026-10-24。分支：`Palamedes`，从 `legacy-3d-scenes` 的 `72a1553da8a16815a4eaf63cc4208987853af5bc` 创建。

## 最终范围

保留定制 URP、渲染扩展、CardArt 实例化、40 个 Shader / 26 个配套 Include、Hosting / Shared 桥接和 BattlePresentation 通用调度框架，连同编辑器工具、测试、资源和 `.meta`。

从 Assets 清理 2,104 个已跟踪文件（含 `.meta`），对应原仓库文件约 302.88 MiB，移除 170 个空目录。清理范围包括旧场景、角色/环境模型库、音频、旧 HUD / 手牌交互、场景适配器、DrawBattle / RunFlow / Minimal2D 前端、DOTween 和相关原生交互插件。保留 Shader 的环境目录没有整目录删除。

当前 Assets 共 691 个文件（含 `.meta`），所有 Shader 数量和路径保持。资源依赖补充了 URP 全局设置、项目图标、TMP 默认资源、卡牌验证桌面材质和图集工具的原始牌面。原始清单见 `inventory.json`，实际保留/删除清单见 `cleanup-plan.json`。

Build Settings 的场景列表已清空，没有保留旧游戏入口，也没有新增完整前端。根 README、脚本/资源模块说明和保留模块说明已更新。

## 必要适配

- 将 BattlePresentation.Unity 及其测试程序集的旧 `Unity.ugui` 引用修正为 Unity 2022.3 实际程序集 `UnityEngine.UI`。
- 为 `BattleTimelineSceneAdapter` 提供统一的 `SubmitAction` 转发接口，支持当前规则的手动摸牌和目标绑定。它只转发参数、处理本地播放门禁，不计算玩法结果。
- 最小宿主测试改为验证当前 XML 的手动摸牌流程，保持两场会话相互隔离的检查。
- 修复 CardArt 测试临时 Shader 的 Include 路径，以及编辑器窗口测试对高 DPI 原生窗口取整的假设；未改变实际 Shader 或窗口行为。
- 移除保留模块不使用的 `com.unity.purchasing` 包依赖并更新锁文件，防止它在每次打开工程时自动重建已清理的 BillingMode 资源。
- 撤销 Unity 验证自动添加的无关 Android 图标配置，保留原 ProjectSettings 内容；仅 Build Settings 的场景清理属于有意修改。

## 验证

- Unity 2022.3.62f3 编译成功。
- 保留的七个 EditMode 测试程序集：**174 通过，0 失败，0 跳过**。
- 最终测试日志无 C# 编译错误或 Shader error。测试涵盖卡牌网格/共享材质/实例参数、导出验证、渲染配置、基础宿主、规则投影、表现调度与编辑器工具；不代表所有 Shader 变体或独立 Player 构建均已验证。
- 静态校验通过：保留文件完整、删除目标已移除、无缺失或孤立 `.meta`、无重复资产 GUID、无悬空序列化 GUID、无缺失程序集引用、无被清理的前端程序集残留。
- 内嵌 URP 与实际 Shader 源码没有修改。

可在 Unity 退出且测试临时资源清理完毕后复查：

```powershell
node Tools/UnityPresentationRetention/validate-retention.cjs
```

结构化结果见 `static-validation.json`、`test-summary.json`、`rules-after.json`。本机完整测试结果位于 `Library/PalamedesValidation/editmode.xml`，日志位于同目录 `editor.log`。

## 规则子仓库保持不动

- 工作区提交前后均为 `b788c096e553633f18fe62d00c3d8deab1b61341`。
- 清理前记录的 783 个文件 SHA-256 校验全部一致，最终子仓库工作区干净。
- 主仓库记录的 gitlink 仍为 `7a0036e1f58b69a6257d1903e9f44056905d7aaa`，没有暂存或改写。它与本地规则 HEAD 的差异在清理前已存在，本次保持原样。
- 没有在子仓库执行 checkout、submodule update、reset 或提交。

改动留在 Palamedes 工作区，尚未创建提交或推送。
