不要压力老人

项目目录与模块职责见 [脚本目录说明](Assets/Scripts/README.md)，模块资源见 [资源目录说明](Assets/GameAssets/README.md)，分批变更见 [CHANGELOG](CHANGELOG.md)。


规则与策划工具现独立维护于 [BattleToolkit](https://github.com/Kaya-s-Casino-Group/BattleToolkit)（私有仓库）。策划只拉该小仓库，Windows 双击 `start.cmd`、macOS 双击 `start.command`；启动器自动准备 .NET 环境并打开配置试玩网页。

首次拉取或切换主工程版本后，先更新子模块，再打开 Unity：

```sh
git submodule update --init --recursive
```

Unity 通过 `External/BattleToolkit/Engine` 本地包引用规则与共享 XML。当前场景内容在 `External/BattleToolkit/Engine/Content/SingleBattleDemoNew.xml`。主工程固定子模块提交，更新引擎或 XML 时先提交/推送小仓库，再提交主工程中的子模块版本；旧 SO 和表现层仍由主工程维护。更多说明见 [独立工具文档](External/BattleToolkit/README.md)。

日常协作见 [两个仓库的协作流程](External/BattleToolkit/COLLABORATION.md)：策划在小仓库交付 XML；纯规则、工具改动也在小仓库提交；Unity 集成方选择通过 CI 的 SHA，验证后在本仓库提交子模块版本与必要的场景适配。拉取主工程后执行 `git submodule update --init --recursive`，即可同步主工程采用的规则与配置。
