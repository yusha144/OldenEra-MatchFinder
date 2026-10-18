using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace OldenEra.MatchFinder;

[BepInPlugin(Id, "Match Finder", Version)]
public sealed class Plugin : BasePlugin
{
    internal const string Id = "local.oldenera.matchfinder";
    internal const string Version = "0.1.3";
    internal static Plugin? Instance;
    internal ConfigEntry<bool> PlaySound = null!;
    internal NotificationCoordinator? Coordinator;
    private Harmony? _patches;

    public override void Load()
    {
        PlaySound = Config.Bind("Notifications", "PlaySound", true,
            "Подавать один встроенный сигнал, если при нахождении матча игра свернута или не на переднем плане. Независимо от фонового звука игры. Учитывает громкость системных звуков Windows. Требуется перезапуск игры после изменения файла.");
        try
        {
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            {
                Log.LogWarning("Match Finder отключен: требуется Windows x64.");
                return;
            }
            if (!BuildCompatibility.IsSupported(Paths.GameRootPath))
            {
                Log.LogWarning($"Match Finder отключен: поддерживается Steam-сборка {BuildCompatibility.SteamBuild}. Файлы игры отличаются от проверенных.");
                return;
            }
            InitializeHooks();
            Log.LogInfo($"Match Finder {Version} загружен; Steam {BuildCompatibility.SteamBuild}; звук={PlaySound.Value}.");
        }
        catch (Exception error)
        {
            Coordinator?.Stop();
            Instance = null;
            _patches?.UnpatchSelf();
            Log.LogError($"Match Finder не запущен: {error}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeHooks()
    {
        var effects = new WindowsNotifications(s => Log.LogInfo(s), s => Log.LogWarning(s));
        Coordinator = new NotificationCoordinator(effects, () => Environment.TickCount64,
            s => Log.LogInfo(s), s => Log.LogWarning(s));
        Instance = this;
        _patches = new Harmony(Id);
        _patches.PatchAll(typeof(Plugin).Assembly);
    }

    public override bool Unload()
    {
        Coordinator?.Stop();
        Instance = null;
        _patches?.UnpatchSelf();
        return true;
    }

    internal void Report(Exception error) => Log.LogWarning($"Обработка уведомления прервана: {error.GetType().Name}.");
}
