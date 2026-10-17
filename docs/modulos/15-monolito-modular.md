# 15 — Arquitetura: monólito modular

## Estrutura (ADR 0001)
```
src/Shared/Messaging.SharedKernel      # IdempotencyKey, IClock, Telemetry (sem dependências)
src/Contracts/Messaging.Contracts      # único canal entre módulos
src/Modules/Messaging.Ingestion        # webhook → evento bruto + outbox
src/Modules/Messaging.Processing       # recuperação de presos, (worker idempotente: spec 002)
src/Modules/Messaging.Operations       # reprocessamento manual
src/Hosts/Messaging.Api                # composição
```

## Como é imposto
`tests/Messaging.ArchitectureTests/ModuleBoundaryTests.cs` (NetArchTest): nenhum módulo depende de outro, `SharedKernel` não depende de nada, e **nenhum projeto conhece Kafka/RabbitMQ**. Quebrar a fronteira quebra o quality gate.

## Regras
`.claude/rules/architecture.md` e `.claude/rules/modular-monolith.md` (como criar módulo, comunicação síncrona só por porta de `Contracts`, tabelas com prefixo por módulo, quando extrair para serviço).

## Pendências
Worker host, adapters `Messaging.Brokers.*`, persistência PostgreSQL (specs 001 T-05, 002, 003).
