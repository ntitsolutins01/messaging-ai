#!/usr/bin/env bash
# Stop hook: antes de o Claude encerrar o turno, garante que a solução compila.
# Exit 2 devolve o erro ao Claude para ele corrigir. Sem solução ou sem dotnet, não faz nada.
set -uo pipefail

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0

command -v dotnet >/dev/null 2>&1 || exit 0
ls ./*.sln ./*.slnx >/dev/null 2>&1 || exit 0

# Evita laço infinito se o hook já bloqueou uma vez neste turno.
input="$(cat)"
active="$(printf '%s' "$input" | python3 -c 'import sys,json; print(json.load(sys.stdin).get("stop_hook_active", False))' 2>/dev/null || echo False)"
[ "$active" = "True" ] && exit 0

if ! out="$(dotnet build --nologo -v q 2>&1)"; then
  {
    echo "dotnet build falhou. Corrija antes de concluir:"
    printf '%s\n' "$out" | grep -E "error " | head -20
  } >&2
  exit 2
fi

exit 0
