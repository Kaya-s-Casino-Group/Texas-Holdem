#!/bin/bash
# macOS system Bash 3.2 compatible. Never stage, commit, stash or force checkout.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin:/usr/local/bin"
root="$(cd "$(dirname "$0")/../.." && pwd -P)"
child="$root/External/BattleToolkit"
repository='Kaya-s-Casino-Group/BattleToolkit'
workflow='standalone.yml'
mode="${1:-}"
fail() { printf '\n已停止：%s\n' "$*" >&2; exit 1; }
case "$mode" in
  --help|-h) printf '%s\n' '用法：sync-battle-toolkit.command [--check | --list]' '默认：检查本地改动 → 列出 CI 通过版本 → 选择并确认 → 更新子模块。' '--check 只检查本地；--list 查询候选但不更新。不会自动提交。'; exit 0 ;;
  ''|--check|--list) ;;
  *) fail '未知参数，可用 --help 查看。' ;;
esac
[ "$#" -le 1 ] || fail '只接受一个可选参数。'
command -v git >/dev/null || fail '请先安装 Git。'
valid_remote() {
  case "$1" in
    https://github.com/Kaya-s-Casino-Group/BattleToolkit|https://github.com/Kaya-s-Casino-Group/BattleToolkit.git|git@github.com:Kaya-s-Casino-Group/BattleToolkit|git@github.com:Kaya-s-Casino-Group/BattleToolkit.git|ssh://git@github.com/Kaya-s-Casino-Group/BattleToolkit.git) return 0 ;;
    *) return 1 ;;
  esac
}
check_repo() {
  local directory="$1" label="$2" gitdir marker changes flags
  gitdir="$(git -C "$directory" rev-parse --absolute-git-dir)" || fail "$label 不是 Git 仓库。"
  for marker in MERGE_HEAD CHERRY_PICK_HEAD REVERT_HEAD rebase-merge rebase-apply sequencer BISECT_LOG index.lock; do
    [ ! -e "$gitdir/$marker" ] || fail "$label 正在合并、变基、拣选或被其他 Git 操作占用，请先处理。"
  done
  flags="$(git -C "$directory" ls-files -v | sed -n '/^[a-zS] /p')"
  [ -z "$flags" ] || fail "$label 存在 assume-unchanged/skip-worktree 标记，无法确认所有本地改动。请先处理这些标记。"
  changes="$(git -C "$directory" -c core.quotePath=false status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
  if [ -n "$changes" ]; then
    printf '\n%s 的未提交/未跟踪内容（最多显示 30 项）：\n' "$label" >&2
    printf '%s\n' "$changes" | sed -n '1,30p' >&2
    printf '完整列表请在该仓库执行 git status --short。\n' >&2
    fail '请先自行保存并提交，或处理这些改动后重试。脚本不会自动 stash、清理或覆盖。'
  fi
}
check_all() {
  [ "$(git -C "$root" rev-parse --show-toplevel)" = "$root" ] || fail '脚本必须位于 Unity 主仓库中。'
  [ "$(git -C "$root" config -f .gitmodules --get submodule.External/BattleToolkit.path)" = 'External/BattleToolkit' ] || fail '.gitmodules 中的子模块路径不匹配。'
  valid_remote "$(git -C "$root" config -f .gitmodules --get submodule.External/BattleToolkit.url)" || fail '子模块仓库地址不匹配，未进行同步。'
  [ -e "$child/.git" ] || fail '子模块尚未初始化。请先执行 git submodule update --init --recursive，再运行本脚本。'
  [ "$(git -C "$child" rev-parse --show-toplevel)" = "$child" ] || fail '子模块路径不是预期的独立仓库（含符号链接时请使用实际工程路径）。'
  valid_remote "$(git -C "$child" config --get remote.origin.url)" || fail '小仓库 origin 地址不匹配。'
  check_repo "$child" 'BattleToolkit 小仓库'
  check_repo "$root" 'Unity 主仓库'
}
check_all
original_parent="$(git -C "$root" rev-parse HEAD)"
original_child="$(git -C "$child" rev-parse HEAD)"
printf '本地检查通过。Unity: %.12s；BattleToolkit: %.12s\n' "$original_parent" "$original_child"
[ "$mode" != '--check' ] || exit 0
command -v gh >/dev/null || fail '需要 GitHub CLI（gh）。Windows: winget install --id GitHub.cli；macOS: brew install gh。安装后运行 gh auth login 并使用有小仓库读取权限的账号。'
gh auth status --hostname github.com >/dev/null 2>&1 || fail 'GitHub CLI 未登录或凭据不可用。请运行 gh auth login，使用有小仓库读取权限的账号。'
fetch_main() { git -C "$child" fetch --no-tags origin refs/heads/main:refs/remotes/origin/main || fail '获取小仓库 main 失败，工作区未切换。'; }
fetch_main
# Keep the latest push run for each SHA BEFORE filtering success. Never fall back
# to an older green run when the same commit has a newer failed/pending run.
runs_query='.workflow_runs | group_by(.head_sha) | map(max_by(.run_number)) | sort_by(.run_number) | reverse | .[] | [.head_sha, (.id|tostring), (.run_attempt|tostring), .status, (.conclusion // "none")] | @tsv'
expected_jobs='["first-launch (macos-latest)","first-launch (windows-latest)","rules-and-host (macos-latest)","rules-and-host (windows-latest)"]'
jobs_query="(.total_count == 4) and (([.jobs[].name] | sort) == $expected_jobs) and all(.jobs[]; .status == \"completed\" and .conclusion == \"success\")"
list_runs() { gh api --hostname github.com "repos/$repository/actions/workflows/$workflow/runs?branch=main&event=push&per_page=100${1:-}" --jq "$runs_query" || fail '读取 CI 失败，请检查网络和仓库权限。'; }
check_jobs() {
  local answer
  answer="$(gh api --hostname github.com "repos/$repository/actions/runs/$1/attempts/$2/jobs?per_page=100" --jq "$jobs_query")" || fail '读取 CI job 失败，未进行更新。'
  [ "$answer" = 'true' ]
}
rows="$(list_runs)"
shas=(); ids=(); attempts=()
while IFS=$'\t' read -r sha id attempt status conclusion; do
  [[ "$sha" =~ ^[0-9a-f]{40}$ && "$id" =~ ^[0-9]+$ && "$attempt" =~ ^[1-9][0-9]*$ ]] || continue
  [ "$status" = completed ] && [ "$conclusion" = success ] || continue
  git -C "$child" cat-file -e "$sha^{commit}" 2>/dev/null || continue
  git -C "$child" merge-base --is-ancestor "$sha" refs/remotes/origin/main || continue
  check_jobs "$id" "$attempt" || continue
  shas+=("$sha"); ids+=("$id"); attempts+=("$attempt")
  [ "${#shas[@]}" -lt 10 ] || break
