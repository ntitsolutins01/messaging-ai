# Sessão 5 — Worker: transação única e idempotência garantida

---

## Objetivo

Entender como o worker processa um evento de forma que **falhar no meio, reiniciar ou receber o mesmo evento duas vezes nunca produz efeito duplicado** — e como a sequência exata dentro da transação é o que torna isso possível.

---

## Quando usar

Esses conceitos entram em jogo quando:

- Você precisa explicar por que o sistema pode ser interrompido no pior momento e ainda assim não duplicar efeitos.
- Alguém questionar a ordem das operações dentro do processamento.
- Você precisar justificar por que o `ack` do broker vem depois do commit do banco — e não antes.

---

## Quando NÃO usar

- **Não processe fora da transação** — qualquer efeito de negócio (registro interno, outbox de saída) que aconteça antes do commit pode ficar sem o rollback se o processo cair.
- **Não faça ack antes do commit** — se o banco falhar após o ack, a mensagem já saiu da fila e o evento se perde.
- **Não use INSERT simples para o registro interno** — use upsert (`INSERT ... ON CONFLICT DO UPDATE`); sem isso, a segunda entrega causa erro de UNIQUE em vez de ignorar silenciosamente.

---

## O que ela faz

### O problema: processamento com múltiplos passos

Processar um evento exige vários passos. Se o processo cair no meio, o que sobrou?

```
Passo 1: marcar evento como Processing     ← estado intermediário
Passo 2: upsert do registro interno        ← efeito de negócio
Passo 3: gravar outbox de saída            ← comunicação com outros sistemas
Passo 4: marcar evento como Done           ← estado final
Passo 5: ack para o broker                 ← liberar a mensagem da fila
```

Se o worker cair entre qualquer um desses passos **sem transação**, o banco fica inconsistente: o evento pode estar como `Processing` para sempre, o registro interno pode existir sem a mensagem de saída, ou a mensagem de saída pode existir sem o registro interno.

---

### A solução: tudo em uma transação, ack só no final

A invariante central da POC (Invariante 2):

> **Estado + efeito interno + outbox gravam na mesma transação.**

```
BEGIN TRANSACTION
  ① UPDATE processing_events SET status = 'Processing', started_at = now, attempts = attempts + 1
     WHERE id = @id AND status IN ('Received', 'Processing')

  ② INSERT INTO processing_internal_records (idempotency_key, ...)
     ON CONFLICT (idempotency_key) DO UPDATE SET ...   ← upsert: nunca INSERT simples

  ③ INSERT INTO processing_outbox (idempotency_key, ...)
     ON CONFLICT (idempotency_key) DO NOTHING

  ④ UPDATE processing_events SET status = 'Done'
     WHERE id = @id
COMMIT
  ⑤ ack para o broker  ← só depois do commit
```

**Por que essa ordem importa:**

- Se o processo cair antes do `COMMIT`: o banco faz rollback automático. Nada persiste. O broker reentrega a mensagem (ainda não teve ack). O worker processa de novo — e como o upsert é idempotente, o resultado é exatamente o mesmo.
- Se o processo cair após o `COMMIT` e antes do `ack`: o broker reentrega. O worker tenta processar de novo — mas o upsert detecta que o registro já existe (`ON CONFLICT`) e a transação completa sem efeito novo. O `Done` já estava gravado; o estado final é correto.

---

### Idempotência no registro interno: upsert obrigatório

A Invariante 3 do projeto:

> **Escrita com `upsert` + `UNIQUE`, nunca `insert` simples.**

```sql
-- ERRADO — falha na segunda entrega:
INSERT INTO processing_internal_records (idempotency_key, amount, ...)
VALUES (@key, @amount, ...);
-- Segunda vez: UNIQUE violation → exception → rollback → reenfileira → loop infinito

-- CORRETO — idempotente:
INSERT INTO processing_internal_records (idempotency_key, amount, ...)
VALUES (@key, @amount, ...)
ON CONFLICT (idempotency_key) DO UPDATE
  SET processed_at = EXCLUDED.processed_at;
-- Segunda vez: atualiza sem erro, transação continua, Done gravado, ack enviado
```

O `ON CONFLICT DO UPDATE` (upsert) garante que a segunda entrega — seja por retry do broker, seja por queda do worker — completa sem duplicar o efeito de negócio.

---

### Exclusão mútua entre workers: `FOR UPDATE SKIP LOCKED`

Com múltiplos workers consumindo a mesma fila, dois podem receber o mesmo evento ao mesmo tempo. Sem lock, os dois executariam a transação em paralelo e o segundo poderia gravar em cima do primeiro.

