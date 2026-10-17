# 001 — Ingestão de webhook

- **Status:** parcialmente implementada (T-01..T-04)
- **Módulo:** Ingestion (+ Host Api)

## Problema
O provedor externo envia eventos por webhook e reenvia por até 20 minutos se não receber 200. Precisamos aceitar rápido, nunca perder evento e nunca gerar efeito duplicado.

## Fora de escopo
Parse do payload (vive atrás de adaptador versionado), processamento (spec 002), reprocessamento (spec 003).

## Requisitos
- R1 Validar a assinatura antes de qualquer efeito. Sem assinatura válida: `401`, nada gravado.
- R2 Gravar o evento bruto antes de qualquer parse, junto com a mensagem de outbox, na mesma transação.
- R3 Derivar a chave de idempotência do ID do evento do provedor (`X-Event-Id`); sem ele, do hash SHA-256 do corpo.
- R4 Repetição do mesmo evento responde `200` sem novo efeito.
- R5 Corpo máximo de 1 MiB. Provedor da rota só `[a-z0-9-]{1,50}`.
- R6 `X-Correlation-Id` propagado e devolvido.

## Critérios de aceite
- **CA1** Dado assinatura inválida, ausente, adulterada ou expirada, Quando postar, Então `401` e zero eventos/outbox.
- **CA2** Dado evento novo e válido, Quando postar, Então `200`, 1 evento bruto e 1 mensagem de outbox.
- **CA3** Dado o mesmo evento 20 vezes em paralelo, Então exatamente 1 evento e 1 outbox; todas as respostas `200`.
- **CA4** Dado corpo que não é JSON, Quando postar com assinatura válida, Então `200` e o bruto é gravado.
- **CA5** Dado provedor com nome inválido, Então `400`.

## Modelo e contratos
`RawEventAccepted(EventId, IdempotencyKey, Provider, ReceivedAt)` em `Messaging.Contracts.Ingestion`.
Tabelas (T-05): `ingestion_events_raw(UNIQUE idempotency_key)` e `ingestion_outbox(UNIQUE idempotency_key)`.

## Riscos e invariantes envolvidos
Invariantes 1, 2, 3, 5 e 6. A atomicidade entre evento bruto e outbox está na assinatura de `IRawEventStore.TryAddAsync`.

## Perguntas em aberto
- [ ] **Esquema real da assinatura do provedor** (algoritmo, cabeçalho, janela de tolerância). Hoje assumimos `t=<unix>,v1=HMAC-SHA256("{t}.{corpo}")`.
- [ ] O provedor envia um ID de evento estável? Em qual campo/cabeçalho?
- [ ] Há rotação de segredo? Precisamos aceitar dois segredos ao mesmo tempo?

## Tarefas
- [x] T-01 `IdempotencyKey`
- [x] T-02 `WebhookSignatureValidator`
- [x] T-03 `WebhookIngestionService` (+ store em memória)
- [x] T-04 Endpoint `POST /webhooks/{provider}`
- [ ] T-05 `IRawEventStore` em PostgreSQL (INSERT … ON CONFLICT DO NOTHING + outbox na mesma transação), com teste de integração via Testcontainers
- [ ] T-06 Rotação de segredo (depende da pergunta em aberto)
