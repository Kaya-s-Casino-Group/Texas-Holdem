# 暴怒 Boss：移机与新会话交接

> 续做与同步：当前七站规则、远端表现保留范围、Boss 专项实际回归结果及代码同步分支以 `Tools/Validation/2026-10-22-wrath-final-regression.md` 为准。用户已授权提交并推送本地实现；下文提交基点、尚未提交和需整目录迁移等表述属于历史移机快照。

> 2026-10-22 后续状态：本机已使用 Unity 2022.3.62f3 导入并运行验证。用户现已明确授权将本地新版流血精英与暴怒接入**新七站冒险**；旧三章 `StandardAdventure.xml` 仍保留暴食。最新接入、逐项验收与已知失败以 `Tools/Validation/2026-10-22-seven-station-enemies.md` 为准。下文是移机时的历史快照，其中“本机无 Unity”“尚无暴怒 PlayMode”“冒险暂不替换”等描述不再代表新七站的当前状态。远端更新后的 BattleDemo 是普通三敌验证场景，流血精英独立场景为 BleedingEliteDemo；此前生成的 WrathBossDemo 保留。

交接日期：2026-10-22。目标：把当前两个仓库的完整实现迁移到有 Unity 的电脑，继续完成 Unity 验收、正式 UI／美术／动画接入及平衡性验证。

原始依据：飞书策划 `https://moonton.feishu.cn/wiki/KZGawy0ypiSMPtkmqwDcAKLFnle` 的 Boss-暴怒小节（此前分析读取 revision 4300）、用户提供的技能表截图，以及本会话后续确认。本文没有重新拉取在线文档，当前实施口径以仓库内已确认规格和后续用户修正为准。

本文是当前会话的接手入口，不表示所有平台已验收。当前规则与 Web 可玩功能已经实现；Unity 代码已编写，但旧电脑没有 Unity Editor，尚未编译、生成或运行暴怒场景。文中路径除另有说明外，均相对 `Texas-Holdem` 根目录，迁移后不需要保持旧电脑用户名和目录。

## 1. 给新会话的首条消息

可将以下文字直接发给新会话：

> 请先阅读 `docs/handoffs/2026-10-22-wrath-boss-unity-handoff.md` 和 `docs/superpowers/specs/2026-10-22-wrath-boss-design.md`。这是一项已有实现的续做任务。先检查主仓库和 External/BattleToolkit 的迁移完整性，再用 Unity 2022.3.62f3 导入、编译、生成独立 WrathBossDemo 场景，运行文档列出的 EditMode 测试和实际 Play 验收。修复发现的问题，保留普通战斗、牌型、道具及已有精英行为。已确认的暴怒规则不需要重新逐项询问。正式素材尚未提供时继续使用占位图标／模型，不把缺素材当成不能验证的理由。请补充暴怒专用 PlayMode 回归，并逐项记录实际通过与未完成项；不要把纯 C# 测试当成 Unity 实机验收。随后在资源可用时接入正式 UI、美术和动画；平衡性先测量再提出调整建议。现有冒险地图仍使用暴食，未经新的产品决定不要替换节点。保留两个仓库内所有已有未提交改动，不要重置工作区。

本文不会自动创建新会话；用户将在新电脑打开迁移后的项目并发送上述消息。

## 2. 仓库与迁移：先确保文件齐全

| 项目 | 当前值 |
|---|---|
| Unity 主仓库 | `https://github.com/Kaya-s-Casino-Group/Texas-Holdem` |
| 规则／Web 子仓库 | `https://github.com/Kaya-s-Casino-Group/BattleToolkit`，位于 `External/BattleToolkit` |
| 两仓库分支 | `codex/wrath-boss-20261022` |
| 主仓库 HEAD | `1500cb1223dc8cc968256563d9420f7784893634` |
| 子仓库 HEAD | `2228665bf9d8afa8b5c0807db32e933653a0c463` |
| Unity | `2022.3.62f3`，revision `96770f904ca7` |
| Unity 规则包 | `Packages/manifest.json` 引用 `file:../External/BattleToolkit/Engine` |
| .NET 测试与 Web SDK | `10.0.100`，`global.json` 允许 `latestFeature` |

