# 09 — Quality Gate

**Tema:** portões que validam o software antes de avançar: carga, complexidade ciclomática, estrutura de dependências, cobertura.

## Os portões (`bash scripts/quality-gate.sh`)
| Portão | Como |
|---|---|
| Build sem avisos + **complexidade ciclomática ≤ 10** | `-p:QualityGate=true` + `CA1502` (`CodeMetricsConfig.txt`) |
| **Estrutura de dependências** | `tests/Messaging.ArchitectureTests` (NetArchTest) |
| Testes unitários e de integração | `dotnet test` |
| **Cobertura ≥ 80%** (`COVERAGE_MIN`) | coverlet (cobertura XML) |
| **Carga** (opcional, `--with-load`) | `tests/load/webhook.js` com k6: p95 < 300 ms, erros < 1% |

Também: agente `quality-gate`, comando `/quality-gate`, skills `load-testing`, `cyclomatic-complexity`, `performance-optimization`, e `.github/workflows/ci.yml`.

## Ressalva
O script **não foi executado** (sem SDK .NET no ambiente de criação). Limites (80%, 10, 300 ms) são pontos de partida: ajuste com medição, nunca para "passar".
