# dev / 旧 3D 场景迁移记录

更新：2026-10-20。本记录随迁移改动一并提交到 `dev`。主工程的远端分支为 `origin/dev` 和 `origin/legacy-3d-scenes`。迁移提交以 `23f9cf20afb7ed9a695b997d191ab5cdfa7bc51e` 为父提交，保留这次已同步的规则版本。

## 归档与本地规则

- 旧工程完整快照：`legacy-3d-scenes`，提交 `72a1553da8a16815a4eaf63cc4208987853af5bc`。包含迁移前已修改和未跟踪的工程资产，保留原路径和 GUID。
- 该快照内的 BattleToolkit gitlink 指向子仓库快照 `7a0036e1f58b69a6257d1903e9f44056905d7aaa`，对应子仓库分支 `codex/pre-unity-migration-20261020`。
- 当前 `External/BattleToolkit` 工作区是运行规则的权威来源。未执行 reset、checkout、同步远端或改写规则实现。迁移期间发现子仓库仍有后续本地更新，均继续保留；归档快照只代表迁移开始时的状态。
- 最终落库时规则更新已独立提交到 `93478b756f65a8f59c814352736b3eee6ed0bd0a`，子仓库工作区干净；`dev` 保持该 gitlink，不回退到归档中的规则快照。
- `Art/`、`ArtSource/`、`Blender/` 等本来被 Git 忽略的源文件仍留在磁盘，不属于 Git 快照，不因本次精简删除。

## 当前开发入口

只保存 `Assets/Scenes/BattleDemo.unity`，Build Settings 也只启用该场景。从 `SingleBattleDemoNew` 迁入 Battle、Card Table 和 UI 的完整绑定，包括 BattleSceneHost、SingleBattleSceneAdapter、ScenePresentationDirector、已配置的时间线、手牌区、选牌候选、摸牌堆、牌库总览、按钮、悬停提示和音效。

迁移时两个胶囊体按原名称分别挂接 Player / Enemy；旧角色、怪物模型已去除。随后按用户提供的新布局图，将胶囊替换为透明角色面片，继续保留 Actor 与表现绑定。保留 BattleDemo 当前使用的背景卡片、纹理和地面依赖。

场景引用本地包 `Content/StandardSingleBattle.xml` 的 `standard-single-battle` 编成。Unity 只做输入、宿主、快照显示与表现桥接，规则仍由子仓库处理。通用卡牌交互、Hosting、2D/3D 表现桥接、VAT/角色动画桥接和相关测试继续保留。

## UI

