namespace Messaging.Contracts.Ingestion;

/// <summary>
/// Contrato público publicado pelo módulo Ingestion (via outbox) quando um evento bruto é aceito.
/// O módulo Processing consome este contrato; nunca os tipos internos de Ingestion.
/// </summary>
public sealed record RawEventAccepted(
    Guid EventId,
    string IdempotencyKey,
    string Provider,
    DateTimeOffset ReceivedAt);
