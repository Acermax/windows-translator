using WindowsTranslator.Core.Translation;
using System.Windows.Threading;

namespace WindowsTranslator.App.Features.Popup;

internal sealed class TranslationPopupService
{
    private TranslationPopup? _popup;
    private System.Windows.Point? _anchorPosition;

    public event EventHandler? OptionsRequested;
    public event EventHandler<TextActionRequestedEventArgs>? TextActionRequested;
    public event EventHandler? ReplaceRequested;

    public void ShowLoading(System.Windows.Point cursorPosition, string sourceText, TextProcessingAction action)
    {
        _anchorPosition = cursorPosition;
        var popup = EnsurePopup();
        popup.SetLoading(sourceText, action);
        ShowAndPlace(popup, cursorPosition, placeAfterLayout: false);
    }

    public void ShowResult(TranslationResult result, TextProcessingAction action)
    {
        var popup = EnsurePopup();
        popup.SetResult(result, action);
        if (_anchorPosition is { } anchorPosition)
        {
            ShowAndPlace(popup, anchorPosition, placeAfterLayout: true);
        }
    }

    public void ShowError(System.Windows.Point cursorPosition, string message, bool canRunActions = false)
    {
        _anchorPosition = cursorPosition;
        var popup = EnsurePopup();
        popup.SetError(message, canRunActions);
        ShowAndPlace(popup, cursorPosition, placeAfterLayout: true);
    }

    public void ShowError(string message, bool canRunActions = false)
    {
        var popup = EnsurePopup();
        popup.SetError(message, canRunActions);
        if (_anchorPosition is { } anchorPosition)
        {
            ShowAndPlace(popup, anchorPosition, placeAfterLayout: true);
        }
    }

    public void Close()
    {
        _popup?.Close();
    }

    private TranslationPopup EnsurePopup()
    {
        if (_popup is { IsVisible: true })
        {
            return _popup;
        }

        _popup = new TranslationPopup();
        _popup.OptionsRequested += HandleOptionsRequested;
        _popup.TextActionRequested += HandleTextActionRequested;
        _popup.ReplaceRequested += HandleReplaceRequested;
        _popup.Closed += (_, _) => _popup = null;
        return _popup;
    }

    private void HandleOptionsRequested(object? sender, EventArgs e)
    {
        _popup?.Close();
        OptionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HandleTextActionRequested(object? sender, TextActionRequestedEventArgs e)
    {
        TextActionRequested?.Invoke(this, e);
    }

    private void HandleReplaceRequested(object? sender, EventArgs e)
    {
        ReplaceRequested?.Invoke(this, EventArgs.Empty);
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
