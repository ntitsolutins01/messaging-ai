---
name: testing-patterns
description: Use ao escrever ou revisar testes (unit, integração com Testcontainers, chaos) em .NET para consumidores, outbox e API de webhook.
---

# Padrões de teste (.NET)

## Stack
xUnit, FluentAssertions, NSubstitute (só em unit), Testcontainers (PostgreSQL, Kafka, RabbitMQ), `WebApplicationFactory` para a API.

## Unit
- Handlers e política de retry com fakes em memória das portas do módulo (ex.: `IRawEventStore`, `IEventReprocessor`) e de `IClock` (`FakeClock` em `tests/Messaging.TestSupport`).
- Um assert de comportamento por teste; nome `Dado_Quando_Entao`.

## Integração
- Um container de PostgreSQL por classe de teste (`IClassFixture`), banco recriado entre testes com `Respawn`.
- Sem mock de banco: o valor do teste é exercitar a constraint única e o upsert reais.
- Esperas por condição, nunca `Thread.Sleep`:
```csharp
await Eventually.Assert(async () =>
    (await db.CountInternalRecords(key)).Should().Be(1),
    timeout: TimeSpan.FromSeconds(10));
```

## Chaos (idempotência)
Esqueleto de cenário "mesmo webhook N vezes em paralelo":
```csharp
var tasks = Enumerable.Range(0, 20).Select(_ => client.PostAsync("/webhooks/provider", body));
await Task.WhenAll(tasks);

await Eventually.Assert(async () =>
{
    (await db.CountInternalRecords(key)).Should().Be(1);
    (await db.CountOutbox(key)).Should().Be(1);
});
```
Esqueleto de "worker cai no meio": injetar uma falha após o upsert e antes do commit (`IFailureInjector`), reiniciar o worker, rodar o job de recuperação e conferir contagem 1/1.

## Evitar
- Asserts só de "não lançou exceção".
- Testes que dependem de ordem de execução ou do relógio real.
- Compartilhar estado entre testes via banco sem limpeza.
