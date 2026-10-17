namespace Messaging.Ingestion;

public enum SignatureResult
{
    Valid,
    Missing,
    Malformed,
    Mismatch,
    Expired,
}
