# Windows PowerShell 5.1 compatible. Never stage, commit, stash or force checkout.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd([IO.Path]::DirectorySeparatorChar)
$child = Join-Path $root 'External/BattleToolkit'
$repository = 'Kaya-s-Casino-Group/BattleToolkit'
$mode = if ($args.Count -gt 0) { [string]$args[0] } else { '' }
function Stop-Sync([string]$Message) { throw $Message }
function Invoke-Git([string]$Directory, [string[]]$ArgumentList) {
    $ErrorActionPreference = 'Continue'
    $output = @(& $script:git -C $Directory @ArgumentList)
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw ('Git 操作失败（退出码 {0}），未强制覆盖任何文件。' -f $code) }
    return $output
}
function Get-GitText([string]$Directory, [string[]]$ArgumentList) {
    return (@(Invoke-Git $Directory $ArgumentList) -join "`n")
}
function Test-Remote([string]$Url) {
    return @('https://github.com/Kaya-s-Casino-Group/BattleToolkit',
        'https://github.com/Kaya-s-Casino-Group/BattleToolkit.git',
        'git@github.com:Kaya-s-Casino-Group/BattleToolkit',
        'git@github.com:Kaya-s-Casino-Group/BattleToolkit.git',
        'ssh://git@github.com/Kaya-s-Casino-Group/BattleToolkit.git') -ccontains $Url
}
function Assert-Clean([string]$Directory, [string]$Label) {
    $gitdir = Get-GitText $Directory @('rev-parse','--absolute-git-dir')
    foreach ($marker in @('MERGE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-merge','rebase-apply','sequencer','BISECT_LOG','index.lock')) {
        if (Test-Path -LiteralPath (Join-Path $gitdir $marker)) {
            Stop-Sync "$Label 正在合并、变基、拣选或被其他 Git 操作占用，请先处理。"
        }
    }
    $flags = @(Invoke-Git $Directory @('ls-files','-v') | Where-Object { $_ -cmatch '^[a-zS] ' })
    if ($flags.Count -gt 0) { Stop-Sync "$Label 存在 assume-unchanged/skip-worktree 标记，无法确认所有本地改动。请先处理这些标记。" }
    $changes = Get-GitText $Directory @('-c','core.quotePath=false','status','--porcelain=v1','--untracked-files=all','--ignore-submodules=none')
    if ($changes) {
        Write-Host "`n$Label 的未提交/未跟踪内容（最多显示 30 项）："
        ($changes -split "`n" | Select-Object -First 30) | Out-Host
        Write-Host "完整列表请在该仓库执行 git status --short。"
        Stop-Sync '请先自行保存并提交，或处理这些改动后重试。脚本不会自动 stash、清理或覆盖。'
    }
}
function Assert-AllClean {
    $top = Get-GitText $root @('rev-parse','--show-toplevel')
    if ([IO.Path]::GetFullPath($top) -ne $root) { Stop-Sync '脚本必须位于 Unity 主仓库中。' }
    if ((Get-GitText $root @('config','-f','.gitmodules','--get','submodule.External/BattleToolkit.path')) -cne 'External/BattleToolkit') { Stop-Sync '.gitmodules 中的子模块路径不匹配。' }
    if (!(Test-Remote (Get-GitText $root @('config','-f','.gitmodules','--get','submodule.External/BattleToolkit.url')))) { Stop-Sync '子模块仓库地址不匹配，未进行同步。' }
    if (!(Test-Path -LiteralPath (Join-Path $child '.git'))) { Stop-Sync '子模块尚未初始化。请先执行 git submodule update --init --recursive，再运行本脚本。' }
    foreach ($folder in @((Join-Path $root 'External'), $child)) {
        if (((Get-Item -LiteralPath $folder).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Stop-Sync '子模块目录使用了链接或 junction，请使用实际工程路径。' }
    }
    $childTop = Get-GitText $child @('rev-parse','--show-toplevel')
    if ([IO.Path]::GetFullPath($childTop) -ne [IO.Path]::GetFullPath($child)) { Stop-Sync '子模块路径不是预期的独立仓库。' }
    if (!(Test-Remote (Get-GitText $child @('config','--get','remote.origin.url')))) { Stop-Sync '小仓库 origin 地址不匹配。' }
    Assert-Clean $child 'BattleToolkit 小仓库'
    Assert-Clean $root 'Unity 主仓库'
}
function Get-Api([string]$Endpoint) {
    $ErrorActionPreference = 'Continue'
    $output = @(& $script:gh api --hostname github.com $Endpoint)
    if ($LASTEXITCODE -ne 0) { throw '读取 CI 失败，请检查网络和仓库权限。工作区未切换。' }
    return (($output -join "`n") | ConvertFrom-Json)
}
function Get-Runs([string]$Sha = '') {
    $suffix = if ($Sha) { '&head_sha=' + $Sha } else { '' }
    $response = Get-Api "repos/$repository/actions/workflows/standalone.yml/runs?branch=main&event=push&per_page=100$suffix"
    # Pick the latest run for each SHA before filtering status/conclusion.
    $latest = @($response.workflow_runs | Group-Object head_sha | ForEach-Object {
        $_.Group | Sort-Object -Property run_number -Descending | Select-Object -First 1
    })
    return @($latest | Sort-Object -Property run_number -Descending)
}
function Test-Jobs($Run) {
    $response = Get-Api "repos/$repository/actions/runs/$($Run.id)/attempts/$($Run.run_attempt)/jobs?per_page=100"
    $jobs = @($response.jobs)
    $expected = @('first-launch (macos-latest)','first-launch (windows-latest)','rules-and-host (macos-latest)','rules-and-host (windows-latest)')
    if ($response.total_count -ne 4 -or $jobs.Count -ne 4 -or
        (($jobs.name | Sort-Object) -join '|') -cne ($expected -join '|')) { return $false }
    return @($jobs | Where-Object { $_.status -cne 'completed' -or $_.conclusion -cne 'success' }).Count -eq 0
}
function Test-OnMain([string]$Sha) {
    $ErrorActionPreference = 'Continue'
    & $script:git -C $child cat-file -e "$Sha`^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }
    & $script:git -C $child merge-base --is-ancestor $Sha refs/remotes/origin/main
    return $LASTEXITCODE -eq 0
}
function Update-MainRef {
    Invoke-Git $child @('fetch','--no-tags','origin','refs/heads/main:refs/remotes/origin/main') | Out-Host
}
try {
    if ($mode -in @('--help','-h')) {
        Write-Host '用法：sync-battle-toolkit.cmd [--check | --list]'
        Write-Host '默认：检查本地改动 → 列出 CI 通过版本 → 选择并确认 → 更新子模块。'
        Write-Host '--check 只检查本地；--list 查询候选但不更新。不会自动提交。'
        exit 0
    }
    if ($args.Count -gt 1 -or $mode -notin @('','--check','--list')) { Stop-Sync '未知参数，可用 --help 查看。' }
    $script:git = (Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (!$script:git) { Stop-Sync '请先安装 Git。' }
    Assert-AllClean
    $originalParent = Get-GitText $root @('rev-parse','HEAD')
    $originalChild = Get-GitText $child @('rev-parse','HEAD')
    Write-Host ("本地检查通过。Unity: {0}；BattleToolkit: {1}" -f $originalParent.Substring(0,12),$originalChild.Substring(0,12))
    if ($mode -eq '--check') { exit 0 }
    $script:gh = (Get-Command gh -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (!$script:gh) { Stop-Sync '需要 GitHub CLI（gh）。请运行 winget install --id GitHub.cli，安装后运行 gh auth login 并使用有小仓库读取权限的账号。' }
    $savedPreference = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & $script:gh auth status --hostname github.com *> $null
    $authCode = $LASTEXITCODE; $ErrorActionPreference = $savedPreference
    if ($authCode -ne 0) { Stop-Sync 'GitHub CLI 未登录或凭据不可用。请运行 gh auth login，使用有小仓库读取权限的账号。' }
    Update-MainRef
    $candidates = @()
    foreach ($run in @(Get-Runs)) {
        if ($run.head_sha -cnotmatch '^[0-9a-f]{40}$' -or [string]$run.id -notmatch '^\d+$' -or [string]$run.run_attempt -notmatch '^[1-9]\d*$') { continue }
        if ($run.status -cne 'completed' -or $run.conclusion -cne 'success') { continue }
        if (!(Test-OnMain $run.head_sha) -or !(Test-Jobs $run)) { continue }
        $candidates += $run
        if ($candidates.Count -ge 10) { break }
    }
    if (!$candidates.Count) { Stop-Sync '最近 100 次 main push CI 中没有可用版本（需要四个指定 job 全部成功且提交属于 main 历史）。' }
    Write-Host "`n可选择的版本（近期 CI 记录，最多 10 项；当前 $($originalChild.Substring(0,12))）："
    for ($index=0; $index -lt $candidates.Count; $index++) {
        $run = $candidates[$index]
        $title = (Get-GitText $child @('show','-s','--format=%cs %s',$run.head_sha)) -replace '[\x00-\x1F\x7F]', ' '
        Write-Host ("{0}) {1}  {2}`n   https://github.com/{3}/actions/runs/{4}" -f ($index+1),$run.head_sha.Substring(0,12),$title,$repository,$run.id)
    }
    if ($mode -eq '--list') { exit 0 }
    $choice = Read-Host "`n选择序号（0 或直接回车取消）"
    if (!$choice -or $choice -eq '0') { Write-Host '已取消，未切换版本。'; exit 0 }
    $number = 0
    if (![int]::TryParse($choice,[ref]$number) -or $number -lt 1 -or $number -gt $candidates.Count) { Stop-Sync '序号无效。' }
    $selected = $candidates[$number-1]
    if ($selected.head_sha -ceq $originalChild) { Write-Host '已经是该版本，无需更新。'; exit 0 }
    Write-Host "`n将从 $originalChild 更新为 $($selected.head_sha)（包含该提交的规则、XML 和工具）。"
    Write-Host '请退出 Unity Play，并关闭正在使用的配置工具。'
    $confirmation = Read-Host '输入 UPDATE 确认，其他输入取消'
    if ($confirmation -cne 'UPDATE') { Write-Host '已取消，未切换版本。'; exit 0 }
    Assert-AllClean
    Update-MainRef
    $latest = @(Get-Runs $selected.head_sha)
    if (!$latest.Count -or $latest[0].head_sha -cne $selected.head_sha -or $latest[0].id -ne $selected.id -or
        $latest[0].run_attempt -ne $selected.run_attempt -or $latest[0].status -cne 'completed' -or $latest[0].conclusion -cne 'success') {
        Stop-Sync '所选版本的最新 CI 记录已变化，请重新运行并选择。'
    }
    if (!(Test-Jobs $selected)) { Stop-Sync '所选版本的四个 CI job 未全部通过。' }
    if (!(Test-OnMain $selected.head_sha)) { Stop-Sync '所选提交已不在 main 历史中，请重新选择。' }
    Assert-AllClean
    if ((Get-GitText $root @('rev-parse','HEAD')) -cne $originalParent -or (Get-GitText $child @('rev-parse','HEAD')) -cne $originalChild) { Stop-Sync '等待期间本地提交版本发生变化，请重新运行。' }
    Invoke-Git $child @('-c','submodule.recurse=false','switch','--detach','--no-overwrite-ignore',$selected.head_sha) | Out-Host
    Write-Host "`n已更新到 $($selected.head_sha)。未暂存、未提交、未推送。"
    Write-Host "请刷新 Unity、重新开局并验证；通过后自行提交 External/BattleToolkit 的版本变化。`n原版本：$originalChild"
    exit 0
} catch {
    [Console]::Error.WriteLine("`n已停止：" + $_.Exception.Message)
    exit 1
}
