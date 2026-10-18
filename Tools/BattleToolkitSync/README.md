# Unity 一键同步 BattleToolkit

主工程根目录双击：

- Windows：`sync-battle-toolkit.cmd`
- macOS：`sync-battle-toolkit.command`

先安装 Git 和 GitHub CLI（`gh`），执行一次 `gh auth login`，使用有 BattleToolkit 私有仓库读取权限的账号。Windows 可用 `winget install --id GitHub.cli`；macOS 已有 Homebrew 时可用 `brew install gh`。脚本不会自动登录、修改系统执行策略或保存令牌。

已有主工程应先初始化子模块：`git submodule update --init --recursive`。脚本不在未知/非空目录里自动初始化仓库。

## 使用步骤

1. 先保存 Unity 中的编辑，退出 Play，并自行处理两个仓库的本地改动。脚本检查的是磁盘上的 Git 状态，不能检测 Unity 或文本编辑器尚未保存的内存内容。
2. 双击入口。主工程或小仓库存在未提交、已暂存、未跟踪内容，或已有未提交的子模块版本变化时，会显示原因并停止。最多列出 30 项，完整清单用 `git status --short` 查看。被 Git 正常忽略的运行缓存不阻止检查。
3. 脚本读取小仓库 main 的 CI，列出最多 10 个通过版本：提交号、日期、说明和 Actions 链接。输入序号选择，回车/0 取消。
4. 核对原版本与目标版本，关闭正在运行的配置试玩工具，输入 `UPDATE` 确认。脚本会再次检查 CI 与本地状态，确认仍有效才切换。
5. 更新后刷新 Unity、重新开局并验证。验证通过后，由你自行暂存并提交 `External/BattleToolkit` 的版本变化及必要的 Unity 配套修改。

**不会自动暂存、提交、推送、stash、reset、clean 或强制覆盖。** 更新后子模块处于 detached HEAD，主工程出现一项未暂存的子模块版本变化，这是预期结果。再次同步前需先处理该变化；程序要修改小仓库内容时，先在小仓库建立工作分支。

## 什么版本会被列出

只检查 `Kaya-s-Casino-Group/BattleToolkit` 的 `standalone.yml` 工作流、main 分支的 push 运行，读取最近 100 条记录，并确认提交属于已获取的 main 历史。浅克隆或超出最近记录范围的老版本可能不出现在列表中。

同一个提交只认最新运行，旧的绿色记录不能盖过新的失败或进行中记录。总结果必须成功，且该次 attempt 的以下四个 job 必须全部完成并成功（跳过也不接受）：

- `rules-and-host (windows-latest)`
- `rules-and-host (macos-latest)`
- `first-launch (windows-latest)`
- `first-launch (macos-latest)`

确认后重新读取所选提交的最新运行和 attempt；期间重新运行 CI、main 历史变化、出现新的本地修改或本地 HEAD 变化，都会停止。工作流名称/job 名称变更时，需要同步修改两个脚本和测试，脚本不会自动放宽条件。

同步包含所选提交的全部引擎、XML 和工具。CI 通过不替代 Unity 场景验收。

## 保护边界

- 检查仓库路径、`.gitmodules` 与 origin 声明的仓库地址；不接收任意仓库/提交参数。保留 Git 用户自身的 SSH/URL rewrite 配置。
- 遇到合并、变基、拣选、bisect、index.lock 或 assume-unchanged/skip-worktree 标记时停止，避免把被隐藏的改动当作干净工作区。
- 更新使用普通 detached 切换及 `--no-overwrite-ignore`，被忽略文件若会被目标提交覆盖，也拒绝切换。
- 查询候选会 fetch Git 对象、更新 origin/main 和 FETCH_HEAD；取消不会撤销这些元数据更新，但不会切换工作文件或提交。
- 不在脚本运行过程中同时编辑文件或执行其他 Git 操作；脚本重复检查并依赖 Git 的冲突保护，但不能锁住 Unity 或其他编辑器。

## 只检查 / 只看候选

```sh
bash sync-battle-toolkit.command --check
bash sync-battle-toolkit.command --list
```

Windows 对应 `sync-battle-toolkit.cmd --check` / `--list`。`--check` 只读本地，不要求 gh 登录；`--list` 查询远端并列出候选，不切换工作区。没有跳过检查或自动确认参数。

## 测试

```sh
python3 -m unittest discover -s Tools/BattleToolkitSync/tests -v
```

开发测试使用 Python 3 与真实临时 Git 仓库；GitHub 数据由本地夹具提供，不更新当前工程。Bash 测试用 jq 执行脚本原有的查询表达式；PowerShell 测试使用 `powershell`、`pwsh` 或 `SYNC_POWERSHELL` 指定的运行器。测试依赖不属于日常脚本依赖。

回归覆盖成功更新且不提交、脏工作区/暂存区/未跟踪文件、未提交子模块指针、合并与隐藏改动、忽略文件冲突、取消、无合格 CI、同提交的新失败/进行中运行、确认期间 CI 重新运行/新增本地改动、错误来源、网络/认证失败和未初始化子模块。

当前验证：macOS 系统 Bash 与 macOS 上的 PowerShell 7 各通过 24 项隔离测试。Windows 入口按系统 PowerShell 5.1 编写，尚未在真实 Windows 环境验证；这些本地回归不会触发远端 CI。
