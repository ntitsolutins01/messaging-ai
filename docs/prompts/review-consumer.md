# Prompt few-shot: revisar um consumidor

```
Revise o consumidor em <arquivo>. Procure efeito duplicado, perda de mensagem e processamento parcial.
Responda no formato dos exemplos. Se não houver problema, diga "Sem achados" e liste o que verificou.

Exemplo 1 (código):
  await bus.AckAsync(msg); await db.SaveChangesAsync();
Exemplo 1 (resposta):
  [BLOQUEANTE] Handler.cs:42 — ack antes do commit. Se o processo cair entre as duas linhas, a mensagem
  é perdida (já confirmada) e o efeito nunca ocorre. Correção: SaveChangesAsync primeiro, ack depois.

Exemplo 2 (código):
  await db.Records.AddAsync(new Record(key)); // sem UNIQUE
Exemplo 2 (resposta):
  [BLOQUEANTE] Handler.cs:57 — insert simples. Reentrega cria registro duplicado. Correção: UNIQUE(idempotency_key)
  e INSERT ... ON CONFLICT DO UPDATE.

Agora revise: <arquivo>
```

Por que funciona: os exemplos fixam severidade, local, causa e correção; o modelo imita a estrutura e o rigor.
