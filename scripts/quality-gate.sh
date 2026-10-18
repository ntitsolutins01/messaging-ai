#!/usr/bin/env bash
# Quality Gate (módulo 09). Falha com exit 1 se QUALQUER portão falhar.
#
# Portões:
#   1. Build com avisos como erro (inclui complexidade ciclomática CA1502 > 10)
#   2. Testes de arquitetura (estrutura de dependências / fronteiras entre módulos)
#   3. Testes unitários com cobertura
#   4. Testes de integração
#   5. Cobertura de linhas >= COVERAGE_MIN (padrão 80)
#   6. (opcional) Teste de carga com k6:  bash scripts/quality-gate.sh --with-load
set -uo pipefail
cd "$(dirname "$0")/.."

COVERAGE_MIN="${COVERAGE_MIN:-80}"
WITH_LOAD=false
[ "${1:-}" = "--with-load" ] && WITH_LOAD=true

SOLUTION="Messaging.sln"
CONFIG="Release"
API_PORT="${API_PORT:-5080}"
failed=()
passed=()

run_gate() {
  local name="$1"; shift
  echo ""
  echo "=== GATE: $name ==="
  if "$@"; then
    passed+=("$name")
  else
    failed+=("$name")
  fi
}

check_coverage() {
  local py_cmd
  py_cmd=$(command -v python3 2>/dev/null || command -v python 2>/dev/null || echo "")
  if [ -z "$py_cmd" ]; then
    echo "python/python3 não encontrado. Instale Python para checar cobertura."
    return 2
  fi
  "$py_cmd" - "$COVERAGE_MIN" <<'PY'
import glob, os, re, sys
minimum = float(sys.argv[1])
files = glob.glob("TestResults/unit/**/coverage.cobertura.xml", recursive=True)
if not files:
    print("sem relatório de cobertura em TestResults/unit")
    sys.exit(2)
latest = max(files, key=os.path.getmtime)
match = re.search(r'<coverage[^>]*line-rate="([0-9.]+)"', open(latest, encoding="utf-8").read())
if not match:
    print("line-rate não encontrado em", latest)
    sys.exit(2)
rate = float(match.group(1)) * 100
print(f"cobertura de linhas: {rate:.1f}% (mínimo {minimum:.0f}%)")
sys.exit(0 if rate >= minimum else 1)
PY
}

load_test() {
  if ! command -v k6 >/dev/null 2>&1; then
    echo "k6 não encontrado. Instale (https://k6.io) para rodar o portão de carga."
    return 1
  fi
  export Webhook__Secret="load-secret"
  dotnet run --project src/Hosts/Messaging.Api -c "$CONFIG" --no-build --urls "http://localhost:${API_PORT}" >/tmp/api-load.log 2>&1 &
  local api_pid=$!
  trap 'kill "$api_pid" 2>/dev/null || true' RETURN
  for _ in $(seq 1 30); do
    curl -fsS "http://localhost:${API_PORT}/health/live" >/dev/null 2>&1 && break
    sleep 1
  done
  BASE_URL="http://localhost:${API_PORT}" WEBHOOK_SECRET="load-secret" k6 run tests/load/webhook.js
}

if [ ! -f "$SOLUTION" ]; then
  echo "Solution não encontrada. Rode: bash scripts/bootstrap.sh"
  exit 1
fi

rm -rf TestResults
run_gate "build (avisos como erro + complexidade)" dotnet build "$SOLUTION" -c "$CONFIG" -p:QualityGate=true --nologo
run_gate "arquitetura (fronteiras entre módulos)" dotnet test tests/Messaging.ArchitectureTests -c "$CONFIG" --no-build --nologo
run_gate "testes unitários" dotnet test tests/Messaging.UnitTests -c "$CONFIG" --no-build --nologo \
  --collect:"XPlat Code Coverage" --results-directory TestResults/unit
run_gate "testes de integração" dotnet test tests/Messaging.IntegrationTests -c "$CONFIG" --no-build --nologo
run_gate "cobertura >= ${COVERAGE_MIN}%" check_coverage
if [ "$WITH_LOAD" = true ]; then
  run_gate "carga (k6: p95 < 300ms, erros < 1%)" load_test
fi

echo ""
echo "================ RESUMO ================"
for g in "${passed[@]:-}"; do [ -n "$g" ] && echo "  PASSOU  $g"; done
for g in "${failed[@]:-}"; do [ -n "$g" ] && echo "  FALHOU  $g"; done
if [ "${#failed[@]}" -gt 0 ]; then
  echo "Quality gate REPROVADO."
  exit 1
fi
echo "Quality gate APROVADO."
