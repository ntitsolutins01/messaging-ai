# Regra: Confiabilidade de mensageria

Estas regras protegem o objetivo da POC: **zero efeitos duplicados**.

## Transação única
O worker grava, na mesma transação de banco: (1) estado do evento → `Processing`/`Done`, (2) registro interno, (3) mensagem na outbox de saída. Se algo falha, tudo desfaz.

## Idempotência em cada efeito
- Toda mensagem carrega `IdempotencyKey`.
- Registro interno: restrição `UNIQUE (idempotency_key)` + `INSERT ... ON CONFLICT DO UPDATE` (upsert). Nunca `INSERT` simples.
- Mensagens de saída propagam a mesma chave para os sistemas internos.

## Eventos presos
- Todo evento tem `Status`, `StartedAt`, `Attempts`.
- Job de recuperação recoloca na fila eventos `Processing` além do timeout.
- Exclusão entre workers: lock por chave (`pg_advisory_xact_lock(hashtext(key))`) ou `SELECT ... FOR UPDATE SKIP LOCKED`.

## Retentativas e DLQ
- Backoff exponencial com limite de tentativas (`MaxAttempts`, padrão 5).
- Esgotadas as tentativas, vai para a DLQ com o motivo da falha.
- Reprocessamento da DLQ usa o mesmo caminho e a mesma chave.

## Webhook
- Validar assinatura, gravar evento bruto, responder 200 rápido. O processamento é assíncrono.
- Chave de idempotência derivada do ID do evento do provedor, não de timestamp.

## Proibido
- Publicar no broker dentro da transação de banco (use outbox).
- `catch` que engole exceção sem registrar e sem decidir retry/DLQ.
- Ack da mensagem antes de commitar a transação.
