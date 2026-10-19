# Sessão 7 — Chaos Tests: os 6 cenários de falha

---

## Objetivo

Entender por que testes unitários não são suficientes para provar zero efeitos duplicados — e como os 6 cenários de chaos obrigatórios do projeto provam cada garantia do sistema contra falhas reais.

---

## Quando usar

Chaos tests entram em jogo quando:

- Você precisa provar ao seu chefe que "o sistema não duplica" — não como afirmação, mas como resultado de teste.
- Um bug de duplicidade aparece em produção e você precisa reproduzi-lo antes de corrigir.
- Você está revisando um PR que toca consumidor, outbox, ou transação do worker (obrigatório pela regra `pr.md`).
- Precisa explicar a diferença entre "o teste passou" e "o comportamento está correto sob falha".

---

## Quando NÃO usar

- **Não use chaos tests para validar lógica de negócio pura** — isso é responsabilidade dos testes unitários.
- **Não mocke o banco em chaos tests** — o valor desses testes está em exercitar o `UNIQUE constraint`, o `ON CONFLICT` e o `FOR UPDATE SKIP LOCKED` reais. Mock de banco não provaria nada sobre duplicação.
- **Não use `Thread.Sleep` para esperar** — use espera por condição com timeout (`Eventually.Assert`) para não criar testes lentos e não-determinísticos.

---

## O que são chaos tests

Testes unitários provam que **o código faz o que você escreveu**. Chaos tests provam que **o sistema se comporta corretamente quando algo dá errado**:

```
Testes unitários:
  "Dado evento novo, quando ingerir, então grava 1 evento e 1 outbox"
  → prova a lógica com banco em memória

Chaos tests:
  "Dado o mesmo evento enviado 20 vezes em paralelo com PostgreSQL real,
   quando ingerir, então existe exatamente 1 linha em events_raw e 1 em outbox"
  → prova que o UNIQUE constraint e o ON CONFLICT DO NOTHING funcionam sob concorrência real
```

A diferença: chaos tests usam **Testcontainers** (PostgreSQL, Kafka, RabbitMQ reais em containers Docker) e injetam falhas reais (queda de worker, falha de rede, restart).

---

## Os 6 cenários obrigatórios

### Cenário 1 — Mesmo webhook N vezes em paralelo

**O que prova:** a deduplicação na ingestão resiste a concorrência real.

**Pré-condição:** PostgreSQL com `UNIQUE(idempotency_key)` em `ingestion_events_raw`.

**Execução:**
```csharp
// 20 requisições simultâneas com o mesmo evento
var tasks = Enumerable.Range(0, 20)
    .Select(_ => client.PostAsync("/webhooks/acme", body));
await Task.WhenAll(tasks);
```

**Asserts obrigatórios** (ao final, não durante):
```csharp
await Eventually.Assert(async () =>
{
    (await db.Count("ingestion_events_raw", key)).Should().Be(1);
    (await db.Count("ingestion_outbox", key)).Should().Be(1);
});
// Todas as 20 respostas HTTP também devem ser 200
```

**Garantia verificada:** `INSERT ... ON CONFLICT DO NOTHING` em PostgreSQL — só uma das 20 escritas concorrentes vence; as outras 19 não criam nova linha e não lançam exceção.

**Status:** o cenário com store em memória já passa (`WebhookIngestionServiceTests` e `WebhookEndpointTests`). A versão com PostgreSQL real aguarda spec 001, T-05 (Testcontainers).

---

### Cenário 2 — Worker derrubado entre upsert e commit

**O que prova:** uma falha no meio do processamento não deixa estado corrompido nem duplica o efeito quando o worker reinicia.

**Pré-condição:** `IFailureInjector` — interface que, quando configurada, lança exceção após o upsert do registro interno mas antes do `COMMIT`.

**Execução:**
```csharp
// Fase 1: injetar falha — worker processa até o upsert e cai
failureInjector.FailAfterUpsert();
await worker.ProcessAsync(eventId);    // lança exceção antes do COMMIT

// Fase 2: rodar o job de recuperação — evento voltou para a fila (status = Processing → timeout)
await recoveryJob.RunAsync();

// Fase 3: processar de novo — sem a falha injetada
failureInjector.Disable();
await worker.ProcessAsync(eventId);   // completa com sucesso
```

**Asserts obrigatórios:**
```csharp
(await db.Count("processing_internal_records", key)).Should().Be(1);  // exatamente 1
(await db.Count("processing_outbox", key)).Should().Be(1);            // exatamente 1
```

**Garantia verificada:** rollback automático do banco garante que a falha entre upsert e commit não persiste estado parcial. O upsert na segunda tentativa detecta que já existe (`ON CONFLICT`) e a transação completa normalmente.

**Status:** pendente — depende de `IProcessingStore` (spec 002, T-01), handler transacional (T-02) e job de recuperação (spec 003, T-02).

---

### Cenário 3 — Webhooks fora de ordem

**O que prova:** a ordem de chegada dos eventos não afeta o estado final.

**Caso típico:** o provedor envia `payment.updated` antes de `payment.created` (reordenamento na rede, retry de mensagem antiga).

