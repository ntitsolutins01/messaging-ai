#!/usr/bin/env bash
# Notificação no Telegram (módulo 12).
# Uso: bash scripts/ops/notify-telegram.sh "mensagem"     ou     echo "mensagem" | bash scripts/ops/notify-telegram.sh
# Requer TELEGRAM_BOT_TOKEN e TELEGRAM_CHAT_ID no ambiente (veja .env.example). Nunca versionar o token.
set -euo pipefail

: "${TELEGRAM_BOT_TOKEN:?defina TELEGRAM_BOT_TOKEN (veja .env.example)}"
: "${TELEGRAM_CHAT_ID:?defina TELEGRAM_CHAT_ID (veja .env.example)}"

message="${1:-}"
[ -z "$message" ] && message="$(cat)"
[ -z "$message" ] && { echo "mensagem vazia" >&2; exit 1; }

# O token vai por stdin (curl -K -) para não aparecer na lista de processos nem em logs de comando.
printf 'url = "https://api.telegram.org/bot%s/sendMessage"\n' "$TELEGRAM_BOT_TOKEN" |
  curl -fsS -m 15 -K - \
    --data-urlencode "chat_id=${TELEGRAM_CHAT_ID}" \
    --data-urlencode "text=${message:0:4000}" \
    --data-urlencode "disable_web_page_preview=true" >/dev/null

echo "notificação enviada"
