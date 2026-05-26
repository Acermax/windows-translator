using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace WindowsTranslator.App.Features.Selection;

internal static class ExcelSelectionService
{
    public static string? TryGetActiveCellText(int maxCharacters)
    {
        try
        {
            var excel = ComRunningObjectService.GetActiveObject("Excel.Application");
            if (excel is null)
            {
                return null;
            }

            try
            {
                return TryGetTextFromExcel(excel, maxCharacters);
            }
            finally
            {
                Marshal.FinalReleaseComObject(excel);
            }
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (RuntimeBinderException)
        {
            return null;
        }
    }

    private static string? TryGetTextFromExcel(dynamic excel, int maxCharacters)
    {
        var selectionText = TryFormatValue(excel.Selection?.Text, maxCharacters);
        if (!string.IsNullOrWhiteSpace(selectionText))
        {
            return selectionText;
        }

        var activeCellText = TryFormatValue(excel.ActiveCell?.Text, maxCharacters);
        if (!string.IsNullOrWhiteSpace(activeCellText))
        {
            return activeCellText;
        }

        return TryFormatValue(excel.ActiveCell?.Value2, maxCharacters);
    }

    private static string? TryFormatValue(object? value, int maxCharacters)
    {
        if (value is null || maxCharacters <= 0)
        {
            return null;
        }

        var text = value switch
        {
            string stringValue => stringValue,
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            _ => value.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        return text.Length <= maxCharacters ? text : text[..maxCharacters];
    }
}
