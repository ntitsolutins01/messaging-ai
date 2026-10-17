using Messaging.SharedKernel;

namespace Messaging.Ingestion;

/// <summary>Evento exatamente como chegou. O parse acontece depois, atrás de um adaptador versionado.</summary>
public sealed record RawEvent(
    Guid Id,
    IdempotencyKey Key,
    string Provider,
    string Payload,
    DateTimeOffset ReceivedAt);