**上述 HEAD 只表示改动之前的提交基点。本次实现尚未提交、推送或合并，不能靠重新 clone／pull 恢复当前成果。** 主仓库和子仓库都有修改及未跟踪文件；只复制 `git diff` 也会漏掉新增文件。

推荐复制整个 `Texas-Holdem` 项目工作目录，包含：

- 主仓库 `Assets`、`Packages`、`ProjectSettings`、`Tools`、`docs` 和所有 Unity `.meta`。
- `External/BattleToolkit` 的完整源码、内容 XML、Web、测试、启动器及新增文件，不能只复制子模块指针。
- 若继续保留 Git 历史，一起复制根目录隐藏的 `.git`，包括 `.git/modules/External/BattleToolkit`。子模块 `.git` 当前是指向该目录的文本文件；不能只复制它而漏掉根 Git 元数据。
- 仓库内其他已跟踪文件也应完整保留，不要只按上面几个目录挑选复制。

可以不迁移会重新生成的 `Library`、`Temp`、`Obj`、`Logs`、`node_modules`、`.runtime`、`.artifacts`、构建 `bin/obj`。不要把“可排除缓存”理解成可以排除同名的源码目录。旧电脑 `/tmp/wrath-dotnet-10` 是临时 SDK，不是项目依赖，不要将它当作移机路径。

`BattleDemo.unity`、字体、渲染设置、流血精英等已有未提交改动与本次暴怒改动并存。需要整体保留，不能把本次清单中的所有变更都归因于暴怒。

### 移机校验附件

同目录的 `2026-10-22-wrath-boss-evidence/` 包含：

- `worktree-manifest.json`：两个仓库的提交基点、状态和当前已修改／未跟踪文件的 SHA-256，以及关键环境文件。它是交接快照，不是源码备份。
- `verify-migration.py`：只读核验上述文件是否遗漏或内容变化。
- `test-results-summary.json`：从原始 TRX 提取的测试统计、失败名称与错误信息；保存在仓库目录，避免清理 Library 后丢失依据。
- `wrath-prompts-iab.png`：Web 文字提示和断手红色 8 的人工验收截图。

在新电脑项目根目录、进行新修改之前执行：

```sh
python3 docs/handoffs/2026-10-22-wrath-boss-evidence/verify-migration.py
git status --short --branch
git -C External/BattleToolkit status --short --branch
```

Windows 可将 `python3` 替换为 `py -3`。校验覆盖本次工作区差异和关键文件，不覆盖全部未修改资产；仍需完整复制项目。若 Git 自动转换换行导致哈希变化，先核对差异，不要自动覆盖新电脑文件。不要在迁移完成前执行会还原文件的 reset／clean／强制子模块更新。若采用提交后同步，必须先保存子仓库改动，再保存主仓库的子模块指针及其他文件；本交接没有代为提交。

旧服务地址 `127.0.0.1:18768` 只属于旧电脑，不是远程发布地址。新电脑需要重新启动服务。浏览器草稿和冒险存档如需保留，应通过网页导出；单场战斗进度在服务内存中，移机后重新开局。

## 3. 已确认规则：以此为准，不恢复旧千手方案

详细决策保存在 `docs/superpowers/specs/2026-10-22-wrath-boss-design.md`。较早分析报告中“待确认”的问题已按用户确认及授权推荐方案解决；若原飞书概述与这里冲突，不能自行恢复旧概述。

