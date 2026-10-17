#!/usr/bin/env bash
# PostToolUse (Write|Edit): formata arquivos C# editados. Falhas não bloqueiam o fluxo.
set -uo pipefail

input="$(cat)"
file="$(printf '%s' "$input" | python3 -c 'import sys,json; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' 2>/dev/null || true)"

case "$file" in
  *.cs)
    if command -v dotnet >/dev/null 2>&1; then
      dotnet format --include "$file" --no-restore >/dev/null 2>&1 || true
    fi
    ;;
esac

exit 0
