#!/usr/bin/env bash
# Análise de logs (módulo 12): resume erros recorrentes em Markdown.
# Uso:  bash scripts/ops/analyze-logs.sh                 (lê `docker compose logs` da última hora)
#       bash scripts/ops/analyze-logs.sh arquivo.log     (lê um arquivo)
#       some-comando | bash scripts/ops/analyze-logs.sh -  (lê stdin)
# Variáveis: SINCE (1h), TOP (10)
set -uo pipefail
cd "$(dirname "$0")/../.."

SINCE="${SINCE:-1h}"
TOP="${TOP:-10}"
tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

case "${1:-}" in
  "")  docker compose logs --no-color --since "$SINCE" >"$tmp" 2>&1 || true ;;
  "-") cat >"$tmp" ;;
  *)   cp "$1" "$tmp" ;;
esac

total="$(wc -l <"$tmp" | tr -d ' ')"
errors="$(grep -ciE '\b(error|fatal|critical|exception)\b' "$tmp" || true)"
warns="$(grep -ciE '\bwarn(ing)?\b' "$tmp" || true)"

echo "## Análise de logs"
echo ""
echo "- Linhas analisadas: $total"
echo "- Erros/exceções: $errors"
echo "- Avisos: $warns"
echo ""

if [ "${errors:-0}" -eq 0 ]; then
  echo "Nenhum erro encontrado."
  exit 0
fi

echo "### Erros mais frequentes (normalizados)"
echo ""
grep -iE '\b(error|fatal|critical|exception)\b' "$tmp" \
  | sed -E 's/^[^|]*\| *//' \
  | sed -E 's/[0-9]{4}-[0-9]{2}-[0-9]{2}[T ][0-9:.,+Z-]+//g' \
  | sed -E 's/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/<guid>/g' \
  | sed -E 's/[0-9]+/N/g' \
  | cut -c1-200 \
  | sort | uniq -c | sort -rn | head -n "$TOP" \
  | awk '{c=$1; $1=""; printf("- %sx %s\n", c, $0)}'
