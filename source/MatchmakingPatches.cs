using HarmonyLib;
using Hex.SharedLibrary.Clients;
using Hex.SharedLibrary.Requests;

namespace OldenEra.MatchFinder;

[HarmonyPatch(typeof(MatchmakingClient), nameof(MatchmakingClient.OnMatchmakingGameFound))]
internal static class MatchFoundPatch
{
    private static void Prefix(MatchmakingClient __instance, MatchmakingGameFoundSignal __0, out MatchNotice? __state)
    {
        __state = null;
        var plugin = Plugin.Instance;
        if (plugin == null || __0 == null) return;
        try
        {
            // Состояние окна фиксируется до штатной обработки события игрой.
            nint window = GameWindow.Find();
            bool minimized = window != 0 && WindowsNative.IsIconic(window);
            bool foreground = window != 0 && WindowsNative.GetForegroundWindow() == window;
            if (window == 0) plugin.Log.LogWarning("Окно Unity текущего процесса не найдено: уведомление будет пропущено.");
            float seconds = __0.AcceptGameTimeLeft;
            long remaining = float.IsFinite(seconds) && seconds > 0
                ? (long)Math.Min(seconds * 1000d, int.MaxValue) : 0;
            __state = new MatchNotice(__0.GameId, __instance.Pointer, __instance.GameMode.ToString(), window, minimized, foreground,
                plugin.PlaySound.Value, Environment.TickCount64 + remaining);
        }
        catch (Exception error) { plugin.Report(error); }
    }

    private static void Postfix(MatchNotice? __state)
    {
        if (__state == null) return;
        var plugin = Plugin.Instance;
        try { plugin?.Coordinator?.OnFound(__state); }
        catch (Exception error) { plugin?.Report(error); }
    }
}

// Не запускаем еще не начатые действия после отмены, выхода или завершения принятия.
[HarmonyPatch]
internal static class EndAcceptingPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MatchmakingClient), nameof(MatchmakingClient.OnGameAcceptCanceled));
        yield return AccessTools.Method(typeof(MatchmakingClient), nameof(MatchmakingClient.OnGameAccepted));
        yield return AccessTools.Method(typeof(MatchmakingClient), nameof(MatchmakingClient.OnReset));
    }

    private static void Postfix(MatchmakingClient __instance)
    {
        var plugin = Plugin.Instance;
        try { plugin?.Coordinator?.Cancel(__instance.Pointer); }
        catch (Exception error) { plugin?.Report(error); }
    }
}
