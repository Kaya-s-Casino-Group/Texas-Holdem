# 暴怒 Boss Unity 续做与验证计划

规格：`docs/superpowers/specs/2026-10-22-wrath-boss-design.md`。
交接：`docs/handoffs/2026-10-22-wrath-boss-unity-handoff.md`。
用户授权在已有未提交实现上继续；保留两仓改动，不重置、不自动提交、不替换暴食冒险节点。已确认规则无需重新询问。Unity 固定 2022.3.62f3。

本轮范围调整：用户随后明确暂缓当前图标、布局和怪物手的呈现适配，优先保证规则。任务 4 的未完成视觉项、任务 6 的正式资源接入保留为后续工作；继续完成 Unity 规则与时序回归、普通/精英对照及实际证据记录。

## Tasks

1. 只读核验迁移快照、Git 基点、包引用；建立本次日志与进度记录。
2. 用真实 Unity 编译并运行交接四个 EditMode 测试类；根据错误修复 API、导入与场景生成问题，记录首次失败和重跑证据。
3. 生成独立 WrathBossDemo，增加真实 PlayMode 场景回归，覆盖开局、目标、断手/再生、预警/追加大招、重开；发现缺陷先复现再修复。
4. 在图形 Editor 中进入 Play，实际操作并检查目标分辨率/720p布局、中文、占位图标、提示和六 Actor；保存截图。对无法覆盖的清单项明确标记。
5. 运行 Unity EditMode/PlayMode 回归与新暴怒规则定向、规则全量，对照交接既有失败，保留普通/精英行为。按实际测量记录战斗数据，不擅自调平衡。
6. 检查可用正式资源；有资源则接入，无资源继续占位并列明后续入口。独立审查本次变更，记录实际通过和未完成项。

## Interfaces / review focus

- Tasks 2→3：场景生成出的六个固定 Actor 和 XML 遭遇必须与播放测试一致；不改 BattleDemo。
- Tasks 3→4：测试经真实 Host/Timeline/HUD，显示不得抢先读取最终权威状态；实际 Play 检查视觉与点击。
- Tasks 2–5：只允许一个 Unity Editor 占用项目；.NET 使用独立 artifacts 输出，不能改 Unity bin/obj。
- Tasks 5→6：旧测试结果仅作基线，最终记录本次 XML/TRX；正式资源缺失不影响功能验收。

进度与实际命令保存在 `Tools/Validation/2026-10-22-wrath-unity.md`，原始运行记录在 `Logs/WrathUnityValidation/` 及 `Logs/wrath-*.xml/log`。不把原交接的“已编写”当作已通过。