```sql
-- Worker A e Worker B recebem o mesmo evento:
SELECT * FROM processing_events
WHERE id = @id AND status = 'Received'
FOR UPDATE SKIP LOCKED;
-- Worker A: obtém o lock → processa → commit → ack
-- Worker B: SKIP LOCKED → não encontra a linha → ignora → ack (sem processar)
```

`FOR UPDATE`: bloqueia a linha para que nenhum outro worker possa processá-la ao mesmo tempo.
`SKIP LOCKED`: em vez de esperar pelo lock (o que causaria contenção), simplesmente pula a linha e não processa.

---

### O ciclo de vida do evento: `EventStatus`

**Arquivo:** `src/Modules/Messaging.Processing/EventStatus.cs`

```
Received → Processing → Done
                     ↘ Failed → DLQ
```

| Status | Significado |
|---|---|
| `Received` | mensagem chegou do broker, ainda não iniciou processamento |
| `Processing` | worker está processando — se ficar aqui além do timeout, está preso |
| `Done` | processado com sucesso — segunda entrega é ignorada (ack sem efeito) |
| `Failed` | falhou após esgotar as tentativas — vai para a DLQ |

O `EventState` carrega os dados necessários para a decisão de recuperação:

```csharp
public sealed record EventState(
    Guid Id,
    EventStatus Status,
    DateTimeOffset? StartedAt,  // quando entrou em Processing
    int Attempts);              // quantas vezes foi tentado
```

---

### Eventos presos: `StuckEventDetector`

**Arquivo:** `src/Modules/Messaging.Processing/StuckEventDetector.cs`

O worker pode cair entre o `Processing` e o `Done`. O evento fica com `status = 'Processing'` para sempre. Um **job de recuperação** roda periodicamente e usa `StuckEventDetector` para decidir o que fazer:

```csharp
public RecoveryAction Decide(EventState state, DateTimeOffset now)
{
    // Só atua em eventos presos (status = Processing)
    if (state.Status != EventStatus.Processing) return RecoveryAction.None;

    // Dentro do timeout normal: talvez ainda esteja processando
    if (state.StartedAt is { } startedAt && now - startedAt < _timeout)
        return RecoveryAction.None;

    // Preso além do timeout: tentativas esgotadas → DLQ; senão → recoloca na fila
    return state.Attempts >= _maxAttempts
        ? RecoveryAction.MoveToDlq
        : RecoveryAction.Requeue;
}
```

**Por que isso é seguro?** Porque o processamento é idempotente. Recolocar na fila (`Requeue`) é seguro — se o worker original ainda estiver rodando, a exclusão com `FOR UPDATE SKIP LOCKED` garante que só um processa. Se o worker já caiu, o novo processa do zero.

**Três decisões possíveis:**

```
None       → evento não está preso ou ainda está dentro do timeout
Requeue    → preso além do timeout, mas ainda tem tentativas → volta à fila
MoveToDlq  → preso com tentativas esgotadas → vai para DLQ (Sessão 6)
```

---

### Ack-after-commit: a regra mais importante

A Invariante da spec 002 (R3):

> **`ack`/commit de offset só depois do commit do banco.**

```
❌ ERRADO (ack antes):
  broker.Ack()         ← mensagem removida da fila
  BEGIN TRANSACTION
    upsert...
  COMMIT               ← se falhar aqui: evento perdido para sempre

✅ CORRETO (ack depois):
  BEGIN TRANSACTION
    upsert...
  COMMIT               ← se falhar aqui: broker reentrega → idempotência resolve
  broker.Ack()         ← só agora remove da fila
```

A consequência é at-least-once: o evento pode ser entregue mais de uma vez. A idempotência do upsert é o que torna isso seguro.

---

### O que já existe vs. o que está pendente

| Componente | Status | Arquivo |
|---|---|---|
| `EventStatus` + `EventState` + `RecoveryAction` | ✅ implementado | `src/Modules/Messaging.Processing/EventStatus.cs` |
| `StuckEventDetector` | ✅ implementado (6 testes) | `src/Modules/Messaging.Processing/StuckEventDetector.cs` |
| `IEventReprocessor` (porta pública) | ✅ implementado | `src/Contracts/Messaging.Contracts/Processing/IEventReprocessor.cs` |
| `ReprocessEventHandler` | ✅ implementado (2 testes) | `src/Modules/Messaging.Operations/ReprocessEventHandler.cs` |
| `IProcessingStore` (transação completa) | ⏳ spec 002, T-01 | — |
| Handler transacional com `IFailureInjector` | ⏳ spec 002, T-02 | — |
| Consumer Kafka/RabbitMQ | ⏳ spec 002, T-03 | — |
| Job de recuperação com lock | ⏳ spec 003, T-02 | — |

---

## O que ela NÃO faz