| 规则 | 已实现口径 |
|---|---|
| 六名敌人 | 1 个暴怒本体 + 4 只饮血手 + 1 只束缚手；不是六个 Boss。每个是独立单位，保留固定 ID 和位置。 |
| 生命 | 本体 1500；手始终 1 血，受至少 1 点实际生命伤害死亡；零伤害／纯控制不直接断手。 |
| 攻击力 | `D = 8 + 5 × 存活手数 + 10 × 成功蓄力次数`；初始 D=33；断手／再生立即更新。蓄力永久累计于本场，眩晕跳过时不增加。 |
| 常规意图 | 用户明确修正为 **普攻 → 普攻 → 防御 → 蓄力**，四步循环，八格预告。眩晕跳过不推进序列。 |
| 大招 | 独立于常规序列，先常规动作再追加 `3D`。无遮挡情况下第 5 轮预警、第 6 轮释放、第 11／12 轮重复；成功释放后 R+5 可预警、R+6 可释放。 |
| 取消与冷却 | 预警时断任一手立即取消，并从取消轮重新冷却 5 轮。冷却结束但手不齐保持就绪；五手齐全时先预警，不直接放大。本体被眩晕时不发起预警，到期大招取消并重新冷却。 |
| 普通序列计数 | 大招不替换常规动作、不多推进一格、不额外触发第二次 ActionCompleted。 |
| 龟缩 | 刷新为 2 层，不叠超过 2。每个独立正数 Damage 段消一层并最终归零，含毒、反伤、道具；原本零伤害不消层。只保护本体，不提供状态免疫。 |
| 强制击杀 | 皇家同花顺的 Eliminate 可穿过龟缩；不额外添加 Boss 秒杀免疫。 |
| 独立再生 | 第 R 轮死的手于第 R+8 轮敌方阶段开始复活；R 轮敌方阶段剩余 8，之后逐轮减少。同轮可全部复活，复用原身份，正式复活清状态。 |
| 再生顺序 | 再生先于该阶段手技能／五手判断；复活当轮可饮血／束缚，立即恢复每手 +5 攻。 |
| 饮血 | 每敌方阶段每只存活可行动饮血手仅给本体治疗 5；四只合计 20，不过上限。手先结算自身开始毒伤，再治疗；本体在治疗后按自身开始时点结算毒伤。 |
| 束缚 | 全场第 5、10、15、20 轮对玩家施眩晕，跳过下一玩家行动窗口；受既有免控阻挡。死亡／眩晕错过不补发，复活不重置周期。 |
| 手的限制 | 饮血／束缚受对应手眩晕影响；眩晕手仍算存活。手不能获得正向加攻或护盾，复活／净化不移除此身份限制；减益、毒等保持普通规则。 |
| 终局 | 本体死立即胜利，仍完成同次出牌已附带的治疗。玩家替死复活立即终止整个敌阶段；20 轮完整结算后判负，不触发替死鬼。 |
| 冒险 | 本体按章节现有生命倍率成长，手豁免倍率。Boss 本场状态不跨场。标准地图暂时仍是暴食节点。 |

保留普通牌型／道具的自然交互：三条等群攻没有单体目标槽；顺子可逐段清手；手雷不耗行动点；葫芦免控可克制束缚；致命一击、皇家同花顺、高倍伤害仍可杀本体。同花治疗不因打手或本体免伤而取消。不要为了 Boss 改动这些通用玩法。

## 4. 当前实现在哪里

下表 `BT/` 表示 `External/BattleToolkit/`；`SB/` 表示 `Assets/Scripts/Gameplay/SingleBattle/Presentation/`。

| 内容 | 入口 |
|---|---|
| 当前 XML／独立模板 | `BT/Engine/Content/StandardSingleBattle-2026-10-21.xml`；`BT/Engine/Content/Templates/10_WrathThousandHands.xml` |
| 遭遇 ID | `standard-wrath-thousand-hands` |
| 权威 Boss 状态与查询 | `BT/Engine/Runtime/Composition/Encounters/WrathBoss.cs` |
| 阶段调度、再生、手技能、预警／追加大招 | 同目录 `TeamBattleWrathBoss.cs`，并接入 `TeamBattleEngine.cs` |
| 手部效果限制 | 同目录 `WrathHandEffectRestrictions.cs`、`RelicNumericModifiers.cs`，以及 `Combat/CombatInteractions.cs`、`CombatEngine.cs` 的可选策略接口 |
| Unity 状态投影／提示去重 | `SB/Shared/Runtime/BattleWrathDisplayProjector.cs`、`WrathPromptTracker.cs` |
| Unity HUD／图标／世界位置数字 | `SB/Scene3D/Runtime/UI/SingleBattleHud.Wrath.cs`、`SingleBattleHud.WrathPrompts.cs`、`WrathIntentPlaceholderGraphic.cs` |
| Unity 场景生成 | `SB/Scene3D/Editor/WrathBossSetup.cs` |
| Web 权威回放帧 | `BT/Tools/BattleContentEditor/PlaytestWrathPresentation.cs`、`PlaytestProjection.cs`、`PlaytestBridge.cs` |
| Web 文字／图标映射 | `BT/Tools/BattleContentEditor/web/wrath-presentation.js` |
| Web 选敌／回放／样式 | 同目录 `playtest.js`、`playtest.css`；新模块服务白名单在 `NativeServer.cs` |

