using WindowsTranslator.Core.Translation;
using System.Windows.Threading;

namespace WindowsTranslator.App.Features.Popup;

internal sealed class TranslationPopupService
{
    private TranslationPopup? _popup;
    private System.Windows.Point? _anchorPosition;

    public event EventHandler? OptionsRequested;

    public void ShowLoading(System.Windows.Point cursorPosition, string sourceText)
    {
        _anchorPosition = cursorPosition;
        var popup = EnsurePopup();
        popup.SetLoading(sourceText);
        ShowAndPlace(popup, cursorPosition, placeAfterLayout: false);
    }

    public void ShowResult(TranslationResult result)
    {
        var popup = EnsurePopup();
        popup.SetResult(result);
        if (_anchorPosition is { } anchorPosition)
        {
            ShowAndPlace(popup, anchorPosition, placeAfterLayout: true);
        }
    }

    public void ShowError(System.Windows.Point cursorPosition, string message)
    {
        _anchorPosition = cursorPosition;
        var popup = EnsurePopup();
        popup.SetError(message);
        ShowAndPlace(popup, cursorPosition, placeAfterLayout: true);
    }

    public void ShowError(string message)
    {
        var popup = EnsurePopup();
        popup.SetError(message);
        if (_anchorPosition is { } anchorPosition)
        {
            ShowAndPlace(popup, anchorPosition, placeAfterLayout: true);
        }
    }

    private TranslationPopup EnsurePopup()
    {
        if (_popup is { IsVisible: true })
        {
            return _popup;
        }

        _popup = new TranslationPopup();
        _popup.OptionsRequested += HandleOptionsRequested;
        _popup.Closed += (_, _) => _popup = null;
        return _popup;
    }

    private void HandleOptionsRequested(object? sender, EventArgs e)
    {
        _popup?.Close();
        OptionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void ShowAndPlace(TranslationPopup popup, System.Windows.Point cursorPosition, bool placeAfterLayout)
    {
        if (!popup.IsVisible)
        {
            popup.Show();
        }

        popup.UpdateLayout();
        PopupPlacementService.PlaceNearCursor(popup, cursorPosition);

        if (!placeAfterLayout)
        {
            return;
        }

        popup.Dispatcher.BeginInvoke(
            () =>
            {
                popup.UpdateLayout();
                PopupPlacementService.PlaceNearCursor(popup, cursorPosition);
            },
            DispatcherPriority.Loaded);
    }
}
