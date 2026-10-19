# Sessão 3 — Outbox Pattern: por que existe e como funciona

---

## Objetivo

Entender por que não publicamos diretamente no Kafka/RabbitMQ após gravar o evento — e como o Outbox Pattern resolve o problema fundamental de consistência entre banco de dados e broker de mensagens.

---

## Quando usar

Esse padrão entra em jogo sempre que:

- Você precisa garantir que uma mensagem **nunca se perde** mesmo que o processo caia no pior momento.
- O broker estiver fora do ar mas você não quer que isso derrube a API de webhook.
- Você precisar explicar por que gravar no banco e publicar no broker não pode ser feito "em sequência".

---

## Quando NÃO usar

- **Quando a mensagem não precisa de garantia de entrega** — notificações de UI, logs de debug, eventos puramente informativos sem consequência de negócio.
- **Quando a operação é idempotente e o reenvio é trivial** — casos onde repetir a chamada não causa nenhum dano e o custo do Outbox não se justifica.
- **Não use o broker dentro da transação de banco** — essa é a forma errada que o Outbox existe para evitar; Kafka/RabbitMQ não participam de transações ACID.

---

## O que ela faz

### O problema que o Outbox resolve

Imagine a sequência "ingênua" — sem Outbox:

```
1. BEGIN TRANSACTION
2.   INSERT INTO raw_events (...)      ← salva no banco
3. COMMIT
4. broker.Publish(RawEventAccepted)    ← publica no Kafka/RabbitMQ
```

**O que pode dar errado entre os passos 3 e 4?**

```
✅ Commit ocorreu
💥 Processo cai / rede cai / broker fora do ar
❌ Mensagem NUNCA é publicada
❌ Worker nunca processa esse evento
❌ Evento se perde silenciosamente
```

Esse é o **dual write problem**: escrever em dois sistemas (banco + broker) sem garantia de atomicidade.

---

### A solução: Outbox como parte da transação

Com o Outbox Pattern, a mensagem destinada ao broker é gravada **na mesma tabela do banco**, na mesma transação:

```
1. BEGIN TRANSACTION
2.   INSERT INTO ingestion_raw_events (...)      ← evento bruto
3.   INSERT INTO ingestion_outbox (...)          ← mensagem para o broker
4. COMMIT                                        ← os dois entram juntos ou nenhum
```

Depois, um processo separado — o **relay de outbox** — lê a tabela `ingestion_outbox` e publica no broker:

```
5. SELECT * FROM ingestion_outbox
     WHERE published_at IS NULL
     FOR UPDATE SKIP LOCKED                      ← exclusão entre workers
6. broker.Publish(mensagem)
7. UPDATE ingestion_outbox SET published_at = NOW()
```

```
ANTES (dual write):          DEPOIS (outbox):
banco ──┐                    banco ──────────────────┐
        ├─ janela de falha   outbox (dentro do banco) ┤
broker ─┘                    relay ──────────────────▶ broker
```

**Garantia:** se o COMMIT aconteceu, a mensagem está na outbox e **será publicada** — mesmo que o relay precise tentar várias vezes.

---

### Como está implementado nesta POC

A POC usa a mesma lógica, mas com uma implementação em memória enquanto o PostgreSQL real não está ligado.

**Contrato** (`IRawEventStore`):

```csharp
// Os dois entram juntos ou nenhum entra.
Task<bool> TryAddAsync(RawEvent rawEvent, RawEventAccepted outboxMessage, CancellationToken ct);
```

A assinatura do método já enforça a invariante: não existe API para gravar o evento sem a mensagem de outbox junto.

**Implementação em memória** (`InMemoryRawEventStore`):

```csharp
// ConcurrentDictionary.TryAdd é atômico — simula o UNIQUE + transação do PostgreSQL
var inserted = _events.TryAdd(rawEvent.Key.Value, rawEvent);
if (inserted)
{
    _outbox.Enqueue(outboxMessage);   // só enfileira se o evento foi inserido
}
return Task.FromResult(inserted);
```

**Em produção (PostgreSQL)** — o mesmo contrato, com SQL real:

```sql
-- Tudo em uma transação:
BEGIN;
  INSERT INTO ingestion_raw_events (id, idempotency_key, provider, payload, received_at)
  VALUES (@id, @key, @provider, @payload, @now)
  ON CONFLICT (idempotency_key) DO NOTHING;   -- ← idempotente

  INSERT INTO ingestion_outbox (id, idempotency_key, provider, received_at, published_at)
  VALUES (@id, @key, @provider, @now, NULL)
  ON CONFLICT DO NOTHING;
COMMIT;
```

**O relay de outbox** (a ser implementado na spec 002):

```sql
-- Pega próxima mensagem sem concorrência entre múltiplos workers
SELECT * FROM ingestion_outbox
WHERE published_at IS NULL
ORDER BY created_at
LIMIT 1
FOR UPDATE SKIP LOCKED;

-- Após publicar no broker:
UPDATE ingestion_outbox SET published_at = NOW() WHERE id = @id;
```

`FOR UPDATE SKIP LOCKED` é a chave: dois relays rodando em paralelo nunca pegam a mesma mensagem — um "pula" a linha que o outro está processando.

---

### Entrega at-least-once e a consequência

O Outbox garante **at-least-once** — a mensagem chega ao consumidor **pelo menos uma vez**, mas pode chegar **mais de uma vez**:

```
Cenário: relay publica no broker, broker confirma, mas relay cai antes de marcar published_at
→ Próxima execução do relay publica de novo a mesma mensagem
→ Worker recebe duas vezes
```

**Isso é aceitável porque o worker é idempotente** — a chave de idempotência garante que processar duas vezes produz o mesmo resultado que processar uma vez. O Outbox e a idempotência são complementares: um não funciona sem o outro.

