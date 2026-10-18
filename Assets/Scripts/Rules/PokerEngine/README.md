# 规则引擎已迁移到 BattleToolkit

纯 C# 引擎、纯规则测试、共享 XML、网页配置与试玩工具统一维护在 [External/BattleToolkit](../../../../External/BattleToolkit/README.md) 子模块。Unity 通过 `com.kaya.battle-engine` 本地 UPM 包消费，程序集名称与已有资源 GUID 保留。

本目录仅保留 Unity 集成测试、打包边界检查和测试面板。完整接口说明见 [引擎文档](../../../../External/BattleToolkit/Engine/README.md)。

首次克隆主工程后运行 `git submodule update --init --recursive`。策划只需克隆 BattleToolkit 小仓库，双击其中 `start.cmd`（Windows）或 `start.command`（macOS）。
