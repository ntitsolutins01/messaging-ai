#!/usr/bin/env bash
# PreToolUse (Bash): bloqueia comandos destrutivos. Exit 2 = bloqueia e devolve o motivo ao Claude.
set -uo pipefail

input="$(cat)"
cmd="$(printf '%s' "$input" | python3 -c 'import sys,json; print(json.load(sys.stdin).get("tool_input",{}).get("command",""))' 2>/dev/null || true)"

block() {
  echo "Bloqueado pelo hook: $1" >&2
  exit 2
}

case "$cmd" in
  *"rm -rf /"*|*"rm -rf ~"*|*"rm -rf *"*) block "rm -rf perigoso." ;;
  *"git push --force"*|*"git push -f"*)   block "force push não é permitido." ;;
  *"DROP DATABASE"*|*"drop database"*)    block "DROP DATABASE não é permitido." ;;
  *"TRUNCATE"*|*"truncate table"*)        block "TRUNCATE direto no banco: use migration ou a API." ;;
  *"docker compose down -v"*|*"docker volume rm"*) block "remover volumes apaga os dados da POC; peça confirmação ao usuário." ;;
esac

exit 0
