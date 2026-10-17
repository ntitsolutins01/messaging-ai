#!/usr/bin/env bash
# Vigia erros recorrentes no Elasticsearch e abre UMA issue por erro (módulo 13).
# A issue nasce com o rótulo `auto-detected`. A correção automática só começa quando um humano
# com permissão de escrita adiciona o rótulo `auto-fix` (workflow claude-auto-fix.yml).
#
# Uso:  DRY_RUN=false GH_REPO=owner/repo bash scripts/observability/watch-errors.sh
# Variáveis (padrões entre parênteses):
#   ES_URL (http://localhost:9200)   ES_INDEX (otel-logs*)   WINDOW (15m)   THRESHOLD (5)
#   ES_QUERY ('SeverityText:(Error OR Critical)')   ES_AGG_FIELD (Body.keyword)
#   DRY_RUN (true: só imprime)       GH_REPO (repositório do git atual)
# Os nomes de campo dependem do mapeamento do exporter: confirme no Kibana (Discover) e ajuste ES_QUERY/ES_AGG_FIELD.
set -uo pipefail

ES_URL="${ES_URL:-http://localhost:9200}"
ES_INDEX="${ES_INDEX:-otel-logs*}"
WINDOW="${WINDOW:-15m}"
THRESHOLD="${THRESHOLD:-5}"
ES_QUERY="${ES_QUERY:-SeverityText:(Error OR Critical)}"
ES_AGG_FIELD="${ES_AGG_FIELD:-Body.keyword}"
DRY_RUN="${DRY_RUN:-true}"
repo_args=()
[ -n "${GH_REPO:-}" ] && repo_args=(--repo "$GH_REPO")

request="$(python3 - "$WINDOW" "$ES_QUERY" "$ES_AGG_FIELD" <<'PY'
import json, sys
window, query, field = sys.argv[1:4]
print(json.dumps({
    "size": 0,
    "query": {"bool": {"filter": [
        {"range": {"@timestamp": {"gte": f"now-{window}"}}},
        {"query_string": {"query": query}},
    ]}},
    "aggs": {"by_message": {"terms": {"field": field, "size": 10}}},
}))
PY
)"

response="$(curl -fsS -m 20 -H 'Content-Type: application/json' -X POST "$ES_URL/$ES_INDEX/_search" -d "$request")" || {
  echo "Falha ao consultar o Elasticsearch em $ES_URL (índice $ES_INDEX)." >&2
  exit 2
}

findings="$(printf '%s' "$response" | python3 -c '
import hashlib, json, re, sys
threshold = int(sys.argv[1])
data = json.load(sys.stdin)
for bucket in data.get("aggregations", {}).get("by_message", {}).get("buckets", []):
    count = bucket["doc_count"]
    if count < threshold:
        continue
    message = str(bucket["key"])
    normalized = re.sub(r"[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}", "<guid>", message)
    normalized = re.sub(r"[0-9]+", "N", normalized)
    fingerprint = hashlib.sha1(normalized.encode()).hexdigest()[:12]
    print(f"{fingerprint}\t{count}\t{message[:200]}".replace("\n", " "))
' "$THRESHOLD")"

if [ -z "$findings" ]; then
  echo "Sem erros recorrentes (>= $THRESHOLD em $WINDOW)."
  exit 0
fi

while IFS=$'\t' read -r fingerprint count message; do
  echo "Erro recorrente fp:$fingerprint ($count ocorrências em $WINDOW): $message"

  open="$(gh issue list "${repo_args[@]}" --label auto-detected --state open --search "fp:$fingerprint in:body" --json number --jq 'length' 2>/dev/null || echo 0)"
  if [ "${open:-0}" != "0" ]; then
    echo "  já existe issue aberta; ignorando."
    continue
  fi
  if [ "$DRY_RUN" != "false" ]; then
    echo "  [dry-run] abriria uma issue. Use DRY_RUN=false para criar."
    continue
  fi

  body="$(printf 'Erro recorrente detectado automaticamente.\n\n- Fingerprint: `fp:%s`\n- Ocorrências: %s em %s\n- Mensagem (truncada): `%s`\n\nInvestigação sugerida: use `/debug-trace` com o CorrelationId de uma ocorrência (skill `observability-debugging`).\n\n**Correção automática:** um mantenedor com permissão de escrita pode adicionar o rótulo `auto-fix` para o Claude tentar corrigir e abrir um PR (que sempre passa por revisão humana).\n' \
    "$fingerprint" "$count" "$WINDOW" "$message")"
  gh issue create "${repo_args[@]}" \
    --title "[auto] Erro recorrente: ${message:0:70}" \
    --label auto-detected --label bug \
    --body "$body"
done <<< "$findings"
