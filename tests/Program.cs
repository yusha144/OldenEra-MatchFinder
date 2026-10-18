using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OldenEra.MatchFinder;

var tests = new (string Name, Action Run)[]
{
    ("Переключатель звука во всех режимах при сворачивании и потере переднего плана", Configurations),
    ("Игра на переднем плане не вызывает уведомление", ForegroundWindow),
    ("Повтор события не уведомляет после позднего ухода из игры", LateLeave),
    ("Одно уведомление при одновременных повторах события", ConcurrentDuplicates),
    ("Новый матч после отмены уведомляет заново", CancelThenNewMatch),
    ("Отмена останавливает еще не запущенные действия", CancelPending),
    ("Отмена другого клиента не мешает уведомлению", OtherClient),
    ("Новый матч отменяет устаревшие действия", Superseded),
    ("Просроченное событие не уведомляет", Expired),
    ("Ожидающее действие учитывает истечение срока", ExpiresWhileQueued),
    ("Ошибка звука записывается без повтора и не мешает новому матчу", SoundFailure),
    ("Остановка мода отменяет действия и новые события", Stop),
    ("Неизвестное окно не вызывает уведомление", MissingWindow),
    ("Пустой идентификатор не вызывает уведомление", MissingId),
    ("Проверка файлов различает совпадение и изменение", BuildHashes),
    ("Отсутствующие файлы не проходят проверку совместимости", MissingBuildFiles),
    ("Встроенный сигнал является коротким непустым PCM WAV", SoundAsset)
};
int failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"OK: {test.Name}"); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL: {test.Name}: {error.Message}"); }
}
Console.WriteLine($"Итог: {tests.Length - failed}/{tests.Length} проверок пройдено.");
return failed == 0 ? 0 : 1;

static MatchNotice Notice(string id = "match-a", bool sound = true, bool minimized = true,
    string mode = "Classic", long expires = 2000, nint client = 1, nint window = 10, bool foreground = false) =>
    new(id, client, mode, window, minimized, foreground, sound, expires);

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Configurations()
{
    foreach (string mode in new[] { "Classic", "Solo", "Arena" })
    foreach (bool sound in new[] { false, true })
    foreach (var state in new[] { (Minimized: true, Foreground: false), (Minimized: false, Foreground: false), (Minimized: false, Foreground: true) })
    {
        var f = new Fixture();
        f.Coordinator.OnFound(Notice(sound: sound, mode: mode, minimized: state.Minimized, foreground: state.Foreground));
        bool notify = state.Minimized || !state.Foreground;
        Check(f.Effects.SoundCount == (sound && notify ? 1 : 0), $"Звук: {mode}, {sound}, {state}");
    }
}

static void ForegroundWindow()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice(minimized: false, foreground: true));
    Check(f.Effects.SoundCount == 0, "Ложное уведомление");
}

static void LateLeave()
{
    foreach (bool minimized in new[] { false, true })
    {
        var f = new Fixture();
        f.Coordinator.OnFound(Notice(minimized: false, foreground: true));
        f.Coordinator.OnFound(Notice(minimized: minimized, foreground: false));
        Check(f.Effects.SoundCount == 0, "Повтор события уведомил");
    }
}

static void ConcurrentDuplicates()
{
    var f = new Fixture();
    Parallel.For(0, 100, _ => f.Coordinator.OnFound(Notice()));
    Check(f.Effects.SoundCount == 1, "Повторные действия");
}

static void CancelThenNewMatch()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice());
    f.Coordinator.Cancel(1);
    f.Coordinator.OnFound(Notice());
    Check(f.Effects.SoundCount == 1, "Старый матч уведомил повторно");
    f.Coordinator.OnFound(Notice("match-b"));
    Check(f.Effects.SoundCount == 2, "Новый матч не уведомил");
}

static void CancelPending()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice());
    var check = f.Effects.LastCheck!;
    Check(check(), "Новое действие уже отменено");
    f.Coordinator.Cancel(1);
    Check(!check(), "Отмененное действие еще допустимо");
}

static void OtherClient()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice());
    f.Coordinator.Cancel(2);
    Check(f.Effects.LastCheck!(), "Отменено действие другого клиента");
}

