namespace Messaging.Processing;

/// <summary>
/// Decide o que fazer com um evento possivelmente preso (worker caiu no meio).
/// Seguro repetir porque o processamento é idempotente (spec 003).
/// </summary>
public sealed class StuckEventDetector
{
    private readonly TimeSpan _timeout;
    private readonly int _maxAttempts;

    public StuckEventDetector(TimeSpan timeout, int maxAttempts)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        _timeout = timeout;
        _maxAttempts = maxAttempts;
    }

    public RecoveryAction Decide(EventState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != EventStatus.Processing)
        {
            return RecoveryAction.None;
        }

        if (state.StartedAt is { } startedAt && now - startedAt < _timeout)
        {
            return RecoveryAction.None;
        }

        return state.Attempts >= _maxAttempts ? RecoveryAction.MoveToDlq : RecoveryAction.Requeue;
    }
}