**Execução:**
```csharp
// Enviar os dois eventos com chaves diferentes e payloads que se referem ao mesmo recurso
await ingest("acme", "evt_updated", payloadUpdated);
await ingest("acme", "evt_created", payloadCreated);
// Processar em qualquer ordem
```

**Asserts obrigatórios:**
```csharp
// Estado final correto independente da ordem:
var state = await db.GetResourceState(resourceId);
state.Should().Be(ExpectedFinalState);
```

**Garantia verificada:** o estado é derivado dos dados do evento, não da posição na fila. O sistema não assume ordem; cada evento é idempotente independentemente.

**Status:** pendente — depende do handler de processamento completo.

---

### Cenário 4 — Dois workers disputando o mesmo evento

**O que prova:** `FOR UPDATE SKIP LOCKED` garante que só um worker processa cada evento.

**Execução:**
```csharp
// Dois workers recebem o mesmo RawEventAccepted do broker ao mesmo tempo
var worker1 = ProcessAsync(eventId);  // pega o lock
var worker2 = ProcessAsync(eventId);  // SKIP LOCKED: não encontra → ack sem processar
await Task.WhenAll(worker1, worker2);
```

**Asserts obrigatórios:**
```csharp
(await db.Count("processing_internal_records", key)).Should().Be(1);
(await db.Count("processing_outbox", key)).Should().Be(1);
```

**Garantia verificada:** `SELECT ... FOR UPDATE SKIP LOCKED` impede que dois workers processem a mesma linha. O segundo worker não bloqueia — ele simplesmente não encontra a linha disponível e encerra sem efeito.

**Status:** pendente — depende de `IProcessingStore` com PostgreSQL real.

---

### Cenário 5 — Reprocessamento da DLQ duas vezes

**O que prova:** reprocessar o mesmo evento via API admin duas vezes produz exatamente o mesmo resultado que uma vez.

**Execução:**
```csharp
// Evento na DLQ (status = Failed)
await SetupFailedEvent(eventId, key);

// Reprocessar duas vezes em sequência
await handler.HandleAsync(eventId);    // primeira: Requeued
await ProcessUntilDone(eventId);       // worker processa

await handler.HandleAsync(eventId);    // segunda: AlreadyDone (sem novo efeito)
```

**Asserts obrigatórios:**
```csharp
(await db.Count("processing_internal_records", key)).Should().Be(1);
(await db.Count("processing_outbox", key)).Should().Be(1);
secondOutcome.Should().Be(ReprocessOutcome.AlreadyDone);
```

**Garantia verificada:** o `AlreadyDone` retornado na segunda chamada prova que o sistema verificou o estado (`Done`) antes de reenfileirar. O upsert no registro interno também impede duplicação mesmo se o fluxo não checar o status.

**Status:** a lógica de `ReprocessEventHandler` já está implementada e testada (Sessão 6). O cenário de chaos completo com PostgreSQL depende da implementação de `IEventReprocessor`.

---

### Cenário 6 — Broker indisponível: outbox acumula e drena

**O que prova:** a API continua respondendo 200 quando o broker está fora do ar, e nenhuma mensagem se perde quando o broker volta.

**Execução:**
```csharp
// Fase 1: derrubar o broker
await kafka.StopAsync();  // ou rabbitmq.StopAsync()

// Fase 2: enviar webhooks com broker fora do ar
for (int i = 0; i < 10; i++)
    await client.PostAsync("/webhooks/acme", body(i));
// Todos devem retornar 200 — a API não depende do broker

// Fase 3: verificar que a outbox acumulou (mas broker ainda fora)
(await db.Count("ingestion_outbox", published_at: null)).Should().Be(10);

// Fase 4: subir o broker de volta
await kafka.StartAsync();

// Fase 5: aguardar relay drenar a outbox
await Eventually.Assert(async () =>
    (await db.Count("ingestion_outbox", published_at: null)).Should().Be(0),
    timeout: TimeSpan.FromSeconds(30));
```

**Asserts obrigatórios:**
```csharp
// Todas as 10 mensagens chegaram ao broker
(await kafka.CountMessages("raw-events")).Should().Be(10);
// Nenhuma duplicata na outbox
(await db.Count("ingestion_outbox")).Should().Be(10);
```

**Garantia verificada:** a gravação na outbox (dentro da transação) é independente do broker. O relay tenta publicar repetidamente; quando o broker volta, drena sem perda nem duplicação.

**Status:** pendente — depende do relay de outbox e da integração com Testcontainers de Kafka/RabbitMQ.

---

## Ferramentas de chaos

### Testcontainers

Containers Docker gerenciados pelo código de teste. Cada classe de teste sobe seu próprio container:

