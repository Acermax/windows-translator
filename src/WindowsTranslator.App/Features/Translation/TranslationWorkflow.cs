using System.Net.Http;
using WindowsTranslator.App.Features.Popup;
using WindowsTranslator.App.Features.Selection;
using WindowsTranslator.App.Infrastructure.Windows;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Translation;

internal sealed class TranslationWorkflow : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly SelectedTextService _selectedTextService;
    private readonly TranslationPopupService _popupService;
    private readonly SelectionReplacementService _replacementService = new();
    private CancellationTokenSource? _currentTranslation;
    private ActiveSelection? _activeSelection;
    private string? _currentReplacementText;

    public TranslationWorkflow(
        SettingsService settingsService,
        SelectedTextService selectedTextService,
        TranslationPopupService popupService)
    {
        _settingsService = settingsService;
        _selectedTextService = selectedTextService;
        _popupService = popupService;
        _popupService.TextActionRequested += HandleTextActionRequested;
        _popupService.ReplaceRequested += HandleReplaceRequested;
    }

    public async Task TranslateSelectionAsync()
    {
        var cancellationToken = BeginTranslation();
        var cursorPosition = CursorPositionService.GetCursorPosition();
        var targetWindowHandle = NativeMethods.GetForegroundWindow();
        _activeSelection = null;
        _currentReplacementText = null;

        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var selectedText = await _selectedTextService.GetSelectedTextAsync(settings.Capture, cursorPosition, cancellationToken);
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                _popupService.ShowError(cursorPosition, "No selected text or editable value was exposed by Windows UI Automation for the active app.");
                return;
            }

            _activeSelection = new ActiveSelection(selectedText, cursorPosition, targetWindowHandle);
            await RunTextActionAsync(TextProcessingAction.Translate, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _popupService.ShowError(cursorPosition, ToFriendlyMessage(ex), _activeSelection is not null);
        }
    }

    public void Dispose()
    {
        _popupService.TextActionRequested -= HandleTextActionRequested;
        _popupService.ReplaceRequested -= HandleReplaceRequested;
        _currentTranslation?.Cancel();
        _currentTranslation?.Dispose();
    }

    private CancellationToken BeginTranslation()
    {
        _currentTranslation?.Cancel();
        _currentTranslation?.Dispose();
        _currentTranslation = new CancellationTokenSource();
        return _currentTranslation.Token;
    }

    private async void HandleTextActionRequested(object? sender, TextActionRequestedEventArgs e)
    {
        var cancellationToken = BeginTranslation();

        try
        {
            await RunTextActionAsync(e.Action, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            var cursorPosition = _activeSelection?.CursorPosition ?? CursorPositionService.GetCursorPosition();
            _popupService.ShowError(cursorPosition, ToFriendlyMessage(ex), _activeSelection is not null);
        }
    }

    private async void HandleReplaceRequested(object? sender, EventArgs e)
    {
        if (_activeSelection is not { } activeSelection)
        {
            _popupService.ShowError("No active selection is available.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentReplacementText))
        {
            _popupService.ShowError("There is no text ready to replace the selection.", canRunActions: true);
            return;
        }

        var replacementText = _currentReplacementText;
        _popupService.Close();

        try
        {
            await _replacementService.ReplaceSelectionAsync(
                activeSelection.TargetWindowHandle,
                replacementText,
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            _popupService.ShowError(
                activeSelection.CursorPosition,
                $"Could not replace selection: {ex.Message}",
                canRunActions: true);
        }
    }

    private async Task RunTextActionAsync(TextProcessingAction action, CancellationToken cancellationToken)
    {
        if (_activeSelection is not { } activeSelection)
        {
            _popupService.ShowError("No active selection is available.");
            return;
        }

        var settings = await _settingsService.LoadAsync(cancellationToken);
        _popupService.ShowLoading(activeSelection.CursorPosition, activeSelection.SourceText, action);
        _currentReplacementText = null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.Translation.TimeoutSeconds));

        using var httpClient = new HttpClient();
        var translator = new OpenAiCompatibleTranslationService(httpClient, settings.Translation);
        var result = await translator.TranslateAsync(
            new TranslationRequest(activeSelection.SourceText, Action: action),
            timeout.Token);
        _currentReplacementText = result.Text;
        _popupService.ShowResult(result, action);
    }

    private static string ToFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            TranslationException => exception.Message,
            HttpRequestException => "Could not reach the translation endpoint. Check vLLM and settings.",
            TaskCanceledException => "Translation timed out.",
            _ => exception.Message
        };
    }

    private sealed record ActiveSelection(
        string SourceText,
        System.Windows.Point CursorPosition,
        IntPtr TargetWindowHandle);
}
