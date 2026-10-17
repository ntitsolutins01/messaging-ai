# Messaging POC — Webhook → Fila → Worker (Kafka / RabbitMQ)

POC em **.NET 8** como **monólito modular**. Recebe webhooks, grava o evento bruto, publica em um broker (Kafka **ou** RabbitMQ, atrás de uma abstração) e processa com worker idempotente.

**Objetivo:** provar **zero efeitos duplicados**, mesmo com reenvio de webhook por 20 minutos, worker caindo no meio e reprocessamento manual.

## Fluxo
```
Provedor --webhook--> Api: valida assinatura, grava evento bruto + outbox (1 transação), responde 200
   Outbox --> Broker (Kafka | RabbitMQ) --> Worker: estado + registro interno + outbox de saída (1 transação)
   falhou N vezes --> DLQ --> API admin de reprocessamento (mesmo caminho, mesma chave)
```

## Estrutura (monólito modular — ADR 0001)
```
src/
  Shared/Messaging.SharedKernel/   # IdempotencyKey, IClock. Sem dependências.
  Contracts/Messaging.Contracts/   # único canal público entre módulos (eventos e portas)
  Modules/
    Messaging.Ingestion/           # webhook: assinatura, evento bruto, outbox
    Messaging.Processing/          # processamento idempotente, recuperação de presos, DLQ
    Messaging.Operations/          # reprocessamento manual
  Hosts/Messaging.Api/             # composição (webhook + admin). Worker: spec 002
tests/  TestSupport · UnitTests · ArchitectureTests · IntegrationTests
specs/  docs/  evals/  scripts/  observability/  ops/
```

## Comandos
- Primeira vez: `bash scripts/bootstrap.sh` (cria a solution)
- Infra: `docker compose up -d` · Observabilidade: `docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d`
- Testes: `dotnet test` · **Gate completo: `bash scripts/quality-gate.sh`**
- Broker: `Messaging__Broker=Kafka|RabbitMq` · Segredo do webhook: `Webhook__Secret` (user-secrets/env)

## Invariantes (nunca violar)
1. Todo evento tem **chave de idempotência** única; ela vale para cada efeito.
2. Estado + efeito interno + outbox gravam **na mesma transação**.
3. Escrita com `upsert` + `UNIQUE`, nunca `insert` simples.
4. Módulos só se falam por `Messaging.Contracts`. Nenhum módulo conhece Kafka/RabbitMQ.
5. Evento bruto é gravado **antes** de qualquer parse; o parse fica atrás de adaptador versionado.
6. Sem segredos no repositório. Webhook sem assinatura válida nunca tem efeito.

## Como trabalhar aqui (fluxo de engenharia)
1. **Spec primeiro** (`/spec`) → 2. **TDD** (`/tdd`: RED → GREEN → REFACTOR) → 3. **`/review`** → 4. **`/quality-gate`** antes do PR.
- Mudou consumidor, outbox ou transação? Leia `.claude/rules/messaging-reliability.md` e acione `messaging-reviewer`.
- Contexto: `docs/context/CONTEXT-MAP.md` diz o que carregar em cada tarefa. Fim de sessão longa: `/handoff`.

## Mapa Claude Code (módulos DAPIA)
Veja `docs/modulos/README.md`: cada um dos 15 módulos aponta os arquivos que o implementam.
Regras: `.claude/rules/` · Comandos: `.claude/commands/` · Skills: `.claude/skills/` · Agentes: `.claude/agents/` · MCP: `.mcp.json` · Hooks: `.claude/settings.json` + `.claude/hooks/` · Cursor: `.cursor/rules/`
