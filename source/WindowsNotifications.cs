using System.Reflection;
using System.Runtime.InteropServices;

namespace OldenEra.MatchFinder;

internal sealed class WindowsNotifications : INotificationEffects
{
    private readonly Action<string> _info;
    private readonly Action<string> _warning;
    private readonly byte[]? _sound;

    internal WindowsNotifications(Action<string> info, Action<string> warning)
    {
        _info = info;
        _warning = warning;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MatchFinder.MatchFound.wav")
                ?? throw new InvalidDataException("Встроенный звук отсутствует.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _sound = memory.ToArray();
        }
        catch (Exception error)
        {
            _warning($"Звук недоступен: {error.GetType().Name}.");
        }
    }

    public void StartSound(MatchNotice notice, Func<bool> isCurrent)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!isCurrent() || _sound == null) return;
                // Синхронный звук только в рабочем потоке. Буфер закреплен до конца воспроизведения.
                var pinned = GCHandle.Alloc(_sound, GCHandleType.Pinned);
                try
                {
                    bool played = WindowsNative.PlaySound(pinned.AddrOfPinnedObject(), 0,
                        WindowsNative.SndMemory | WindowsNative.SndSystem |
                        WindowsNative.SndNoDefault | WindowsNative.SndNoStop);
                    if (played) _info("Звуковой вызов Windows выполнен. Слышимость зависит от громкости системных звуков и устройства.");
                    else _warning("Windows не воспроизвела сигнал. Повторной попытки не будет.");
                }
                finally { pinned.Free(); }
            }
            catch (Exception error) { _warning($"Ошибка звука: {error.GetType().Name}."); }
        });
    }
}
