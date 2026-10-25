# 流血状态与敌方伤害预告的信息一致性

日期：2026-10-25。范围：七站冒险的流血显示与敌方攻击预告，只调整展示，不改战斗结算。

## 本次调整

- 玩家血条下保留流血图标和层数，常驻文案改为“流血 X 层 · 加伤已计入意图”。百分比移入悬浮详情，说明“已计入敌方伤害预告，无需再次计算；格挡另算”。
- 攻击意图的数字旁改为两行“预计／伤害”，详情显示“预计伤害：X（格挡前）”。沿用原提示框、图标、数字位置和美术，仅调整框内文字。
- 确实受到流血修正的伤害，详情补充“已含流血加伤”及引擎记录的实际修正过程；不按最终数字倒推，不把本次攻击之后才施加的流血算到先前攻击上，零伤害不误报加伤。
- 保留原流血比例、层数衰减、清除、格挡、伤害取整和道具自伤规则。未修改新手教学的首次触发条件或重置用户教学记录。

示例：第 6 回合，基础攻击 33、流血 4 层，总倍率 2.2，预告显示 72；已有 20 格挡时实际扣血 52。预告的 72 已包含流血加伤。

## Unity 验证

使用 Unity **2022.3.62f3 (96770f904ca7)**，在现有隔离验证副本中运行，避免打断用户正在打开的主工程：

`/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`

最终核对 Assets 下的源码、内容文件、美术、场景及布局与主工程一致；差异仅为验证时生成的两份字体缓存。Packages 一致，ProjectSettings 仅 MCP 编辑器连接配置不同。479 份主工程 C# 与 287 份引擎 C# 均与主工程逐文件哈希一致。

| 验证项 | 实际结果 | 证据 |
| --- | --- | --- |
| 最终 Unity 编译与 EditMode 相关回归 | **84 / 84 通过**，最终日志无 C# 编译错误、Shader 错误或异常 | `Library/BleedingIntentClarity-20261025/editmode-layout-final.xml`、同名 `.log` |
| 最终实际渲染 Play：流血已计入、两处真实射线悬浮、文字边界、预告与结算一致 | 通过；实际走到第 6 回合，确认 72 − 20 = 52，查看提示不推进战斗 | `playmode-layout-final.xml` 中 `EliteBleedingForecastExplainsIncludedAmplificationAndStillResolvesBlockOnce` |
| 最终实际渲染 Play：普通出牌、遗物、踢脚牌及战后奖励 | 通过 | 同一结果中的 `AdventureBattleUsesOutcomeRelicsKickerFlightsAndAshBeforeReturningToRewards` |
| 实际渲染 Play：精英六回合施法／普攻循环与实际扣血 | 通过，伤害序列 4、20、11、43、22、72 | `playmode.xml` 中 `SevenStationEliteShowsEveryCastBetweenItsActualAttacks` |
| 实际渲染 Play：流血层数、增幅与葫芦清除 | 通过 | `playmode.xml` 中 `SevenStationEliteBleedingCueRendersRealStacksAmplificationAndFullHouseCleanse` |
| 104px 双意图框中的两位／三位伤害 | 72、160 两例均通过；实际 TMP 字形不与剑图标、伤害数字重叠，且位于框内 | 最终 EditMode 中 `NarrowEliteDamageCaptionFitsBesideTheActualSwordAndTwoOrThreeDigitGlyphs` 两例 |

PlayMode 使用带 Metal 渲染的 Unity 编辑器运行，未使用 `-batchmode` 或 `-nographics`，不是纯 C# 测试替代。共 4 个不同场景用例分批通过，其中最终文字排版修改后复验上述前 2 个场景。测试使用临时场景，关闭冒险自动存档，并恢复测试前的教学设置；10000 生命和 20 格挡仅为测试夹具。

EditMode 覆盖流血提示及循环、Boss 世界 HUD 与免伤显示、意图悬浮、布局、选牌预览、主动道具、出牌输入、反馈文本、新手教学和牌型指南。

实际验收中发现并修复了四字标签溢出／与伤害数字重叠、流血详情超过原弹框高度。新增对应边界检查。验证启动期间曾遇到一次授权客户端超时、一次 Unity Burst 内部原生崩溃；保留失败日志，正常重启后通过，未为此修改项目配置。未执行独立 Player 构建或全项目全量测试。

## 截图

最终截图位于 `Library/BleedingIntentClarity-20261025/screenshots/`：

- `bleeding-intent-included-summary.png`：常驻提示和预告伤害。
- `bleeding-status-included-tooltip.png`：流血图标悬浮详情。
- `bleeding-attack-included-tooltip.png`：攻击意图悬浮详情及实际修正记录。

## 实现验收时的工作区快照

开始时记录主工程已有 114 个未提交文件，现全部保留：108 个字节完全一致，6 个相关文件仅追加本次改动；两个字体缓存均未被本次修改。另增加本次 PlayMode 回归文件和本报告，更新 3 条已有 Boss HUD 文案断言。

`External/BattleToolkit` 开始和结束均干净，本次未修改引擎。两仓库 HEAD 和暂存区保持不变，没有提交或推送。

保留审计：`Library/BleedingIntentClarity-20261025/before.json`、`after.json`、`source-parity.json`；开始时文件副本位于同目录 `baseline/`。

## 提交范围

后续按用户要求，只提交流血提示实现、必要的流血状态挂接／中文映射／重复施法意图刷新、流血回归及所需的辅助方法。混有其他优化的文件分段暂存，字体缓存与其他界面功能继续保留未提交。

上述 84 项结果来自包含既有未提交改动的验收工作区；本次提交没有纳入所有其他优化及其测试。按用户要求不重复执行已通过的测试，提交及推送结果以 Git 记录为准。
