namespace Messaging.SharedKernel;

/// <summary>
/// Chave única de um evento. Vale para todos os efeitos (registro interno, outbox, mensagens de saída).
/// Formato: "{provider}:{providerEventId}".
/// </summary>
public sealed record IdempotencyKey
{
    public const int MaxLength = 200;

    private IdempotencyKey(string value) => Value = value;

    public string Value { get; }

    public static IdempotencyKey From(string provider, string providerEventId)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("provider is required", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            throw new ArgumentException("providerEventId is required", nameof(providerEventId));
        }

        var value = $"{provider.Trim().ToLowerInvariant()}:{providerEventId.Trim()}";
        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"key exceeds {MaxLength} characters", nameof(providerEventId));
        }

        return new IdempotencyKey(value);
    }

    public override string ToString() => Value;
}