```
Outbox  →  garante que a mensagem CHEGA (at-least-once)
Idempotência  →  garante que processar DUAS vezes não causa dano
```

---

### Broker fora do ar — o que acontece

Sem Outbox:
```
Broker fora do ar → publicação falha → API retorna 5xx → provedor reenvia → caos
```

Com Outbox:
```
Broker fora do ar → relay falha ao publicar → mensagem continua na outbox
                 → API continua respondendo 200 normalmente
                 → outbox acumula enquanto broker está fora
                 → broker volta → relay drena a outbox → zero perda
```

Esse comportamento é descrito no ADR 0002:
> "(+) Broker fora do ar não derruba o webhook; a outbox acumula e drena depois."

---

### Custo e trade-offs (do ADR 0002)

| Vantagem | Desvantagem |
|---|---|
| Sem perda: se o commit aconteceu, a mensagem será publicada | Latência extra: mensagem não chega instantaneamente ao consumidor |
| Broker fora do ar não derruba a API | Tabela de outbox cresce e precisa de limpeza periódica |
| Tudo em uma transação ACID | Relay é mais uma peça para operar e monitorar |
| Consumidores podem ser idempotentes com segurança | Consumidores **precisam** ser idempotentes (já é invariante) |

---

## O que ela NÃO faz

- Não publica diretamente no broker — quem publica é o relay, que ainda será implementado (spec 002, T-03).
- Não garante entrega *exactly-once* — garante *at-least-once*. A idempotência no consumidor completa a garantia.
- Não remove mensagens da outbox automaticamente — o relay marca `published_at`; limpeza periódica remove as antigas.
- A implementação em memória (`InMemoryRawEventStore`) não persiste após reinício — é só para testes e desenvolvimento.

---

## Argumentos / configuração

| Componente | Status na POC | Onde implementar |
|---|---|---|
| Gravação evento + outbox | ✅ implementado (em memória) | `InMemoryRawEventStore` → PostgreSQL (spec 001, T-05) |
| Relay outbox → broker | ⏳ pendente | spec 002, T-03 (`IMessageBus`) |
| Limpeza da outbox | ⏳ pendente | job periódico com DELETE WHERE published_at < NOW() - interval |
| `FOR UPDATE SKIP LOCKED` | ⏳ pendente | implementação PostgreSQL do relay |

---

## Exemplo de uso

### Confirmar que o evento e a outbox entram juntos (teste unitário)

```bash
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "WebhookIngestionService" -v normal
```

Observe os dois asserts do teste `Dado_evento_novo_Quando_ingerir_Entao_grava_evento_bruto_e_outbox`:

```csharp
outcome.Should().Be(IngestionOutcome.Accepted);
_store.EventCount.Should().Be(1);   // evento bruto gravado
_store.OutboxCount.Should().Be(1);  // mensagem de outbox gravada junto
```

### Confirmar que duplicata não duplica a outbox

Observe `Dado_mesmo_evento_repetido_Quando_ingerir_Entao_duplicata_sem_novo_efeito`:

```csharp
_store.EventCount.Should().Be(1);   // continua 1
_store.OutboxCount.Should().Be(1);  // continua 1 — outbox não duplicou
```

### Ler o ADR que documenta a decisão

```bash
cat docs/adr/0002-outbox-over-dual-write.md
```

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Rodar os testes que provam o comportamento do outbox
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "WebhookIngestionService" -v normal

# 2. Arquivos-chave para walkthrough ao vivo:
#    docs/adr/0002-outbox-over-dual-write.md            ← decisão arquitetural
#    src/Modules/Messaging.Ingestion/IRawEventStore.cs  ← contrato que enforça a invariante
#    src/Modules/Messaging.Ingestion/InMemoryRawEventStore.cs ← implementação atual
#    specs/002-idempotent-processing.md                 ← onde o relay será implementado

# 3. Para ver o dual write problem na prática (demonstração mental):
#    Pergunte: "o que acontece se o processo cair entre o COMMIT e o broker.Publish?"
#    Resposta sem outbox: mensagem perdida.
#    Resposta com outbox: relay tenta de novo na próxima execução.
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"Por que não publicar no Kafka direto depois de salvar no banco?"**
— Porque se o processo cair entre os dois passos, o banco tem o evento mas o Kafka não tem a mensagem. O worker nunca processa esse evento e ele se perde silenciosamente, sem erro visível. Com Outbox, a mensagem para o Kafka entra na mesma transação do banco — ou os dois acontecem ou nenhum.

**"O que é o relay de outbox?"**
— É um processo que fica lendo a tabela de outbox do banco procurando mensagens com `published_at = NULL`, publica no Kafka/RabbitMQ e marca como publicada. Se o broker estiver fora do ar, ele simplesmente tenta de novo depois — sem perder nada, sem derrubar a API.

**"Se o relay publicar a mesma mensagem duas vezes, o sistema duplica o processamento?"**
— Não. O Outbox garante *at-least-once* — a mensagem pode chegar mais de uma vez. Mas o worker é idempotente: processar duas vezes com a mesma chave de idempotência produz exatamente o mesmo resultado que processar uma vez. Os dois padrões se complementam.

**"Qual é o custo dessa abordagem?"**
— Uma tabela extra no banco, um processo relay para operar e monitorar, e uma latência pequena extra (a mensagem não vai instantaneamente ao broker — passa pela tabela primeiro). Para a garantia de zero perda, esse custo é baixo e previsível.

---

## Próxima sessão

**Sessão 4 — Broker: Kafka e RabbitMQ com troca por variável de ambiente**

Vamos ver como o projeto abstrai o broker atrás de `IMessageBus` e como trocar de Kafka para RabbitMQ sem alterar uma linha de código de domínio.
