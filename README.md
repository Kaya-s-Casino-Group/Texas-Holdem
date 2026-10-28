# Palamedes

Unity 表现层重构基线，基于 `legacy-3d-scenes` 建立。当前保留自研渲染管线、Card Instancing、全部 Shader、基础会话桥接与通用表现调度，并已恢复世界空间手牌动效和交互。完整角色、正式 HUD、镜头与终局演出尚未接入当前战斗场景。

## 保留模块

- 内嵌 URP 14.0.12 与渲染扩展：`Packages/com.unity.render-pipelines.universal`、`Assets/Scripts/Infrastructure/Rendering`。
- 卡牌实例化与美术工具：`Assets/Scripts/Presentation/CardArt`、`Assets/GameAssets/CardArt`，包括共享网格、材质、图集及原始牌面。
- Shader：保留原路径、Include 和 `.meta`；部分环境目录现在只保存 Shader。
- 会话接入：`Assets/Scripts/Gameplay/SingleBattle/Hosting`、`Presentation/Shared`。
- 通用表现：`Assets/Scripts/Presentation/BattlePresentation`，包含显示快照、时间线、动作和交互协议。

现行表现层接入、当前完成范围及历史资料索引统一见 [CardPresentation README](Assets/Scripts/Presentation/CardPresentation/README.md)。清理时的范围和依赖快照见 [历史保留清单](Tools/UnityPresentationRetention/README.md)。

## 规则边界

C# 规则、玩法、结算和共享 XML 位于 `External/BattleToolkit` 子仓库，通过 `com.kaya.battle-engine` 本地 UPM 包引用。本次清理不修改其代码、工作区提交或主仓库记录的 gitlink。

当前本地规则工作区可能比旧分支记录的 gitlink 新。已有工作区不要为了打开本分支自动执行子模块更新；首次克隆时才按仓库记录初始化，并按项目协作流程选择接入更新的规则版本。

Unity 接入层提交命令、查询规则并投影结果；动画、输入与显示时间线不拥有玩法结算。

## 开发与验证

使用 Unity 2022.3.62f3。当前手牌接入场景为 `Assets/Scene/InTest/SingleBattle.unity`，具体组件、配方和验证范围见上述现行入口。

`Assets/GameAssets/SingleBattle/Hosting/BattleSceneHostMinimal.prefab` 保留为无角色、无卡牌视图的会话接入入口。卡牌与渲染模块的编辑器工具和测试仍保留，卡牌验证场景可由对应工具生成。

旧完整 3D 工程可在 `legacy-3d-scenes` 分支查看。`Tools` 下其他历史说明记录旧实现，不代表 Palamedes 当前保留了对应场景或前端。
