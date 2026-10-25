# 七站高牌初始基础分调整

本文件记录首次仅移除高牌初始基础分时的历史结果。后续用户决定将七站所有牌型的初始基础分及每级基础分增长清零，最新规则及验证见 [七站纯牌面计分回归](2026-10-22-card-face-scoring.md)；下文的“保留高牌基础分成长”及其他牌型旧伤害不再是当前规则。

2026-10-22，按用户“自带五点基础分是哪里来的？可以去掉吗？”的请求，移除七站高牌初始 5 点基础分。

## 来源与变更

来源是 BattleToolkit `Engine/Content/final.xml` 数值初版，文件注释明确写出“牌型加入基础筹码”，高牌配置为 `points="5"`。历史提交 `5d0263c` 保存该数值初版，七站基线提交 `b034858` 沿用了它。

本轮仅将 `Engine/Content/SingleChapterBattle.xml` 的 HighCard 初始 `points` 从 5 改为 0，内容版本更新到 `single-chapter-battle.2026-10-22.2`。高牌每级 +5 点、+1 倍率仍保留；其他牌型、道具、精英与暴怒技能未修改。七站四个战斗入口共用该评分配置。历史 `final.xml`、标准单局和旧三章文件未改。

对 XML 做语义比较，除根版本之外，唯一数值差异为 HighCard.points=0；记录在 `Library/HighCardBase-20261022/scope-check.json`。

## 实际验证

- 既有规则回归 **66/66 通过，0 失败、0 跳过**：SingleChapterContentTests、SingleChapterEnemyRulesTests、AdventureEconomyTests、CombatWrathImmunityTests、WrathBossRulesTests。证据：`Library/HighCardBase-20261022/results/high-card-regression.trx`。
- 实际 HTTP 战斗接口 **24/24 通过**：原始磁盘 XML，普通一、普通二、精英、暴怒四入口分别检查单张 2/9/K、一对两张 2、三条三张 2、单张 9 配倍率 +2 遗物。仅通过管理员接口换手牌或遗物，敌人与规则不使用替换夹具。
- 四入口单张 2/9/K 的预览与实际生命差均为 **2/9/13**；一对仍为 **28**；三条仍为 **93**；单张 9 配倍率 +2 遗物为 **27**。均在初始玩家阶段结算，未被免伤或后续饮血回血混淆。证据：`Library/HighCardBase-20261022/http-summary.json`、`http-responses.json`。临时服务已停止。

本轮没有重跑 Unity 实机出牌，不将 HTTP 或纯 C# 结果记为 Unity 验收。AdventureSceneHost 在 Initialize 时仅编译内容一次；Unity 应退出 Play、重新进入，再开七站新局读取新版配置。旧内容指纹的存档按已有规则拒绝恢复，原文件保留，不做额外迁移。

保留两个仓库已有未提交改动；未提交或推送本轮调整。