done <<< "$rows"
[ "${#shas[@]}" -gt 0 ] || fail '最近 100 次 main push CI 中没有可用版本（需要四个指定 job 全部成功且提交属于 main 历史）。'
printf '\n可选择的版本（近期 CI 记录，最多 10 项；当前 %.12s）：\n' "$original_child"
for ((index=0; index<${#shas[@]}; index++)); do
  title="$(git -C "$child" show -s --format='%cs %s' "${shas[$index]}" | tr '\r\n\t' ' ' | LC_ALL=C tr -d '\000-\010\013\014\016-\037\177')"
  printf '%s) %.12s  %s\n   https://github.com/%s/actions/runs/%s\n' "$((index+1))" "${shas[$index]}" "$title" "$repository" "${ids[$index]}"
done
[ "$mode" != '--list' ] || exit 0
printf '\n选择序号（0 或直接回车取消）：'
IFS= read -r choice || choice=''
[ -n "$choice" ] && [ "$choice" != 0 ] || { printf '已取消，未切换版本。\n'; exit 0; }
[[ "$choice" =~ ^[1-9][0-9]?$ ]] || fail '序号无效。'
index=$((10#$choice - 1))
[ "$index" -lt "${#shas[@]}" ] || fail '序号超出范围。'
selected="${shas[$index]}"; selected_id="${ids[$index]}"; selected_attempt="${attempts[$index]}"
[ "$selected" != "$original_child" ] || { printf '已经是该版本，无需更新。\n'; exit 0; }
printf '\n将从 %s 更新为 %s（包含该提交的规则、XML 和工具）。\n请退出 Unity Play，并关闭正在使用的配置工具。\n输入 UPDATE 确认，其他输入取消：' "$original_child" "$selected"
IFS= read -r confirmation || confirmation=''
[ "$confirmation" = UPDATE ] || { printf '已取消，未切换版本。\n'; exit 0; }
check_all
fetch_main
# Re-read the run and its exact attempt: a re-run can invalidate the candidate.
latest="$(list_runs "&head_sha=$selected" | head -n 1)"
IFS=$'\t' read -r fresh_sha fresh_id fresh_attempt fresh_status fresh_conclusion <<< "$latest"
[ "$fresh_sha" = "$selected" ] && [ "$fresh_id" = "$selected_id" ] && [ "$fresh_attempt" = "$selected_attempt" ] && [ "$fresh_status" = completed ] && [ "$fresh_conclusion" = success ] || fail '所选版本的最新 CI 记录已变化，请重新运行并选择。'
check_jobs "$selected_id" "$selected_attempt" || fail '所选版本的四个 CI job 未全部通过。'
git -C "$child" merge-base --is-ancestor "$selected" refs/remotes/origin/main || fail '所选提交已不在 main 历史中，请重新选择。'
check_all
[ "$(git -C "$root" rev-parse HEAD)" = "$original_parent" ] && [ "$(git -C "$child" rev-parse HEAD)" = "$original_child" ] || fail '等待期间本地提交版本发生变化，请重新运行。'
git -C "$child" -c submodule.recurse=false switch --detach --no-overwrite-ignore "$selected" || fail 'Git 拒绝切换；未使用强制覆盖，请检查上方错误。'
printf '\n已更新到 %s。未暂存、未提交、未推送。\n请刷新 Unity、重新开局并验证；通过后自行提交 External/BattleToolkit 的版本变化。\n原版本：%s\n' "$selected" "$original_child"
