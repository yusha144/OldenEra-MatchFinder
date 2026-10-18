namespace OldenEra.MatchFinder;

// Только управляемые значения: объекты IL2CPP не передаются в рабочие потоки.
internal sealed record MatchNotice(
    string GameId, nint Client, string Mode, nint Window, bool WasMinimized, bool WasForeground,
    bool SoundEnabled, long ExpiresAt);

internal interface INotificationEffects
{
    void StartSound(MatchNotice notice, Func<bool> isCurrent);
}

internal sealed class NotificationCoordinator
{
    private readonly object _gate = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenOrder = new();
    private readonly INotificationEffects _effects;
    private readonly Func<long> _clock;
    private readonly Action<string> _info;
    private readonly Action<string> _warning;
    private MatchNotice? _current;
    private bool _stopped;

    internal NotificationCoordinator(INotificationEffects effects, Func<long> clock, Action<string> info, Action<string> warning)
    {
        _effects = effects;
        _clock = clock;
        _info = info;
        _warning = warning;
    }

    internal void OnFound(MatchNotice notice)
    {
        if (string.IsNullOrWhiteSpace(notice.GameId))
        {
            _warning("Найденный матч без идентификатора: уведомление пропущено.");
            return;
        }

        lock (_gate)
        {
            if (_stopped || !_seen.Add(notice.GameId)) return;
            _seenOrder.Enqueue(notice.GameId);
            if (_seenOrder.Count > 256) _seen.Remove(_seenOrder.Dequeue());
            _current = notice;
        }

        _info($"Найден матч: режим={notice.Mode}; окно свернуто={notice.WasMinimized}; окно на переднем плане={notice.WasForeground}; звук={notice.SoundEnabled}.");
        if (notice.Window == 0 || !IsCurrent(notice)) return;
        if (!notice.WasMinimized && notice.WasForeground)
        {
            _info("Уведомление пропущено: игра уже была на переднем плане.");
            return;
        }

        if (notice.SoundEnabled)
        {
            try { _effects.StartSound(notice, () => IsCurrent(notice)); }
            catch (Exception error) { _warning($"Не удалось запустить звук: {error.GetType().Name}."); }
        }
    }

    internal bool IsCurrent(MatchNotice notice)
    {
        lock (_gate)
            return !_stopped && ReferenceEquals(_current, notice) && _clock() < notice.ExpiresAt;
    }

    internal void Cancel(nint client)
    {
        lock (_gate)
        {
            if (_current?.Client == client) _current = null;
        }
    }

    internal void Stop()
    {
        lock (_gate)
        {
            _stopped = true;
            _current = null;
        }
    }
}
