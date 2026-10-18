#!/bin/bash
set -e
project="$(cd "$(dirname "$0")/../.." && pwd)"
root="$project/External/BattleToolkit"
if [ ! -f "$root/start.command" ]; then
  echo "请先在主工程运行 git submodule update --init --recursive"
  read -r -p "按回车关闭" _
  exit 1
fi
exec bash "$root/start.command" "$@"
