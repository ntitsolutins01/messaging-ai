---
name: outbox-idempotency
description: Use ao implementar ou revisar outbox, idempotência, transação única do worker, upsert com chave única ou recuperação de eventos presos em .NET + PostgreSQL.
---

# Outbox + Idempotência (.NET / PostgreSQL)

## Tabelas mínimas
```sql
CREATE TABLE events_raw (
  id uuid PRIMARY KEY,
  idempotency_key text NOT NULL UNIQUE,
  payload jsonb NOT NULL,
  schema_version text,
  status text NOT NULL,            -- Received | Processing | Done | Failed
  attempts int NOT NULL DEFAULT 0,
  started_at timestamptz,
  last_error text,
  received_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE outbox (
  id uuid PRIMARY KEY,
  idempotency_key text NOT NULL UNIQUE,
  topic text NOT NULL,
  payload jsonb NOT NULL,
  published_at timestamptz
);

CREATE TABLE internal_records (
  id uuid PRIMARY KEY,
  idempotency_key text NOT NULL UNIQUE,
  data jsonb NOT NULL,
  updated_at timestamptz NOT NULL DEFAULT now()
);
```

## Recepção do webhook
`INSERT INTO events_raw ... ON CONFLICT (idempotency_key) DO NOTHING`. Se não inseriu, é repetição: responda 200 sem enfileirar de novo.

## Worker (uma transação)
1. `BEGIN`
2. `SELECT ... FROM events_raw WHERE id = @id FOR UPDATE SKIP LOCKED` (se não vier linha, outro worker já tem o evento)
3. Se `Done`, `COMMIT` e ack (já processado).
4. Marcar `Processing`, `started_at = now()`, `attempts = attempts + 1`.
5. Upsert em `internal_records`: `INSERT ... ON CONFLICT (idempotency_key) DO UPDATE ...`.
6. `INSERT INTO outbox ... ON CONFLICT (idempotency_key) DO NOTHING`.
7. Marcar `Done`. `COMMIT`. Só então ack no broker.

## Publisher da outbox
Lê `published_at IS NULL` com `FOR UPDATE SKIP LOCKED`, publica no broker com a chave de idempotência, marca `published_at`. Entrega é *at-least-once*; o consumidor final deduplica pela chave.

## Recuperação de presos
```sql
UPDATE events_raw SET status = 'Received'
WHERE status = 'Processing' AND started_at < now() - interval '5 minutes';
```
Os eventos recuperados são republicados na fila. Seguro porque o processamento é idempotente.

## Checklist de revisão
- [ ] Ack só depois do commit?
- [ ] Todo efeito tem UNIQUE + upsert?
- [ ] Publicação no broker só pela outbox?
- [ ] Teste de chaos cobre repetição e queda do worker?
