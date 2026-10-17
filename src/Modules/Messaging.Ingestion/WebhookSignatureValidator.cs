using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Messaging.SharedKernel;

namespace Messaging.Ingestion;

/// <summary>
/// Valida o cabeçalho "t=&lt;unix&gt;,v1=&lt;hex&gt;" onde v1 = HMAC-SHA256(secret, "{t}.{corpo}").
/// ASSUNÇÃO: o esquema real do provedor ainda não foi confirmado (ver specs/001). Só este tipo muda quando confirmar.
/// </summary>
public sealed class WebhookSignatureValidator
{
    private const long MaxUnixSeconds = 253402300799;

    private readonly byte[] _key;
    private readonly IClock _clock;
    private readonly TimeSpan _tolerance;

    public WebhookSignatureValidator(string secret, IClock clock, TimeSpan tolerance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(clock);

        _key = Encoding.UTF8.GetBytes(secret);
        _clock = clock;
        _tolerance = tolerance;
    }

    public SignatureResult Validate(ReadOnlySpan<byte> body, string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return SignatureResult.Missing;
        }

        if (!TryParseHeader(header, out var timestamp, out var signature) || !TryDecodeHex(signature, out var provided))
        {
            return SignatureResult.Malformed;
        }

        if (!CryptographicOperations.FixedTimeEquals(Compute(timestamp, body), provided))
        {
            return SignatureResult.Mismatch;
        }

        return IsFresh(timestamp) ? SignatureResult.Valid : SignatureResult.Expired;
    }

    private static bool TryParseHeader(string header, out long timestamp, out string signature)
    {
        timestamp = 0;
        signature = string.Empty;
        var hasTimestamp = false;
        var hasSignature = false;

        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                return false;
            }

            if (pair[0] == "t")
            {
                hasTimestamp = long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out timestamp);
            }
            else if (pair[0] == "v1")
            {
                signature = pair[1];
                hasSignature = true;
            }
        }

        return hasTimestamp && hasSignature && timestamp <= MaxUnixSeconds;
    }

    private static bool TryDecodeHex(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private byte[] Compute(long timestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload.AsSpan(prefix.Length));
        return HMACSHA256.HashData(_key, payload);
    }

    private bool IsFresh(long timestamp)
    {
        var sentAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        return (_clock.UtcNow - sentAt).Duration() <= _tolerance;
    }
}
