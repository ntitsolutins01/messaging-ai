using Messaging.Contracts.Processing;

namespace Messaging.Operations;

/// <summary>
/// Caso de uso da tela/API de operações: reprocessar um evento. Fala com o módulo Processing só pelo contrato.
/// </summary>
public sealed class ReprocessEventHandler
{
    private readonly IEventReprocessor _reprocessor;

    public ReprocessEventHandler(IEventReprocessor reprocessor)
    {
        ArgumentNullException.ThrowIfNull(reprocessor);
        _reprocessor = reprocessor;
    }

    public Task<ReprocessOutcome> HandleAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("eventId is required", nameof(eventId));
        }

        return _reprocessor.ReprocessAsync(eventId, cancellationToken);
    }
}