```csharp
// PostgreSQL real em container:
public class ChaosTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

**Por que não mock de banco?** O valor dos chaos tests está em exercitar o comportamento real do banco — o `UNIQUE constraint` real, o `ON CONFLICT` real, o `FOR UPDATE SKIP LOCKED` real. Um mock que simula essas operações não prova nada sobre o comportamento em produção.

### `FakeClock`

Para controlar o tempo nos testes de timeout sem esperar tempo real:

```csharp
var clock = new FakeClock(DateTimeOffset.UtcNow);
// Avançar o relógio 6 minutos para simular evento preso:
clock.Advance(TimeSpan.FromMinutes(6));
// StuckEventDetector.Decide() agora vê o evento como preso
```

### `IFailureInjector` (a implementar)

Interface que permite injetar falha em pontos específicos da transação para o cenário 2:

```csharp
public interface IFailureInjector
{
    void FailAfterUpsert();  // lança após step 5 da transação
    void Disable();
}
```

### `Eventually.Assert`

Espera ativa por condição com timeout — nunca `Thread.Sleep`:

```csharp
// ERRADO:
await Task.Delay(5000);
(await db.Count(key)).Should().Be(1);

// CORRETO:
await Eventually.Assert(
    async () => (await db.Count(key)).Should().Be(1),
    timeout: TimeSpan.FromSeconds(10),
    interval: TimeSpan.FromMilliseconds(100));
```

---

## O que já existe vs. o que está pendente

| Cenário | Status atual | O que falta |
|---|---|---|
| 1 — Webhook N vezes em paralelo | ✅ parcial (memória) | PostgreSQL real (spec 001, T-05) |
| 2 — Worker cai no meio | ⏳ pendente | IProcessingStore + IFailureInjector (spec 002, T-01/T-02) |
| 3 — Webhooks fora de ordem | ⏳ pendente | handler de processamento completo |
| 4 — Dois workers disputando | ⏳ pendente | IProcessingStore com SKIP LOCKED (spec 002, T-01) |
| 5 — Reprocessamento DLQ 2x | ✅ parcial (lógica) | IEventReprocessor implementado (spec 003, T-03) |
| 6 — Broker indisponível | ⏳ pendente | relay de outbox + Testcontainers broker |

---

## O que ela NÃO faz

- Chaos tests não substituem testes unitários — cada tipo tem seu papel na pirâmide.
- Não testam performance ou latência — isso é responsabilidade dos testes de carga (k6, Sessão 8).
- Não testam falha de hardware ou partição de rede — esse nível de chaos é para ambientes de produção (ex.: Chaos Monkey).
- Não rodam no CI sem Docker disponível — os chaos tests com Testcontainers precisam de daemon Docker no CI.

---

## Instruções operacionais — como rodar esta sessão

```bash
# 1. Rodar os chaos tests que já existem (cenários 1 e 5 parciais)
dotnet test tests/Messaging.UnitTests -c Release --no-build --nologo \
  --filter "vinte_envios_concorrentes" -v normal

dotnet test tests/Messaging.IntegrationTests -c Release --no-build --nologo \
  --filter "repetido_em_paralelo" -v normal

# 2. Ver a regra de testes completa
cat .claude/rules/testing.md

# 3. Ver os esqueletos de código nos skills
cat .claude/skills/outbox-idempotency/SKILL.md     # checklist e SQL
cat .claude/skills/testing-patterns/SKILL.md       # esqueletos de chaos

# 4. Arquivos-chave para walkthrough ao vivo:
#    tests/Messaging.UnitTests/WebhookIngestionServiceTests.cs   ← cenário 1 (memória)
#    tests/Messaging.IntegrationTests/WebhookEndpointTests.cs    ← cenário 1 (API)
#    .claude/rules/testing.md                                    ← 6 cenários obrigatórios
```

---

## O que você deve ser capaz de explicar ao seu chefe

**"Por que vocês precisam de chaos tests se já têm testes unitários?"**
— Testes unitários provam que o código faz o que você escreveu, com banco em memória. Chaos tests provam que o sistema se comporta corretamente sob falhas reais — concorrência real no PostgreSQL, worker caindo no meio de uma transação, broker saindo do ar. São dois tipos de garantia diferentes.

**"O que é Testcontainers e por que usar?"**
— É uma biblioteca que sobe containers Docker dentro do código de teste. Cada teste usa um PostgreSQL real, um Kafka real ou um RabbitMQ real — não um mock. Isso garante que o `UNIQUE constraint`, o `ON CONFLICT` e o `FOR UPDATE SKIP LOCKED` funcionam exatamente como em produção.

**"Como vocês provam que o sistema não duplica mesmo com falha?"**
— Com asserts de contagem ao final de cada cenário de chaos: `COUNT(*) na tabela de registros internos deve ser 1` e `COUNT(*) na outbox de saída deve ser 1` — independentemente de quantas vezes o evento foi entregue ou de quantas vezes o worker caiu. Se esses dois números chegam a 1, a garantia está provada.

**"O que acontece se o broker cair durante o processamento?"**
— Nada se perde. A mensagem para o broker está na outbox do banco — gravada na mesma transação do evento. Quando o broker volta, o relay lê a outbox e publica as mensagens que ficaram pendentes. A API nunca soube que o broker estava fora do ar.

---

## Próxima sessão

**Sessão 8 — Quality Gate e Architecture Tests**

Vamos ver como os 5 portões do quality gate se encaixam, o que cada um verifica, e como o `ModuleBoundaryTests` torna as fronteiras de arquitetura executáveis e parte do CI.
