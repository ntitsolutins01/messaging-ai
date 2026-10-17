using System.Security.Cryptography;
using System.Text;
using Messaging.Contracts.Ingestion;
using Messaging.SharedKernel;

namespace Messaging.Ingestion;

/// <summary>
/// Caso de uso do webhook: valida assinatura, deriva a chave de idempotência e grava o evento bruto + outbox.
/// Não faz parse do payload.
/// </summary>
public sealed class WebhookIngestionService
{
    private readonly WebhookSignatureValidator _validator;
    private readonly IRawEventStore _store;
    private readonly IClock _clock;

    public WebhookIngestionService(WebhookSignatureValidator validator, IRawEventStore store, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        _validator = validator;
        _store = store;
        _clock = clock;
    }

    public async Task<IngestionOutcome> IngestAsync(
        string provider,
        string? providerEventId,
        byte[] body,
        string? signatureHeader,
        CancellationToken cancellationToken)
    {
        // Só provedor e resultado vão para o trace: nunca payload, segredo ou assinatura.
        using var activity = Telemetry.Source.StartActivity("webhook.ingest");
        activity?.SetTag("messaging.provider", provider);

        var outcome = await IngestCoreAsync(provider, providerEventId, body, signatureHeader, cancellationToken);

        activity?.SetTag("messaging.outcome", outcome.ToString());
        return outcome;
    }

    private async Task<IngestionOutcome> IngestCoreAsync(
        string provider,
        string? providerEventId,
        byte[] body,
        string? signatureHeader,
        CancellationToken cancellationToken)
    {
        if (_validator.Validate(body, signatureHeader) != SignatureResult.Valid)
        {
            return IngestionOutcome.InvalidSignature;
        }

        var eventId = string.IsNullOrWhiteSpace(providerEventId) ? HashOf(body) : providerEventId;
        var key = IdempotencyKey.From(provider, eventId);
        var now = _clock.UtcNow;
        var id = Guid.NewGuid();

        var raw = new RawEvent(id, key, provider, Encoding.UTF8.GetString(body), now);
        var message = new RawEventAccepted(id, key.Value, provider, now);

        var inserted = await _store.TryAddAsync(raw, message, cancellationToken);
        return inserted ? IngestionOutcome.Accepted : IngestionOutcome.Duplicate;
    }

    // Fallback quando o provedor não manda um ID de evento: o mesmo corpo gera a mesma chave.
    private static string HashOf(byte[] body) =>
        "sha256-" + Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
}
