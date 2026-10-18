using System.Text;

namespace OldenEra.MatchFinder;

internal static class GameWindow
{
    internal static nint Find()
    {
        nint found = 0;
        WindowsNative.EnumWindows((window, _) =>
        {
            if (IsGameWindow(window) && WindowsNative.GetWindow(window, 4 /* GW_OWNER */) == 0 &&
                (WindowsNative.IsWindowVisible(window) || WindowsNative.IsIconic(window)))
            {
                found = window;
                return false;
            }
            return true;
        }, 0);
        return found;
    }

    internal static bool IsGameWindow(nint window)
    {
        if (window == 0 || !WindowsNative.IsWindow(window)) return false;
        WindowsNative.GetWindowThreadProcessId(window, out uint processId);
        if (processId != (uint)Environment.ProcessId) return false;
        var name = new StringBuilder(128);
        return WindowsNative.GetClassNameW(window, name, name.Capacity) > 0 && name.ToString() == "UnityWndClass";
    }
}
