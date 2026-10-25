# Unity 编译恢复与七站实际运行验证

日期：2026-10-22。Unity：2022.3.62f3，macOS / Metal。

验证基于主仓 `740876a`（已合并远端 dev）及 BattleToolkit `7322744`，另加下列本地测试修复。

## 编译阻断与修复

实际启动 Unity 出现 Safe Mode，编译报错：

```text
External/BattleToolkit/Engine/Tests/Editor/SingleChapterEnemyRulesTests.cs(131,20):
error CS0117: 'Assert' does not contain a definition for 'Multiple'
```

此前 .NET 使用 NUnit 3.14，Unity Test Framework 使用的 NUnit 不支持 `Assert.Multiple`。
移除分组包装，保留原四条生命、攻击序列及伤害断言，改用现有兼容断言。
未修改生产规则、XML 数值或运行时表现。

通过项目内 MCP Unity 的 `recompile_scripts` 重新编译脚本；修复前后证据保存在
`Library/UnityCompileFix-20261022/compile-errors-before.txt`、`unity-compile.json` 和 `unity-compile-final.json`。
两次重新编译均成功，编译器 0 错误、0 警告，编辑器已退出 Safe Mode。

## 本次实际执行

| 验证 | 结果 | 证据（位于 Library/UnityCompileFix-20261022） |
| --- | --- | --- |
| .NET 数值、牌型、道具、免伤、暴怒及精英专项 | 197/197 通过 | dotnet-targeted.trx |
| Unity EditMode：SingleChapterEnemyRulesTests | 9/9 通过 | unity-enemy-editmode.json |
| Unity EditMode：SingleChapterScoringTests | 38/38 通过 | unity-scoring-editmode.json |
| Unity EditMode：SingleChapterContentTests | 6/6 通过 | unity-content-editmode.json |
| Unity 实际 PlayMode：七站场景四项夹具 | 4/4 通过，耗时 73.94 秒，进程退出码 0 | unity-adventure-playmode-final.xml |

Unity EditMode 合计 53/53；这些是在当前 Unity Test Runner 中执行的结果，和 .NET 结果分开记录。

## PlayMode 首轮发现的测试问题

实际运行 `Poker.Adventure.Tests.SingleChapterAdventureSceneTests` 四项测试，首轮 3 通过、1 失败：

- 完整七站按钮流程、结算及通关：通过。
- 流血精英四轮实际伤害及远端美术：通过。
- 暴怒 Boss 五手断裂、再生、同一演员及原位置恢复：通过。
- 普通战斗计分牌／弃牌、道具与灰烬：飞行距离检查失败。

首轮完整结果：`unity-adventure-playmode-first.xml`。

失败原因是测试仅在三张弃牌和两张攻击牌同时可见时检查飞行距离。
远端实现让弃牌按普通弃牌配置逐张结束，约 0.20／0.28／0.36 秒隐藏；攻击牌抬升 0.12 秒后飞行 0.64 秒，
达到测试要求的 1.5 纵深时，三张弃牌已不能同时可见。运行时路径没有缩短。

修复只将攻击牌的逐帧飞行／尾宽检查移出弃牌同时可见条件；保留两张攻击、三张弃牌的分流断言、
弃牌无拖尾、攻击牌尾宽及大于 1.5 的原距离要求。随后重新编译并重跑整个四项夹具。

一次编辑器内重跑停在隔离场景菜单，没有输出新结果；停止请求未使其退出，随后正常退出 Unity。
该次不计为通过。改用 Unity 自身 `-runTests -testPlatform PlayMode` 从新进程执行同一四项夹具，
结果以独立输出的 `unity-adventure-playmode-final.xml` 为准，不使用桥接调用超时或旧 XML 判定成功。

最终四项均通过：

1. 普通战斗道具使用、计分／未计分牌分流、远距离飞行、拖尾、灰烬及奖励导航：8.31 秒。
2. 七站实际按钮流程、普通战斗出牌获胜、奖励、商店、休息及通关：19.51 秒。
3. Boss 本体与五手 PSD 美术、手雷断五手、再生倒计时、菜单恢复、第 9 轮原演员原位置再生及重新选择目标：31.83 秒。
4. 流血精英 500 生命、愚者 144 上限、前四轮实际伤害 4／36／21／95 及远端美术：14.26 秒。

首轮失败已在保留原飞行距离阈值的情况下复验转绿。运行截图另存于
`Library/UnityCompileFix-20261022/screenshots/`；本次没有重写运行时飞行动画来满足测试。

## 范围与保留项

- 不实际跑满 20 轮 Unity；Boss 测试到第 9 轮，检查断手及再生；20 轮判负仍由规则回归覆盖。
- 未打包或验证独立 Player 构建；本次运行验证为真实 Unity Editor PlayMode。
- PlayMode 使用正式冒险场景的隔离副本并关闭自动存档，不读取或覆盖用户旅程存档。
- macOS Metal 在 Play 时出现 memoryless depth surface load/store 警告，与脚本编译错误分开记录。
- 未重置或清理两个仓库。原两份未跟踪排查文档保持不变；TMP 字体原有改动保留，Unity 实际 Play 又追加动态字形缓存，保留当前资源及拉取前的完整快照。
- 修复尚未提交或推送；先前全量 .NET 的 12 个既有失败没有在本任务中修复，本次不宣称全量测试全绿。
