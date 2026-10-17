# 2026-10-17 脚本目录迁移验证

## 范围

自研代码集中到 `Assets/Scripts`，模块资源集中到 `Assets/GameAssets`。目录映射见脚本目录 README；程序集名、命名空间、类名及原资产 GUID 保留。场景、管线设置和玩法配置内容不因本次路径迁移改变。

## 已完成检查

- Unity 2022.3.62f3 脚本编译通过，0 警告。
- 706 个迁移项的 `.meta` GUID 与迁移前逐一一致。
- 自研 C# 全部位于 Scripts；第三方 Plugins 保持原位。
- 模块 Markdown 相对链接检查通过，无失效目标。
- 工具加载路径、Shader include、离线 Bash / PowerShell 测试入口已同步。
- Unity EditMode：PackagingTests 10/10、Battle2DValidation.Tests 11/11、BattleActorTests 36/36，CardValidationUnlitTests 5/5、CardRenderingTests 4/4、CardMatchHostTests 7/7，共 73 项通过。
- 离线 PokerEngine 全量：436 项通过、1 项失败。使用与 Unity 相同的 NUnit 测试源；Mono 编译时将原有 CS0219 从错误降为警告，未修改规则代码。

## 已有问题与验证限制

- `CardEffectsTests.IneffectiveCardStillFormsPairButSkipsAllPerCardScoreContributions`：预期 72，实际 52。该测试及 Core / Scoring / Abilities 相关源文件与迁移前逐字一致，未在目录整理中调整规则或测试期望。
- 原有 `TeamBattleEngine` 溢出检测局部变量触发 Mono CS0219；默认离线脚本的 warn-as-error 会因此失败。上述全量执行仅临时降级此警告，仓库脚本的严格级别保持不变。
- 较大的 Unity 测试批次请求超时，未计入通过数量；Shader 相关专项在导入稳定后重试通过。未声称全量 Unity 测试或最终设备画面验收通过。

## 分批提交

1. 纯规则：BattleSession / BattleHost / Playback、公共表现协议和个人牌型等级。
2. 场景接入：共用 Hosting、配置、卡牌命令与表现模块整理。
3. 2D 验证：简单表现、Classic 资源、Unlit Shader 与固定 Forward2D Renderer。
4. 工具：中文菜单和模块说明。
5. 资源：已有场景、光照、音频、材质和项目设置变更。
6. 路径：Scripts / GameAssets 迁移、路径修复和目录规范文档。

AudioReference 原始参考素材、生成缓存和本地输出不纳入版本控制；已导入的 Assets/Audio 资源随资源批次提交。