提示已经实现：入场每场一次的 `警告：每存活一只手，暴怒攻击力+5！！`、可回看的机制说明、大招红字 `警告：Boss有五只手臂，将释放大招！！`、断手位置红色数字及敌阶段冒字 `X个回合后再生`。预览、重复刷新、同一响应重放不能重复触发冒字。回放结束恢复最终权威状态；旧局回放不能覆盖新局。

Web 单体攻击可点击本体／手，或用 Enter／Space／下拉框选敌，选中态同步。死亡手不能选；群攻没有虚假单选。普通敌人的意图悬浮提示保留。大招不占八格常规意图图标。

继续接显示时使用 `WrathBossQuery.Read(state)`。`UltimateBaseDamage`／`PendingUltimateBaseDamage` 是非 nullable 的 `long` 基础值，不包含最终攻伤／承伤修正；是否展示待释放大招由 `WarningActive` 决定，不能用数值非零代替。普通意图预测使用 `PreviewEnemyAction(..., includeWrathUltimate:false)`，避免把大招伤害叠进普攻图标。

## 5. Unity 新电脑的第一轮操作

1. 使用 Unity Hub 打开项目，优先安装／使用 `2022.3.62f3`。等包导入及脚本编译结束，先处理真实 Console 错误；不要先升级 Unity 或包版本来掩盖问题。
2. 确认本地规则包路径正确，`External/BattleToolkit/Engine` 内容齐全；`Packages/manifest.json` 已将 `com.kaya.battle-engine` 放入 `testables`。
3. 退出 Play 模式，点击菜单：**工具 → 卡牌战斗 → 示例场景 → 打开暴怒 Boss（本体与五手）**。
4. 首次生成 `Assets/Scenes/WrathBossDemo.unity`，内容来自现有 `BattleDemo.unity` 副本，并绑定本体、四饮血、一束缚六个 Actor。原 `BattleDemo` 仍是流血精英入口。
5. 点击 Play，按第 7 节逐项验收，保留 Console 日志、测试 XML 和截图。

**场景生成注意：**旧电脑尚未生成 `WrathBossDemo.unity`。已有同名场景时，菜单只打开，不重配／升级。首次复制后若配置验证失败，可能留下未配置完整的副本；修复原因后应先备份并移开该副本，再生成。不要丢弃新电脑已有的手工修改。`ConfigureScene(Scene)` 会重设位置、比例和内容绑定，但不负责保存。

### Unity 自动测试

先通过 **Window → General → Test Runner → EditMode** 运行以下四个测试类，共 24 项：

| 程序集 | 测试类 | 当前证据 |
|---|---|---|
| `Poker.BattlePresentation.Shared.Tests` | `Poker.BattlePresentation.Tests.BattleWrathPresentationTests`（8） | 纯 C# 已通过；尚未在 Unity 跑 |
| 同上 | `Poker.BattlePresentation.Tests.WrathPromptPresentationTests`（10） | 纯 C# 已通过；尚未在 Unity 跑 |
| `Poker.SingleBattle.Tests.Editor` | `Poker.SingleBattle.Tests.WrathBossSceneTests`（2） | 未运行 |
| 同上 | `Poker.SingleBattle.Tests.WrathHudPromptTests`（4） | 未运行 |

规则测试程序集为 `PokerEngine.Tests.Editor`。现有 `Poker.SingleBattle.Tests.PlayMode` 有通用及精英回归，**尚无专门的暴怒 PlayMode 测试**；需补充真实场景播放用例，不能将现有测试全绿等同于暴怒演出已验证。

以下 Bash 命令从主仓库根目录运行；把 `POKER_UNITY` 改为新电脑 Unity 可执行文件路径。命令按仓库测试入口整理，尚未在 Unity 实测；也可直接用 Test Runner。Windows 使用对应 Unity.exe 和 PowerShell 调用语法。

