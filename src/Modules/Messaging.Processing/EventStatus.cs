namespace Messaging.Processing;

public enum EventStatus
{
    Received,
    Processing,
    Done,
    Failed,
}

public enum RecoveryAction
{
    None,
    Requeue,
    MoveToDlq,
}

public sealed record EventState(Guid Id, EventStatus Status, DateTimeOffset? StartedAt, int Attempts);
