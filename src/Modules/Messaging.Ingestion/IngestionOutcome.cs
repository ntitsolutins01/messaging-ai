namespace Messaging.Ingestion;

public enum IngestionOutcome
{
    Accepted,
    Duplicate,
    InvalidSignature,
}
