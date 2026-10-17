#!/usr/bin/env bash
# Backup automático do PostgreSQL da POC (módulo 12). Seguro para rodar de cron/Agendador.
# Uso: bash scripts/ops/backup-postgres.sh      Variáveis: BACKUP_DIR (backups), RETENTION_DAYS (7)
set -euo pipefail
cd "$(dirname "$0")/../.."

BACKUP_DIR="${BACKUP_DIR:-backups}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"
mkdir -p "$BACKUP_DIR"

file="$BACKUP_DIR/messaging_poc-$(date +%Y%m%d-%H%M%S).sql.gz"

if ! docker compose exec -T postgres pg_dump -U poc -d messaging_poc | gzip > "$file"; then
  rm -f "$file"
  echo "FALHA no pg_dump; backup descartado." >&2
  exit 1
fi

# Valida: arquivo íntegro e com conteúdo.
if ! gzip -t "$file" || [ "$(gzip -dc "$file" | wc -c)" -lt 100 ]; then
  rm -f "$file"
  echo "FALHA: backup corrompido ou vazio; descartado." >&2
  exit 1
fi

# Retenção: remove só backups desta rotina com mais de RETENTION_DAYS dias.
find "$BACKUP_DIR" -maxdepth 1 -name 'messaging_poc-*.sql.gz' -mtime +"$RETENTION_DAYS" -delete

echo "OK $file ($(du -h "$file" | cut -f1))"
