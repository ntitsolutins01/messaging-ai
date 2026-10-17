# ADR 0002: Outbox em vez de dual write

- **Status:** aceito
- **Contexto:** gravar no banco e publicar no broker são duas operações. Se o processo cai entre elas, há evento perdido ou efeito duplicado.
- **Decisão:** toda publicação passa por uma tabela `outbox` gravada na **mesma transação** do estado. Um publisher lê `published_at IS NULL` com `FOR UPDATE SKIP LOCKED` e publica. Entrega é *at-least-once*; a deduplicação é pela chave de idempotência.
- **Consequências:**
  - (+) Sem perda: se o commit aconteceu, a mensagem existe e será publicada.
  - (+) Broker fora do ar não derruba o webhook; a outbox acumula e drena depois.
  - (−) Latência extra do publisher e uma tabela a mais para operar e limpar.
  - (−) Consumidores precisam ser idempotentes (já é invariante do projeto).