```sh
POKER_UNITY="/path/to/Unity"
mkdir -p Logs
"$POKER_UNITY" -batchmode -projectPath "$PWD" \
  -runTests -runSynchronously -testPlatform EditMode \
  -assemblyNames 'Poker.SingleBattle.Tests.Editor;Poker.BattlePresentation.Shared.Tests' \
  -testFilter 'Poker.SingleBattle.Tests.WrathBossSceneTests;Poker.SingleBattle.Tests.WrathHudPromptTests;Poker.BattlePresentation.Tests.BattleWrathPresentationTests;Poker.BattlePresentation.Tests.WrathPromptPresentationTests' \
  -testResults "$PWD/Logs/wrath-editmode.xml" \
  -logFile "$PWD/Logs/wrath-editmode.log"
```

批处理场景生成方法是 `-executeMethod Poker.SingleBattle.Editor.WrathBossSetup.Open`，首次优先用菜单查看保存／导入提示。测试命令不要在同一项目已有 Editor 实例占用时并发运行。旧 `Library/WrathValidation/SharedTests.targets` 含旧电脑绝对路径，不可直接沿用；Unity Test Runner 无需它。

## 6. 规则与 Web 的移机复现

### 规则测试

以下从主仓库根目录运行，使用新电脑可用的 .NET 10 SDK：

若 SDK 仅由 Web 启动器装在子仓库 `.runtime/dotnet`、尚未加入 PATH，将下方 `dotnet` 替换为该可执行文件的完整路径；不必修改全局环境。

```sh
dotnet test External/BattleToolkit/Tests/BattleToolkit.Tests.csproj \
  --configuration Release --artifacts-path Library/WrathValidation/NewMachineArtifacts \
  --filter 'FullyQualifiedName~WrathBossRulesTests|FullyQualifiedName~CombatWrathImmunityTests|FullyQualifiedName~WrathAdventureHealthTests|FullyQualifiedName~WrathAdventureReplayTests|FullyQualifiedName~WrathHandEffectRestrictionsTests' \
  --logger 'trx;LogFileName=wrath-new-machine.trx' --results-directory Logs

dotnet test External/BattleToolkit/Tests/BattleToolkit.Tests.csproj \
  --configuration Release --artifacts-path Library/WrathValidation/NewMachineArtifacts \
  --logger 'trx;LogFileName=rules-new-machine-full.trx' --results-directory Logs
```

第一条选择新规则／免伤／成长／存档／手部限制用例；不要只筛 `WrathThousandHandsTests`，那是读取旧 `.20.xml` 的历史千手测试。当前交付配置是 `.21.xml`，历史内容和测试仍保留。

### Web 启动

进入 `External/BattleToolkit`，Windows 双击 `start.cmd`，macOS 双击 `start.command`。启动器可在本机准备 .NET 10 并构建；日常试玩不需要 Python、Node 或 Unity。首次下载／包恢复需要网络。

```sh
# macOS，在 External/BattleToolkit 内
bash start.command --check
bash start.command --port 18768
```

```bat
rem Windows，在 External\BattleToolkit 内
start.cmd --check
start.cmd --port 18768
```

打开启动器输出的 localhost 地址，载入标准内容，选择 `standard-wrath-thousand-hands` 开新局。C# 修改后重新构建／启动；XML 修改后重新载入配置并开新局。可不指定端口，默认 18767。

### Web 回归

测试额外需要 Python 3、Node/npm、Google Chrome；仓库根锁定 Playwright 1.62.1。仍在 `External/BattleToolkit` 内运行：

```sh
npm ci
unset EDITOR_URL
python3 -m unittest discover -s Tools/BattleContentEditor/tests -p 'test_wrath_projection.py' -v
PLAYWRIGHT_CHANNEL=chrome python3 Tools/BattleContentEditor/tests/run_browser.py
```

PowerShell 用 `Remove-Item Env:EDITOR_URL -ErrorAction SilentlyContinue` 清除该变量，`$env:PLAYWRIGHT_CHANNEL = "chrome"`，并将 Python 调用换成 `py -3`。这些入口自动构建隔离临时宿主并顺序回归；不要指向正在试玩的服务，也不要让多个长套件并发共用一个服务。

