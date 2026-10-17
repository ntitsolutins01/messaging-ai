---
name: load-testing
description: Use ao criar, rodar ou interpretar testes de carga (k6) do webhook e do worker: cenários, thresholds, gargalos e como ler os resultados.
---

# Teste de carga (k6)

Arquivo base: `tests/load/webhook.js`. Rodar: `bash scripts/quality-gate.sh --with-load` ou direto com `k6 run`.

## Cenários que importam neste sistema
- **Eventos únicos** (`constant-arrival-rate`): mede throughput real. Use *arrival rate*, não VUs fixos, para não mascarar lentidão (modelo aberto).
- **Tempestade de reenvios**: o mesmo evento muitas vezes. Valida que duplicata é barata e sempre `200`.
- **Pico (spike)**: `ramping-arrival-rate` de 10 → 10x em segundos; observa fila, banco e recuperação.
- **Soak**: taxa moderada por horas; procura vazamento de memória e crescimento da outbox.

## Thresholds (o gate)
`http_req_failed: rate<0.01` · `http_req_duration: p(95)<300` · `unexpected_status: count==0`.
Ajuste os números com base na medição e registre em `docs/decisao-broker.md`; nunca afrouxe só para passar.

## Como ler o resultado
- `p(95)` e `p(99)` importam mais que a média.
- `dropped_iterations > 0`: o sistema não aguentou a taxa pedida.
- `http_req_failed` alto com `401`: problema de assinatura/relógio no teste, não de carga.
- Latência cresce com o tempo: fila ou outbox acumulando; olhe o banco e o publisher.

## Gargalos usuais
1. Banco: falta de índice em `idempotency_key`, transações longas, pool pequeno.
2. Serialização/alocação por request (ver skill `performance-optimization`).
3. Publisher da outbox lendo um registro por vez (use lote).
4. Broker: partições insuficientes (Kafka) ou prefetch mal ajustado (RabbitMQ).

## Regras
- Carga só contra ambiente local/descartável. Nunca contra produção ou contra serviço de terceiros.
- Meça antes e depois de qualquer otimização com o mesmo cenário.