- O worker não faz parse do schema do provedor — recebe `RawEventAccepted` (sem payload) e busca o evento bruto pelo `EventId` quando precisa do conteúdo.
- O worker não publica diretamente no broker — a saída vai para a outbox de saída na mesma transação.
- O `StuckEventDetector` não executa nada — é uma classe de decisão pura. Quem executa é o job de recuperação (pendente).
- Não há backoff exponencial implementado ainda — está previsto na spec 003.

---

## Argumentos / configuração

| Parâmetro | Padrão | Onde configurar |
|---|---|---|
| Timeout de evento preso | 5 minutos | `new StuckEventDetector(TimeSpan.FromMinutes(5), ...)` |
| Máximo de tentativas | 5 | `new StuckEventDetector(..., maxAttempts: 5)` |
| Lock de exclusão | `FOR UPDATE SKIP LOCKED` | implementação de `IProcessingStore` (pendente) |

---

## Exemplo de uso

### Rodar os testes do `StuckEventDetector`

```bash
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "StuckEventDetector" -v normal

# 6 testes:
# - evento Received/Done/Failed → None (não interfere)
# - Processing dentro do timeout → None
# - Processing além do timeout → Requeue
# - Processing com tentativas esgotadas → MoveToDlq
# - Processing sem StartedAt → Requeue (trata como preso)
# - timeout inválido → ArgumentOutOfRangeException
```

### Simular o ciclo de vida na cabeça (walkthrough)

```
1. Broker entrega RawEventAccepted(id="evt_123", key="acme:evt_123")
2. Worker inicia transação:
   - UPDATE status = 'Processing', attempts = 1
   - INSERT internal_records ON CONFLICT DO UPDATE  ← upsert
   - INSERT outbox_saida ON CONFLICT DO NOTHING
   - UPDATE status = 'Done'
3. COMMIT
4. broker.Ack()  ← só aqui

5. Broker reentrega (at-least-once):
   - Worker inicia transação:
   - UPDATE status = 'Processing' WHERE status IN ('Received','Processing')
     → evento já está 'Done' → 0 linhas afetadas → transação vazia
   - COMMIT
   - broker.Ack()  ← sem efeito novo
```

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Rodar os testes do processamento implementado
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "StuckEventDetector|ReprocessEventHandler" -v normal

# 2. Resultado esperado: 8 testes passando
#    StuckEventDetectorTests    → 6 testes
#    ReprocessEventHandlerTests → 2 testes

# 3. Arquivos-chave para walkthrough ao vivo:
#    src/Modules/Messaging.Processing/EventStatus.cs       ← ciclo de vida
#    src/Modules/Messaging.Processing/StuckEventDetector.cs ← decisão de recuperação
#    src/Contracts/Messaging.Contracts/Processing/IEventReprocessor.cs ← porta pública
#    src/Modules/Messaging.Operations/ReprocessEventHandler.cs ← caso de uso admin
#    specs/002-idempotent-processing.md                    ← o que ainda falta
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"O que acontece se o worker cair no meio do processamento?"**
— O evento fica com `status = 'Processing'`. Um job de recuperação detecta que está preso (além do timeout de 5 minutos) e o recoloca na fila. O broker reentrega. O worker processa de novo — mas como usa upsert e a transação é idêntica, o resultado final é o mesmo: 1 registro interno, 1 mensagem de saída.

**"Por que o ack vem depois do commit do banco?"**
— Se o ack viesse antes e o banco falhasse no commit, a mensagem teria saído da fila sem o processamento ter sido completado — perda permanente. Com ack depois do commit, uma falha no banco faz o broker reentrigar a mensagem. O upsert garante que o reprocessamento não duplica o efeito.

**"Como dois workers não processam o mesmo evento ao mesmo tempo?"**
— `SELECT ... FOR UPDATE SKIP LOCKED`. O primeiro worker que pegar a linha bloqueia. O segundo, em vez de esperar, simplesmente "pula" a linha bloqueada e não processa. Sem contenção, sem deadlock, sem duplicata.

**"Por que usar upsert e não um INSERT simples?"**
— INSERT simples na segunda entrega causaria erro de UNIQUE constraint → exception → rollback → o evento volta à fila → loop infinito. Com `INSERT ... ON CONFLICT DO UPDATE`, a segunda tentativa completa normalmente sem criar um novo registro, e o worker pode marcar `Done` e enviar o ack.

---

## Próxima sessão

**Sessão 6 — DLQ e reprocessamento manual**

Quando um evento falha várias vezes e esgota as tentativas, vai para a Dead Letter Queue. Vamos ver o caminho de reprocessamento manual pela API admin — e por que usar a mesma chave de idempotência garante que reprocessar duas vezes não duplica o efeito.
