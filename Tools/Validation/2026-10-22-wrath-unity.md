# 暴怒 Boss Unity 实际验证记录（2026-10-22）

对应计划：`docs/superpowers/plans/2026-10-22-wrath-boss-unity-validation.md`。
对应规格：`docs/superpowers/specs/2026-10-22-wrath-boss-design.md`。

## 环境与边界

- 工作区：`/Users/mt/code-yang/Texas-Holdem`；两仓分支 `codex/wrath-boss-20261022`。
- Unity：`/Applications/Unity/Unity.app/Contents/MacOS/Unity`，2022.3.62f3 (96770f904ca7)，Apple Silicon / Metal。
- 保留所有已有未提交改动；不替换标准冒险中的暴食节点，不调整普通玩法/精英数值。
- 本文是本次运行的累积记录；下方未完成项不代表通过。

## 本轮范围修正

用户在图形 Play 检查期间明确：暂不考虑当前图标、布局与手部呈现，先保证规则；后续手不采用当前怪物的呈现方式。因此停止继续适配当前占位造型，保留规则与真实 Unity 场景结算回归。下方未完成的视觉项不作为规则通过项，不再据此阻塞规则交付。

## 迁移及导入

- 开始改动前，119 个记录文件全部匹配，0 缺失/变化；两仓 HEAD、分支及本地 Engine 包/testables 与交接一致。
- Unity 首次编译发现 Editor API 不兼容（OpenPreviewScene 不存在）、快照构造缺 zones 参数；已修正。Editor 测试使用独立临时场景副本，关闭并删除测试副本，校验源 BattleDemo 字节不变。
- 修正 WrathAdventureHealthTests.cs.meta 的 33 位非法 GUID 为合法 32 位；未找到其他资产引用该非法值。
- 为新增 PlayMode 程序集补入已有 Shared 程序集引用，未修改其他已有引用。
- Unity 已生成并保存 Assets/Scenes/WrathBossDemo.unity，本体、四饮血手、一束缚手分别绑定六个 Actor。普通／精英原场景和冒险暴食节点保持原配置。

## 已实际运行的结果

| 验证环境 | 实际结果 | 证据 |
| --- | --- | --- |
| Unity 2022.3.62f3 EditMode，原交接 24 项 | 24/24 通过 | wrath-editmode-third.xml；最终定向中也包含全部 24 项 |
| Unity EditMode，新增手状态和独立场景布局回归后 | 29/29 通过，0 跳过 | Logs/wrath-editmode-final.xml / .log |
| Unity PlayMode 完整场景套件 | 运行中，尚不能记录通过 | Logs/wrath-and-existing-playmode.log |
| .NET 新规则定向 | 59/59 通过 | Logs/WrathUnityValidation/wrath-rules-targeted.trx |
| .NET 全规则 | 1667 项：1654 通过，13 失败；与交接基线名称和消息一致，无新增失败 | Logs/WrathUnityValidation/rules-full.trx |
| Unity 图形 Editor Play | 已实际启动独立场景，检查六 Actor、D=33；先前图形操作已观察断手后 D=28 | Logs/wrath-graphical-play.log、Logs/WrathUnityValidation/screenshots/ |

最后两行测试不能互相替代：.NET 是规则证据；Unity PlayMode 载入保存的真实场景、运行权威 Session 和异步表现时间线；图形 Editor 检查是独立的人工画面证据。PlayMode 中直接调用 HUD 按钮监听或使用确定牌组，不代表鼠标命中、正式美术和所有分辨率已验收。

wrath-editmode-fifth.log 是 Editor 占用项目导致的启动失败（未执行测试），不能计入成功结果。

## 本轮发现并修正的问题

1. 上述 Unity 专有编译及 meta 问题。
2. 活着的手在暴怒 HUD 只展示血量，遗漏权威意图中的饮血、等待束缚和眩晕跳过说明。新增测试先出现 3 个失败，再在暴怒专用面板读取已投影的意图首行；不在 HUD 重写规则。
3. 切换场景时，世界标记可能先于旧 HUD/Adapter 销毁，旧显示同步访问 marker.Root 会抛 MissingReferenceException。第二次 PlayMode 6 项中 3 通过、3 被此真实产品异常打断；已对已销毁 Root 加 Unity 空判断，等待完整套件复验。
4. 早期 PlayMode 等待使用固定帧数，在高帧率批处理中过早超时；改为真实时间期限并采样每个显示检查点。该修改只影响测试等待，不加速或跳过产品结算。

独立场景的对局修改器按钮曾覆盖首格意图，已调整该场景按钮位置并有回归；用户收窄范围后停止进一步布局工作。未修改普通战斗布局来适配暴怒。

## 新增的 Unity PlayMode 规则覆盖

WrathBossScenePlaybackTests 共 10 项（最终运行结果见上表）：

- 保存场景开局：六个独立身份、1500/1 血、D=33、八格常规序列；预览不改变权威状态。
- HUD 指定单手：权威先死亡、显示在命中点切换，D=28、死亡手退出候选、R+8 同 Actor 再生并恢复攻击贡献。
- 测试内存夹具仅把束缚周期改为 20，以保留预警后的玩家窗口，验证实际出牌断手即时取消预警，重开恢复原 XML 和新局身份。
- 默认束缚配置：同一结束回合命令内观察第 5 轮预警、第 6 轮常规 43 与追加 129，额外大招不推进第二格。
- 实际顺子逐段伤害 0/0/8/10/12 消耗两层龟缩；皇家同花顺通过 Eliminate 获胜。
- 第 6 轮普攻触发玩家替死，停止尚未发生的大招并保持复活状态。
- 实际 HUD 手雷三次：不耗行动点、消耗两层免伤、五手同时死亡、R+8 原身份同时再生，四饮血手当轮治疗。
- 实际葫芦：免伤不阻止控制，玩家获得免控，保持本体序列和下一玩家窗口。此用例中葫芦同时杀束缚手，因此不是“活束缚手施法被免控拒绝”的隔离证据；后者见规则测试。
- 实际同花击杀本体：五手仍存活也胜利，同次牌附带治疗继续完成。
- 内存夹具仅提高玩家最大生命，保留默认 Boss 和束缚，完整结算 20 轮后判负、不消耗替死，检查蓄力次数和累计伤害。

规则逐项证据与自然交互见 docs/handoffs/2026-10-22-wrath-boss-evidence/rules-final-review.md；其中标明静态推导、实际测量与历史测试债。

## 待完成／按用户要求暂缓

- 完整 PlayMode 与更广 Unity EditMode 运行及结果对照仍在执行。
- 占位图形在图形 Play 中有未渲染问题，手位置红色倒计时亦未完成视觉验收；不将 active/text 断言写成像素验收。此轮按用户新要求暂停修复和当前模型适配。
- 正式 UI、美术、动画及最终手的呈现方式留待后续资源和方案；当前未接入正式资源。
- 720p/其他分辨率、移动安全区、鼠标命中和完整逐帧视觉清单未全部验收。
- 平衡尚无多种子正常构筑胜率测量，不据耐久夹具直接调数值；本轮未调整 Boss、普通战斗、牌型或道具数值。
