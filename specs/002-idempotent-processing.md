# 002 — Processamento idempotente

- **Status:** rascunho
- **Módulo:** Processing (+ Host Worker)

## Problema
Processar o evento tem vários passos e pode falhar no meio. Não pode sobrar registro pela metade nem efeito duplicado.

## Requisitos
- R1 Consumir `RawEventAccepted` do broker.
- R2 Em **uma transação**: marcar `Processing`, fazer upsert do registro interno por `idempotency_key`, gravar a outbox de saída, marcar `Done`.
- R3 `ack`/commit de offset só depois do commit do banco.
- R4 Evento já `Done`: ack sem efeito.
- R5 Lock por chave para dois workers não pegarem o mesmo evento (`FOR UPDATE SKIP LOCKED`).
- R6 Mensagens de saída propagam a chave de idempotência.

## Critérios de aceite
- **CA1** Mesmo evento entregue 5 vezes → 1 registro interno, 1 mensagem de saída.
- **CA2** Falha injetada após o upsert e antes do commit → nada persiste; reentrega conclui com contagem 1/1.
- **CA3** Dois workers disputando o mesmo evento → só um processa.
- **CA4** Broker entrega fora de ordem → estado final correto.

## Perguntas em aberto
- [ ] Quais são os "sistemas internos" que recebem a mensagem de saída e como deduplicam?
- [ ] Qual é o modelo do "registro interno" (campos mínimos para o lançamento)?

## Tarefas
- [ ] T-01 Porta `IProcessingStore` + teste de contrato com fake e com PostgreSQL
- [ ] T-02 Handler transacional com `IFailureInjector` para o teste de chaos
- [ ] T-03 Consumer Kafka e consumer RabbitMQ atrás de `IMessageBus`
