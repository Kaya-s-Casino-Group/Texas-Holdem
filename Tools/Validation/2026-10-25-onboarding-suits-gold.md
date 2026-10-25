# 新手教学、花色辨识与金币来源验收（2026-10-25）

## 实现范围

- 首次进入第一场战斗、发牌和开场表现结束后展示教学，可跳过或开始战斗。确认后在本机记住，不因刷新、继续旅程或重建界面反复弹出；主菜单「玩法说明」可回看。
- 说明出牌/弃牌行动点、弃牌补牌、弃牌获得格挡、默认下次自己回合开始清空格挡，以及保盾例外。说明从当前内容和实际规则读取，区分可见行动点与特殊能量限制。
- 教学给出一对的单体伤害、三条的全体伤害示例。牌型指南在公式前增加当前牌型核心效果、伤害对象和状态效果说明，保留原有组合、倍率及结算公式。
- 教学打开时阻止战斗输入和嵌套牌型指南，键盘方向选择限于两个教学按钮；关闭后恢复原来的输入和焦点，不提交战斗行动。
- 沿用 FourBeasts 原图中的花色形状，在两个对角放大并描边，覆盖手牌、牌型指南、牌库及商店改牌列表。黑桃和梅花保留各自形状；背面、翻面、飞牌暂时隐藏及对象复用同步处理。
- 商店花色叠层跟随实际等比容纳的纸牌边界，不占用缩略图留白，不改变格子或按钮位置。
- 金币反馈列明战斗奖励、宝箱奖励、休息抽奖的收入/损失、出售道具，以及存折等道具离开地点的利息；显示实际变化和余额。反馈在成功保存命令后生成，刷新、失败命令、菜单恢复和读档均不重复发钱或重播旧收入。

没有修改 BattleToolkit、战斗内容数值、计分、弃牌收益、格挡到期规则、Boss/精英技能、经济规则、现有原画、图集或场景布局。

## 验证环境与编译

- Unity **2022.3.62f3**，macOS，Metal，实际渲染分辨率 **1280×720**。
- 使用已有隔离副本 `/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`，不关闭用户主项目或使用用户存档。
- 主仓库全部 **478** 个 C# 文件、BattleToolkit Engine 全部 **287** 个 C# 文件与验证副本一致；内容、场景、牌图、包配置另有一致性记录。最终编译通过，无 C# 或 Shader 编译错误。
- EditMode 使用 Unity 测试运行器；以下 PlayMode 使用带图形界面的 Unity Editor 实际渲染，未使用 `-batchmode` / `-nographics` 代替界面验收。

## 回归结果

| 批次 | 实际结果 | 范围 |
| --- | --- | --- |
| `editmode-final.xml` | **184/184 通过，0 跳过** | 教学 12、金币反馈 8、核心效果说明 4、花色 18，以及既有牌型指南、卡牌渲染/状态/飞牌、资源输入、道具、商店、布局、Boss 免伤和选择预览回归 |
| `playmode-visual-green.xml` | **4/4 通过，0 跳过** | 教学与弃牌、帮助/手牌/牌型指南、金币全路径与商店、GPU 花色像素 |
| `playmode-shop-fit-green.xml` | **2/2 通过，0 跳过** | 商店叠层对齐后的金币/商店完整路径、GPU 花色像素复验 |
| `playmode-final.xml` 中既有兼容性用例 | **5/5 通过** | 同花/同花顺说明、旧规则指南、目标变化后的指南缓存、普通出牌/道具/飞牌/清场、Boss 真实顺子消耗两次免伤及目标切换 |

合计 **9 个不同的渲染 PlayMode 用例通过**，按影响范围分批运行，没有把重复运行累计成新的用例。`playmode-final.xml` 当时完整批次为 7 通过、1 失败：失败的是新商店路径缺少 CanvasRenderer，已修复并由上述最后两批实际通过覆盖。

具体实际验收：

- 教学打开时结束回合被阻止，战斗修订号不变；通过真实 UI 射线点击「跳过教学」后恢复操作。
- 实际弃掉 K/Q/J，获得 **36 格挡**并补回三张牌；结束回合进入下次玩家回合，默认剩余格挡清零。
- 主菜单说明、五张人头牌手牌、顺子指南、商店 52 张改牌缩略图均实际渲染并截图。
- 实际出牌获胜获得战斗奖励 **+50**、进入宝箱获得 **+100**；持有存折离开地点获得 **+24** 利息、余额 **264**。来源、实际余额、刷新/继续/恢复时不重复入账均通过。
- GPU 读取实际截图，检查黑色牌墨、浅色描边、轮廓周围透明区域以及黑桃/梅花不同的像素形状；商店真实滚动区域另检查无青色占位块，叠层比例与纸牌一致。

过程中发现并处理了教学与指南的输入门冲突、教学焦点、发牌/飞牌隐藏时残留花色、商店独立绘制路径、CanvasRenderer 依赖和缩略图留白对齐。测试夹具的 EventSystem 注册及当前 Unity NUnit 不支持的断言接口亦已修正，最终结果以上述通过记录为准。

首次商店截图的青色方块属于 Unity Editor 异步编译新着色器变体时的临时加载画面。验收现在先触发渲染、等待编译完成，再取完整帧检查；没有修改项目的全局着色器编译设置。[Unity 官方说明](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorSettings-asyncShaderCompilation.html)

## 截图和原始证据

证据目录：`/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/`。

- [首次战斗教学](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-first-battle-teaching.png)
- [人头牌与花色](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-face-suits.png)
- [牌型效果指南](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-hand-guide.png)
- [商店牌面花色与边界](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-shop-deck-suits.png)
- [战斗奖励来源](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-battle-gold.png)
- [宝箱奖励来源](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-chest-gold.png)
- [存折利息来源](/Users/mt/code-yang/Texas-Holdem/Library/OnboardingClarity-20261025/screenshots/clarity-location-interest.png)

同目录保留 XML、Unity 日志、`test-evidence.json`、`source-parity.json`、`validation-environment-parity.json`、`preservation-before.json` 和 `preservation-after.json`。

## 工作区保留与边界

- 主仓库开始时 **80 个**未提交文件全部保留，其中 **77 个逐字节未变**；另外 3 个既有脏文件仅叠加本次必要修改：AdventureScreen.cs、AdventureScreen.Pages.cs、SingleChapterAdventureSceneTests.cs。原文件备份和补丁保留在证据目录。
- 两个字体缓存与本次开始时完全相同；没有借此清理其既有格式/缓存改动。
- 主仓库与子仓库 HEAD 和暂存区均未改变；BattleToolkit 仍保持本次开始时的干净状态。未提交、未推送。
- 本次功能文件 `git diff --check` 通过。整个工作区的检查仍会报告原有 Noto 字体缓存的 3 处尾随空白，原缓存未改动。
- 没有执行全库全平台测试、独立 Player 构建、移动端触控/手柄完整验收或 20 回合耐久测试；这些不在本批信息提示改动的验证范围内。
