---
name: performance-optimization
description: Use quando o teste de carga reprovar ou houver suspeita de lentidão/alocação excessiva em .NET: medir primeiro, achar o gargalo real e otimizar sem quebrar as invariantes.
---

# Otimização de performance

**Regra zero: meça antes e depois. Sem número, não é otimização, é palpite.**

## Processo
1. **Reproduza** com o cenário do k6 (`tests/load/webhook.js`) e anote p95/p99 e throughput.
2. **Localize** o gargalo: tracing/logs com `CorrelationId` (módulo de observabilidade), `dotnet-trace`/`dotnet-counters`, `EXPLAIN (ANALYZE)` no PostgreSQL.
3. **Hipótese única**: mude uma coisa por vez.
4. **Meça de novo** com o mesmo cenário. Mantenha só o que melhorou de verdade.
5. **Proteja com teste**: benchmark (BenchmarkDotNet) para código quente; teste de carga como gate.

## Onde costuma estar o ganho aqui
- **Banco**: índice em `idempotency_key` (já `UNIQUE`); transação curta; lote na outbox (`FOR UPDATE SKIP LOCKED LIMIT n`); pool de conexões dimensionado; evitar N+1.
- **Request do webhook**: ler o corpo uma vez; evitar cópias (`ReadOnlySpan<byte>`, `ArrayPool`); `HMACSHA256.HashData` estático; não logar payload.
- **Async**: sem `.Result`/`.Wait()`; `CancellationToken` propagado; sem `Task.Run` em código de I/O.
- **Broker**: publicação em lote e *confirms* assíncronos; Kafka: partições e `linger.ms`/`batch.size`; RabbitMQ: `prefetch` e publisher confirms.

## Armadilhas
- Otimizar o que não é gargalo (a assinatura HMAC raramente é o problema; o banco costuma ser).
- Cache de resultado de idempotência sem invalidação correta: arrisca efeito duplicado.
- Paralelizar sem lock por chave: quebra o invariante 1.
- Trocar `UNIQUE` + upsert por checagem em memória "para ganhar velocidade": **proibido**.

## Entregável
Tabela antes/depois (cenário, p95, throughput), o que mudou e por que é seguro para as invariantes.