static void Superseded()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice());
    var old = f.Effects.LastCheck!;
    f.Coordinator.OnFound(Notice("match-b"));
    Check(!old() && f.Effects.LastCheck!(), "Не обновилось текущее уведомление");
}

static void Expired()
{
    var f = new Fixture { Now = 2000 };
    f.Coordinator.OnFound(Notice());
    Check(f.Effects.SoundCount == 0, "Просроченное уведомление запущено");
}

static void ExpiresWhileQueued()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice());
    f.Now = 2000;
    Check(!f.Effects.LastCheck!(), "Просроченная работа осталась допустимой");
}

static void SoundFailure()
{
    var f = new Fixture();
    f.Effects.ThrowSound = true;
    f.Coordinator.OnFound(Notice());
    f.Coordinator.OnFound(Notice());
    Check(f.Effects.SoundCount == 0 && f.Warnings.Count == 1, "Нет предупреждения либо ошибка вызвала повтор");
    f.Effects.ThrowSound = false;
    f.Coordinator.OnFound(Notice("match-b"));
    Check(f.Effects.SoundCount == 1, "Ошибка звука остановила последующие уведомления");
}

static void Stop()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice());
    var pending = f.Effects.LastCheck!;
    f.Coordinator.Stop();
    f.Coordinator.OnFound(Notice("match-b"));
    Check(!pending() && f.Effects.SoundCount == 1, "Мод не остановлен");
}

static void MissingWindow()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice(window: 0));
    Check(f.Effects.SoundCount == 0, "Действия без окна");
}

static void MissingId()
{
    var f = new Fixture();
    f.Coordinator.OnFound(Notice(" "));
    Check(f.Effects.SoundCount == 0 && f.Warnings.Count == 1, "Нет проверки идентификатора");
}

static void BuildHashes()
{
    string path = Path.GetTempFileName();
    try
    {
        File.WriteAllText(path, "known-build");
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        Check(BuildCompatibility.Matches(path, hash), "Совпадающий файл отвергнут");
        File.AppendAllText(path, "changed");
        Check(!BuildCompatibility.Matches(path, hash), "Измененный файл принят");
    }
    finally { File.Delete(path); }
}

static void MissingBuildFiles()
{
    string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    try
    {
        Check(!BuildCompatibility.IsSupported(directory), "Несуществующая сборка принята");
    }
    catch (DirectoryNotFoundException) { }
}

static void SoundAsset()
{
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MatchFinder.MatchFound.wav")!;
    using var reader = new BinaryReader(stream);
    string Four() => Encoding.ASCII.GetString(reader.ReadBytes(4));
    Check(Four() == "RIFF", "Нет RIFF");
    Check(reader.ReadUInt32() == stream.Length - 8, "Неверная длина RIFF");
    Check(Four() == "WAVE" && Four() == "fmt ", "Нет WAV/fmt");
    Check(reader.ReadUInt32() == 16 && reader.ReadUInt16() == 1, "Не PCM");
    Check(reader.ReadUInt16() == 1 && reader.ReadUInt32() == 44100, "Неверный формат каналов/частоты");
    Check(reader.ReadUInt32() == 88200 && reader.ReadUInt16() == 2 && reader.ReadUInt16() == 16, "Неверная разрядность");
    Check(Four() == "data", "Нет звуковых данных");
    uint size = reader.ReadUInt32();
    Check(size == stream.Length - stream.Position && size > 10000 && size < 88200, "Неверная длина сигнала");
    int peak = 0;
    while (stream.Position < stream.Length) peak = Math.Max(peak, Math.Abs((int)reader.ReadInt16()));
    Check(peak > 1000 && peak < 30000, "Сигнал пустой или слишком громкий");
}

sealed class Fixture
{
    internal long Now;
    internal FakeEffects Effects = new();
    internal List<string> Warnings = new();
    internal NotificationCoordinator Coordinator;
    internal Fixture() => Coordinator = new(Effects, () => Now, _ => { }, s => Warnings.Add(s));
}

sealed class FakeEffects : INotificationEffects
{
    internal int SoundCount;
    internal bool ThrowSound;
    internal Func<bool>? LastCheck;
    public void StartSound(MatchNotice notice, Func<bool> isCurrent)
    {
        if (ThrowSound) throw new InvalidOperationException("Звук недоступен");
        Interlocked.Increment(ref SoundCount);
        LastCheck = isCurrent;
    }
}
