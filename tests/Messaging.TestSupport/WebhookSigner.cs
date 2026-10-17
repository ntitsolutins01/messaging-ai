using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Messaging.TestSupport;

/// <summary>Gera o cabeçalho X-Signature no mesmo esquema do validador: t=&lt;unix&gt;,v1=HMAC-SHA256("{t}.{corpo}").</summary>
public static class WebhookSigner
{
    public static string Sign(string secret, DateTimeOffset at, byte[] body)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var payload = prefix.Concat(body).ToArray();
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        return $"t={timestamp},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }
}
