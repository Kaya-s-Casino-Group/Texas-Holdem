#!/bin/bash
set -e
root="$(cd "$(dirname "$0")/.." && pwd)"
script="$root/External/BattleToolkit/Tools/test-poker-engine.sh"
if [ ! -f "$script" ]; then echo "Run git submodule update --init --recursive first." >&2; exit 1; fi
exec bash "$script" "$@"
