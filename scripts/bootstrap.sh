#!/usr/bin/env bash
# Cria a solution e adiciona todos os projetos. Rode uma vez: bash scripts/bootstrap.sh
set -euo pipefail
cd "$(dirname "$0")/.."

# SDK 8 gera .sln por padrão; SDK 9+ gera .slnx, então força o formato clássico.
if [ ! -f Messaging.sln ]; then
  dotnet new sln -n Messaging --format sln 2>/dev/null || dotnet new sln -n Messaging
fi
find src tests -name '*.csproj' -print0 | xargs -0 dotnet sln Messaging.sln add
dotnet restore
echo "Solution pronta. Próximo passo: bash scripts/quality-gate.sh"
