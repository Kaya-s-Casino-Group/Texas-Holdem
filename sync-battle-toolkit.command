#!/bin/bash
root="$(cd "$(dirname "$0")" && pwd -P)" || exit 1
/bin/bash "$root/Tools/BattleToolkitSync/sync.sh" "$@"
result=$?
if [ "$#" -eq 0 ] && [ -t 0 ]; then printf '\n按回车关闭窗口：'; read -r _; fi
exit "$result"
