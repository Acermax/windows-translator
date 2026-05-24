using System.Windows;
using Forms = System.Windows.Forms;

namespace WindowsTranslator.App.Infrastructure.Windows;

internal static class CursorPositionService
{
    public static System.Windows.Point GetCursorPosition()
    {
        var position = Forms.Cursor.Position;
        return new System.Windows.Point(position.X, position.Y);
    }
}
