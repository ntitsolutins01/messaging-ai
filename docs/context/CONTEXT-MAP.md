# Mapa de contexto: o que carregar em cada tarefa

| Tarefa | Carregue | Não carregue |
|---|---|---|
| Nova funcionalidade | `specs/NNN-*.md`, `CLAUDE.md`, `rules/tdd.md` | ADRs, evals, observability |
| Mexer em consumidor/outbox/transação | `rules/messaging-reliability.md`, skill `outbox-idempotency` | specs não relacionadas |
| Adapter de broker | skill `kafka-vs-rabbitmq`, `rules/architecture.md` | docs de segurança |
| Endpoint / webhook | `rules/api-conventions.md`, `rules/security.md`, spec 001 | adapters de broker |
| Nova dependência entre módulos | `rules/modular-monolith.md`, ADR 0001, `ModuleBoundaryTests.cs` | tudo o mais |
| Bug de duplicidade | skill `outbox-idempotency`, agente `chaos-tester` | specs de UI/ops |
| Investigar erro em runtime | skill `observability-debugging`, agente `observability-analyst` | specs |
| Segurança / pentest | skill `owasp-top10`, agente `security-auditor` / `pentest-agent` | specs de ops |
| Performance | skill `performance-optimization`, `load-testing` | specs |
| Antes do PR | `/review`, `/quality-gate` | — |

## Orçamento de contexto
- `CLAUDE.md` + rules sempre ativas: manter abaixo de ~3 mil tokens.
- Leia arquivos por trecho. Prefira `Grep` a abrir o arquivo inteiro.
- Saída de comando longa (build, logs): filtre com `grep`/`tail` antes de trazer ao chat.

## Fontes de verdade
- Requisitos: `specs/`
- Decisões de arquitetura: `docs/adr/`
- Invariantes e comandos: `CLAUDE.md`
- Estado da sessão anterior: `docs/context/HANDOFF.md` (gerado por `/handoff`, não versionado)
