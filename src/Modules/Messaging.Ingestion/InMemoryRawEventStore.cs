using System.Collections.Concurrent;
using Messaging.Contracts.Ingestion;

namespace Messaging.Ingestion;

/// <summary>
/// Implementação em memória, só para testes e para a API subir sem banco.
/// TODO(specs/001, T-05): substituir por PostgreSQL com UNIQUE(idempotency_key) + transação única com a outbox.
/// </summary>
public sealed class InMemoryRawEventStore : IRawEventStore
{
    private readonly ConcurrentDictionary<string, RawEvent> _events = new();
    private readonly ConcurrentQueue<RawEventAccepted> _outbox = new();

    public int EventCount => _events.Count;

    public int OutboxCount => _outbox.Count;

    public Task<bool> TryAddAsync(RawEvent rawEvent, RawEventAccepted outboxMessage, CancellationToken cancellationToken)
    {
        // TryAdd é atômico: só uma chamada concorrente por chave vence e enfileira a outbox.
        var inserted = _events.TryAdd(rawEvent.Key.Value, rawEvent);
        if (inserted)
        {
            _outbox.Enqueue(outboxMessage);
        }

        return Task.FromResult(inserted);
    }
}
