不要压力老人

项目目录与模块职责见 [脚本目录说明](Assets/Scripts/README.md)，模块资源见 [资源目录说明](Assets/GameAssets/README.md)，分批变更见 [CHANGELOG](CHANGELOG.md)。


规则与策划工具现独立维护于 [BattleToolkit](https://github.com/Kaya-s-Casino-Group/BattleToolkit)（私有仓库）。策划只拉该小仓库，Windows 双击 `start.cmd`、macOS 双击 `start.command`；启动器自动准备 .NET 环境并打开配置试玩网页。

首次拉取或切换主工程版本后，先更新子模块，再打开 Unity：

```sh
git submodule update --init --recursive
```

Unity 通过 `External/BattleToolkit/Engine` 本地包引用规则与共享 XML。当前场景内容在 `External/BattleToolkit/Engine/Content/StandardSingleBattle.xml`。主工程固定子模块提交，更新引擎或 XML 时先提交/推送小仓库，再提交主工程中的子模块版本；旧 SO 和表现层仍由主工程维护。更多说明见 [独立工具文档](External/BattleToolkit/README.md)。

日常协作见 [两个仓库的协作流程](External/BattleToolkit/COLLABORATION.md)：策划在小仓库交付 XML；纯规则、工具改动也在小仓库提交；Unity 集成方选择通过 CI 的 SHA，验证后在本仓库提交子模块版本与必要的场景适配。拉取主工程后执行 `git submodule update --init --recursive`，即可同步主工程采用的规则与配置。

主动接入小仓库新版本时，可双击根目录 `sync-battle-toolkit.cmd`（Windows）或 `sync-battle-toolkit.command`（macOS）：检查两边工作区、选择四项 CI 均通过的版本并再次确认后更新；不覆盖本地改动，不自动提交。用法和前置条件见 [一键同步说明](Tools/BattleToolkitSync/README.md)。

开发入口为 `Assets/Scenes/BattleDemo.unity`，使用官方 URP 14 的标准 Forward Renderer。旧 3D 场景与定制管线已保存在本地 `legacy-3d-scenes` 分支，范围、恢复点和验证结果见 [迁移记录](Tools/BranchMigration-2026-10-20.md)。有尚未提交的本地规则更新时，以子仓库工作区为准，不执行子模块覆盖更新。
