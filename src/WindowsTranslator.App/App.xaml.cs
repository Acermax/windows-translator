using System.Windows;
using WindowsTranslator.App.Features.Hotkeys;
using WindowsTranslator.App.Features.Popup;
using WindowsTranslator.App.Features.Selection;
using WindowsTranslator.App.Features.Settings;
using WindowsTranslator.App.Features.Translation;
using WindowsTranslator.App.Features.Tray;
using WindowsTranslator.App.Infrastructure.Windows;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.App;

public partial class App : System.Windows.Application
{
    private SettingsService? _settingsService;
    private TrayIconService? _trayIconService;
    private GlobalHotkeyService? _hotkeyService;
    private TranslationWorkflow? _translationWorkflow;
    private TranslationPopupService? _popupService;
    private SettingsWindow? _settingsWindow;
    private ConsoleShutdownService? _consoleShutdownService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Windows Translator startup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _translationWorkflow?.Dispose();
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _consoleShutdownService?.Dispose();
        base.OnExit(e);
    }

    private async Task InitializeAsync()
    {
        _consoleShutdownService = new ConsoleShutdownService();
        _consoleShutdownService.Start();

        _settingsService = new SettingsService();
        var settings = await _settingsService.LoadAsync();

        _popupService = new TranslationPopupService();
        _popupService.OptionsRequested += HandleSettingsRequested;
        _translationWorkflow = new TranslationWorkflow(
            _settingsService,
            new SelectedTextService(),
            _popupService);

        _trayIconService = new TrayIconService();
        _trayIconService.TranslateRequested += HandleTranslateRequested;
        _trayIconService.SettingsRequested += HandleSettingsRequested;
        _trayIconService.ExitRequested += HandleExitRequested;

        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.HotkeyPressed += HandleTranslateRequested;
        RegisterHotkey(settings.Hotkey);
    }

    private void RegisterHotkey(HotkeySettings hotkey)
    {
        try
        {
            _hotkeyService?.Register(hotkey);
        }
        catch (Exception ex)
        {
            _trayIconService?.ShowError("Hotkey not registered", ex.Message);
        }
    }

    private void HandleTranslateRequested(object? sender, EventArgs e)
    {
        _ = _translationWorkflow?.TranslateSelectionAsync();
    }

    private void HandleSettingsRequested(object? sender, EventArgs e)
    {
        _ = ShowSettingsAsync();
    }

    private void HandleExitRequested(object? sender, EventArgs e)
    {
        Shutdown();
    }

    private async Task ShowSettingsAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        if (_settingsWindow?.IsVisible == true)
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            var settings = await _settingsService.LoadAsync();
            _settingsWindow = new SettingsWindow(settings);
            var saved = _settingsWindow.ShowDialog() == true;
            if (!saved)
            {
                return;
            }

            await _settingsService.SaveAsync(_settingsWindow.Settings);
            RegisterHotkey(_settingsWindow.Settings.Hotkey);
            _trayIconService?.ShowInfo("Settings saved", "Translator settings were updated.");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Settings error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _settingsWindow = null;
        }
    }
}
