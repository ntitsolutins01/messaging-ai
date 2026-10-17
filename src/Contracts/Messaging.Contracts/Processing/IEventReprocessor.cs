namespace Messaging.Contracts.Processing;

public enum ReprocessOutcome
{
    Requeued,
    AlreadyDone,
    NotFound,
}

/// <summary>
/// Porta pública do módulo Processing usada pelo módulo Operations para reprocessar um evento.
/// Reprocessar usa o mesmo caminho e a mesma chave de idempotência do processamento normal.
/// </summary>
public interface IEventReprocessor
{
    Task<ReprocessOutcome> ReprocessAsync(Guid eventId, CancellationToken cancellationToken);
}
