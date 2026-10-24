# 七站冒险：实际 20 回合失败界面验收

> 后续定位更正：下述两次初测受到 Unity Test Framework 1.3.3 的 180 秒默认超时及超时后同步耗尽协程的影响，不能据此认定正式游戏结算卡死。帧级诊断已复现测试运行器在同一帧重复执行等待循环。修复及重新验收见 [长时测试修复记录](2026-10-23-round-limit-runner-fix.md)。以下保留初测原始记录。

日期：2026-10-23。结论：**未通过**。实际完成第 20 个敌方回合后，规则正确判定回合耗尽失败，但未观察到失败页面正常显示。

## 运行环境与边界

- Unity 2022.3.62f3，macOS，Metal，1280×720；图形模式 PlayMode，未使用 batchmode 或 nographics。
- 从当前工作区（包含已有未提交改动）创建隔离副本：`/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`。
- 使用正式七站冒险场景、正式 Boss 和五只手、正式 20 回合上限及演出流程。
- 前置站点由测试辅助流程快速通过；Boss 战从第 1 回合起逐轮执行真实「结束回合」按钮事件与敌方演出，没有直接设置为第 20 回合。
- 为避免主角提前死亡，仅在测试副本运行时将主角当前/最大血量设为 100000，并清空遗物。因此本次验证超时流程，不验证常规生存或遗物组合。
- 束缚手的眩晕保留。第 5、10、15 回合后的玩家行动被跳过，17 次按钮点击实际执行了完整的 20 个敌方回合；逐轮快照已记录 1～20。
- 正式工程和 BattleToolkit 的运行时代码均未修改。新增测试及 MCP 自动启动设置仅存在隔离副本。

## 实际结果

| 检查项 | 结果 |
| --- | --- |
| Unity 图形模式进入七站冒险 Boss 战 | 通过 |
| 实际执行并播放 20 个敌方回合 | 通过，两次完整运行均到达第 20 回合 |
| 第 20 回合后按回合耗尽判负 | 通过，状态 `Lost`，原因 `RoundLimit` |
| 排除主角死亡/Boss 死亡触发结算 | 通过，主角 98774/100000，Boss 2000/2000，五只手仍存活 |
| 最终演出结束并显示失败页面 | 未通过 |
| 失败页统计及「返回首页」按钮 | 未完成，未到达可验证页面 |

第一次完整运行在规则已判负后，等待演出结束 40 秒仍为 `IsPresenting = true`。NUnit 结果为 1 项失败：`Enemy playback timeout after completed round 19`；这里的 19 是最后一次点击前的已完成回合数，点击后的快照已到第 20 回合。

第二次完整运行通过了演出结束检查，并进入结果页面切换前的延时检查，保存了第 20/20 回合战斗画面；随后 Unity 主线程不再推进、CPU 约 99%，未观察到失败页面，也未生成最终 NUnit 结果。采集原生调用栈后，仅终止隔离副本的测试编辑器（PID 6943，SIGTERM，退出码 143）。该次运行按中止记录，不计为通过。

当前证据表明规则判负与失败界面显示之间存在问题。原生堆栈包含 Coroutine MoveNext、未解析的托管调用及 GetComponents 递归，尚不能据此确认具体根因；没有修改正式代码尝试修复。

## 证据

证据目录：`/Users/mt/code-yang/Texas-Holdem/Library/RoundLimitValidation-20261023-221206`（Library 为本地运行产物）。

- `screenshots/boss-twenty-round-trace.txt`：第二次完整运行的 20 轮演出与按钮点击记录。
- `screenshots/boss-round-20-result-delay.png`：实际第 20/20 回合仍显示战斗界面的截图。
- `attempt3-real-twenty-round-stall/unity-playmode.xml`：第一次完整运行的失败结果，耗时约 213 秒。
- `attempt3-real-twenty-round-stall/unity-playmode.log`：第一次完整运行的 Unity 日志。
- `unity-playmode.log`、`unity-hang-sample.txt`、`aborted-hang.json`：第二次完整运行日志、无响应采样及中止记录。
- `RoundLimitPlayModeFixture.cs.txt`：隔离副本中执行的测试代码。
- `source-comparison.json`：运行时代码、场景、内容和设置的副本对照；1632 个文件一致，只有隔离副本测试文件和关闭 MCP 自动启动的设置存在预期差异。
- `workspace-preservation.json`：主仓库和子仓库所有原有未提交文件内容的哈希复核。

早期测试曾因测试代码类型不匹配未启动，以及错误假定一次点击只推进一个回合而停止；这两次不计为游戏失败，也不计为完整 20 回合验收。以上结论依据修正测试后两次实际完整运行。

## 工作区保留

本次仅向正式工程新增本验证记录；字体缓存、Boss 提示、现有测试以及 BattleToolkit 等所有已有未提交改动均保留。未提交、未推送、未重置工作区。