启动器 SDK 覆盖变量是 `BATTLE_TOOLKIT_DOTNET`，Python 测试覆盖变量是 `POKER_DOTNET`；默认无需设置。需要覆盖时使用新电脑 SDK 的绝对路径，不能照抄旧 `/tmp` 路径。

## 7. Unity 实际场景验收清单

下列项目前均未在 Unity 实机勾选。先用占位资源验证功能，再换正式资源；记录实际轮次与权威状态，不让动画自行递减计时。

- [ ] 导入／编译无新增错误；独立场景生成成功，六个 Actor 对应正确。本体血条与五手状态分开，原普通战／精英场景仍可打开和运行。
- [ ] 本体、四饮血手、束缚手名称及选中态可辨识；单体伤害落到所选目标，三条等群攻无需单选，死亡手不能成为目标。
- [ ] 初始本体 1500、各手 1、D=33；断一手后 D=28，复活恢复贡献。手不能获得加攻或护盾；纯控制不直接杀手。
- [ ] 八格常规意图为两遍“普攻、普攻、防御、蓄力”；蓄力成功后加 10，眩晕跳过不增加或推进。大招不混入普通八格。
- [ ] 龟缩只保护本体的两段正伤害，状态仍能附加；顺子多段、毒、反伤、手雷与皇家强制击杀保持既有互动。
- [ ] 第 R 轮玩家阶段断手，原位置立即显示红色 8；该轮敌阶段冒一次“8个回合后再生”，之后各阶段 7→1；R+8 复活移除数字，不冒 0。不同时点断手独立计时，同轮到期可全部复活。若在敌阶段入口之后因毒／反伤等死亡，仍立即显示权威剩余数字，但不会补播已过去入口的冒字；下一敌阶段入口从 7 开始，再生期限仍为 R+8。
- [ ] 入场指定红字每场一次，“规则说明”可回看；重新开局再次提示。重复投影／刷新／预览不能重复冒字，结束战斗清理提示。
- [ ] 五手齐全时第 5 轮预警、第 6 轮放大；断手即时撤销警告。本体眩晕／被杀、玩家替死等中断均及时停止后续动作。
- [ ] 无控制且全手存活时，第 6 轮普通攻击 43 后追加 129；第 12 轮先蓄力再追加 189。若用默认束缚配置跳过玩家阶段，要核对同一命令的中间预警画面，不能只看最终快照。
- [ ] 饮血治疗目标、毒伤先后、束缚全场 5 轮周期、控制免疫阻挡及复活当轮参与技能均正确。
- [ ] 本体先死且手存活仍胜利，同次牌的附带治疗完成；替死立即终止敌阶段；第 20 轮完整结算后失败且不触发替死。
- [ ] 世界位置红字随对应手、镜头定位正确；中文字体不缺字；720p 及目标分辨率无明显遮挡、越界或按钮拦截；普通 HUD 不出现 Boss 专属提示。
- [ ] 补充暴怒专用 PlayMode 自动化，至少覆盖生成／开局、指定手死亡→提示→再生、预警取消、重开清理；再运行通用／精英播放回归。

## 8. 已有测试证据与已知失败

| 验证 | 已运行结果与边界 |
|---|---|
| 新核心规则、免伤、成长、存档 | 第一阶段 44/44；不是当前所有测试的总数 |
| 后补手臂增益／护盾限制 | 15/15，先验证旧实现失败再修复 |
| 最新规则全量 | 1667 项，1654 通过、13 失败；失败名称集合与干净子仓库基线相同，无新增失败 |
| 暴怒共享展示／提示 | 最新定向 18/18，纯 C#；包括真实 BattleSession→Timeline→投影→提示的时序测试 |
| 共享显示历史全量 | 第一阶段 28 项中 27 通过、1 个既有精英断言失败；不能把后续 18 个定向通过说成全显示套件全绿 |
| Web | HTTP 8/8、新交互 8 组、原暴怒 DOM 5 组、普通试玩 15/15、冒险 17/17、悬浮与移动布局定向 3/3；宿主构建 0 警告、0 错误 |
| Unity Editor／PlayMode | 未运行；场景／HUD 的 6 项 Editor 测试仅已编写，共享 18 项仅在 .NET 运行 |

