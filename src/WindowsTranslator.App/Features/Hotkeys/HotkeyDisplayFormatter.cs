using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.App.Features.Hotkeys;

internal static class HotkeyDisplayFormatter
{
    public static string Format(HotkeySettings settings)
    {
        List<string> parts = [];
        if (settings.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (settings.Alt)
        {
            parts.Add("Alt");
        }

        if (settings.Shift)
        {
            parts.Add("Shift");
        }

        if (settings.Win)
        {
            parts.Add("Win");
        }

        parts.Add(settings.Key.ToUpperInvariant());
        return string.Join("+", parts);
    }
}
