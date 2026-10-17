#!/usr/bin/env bash
# Relatório de status (módulo 12): containers, saúde da API, erros recentes e idade do último backup.
# Envia ao Telegram se TELEGRAM_BOT_TOKEN/TELEGRAM_CHAT_ID existirem; senão só imprime.
# Variáveis: API_URL (http://localhost:5080), BACKUP_DIR (backups), BACKUP_MAX_AGE_HOURS (26)
set -uo pipefail
cd "$(dirname "$0")/../.."

API_URL="${API_URL:-http://localhost:5080}"
BACKUP_DIR="${BACKUP_DIR:-backups}"
BACKUP_MAX_AGE_HOURS="${BACKUP_MAX_AGE_HOURS:-26}"
problems=0

report="Status Messaging POC — $(date '+%Y-%m-%d %H:%M')"$'\n'

if docker compose ps --format '{{.Service}}: {{.State}}' >/tmp/ps.$$ 2>/dev/null && [ -s /tmp/ps.$$ ]; then
  report+="Containers:"$'\n'"$(sed 's/^/  /' /tmp/ps.$$)"$'\n'
  grep -qv ': running' /tmp/ps.$$ && problems=$((problems + 1))
else
  report+="Containers: indisponível"$'\n'
  problems=$((problems + 1))
fi
rm -f /tmp/ps.$$

if curl -fsS -m 5 "$API_URL/health/ready" >/dev/null 2>&1; then
  report+="API: saudável"$'\n'
else
  report+="API: FORA DO AR ($API_URL)"$'\n'
  problems=$((problems + 1))
fi

errors="$(SINCE=1h bash scripts/ops/analyze-logs.sh 2>/dev/null | sed -n 's/^- Erros\/exceções: //p')"
report+="Erros na última hora: ${errors:-?}"$'\n'
[ "${errors:-0}" != "0" ] && [ -n "${errors:-}" ] && problems=$((problems + 1))

latest="$(ls -t "$BACKUP_DIR"/messaging_poc-*.sql.gz 2>/dev/null | head -n 1 || true)"
if [ -n "$latest" ]; then
  age_h=$(( ( $(date +%s) - $(stat -c %Y "$latest" 2>/dev/null || stat -f %m "$latest") ) / 3600 ))
  report+="Último backup: há ${age_h}h"$'\n'
  [ "$age_h" -gt "$BACKUP_MAX_AGE_HOURS" ] && { report+="  ATENÇÃO: backup antigo"$'\n'; problems=$((problems + 1)); }
else
  report+="Último backup: nenhum"$'\n'
  problems=$((problems + 1))
fi

if [ "$problems" -eq 0 ]; then report+="Resultado: OK"; else report+="Resultado: $problems problema(s)"; fi

echo "$report"
if [ -n "${TELEGRAM_BOT_TOKEN:-}" ] && [ -n "${TELEGRAM_CHAT_ID:-}" ]; then
  bash scripts/ops/notify-telegram.sh "$report" || echo "falha ao notificar o Telegram" >&2
fi
[ "$problems" -eq 0 ]
