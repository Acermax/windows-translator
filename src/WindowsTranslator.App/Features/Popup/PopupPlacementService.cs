using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WindowsTranslator.App.Features.Popup;

internal static class PopupPlacementService
{
    public static void PlaceNearCursor(System.Windows.Window window, System.Windows.Point cursorPosition)
    {
        const double margin = 16;
        var screen = Forms.Screen.FromPoint(new Drawing.Point((int)cursorPosition.X, (int)cursorPosition.Y)).WorkingArea;
        var width = GetDimension(window.ActualWidth, window.Width, 440);
        var height = GetDimension(window.ActualHeight, window.Height, 260);

        var minLeft = screen.Left + margin;
        var maxLeft = screen.Right - margin - width;
        var minTop = screen.Top + margin;
        var maxTop = screen.Bottom - margin - height;

        var left = cursorPosition.X + margin;
        var top = cursorPosition.Y + margin;

        if (left + width > screen.Right - margin)
        {
            left = cursorPosition.X - width - margin;
        }

        if (top + height > screen.Bottom - margin)
        {
            top = cursorPosition.Y - height - margin;
        }

        window.Left = ClampToScreen(left, minLeft, maxLeft);
        window.Top = ClampToScreen(top, minTop, maxTop);
    }

    private static double GetDimension(double actualValue, double configuredValue, double fallback)
    {
        if (actualValue > 0 && !double.IsNaN(actualValue))
        {
            return actualValue;
        }

        return configuredValue > 0 && !double.IsNaN(configuredValue) ? configuredValue : fallback;
    }

    private static double ClampToScreen(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