13 项规则失败的完整名称和原始断言见附件 `test-results-summary.json`，也在 `Tools/Validation/2026-10-22-wrath-boss.md` 列出。不能通过全局开启 checked 算术、放宽断言或改普通玩法来伪造回归通过；新电脑应先比较失败名称及原因再判断是否新增。

共享显示既有失败为 `ConfiguredElitePreviewShowsGrowthAndDamageThenBleedingDecay`：旧断言攻击 90／伤害 45，当前工作区精英配置为攻击 8／伤害 4；它是独立于上述 13 项规则失败的已有问题。此前没有修改此断言或精英数值来掩盖失败。

主仓库全范围 `git diff --check` 另有既有字体资源尾随空格提示（`Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset`）；本次脚本／文档范围和子仓库检查通过。不要为交接去格式化大型字体资源。

原始 TRX／日志主要在旧机器 `Library/WrathValidation`，不会自动随 Git 迁移。附件已经提取关键结果；如需完整原始记录可另外带走该目录，**不要拿旧 TRX 充当新机器运行证据**。

## 9. 后续资源接入与完成标准

| 后续事项 | 接入约定 |
|---|---|
| Unity 正式图标 | `SingleBattleHud` 的 `wrathAttackIcon`、`wrathDefenseIcon`、`wrathChargeIcon` Sprite 槽，或 `ConfigureWrathIcons`。留空继续使用矢量占位。 |
| Web 正式图标 | `wrath-presentation.js` 的 `wrathIntentIcon`，目前 action ID 对应 `sword/shield/spark`；不要按数值猜图标。 |
| Unity 正式美术／预警 | 当前五手复用缩小模型，预警是 HUD 红底、红字和 Outline；Boss 材质红光／专用立绘尚未接入。 |
| Web 正式美术／预警 | 目前通用 `grunt.png` 与 CSS 红光。`playtest.js` 的 `artFor` 可按敌人定义 ID 映射专用资源；Unity 资源不会自动同步到 Web。 |
| Web 资源限制 | 当前 `web/assets` 顶层图片支持 PNG/WebP/JPG/SVG，单文件上限 512 KiB。若正式素材超限／改路径，需一起调整 NativeServer、兼容服务器和资源预算测试。 |
| 动画 | 补断手、再生、蓄力、大招演出；绑定权威事件／回放，不在客户端追加伤害、计算冷却或重抽意图。 |
| 平衡验证 | 记录普通牌组、手雷、葫芦／控制、致命一击、高倍转盘的战斗轮数、生命与清手／硬抗选择。暂无足够实战数据证明数值平衡，不先削弱普通牌／道具。 |
| 冒险节点 | 暂不更换暴食；需要新的产品决定后再接入暴怒节点。当前独立战斗与冒险兼容测试不代表正式地图已经换 Boss。 |

完成标准：Unity 编译和真实场景验收有记录；新增 Boss 用例及受影响普通／精英回归无新增失败；素材已到位的项目完成替换，未到位的保留占位并明确列出；Web 与 Unity 对同一状态的 D、倒计时、预警一致；验证文档写明机器环境、命令和实际结果。

## 10. 继续阅读顺序

1. 本交接文档及附件校验结果。
2. `docs/superpowers/specs/2026-10-22-wrath-boss-design.md`：已锁定的完整规格。
3. `Tools/Validation/2026-10-22-wrath-prompts.md`：最新文字／占位／选敌／手部限制的验证记录。
4. `Tools/Validation/2026-10-22-wrath-boss.md`：核心第一阶段证据、历史失败和四步意图纠正。
5. `docs/superpowers/plans/2026-10-22-wrath-boss.md` 及 `2026-10-22-wrath-boss-prompts.md`：已执行实现计划。计划勾选不代表 Unity 实机场景通过，以验证记录为准。

本次交接只新增文档、校验附件及验证摘要，没有修改游戏规则、配置或场景，也没有创建提交／PR、推送或替换冒险地图。
