using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using WindowsTranslator.App.Infrastructure.Windows;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.App.Features.Selection;

internal sealed class SelectedTextService
{
    public async Task<string?> GetSelectedTextAsync(
        CaptureSettings settings,
        System.Windows.Point cursorPosition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        if (settings.FocusSettleDelayMs > 0)
        {
            await Task.Delay(settings.FocusSettleDelayMs, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return TryGetSelectedText(settings, cursorPosition, cancellationToken);
    }

    private static string? TryGetSelectedText(
        CaptureSettings settings,
        System.Windows.Point cursorPosition,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in GetDirectCandidates(cursorPosition))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selectedText = TryGetSelectedTextFromElementAndAncestors(
                element,
                settings.MaxSelectedTextCharacters,
                visited);
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                return selectedText;
            }
        }

        foreach (var root in GetSearchRoots(cursorPosition))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selectedText = TryGetSelectedTextFromDescendants(root, settings, visited, cancellationToken);
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                return selectedText;
            }
        }

        var valueFallbackVisited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in GetValueFallbackCandidates(cursorPosition))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var valueText = TryGetWholeTextFromElementAndAncestors(
                element,
                settings.MaxSelectedTextCharacters,
                valueFallbackVisited);
            if (!string.IsNullOrWhiteSpace(valueText))
            {
                return valueText;
            }
        }

        return null;
    }

    private static IEnumerable<AutomationElement> GetDirectCandidates(System.Windows.Point cursorPosition)
    {
        var pointerElement = TryGetElementFromPoint(cursorPosition);
        if (pointerElement is not null)
        {
            yield return pointerElement;
        }

        var focusedElement = TryGetFocusedElement();
        if (focusedElement is not null)
        {
            yield return focusedElement;
        }

        var foregroundWindow = TryGetForegroundWindowElement();
        if (foregroundWindow is not null)
        {
            yield return foregroundWindow;
        }
    }

    private static IEnumerable<AutomationElement> GetValueFallbackCandidates(System.Windows.Point cursorPosition)
    {
        var focusedElement = TryGetFocusedElement();
        if (focusedElement is not null)
        {
            yield return focusedElement;
        }

        var pointerElement = TryGetElementFromPoint(cursorPosition);
        if (pointerElement is not null && !IsSameElement(focusedElement, pointerElement))
        {
            yield return pointerElement;
        }
    }

    private static IEnumerable<AutomationElement> GetSearchRoots(System.Windows.Point cursorPosition)
    {
        var pointerElement = TryGetElementFromPoint(cursorPosition);
        var pointerWindow = pointerElement is null ? null : TryGetTopLevelAncestor(pointerElement);
        if (pointerWindow is not null)
        {
            yield return pointerWindow;
        }

        var foregroundWindow = TryGetForegroundWindowElement();
        if (foregroundWindow is not null && !IsSameElement(pointerWindow, foregroundWindow))
        {
            yield return foregroundWindow;
        }
    }

    private static string? TryGetSelectedTextFromElementAndAncestors(
        AutomationElement element,
        int maxCharacters,
        HashSet<string> visited)
    {
        AutomationElement? current = element;
        for (var depth = 0; current is not null && depth < 12; depth++)
        {
            if (MarkVisited(current, visited))
            {
                var selectedText = TryGetSelectedTextFromElement(current, maxCharacters);
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    return selectedText;
                }
            }

            current = TryGetParent(current);
        }

        return null;
    }

    private static string? TryGetWholeTextFromElementAndAncestors(
        AutomationElement element,
        int maxCharacters,
        HashSet<string> visited)
    {
        AutomationElement? current = element;
        for (var depth = 0; current is not null && depth < 12; depth++)
        {
            if (MarkVisited(current, visited))
            {
                var text = TryGetWholeTextFromElement(current, maxCharacters);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            current = TryGetParent(current);
        }

        return null;
    }

    private static string? TryGetSelectedTextFromDescendants(
        AutomationElement root,
        CaptureSettings settings,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var textPatternCondition = new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true);
        AutomationElementCollection descendants;

        try
        {
            descendants = root.FindAll(TreeScope.Descendants, textPatternCondition);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }

        var count = Math.Min(descendants.Count, settings.MaxAutomationElements);
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = descendants[index];
            if (!MarkVisited(element, visited))
            {
                continue;
            }

            var selectedText = TryGetSelectedTextFromElement(element, settings.MaxSelectedTextCharacters);
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                return selectedText;
            }
        }

        return null;
    }

    private static string? TryGetSelectedTextFromElement(AutomationElement element, int maxCharacters)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) || pattern is not TextPattern textPattern)
            {
                return null;
            }

            var ranges = textPattern.GetSelection();
            if (ranges.Length == 0)
            {
                return null;
            }

            var remainingCharacters = maxCharacters;
            List<string> parts = [];
            foreach (var range in ranges)
            {
                var text = ReadRange(range, remainingCharacters).TrimEnd('\r', '\n');
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                parts.Add(text);
                remainingCharacters -= text.Length;
                if (remainingCharacters <= 0)
                {
                    break;
                }
            }

            return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? TryGetWholeTextFromElement(AutomationElement element, int maxCharacters)
    {
        try
        {
            if (!IsWholeTextCandidate(element))
            {
                return null;
            }

            var value = TryGetValuePatternText(element, maxCharacters);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var documentText = TryGetTextPatternDocumentText(element, maxCharacters);
            if (!string.IsNullOrWhiteSpace(documentText))
            {
                return documentText;
            }

            return TryGetNameText(element, maxCharacters);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? TryGetValuePatternText(AutomationElement element, int maxCharacters)
    {
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) || pattern is not ValuePattern valuePattern)
        {
            return null;
        }

        return TrimAndLimit(valuePattern.Current.Value, maxCharacters);
    }

    private static string? TryGetTextPatternDocumentText(AutomationElement element, int maxCharacters)
    {
        var controlType = element.Current.ControlType;
        if (controlType != ControlType.Edit
            && controlType != ControlType.DataItem
            && controlType != ControlType.Text)
        {
            return null;
        }

        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) || pattern is not TextPattern textPattern)
        {
            return null;
        }

        return TrimAndLimit(textPattern.DocumentRange.GetText(maxCharacters), maxCharacters);
    }

    private static string? TryGetNameText(AutomationElement element, int maxCharacters)
    {
        var controlType = element.Current.ControlType;
        if (controlType != ControlType.Edit
            && controlType != ControlType.DataItem
            && controlType != ControlType.Text)
        {
            return null;
        }

        var name = TrimAndLimit(element.Current.Name, maxCharacters);
        return IsLikelyCellAddress(name) ? null : name;
    }

    private static bool IsWholeTextCandidate(AutomationElement element)
    {
        var controlType = element.Current.ControlType;
        return controlType == ControlType.Edit
            || controlType == ControlType.DataItem
            || controlType == ControlType.Text
            || controlType == ControlType.Custom
            || controlType == ControlType.Document;
    }

    private static string? TrimAndLimit(string? text, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text) || maxCharacters <= 0)
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= maxCharacters ? trimmed : trimmed[..maxCharacters];
    }

    private static bool IsLikelyCellAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 8)
        {
            return false;
        }

        var index = 0;
        while (index < text.Length && char.IsAsciiLetter(text[index]))
        {
            index++;
        }

        if (index == 0 || index == text.Length)
        {
            return false;
        }

        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return index == text.Length;
    }

    private static string ReadRange(TextPatternRange range, int maxCharacters)
    {
        return maxCharacters <= 0 ? string.Empty : range.GetText(maxCharacters);
    }

    private static AutomationElement? TryGetElementFromPoint(System.Windows.Point point)
    {
        try
        {
            return AutomationElement.FromPoint(point);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static AutomationElement? TryGetFocusedElement()
    {
        try
        {
            return AutomationElement.FocusedElement;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static AutomationElement? TryGetForegroundWindowElement()
    {
        try
        {
            var handle = NativeMethods.GetForegroundWindow();
            return handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static AutomationElement? TryGetTopLevelAncestor(AutomationElement element)
    {
        AutomationElement? current = element;
        AutomationElement? topLevel = null;

        for (var depth = 0; current is not null && depth < 24; depth++)
        {
            var parent = TryGetParent(current);
            if (parent is null || IsSameElement(parent, AutomationElement.RootElement))
            {
                return topLevel ?? current;
            }

            topLevel = parent;
            current = parent;
        }

        return topLevel;
    }

    private static AutomationElement? TryGetParent(AutomationElement element)
    {
        try
        {
            return TreeWalker.ControlViewWalker.GetParent(element);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool MarkVisited(AutomationElement element, HashSet<string> visited)
    {
        return visited.Add(GetElementKey(element));
    }

    private static string GetElementKey(AutomationElement element)
    {
        try
        {
            return string.Join(".", element.GetRuntimeId());
        }
        catch (InvalidOperationException)
        {
            return element.GetHashCode().ToString();
        }
        catch (COMException)
        {
            return element.GetHashCode().ToString();
        }
    }

    private static bool IsSameElement(AutomationElement? first, AutomationElement? second)
    {
        if (first is null || second is null)
        {
            return false;
        }

        return GetElementKey(first) == GetElementKey(second);
    }
}
