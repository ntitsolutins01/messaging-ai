using Messaging.Contracts.Ingestion;

namespace Messaging.Ingestion;

public interface IRawEventStore
{
    /// <summary>
    /// Grava o evento bruto E a mensagem de outbox na MESMA transação.
    /// Retorna false (sem efeito algum) quando a chave de idempotência já existe.
    /// A implementação de produção usa INSERT ... ON CONFLICT (idempotency_key) DO NOTHING.
    /// </summary>
    Task<bool> TryAddAsync(RawEvent rawEvent, RawEventAccepted outboxMessage, CancellationToken cancellationToken);
}