功能参考 [飞书对局 UI 文档](https://moonton.feishu.cn/docx/EQordOsHeobymRxxAubc2l4MnTb)，排布按用户后续附图：左上回合/能量和玩家血条，左侧玩家面片，中间敌人面片，右上斜向敌人血条与意图，右侧状态/遗物竖栏。底部横条容纳手牌、候选牌型、牌库统计及入口，右下操作区按摸牌/出牌阶段切换。

沿用原有牌面、字体和图标；角色面片来源、生成提示词与使用说明见 `Tools/BattleBillboards/README.md`。牌区与角色面片按主相机视口和安全区调整位置与大小，HUD 使用 Overlay Canvas。

## 渲染与卡牌

- 官方 URP 14.0.12（Package Manager 在该 Unity 版本解析为 builtin），删除嵌入式定制包。
- Graphics / Quality 统一使用 BattleURP，只有一个标准 BattleForwardRenderer，Forward，无 Renderer Features，Render Scale 1。
- 删除自研 Simple2DRenderer、低分辨率和各后处理 Feature；相机桥接使用官方 URP API。
- CardDeferred 重命名为 CardUnlit，保留原 `.meta` GUID。主 Shader 只有 ForwardUnlit 和 DepthOnly，不再有法线、光照、阴影、GBuffer 或 DepthNormals。
- 镭射（UV/时间驱动）、火漆颜色/旋转/自发光、红叉覆盖、正背面图集、厚度边缘色、透明裁切、每实例颜色和 GPU instancing 保留。Canvas 卡牌也不采样法线或灯光。
- 保留 Card、FourBeastsCard、ClassicCard、CardUIItem 和全部牌区/UI Prefab；保留卡牌 Mesh、图集、Profile、原始牌面、FoilPattern / WaxSeal_Paw / RedCross。蜡封法线图已删除。

## 删除范围

旧场景及其专用角色/怪物/环境模型、材质、渲染配置和专用导入验证工具从当前工作区移除。第二批清理 1,072 个文件，约 199.18 MiB；包含场景迁移前已存档的文件。嵌入式 URP 与前序专用 Feature 清理另计。

精确清单见 `Tools/BattleMigrationRemovedFiles.txt`。卡牌与 BattleDemo 依赖不以目录名称决定删除：保留资产递归 GUID 检查结果为空，无丢失引用。

提交前逐项核对：本次删除的 3,965 个已跟踪文件以及上述清理清单的 1,072 项均存在于 `legacy-3d-scenes`，没有未归档的删除项。这两组清单有重叠，不能相加作为删除总数。

## 验证

验证报告在 `Library/BattleDemoMigration/`：

- 本地规则层：1,366 项通过，见 `rules.log`。
- Unity EditMode：533 项通过，0 失败、0 跳过，见 `editmode.xml`。
- Unity PlayMode：8 项通过，0 失败、0 跳过，见 `playmode.xml`。覆盖 BattleDemo 时间线、选牌/出牌/弃牌、伤害与相机表现、取消/重启、宿主禁用恢复、多个独立宿主及 2D 表现桥接。
- GPU 卡牌效果：通过，见 `card-effects.json`。验证镭射、火漆颜色/旋转/自发光、红叉叠加、正背面隔离以及灯光开关不影响 Unlit 输出；截图在 `ArtSource/Cards/RenderingValidation/Seal/`。
- 保留资产的序列化 GUID 检查：无丢失引用，见 `guid-audit.json`；修改文件空白检查通过。

测试生成的临时场景已清理，磁盘中只剩 BattleDemo 场景。完成时 Unity Console 无错误，编辑器回到已保存的 BattleDemo 编辑状态。

运行回归同时修复两处表现边界：HUD 在牌区停止时不再捕获选择；共享表现调度器的即时批次标记为全部消费，防止后续 Update 再次应用相同事件。相应恢复与即时播放回归已纳入检查。

Unity 在从嵌入式管线切换到官方包后曾崩溃，现已重启并重新绑定 Renderer；后续编译与运行检查使用重启后的工程。

## 最终布局与分支检查

角色面片和参考图 UI 完成后再次通过 EditMode 533 项、PlayMode 8 项，报告在 `Library/BattleDemoLayout/`。实际点击跳过摸牌、选牌和出牌，能量与敌人血量变化正常。Unity Test Runner 1.3.3 的退出回调曾产生编辑器内部空引用；重新进入游戏后无运行错误。

最终分支检查摘要保存在 [2026-10-20-branch-migration.json](Validation/2026-10-20-branch-migration.json)，包含归档提交、规则提交、资源引用检查和测试结果。当前 Assets 与 ProjectSettings 的序列化 GUID 无缺失，Assets 内无重复 GUID。

## 使用旧场景

旧 3D 工程保留在 `legacy-3d-scenes`，配套规则快照保留在子仓库分支 `codex/pre-unity-migration-20261020`。需要打开历史工程时，使用单独检出目录，并初始化到该归档提交记录的子模块版本。不要把整个旧分支合并回 `dev`；复用单个资产时连同 `.meta` 和依赖一起迁入。

归档发布顺序为：先推送 BattleToolkit 的 `codex/pre-unity-migration-20261020` 快照分支，再推送主工程的 `legacy-3d-scenes`，保证归档 gitlink 可被其他机器获取。主工程分支位于 `LifeSugar/Texas-Holdem`，规则快照位于 `Kaya-s-Casino-Group/BattleToolkit`。在单独检出目录获取 `origin/legacy-3d-scenes` 后，执行 `git submodule update --init --recursive` 即可恢复配套规则版本。
