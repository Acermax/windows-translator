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
    private CancellationTokenSource? _currentTranslation;

    public TranslationWorkflow(
        SettingsService settingsService,
        SelectedTextService selectedTextService,
        TranslationPopupService popupService)
    {
        _settingsService = settingsService;
        _selectedTextService = selectedTextService;
        _popupService = popupService;
    }

    public async Task TranslateSelectionAsync()
    {
        _currentTranslation?.Cancel();
        _currentTranslation?.Dispose();
        _currentTranslation = new CancellationTokenSource();
        var cancellationToken = _currentTranslation.Token;

        var cursorPosition = CursorPositionService.GetCursorPosition();

        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var selectedText = await _selectedTextService.GetSelectedTextAsync(settings.Capture, cursorPosition, cancellationToken);
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                _popupService.ShowError(cursorPosition, "No selected text was exposed by Windows UI Automation for the active app.");
                return;
            }

            _popupService.ShowLoading(cursorPosition, selectedText);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.Translation.TimeoutSeconds));

            using var httpClient = new HttpClient();
            var translator = new OpenAiCompatibleTranslationService(httpClient, settings.Translation);
            var result = await translator.TranslateAsync(new TranslationRequest(selectedText), timeout.Token);
            _popupService.ShowResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _popupService.ShowError(cursorPosition, ToFriendlyMessage(ex));
        }
    }

    public void Dispose()
    {
        _currentTranslation?.Cancel();
        _currentTranslation?.Dispose();
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
}
