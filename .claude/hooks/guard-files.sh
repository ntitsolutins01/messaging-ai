#!/usr/bin/env bash
# PreToolUse (Write|Edit): protege migrations já aplicadas e impede segredos em arquivos.
set -uo pipefail

input="$(cat)"
read_field() {
  printf '%s' "$input" | python3 -c "import sys,json; d=json.load(sys.stdin).get('tool_input',{}); print(d.get('$1',''))" 2>/dev/null || true
}

file="$(read_field file_path)"
content="$(read_field content)$(read_field new_string)"

block() {
  echo "Bloqueado pelo hook: $1" >&2
  exit 2
}

# Migrations existentes não são editadas; crie uma nova.
if [[ "$file" == *"/Migrations/"* && -f "$file" && "$file" != *"ModelSnapshot.cs" ]]; then
  block "migration já existente ($file). Crie uma nova migration com 'dotnet ef migrations add'."
fi

# .env nunca é escrito pelo agente.
case "$file" in
  *".env"|*".env."*) block "não escreva em arquivos .env." ;;
esac

# Padrões comuns de segredo.
if printf '%s' "$content" | grep -Eq '(AKIA[0-9A-Z]{16}|-----BEGIN (RSA |EC )?PRIVATE KEY-----|password\s*=\s*"[^"]{8,}")'; then
  block "possível segredo no conteúdo. Use user-secrets ou variáveis de ambiente."
fi

exit 0
