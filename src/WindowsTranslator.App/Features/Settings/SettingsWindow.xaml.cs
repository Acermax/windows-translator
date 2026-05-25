using System.Globalization;
using System.Net.Http;
using System.Windows;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Settings;

public partial class SettingsWindow : Window
{
    private CancellationTokenSource? _modelsCancellation;

    public SettingsWindow(AppSettings settings)
    {
        Settings = settings;
        Settings.Normalize();
        InitializeComponent();
        LoadSettings();
        Loaded += SettingsWindow_Loaded;
    }

    public AppSettings Settings { get; }

    private void LoadSettings()
    {
        EndpointTextBox.Text = Settings.Translation.Endpoint;
        ApiKeyPasswordBox.Password = Settings.Translation.ApiKey;
        ModelComboBox.Items.Add(Settings.Translation.Model);
        ModelComboBox.Text = Settings.Translation.Model;
        ModelsStatusTextBlock.Text = "Refresh to load models from the configured endpoint.";
        TargetLanguageTextBox.Text = Settings.Translation.TargetLanguage;
        TimeoutTextBox.Text = Settings.Translation.TimeoutSeconds.ToString();
        MaxInputTextBox.Text = Settings.Translation.MaxInputCharacters.ToString();
        MaxOutputTextBox.Text = Settings.Translation.MaxOutputTokens.ToString();
        TemperatureTextBox.Text = FormatDouble(Settings.Translation.Temperature);
        TopPTextBox.Text = FormatDouble(Settings.Translation.TopP);
        TopKTextBox.Text = Settings.Translation.TopK.ToString();
        MinPTextBox.Text = FormatDouble(Settings.Translation.MinP);
        PresencePenaltyTextBox.Text = FormatDouble(Settings.Translation.PresencePenalty);
        RepetitionPenaltyTextBox.Text = FormatDouble(Settings.Translation.RepetitionPenalty);
        EnableThinkingCheckBox.IsChecked = Settings.Translation.EnableThinking;
        IncludeReasoningCheckBox.IsChecked = Settings.Translation.IncludeReasoning;

        CtrlCheckBox.IsChecked = Settings.Hotkey.Ctrl;
        AltCheckBox.IsChecked = Settings.Hotkey.Alt;
        ShiftCheckBox.IsChecked = Settings.Hotkey.Shift;
        WinCheckBox.IsChecked = Settings.Hotkey.Win;
        HotkeyKeyTextBox.Text = Settings.Hotkey.Key;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync(showSuccess: false);
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync(showSuccess: true);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimeoutTextBox.Text, out var timeoutSeconds))
        {
            ShowValidationError("Timeout seconds must be a number.");
            return;
        }

        if (!int.TryParse(MaxInputTextBox.Text, out var maxInputCharacters))
        {
            ShowValidationError("Max input chars must be a number.");
            return;
        }

        if (!int.TryParse(MaxOutputTextBox.Text, out var maxOutputTokens))
        {
            ShowValidationError("Max output tokens must be a number.");
            return;
        }

        if (!TryParseDouble(TemperatureTextBox.Text, out var temperature))
        {
            ShowValidationError("Temperature must be a number.");
            return;
        }

        if (!TryParseDouble(TopPTextBox.Text, out var topP))
        {
            ShowValidationError("Top P must be a number.");
            return;
        }

        if (!int.TryParse(TopKTextBox.Text, out var topK))
        {
            ShowValidationError("Top K must be a number.");
            return;
        }

        if (!TryParseDouble(MinPTextBox.Text, out var minP))
        {
            ShowValidationError("Min P must be a number.");
            return;
        }

        if (!TryParseDouble(PresencePenaltyTextBox.Text, out var presencePenalty))
        {
            ShowValidationError("Presence penalty must be a number.");
            return;
        }

        if (!TryParseDouble(RepetitionPenaltyTextBox.Text, out var repetitionPenalty))
        {
            ShowValidationError("Repetition penalty must be a number.");
            return;
        }

        Settings.Translation.Endpoint = EndpointTextBox.Text;
        Settings.Translation.Model = ModelComboBox.Text;
        Settings.Translation.ApiKey = ApiKeyPasswordBox.Password;
        Settings.Translation.TargetLanguage = TargetLanguageTextBox.Text;
        Settings.Translation.TimeoutSeconds = timeoutSeconds;
        Settings.Translation.MaxInputCharacters = maxInputCharacters;
        Settings.Translation.MaxOutputTokens = maxOutputTokens;
        Settings.Translation.Temperature = temperature;
        Settings.Translation.TopP = topP;
        Settings.Translation.TopK = topK;
        Settings.Translation.MinP = minP;
        Settings.Translation.PresencePenalty = presencePenalty;
        Settings.Translation.RepetitionPenalty = repetitionPenalty;
        Settings.Translation.EnableThinking = EnableThinkingCheckBox.IsChecked == true;
        Settings.Translation.IncludeReasoning = IncludeReasoningCheckBox.IsChecked == true;

        Settings.Hotkey.Ctrl = CtrlCheckBox.IsChecked == true;
        Settings.Hotkey.Alt = AltCheckBox.IsChecked == true;
        Settings.Hotkey.Shift = ShiftCheckBox.IsChecked == true;
        Settings.Hotkey.Win = WinCheckBox.IsChecked == true;
        Settings.Hotkey.Key = HotkeyKeyTextBox.Text;
        Settings.Normalize();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _modelsCancellation?.Cancel();
        _modelsCancellation?.Dispose();
        base.OnClosed(e);
    }

    private async Task RefreshModelsAsync(bool showSuccess)
    {
        _modelsCancellation?.Cancel();
        _modelsCancellation?.Dispose();
        _modelsCancellation = new CancellationTokenSource();
        var cancellationToken = _modelsCancellation.Token;

        var currentModel = ModelComboBox.Text.Trim();
        RefreshModelsButton.IsEnabled = false;
        ModelsStatusTextBlock.Text = "Loading models...";

        try
        {
            using var httpClient = new HttpClient();
            var modelService = new OpenAiCompatibleModelService(httpClient, CreateModelLookupSettings());
            var models = await modelService.ListModelsAsync(cancellationToken);

            ModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                ModelComboBox.Items.Add(model);
            }

            if (!string.IsNullOrWhiteSpace(currentModel))
            {
                ModelComboBox.Text = currentModel;
            }
            else if (models.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }

            ModelsStatusTextBlock.Text = showSuccess
                ? $"Loaded {models.Count} model(s)."
                : string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is TranslationException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            ModelsStatusTextBlock.Text = $"Could not load models: {ex.Message}";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                RefreshModelsButton.IsEnabled = true;
            }
        }
    }

    private TranslationSettings CreateModelLookupSettings()
    {
        var settings = new TranslationSettings
        {
            Endpoint = EndpointTextBox.Text,
            Model = ModelComboBox.Text,
            ApiKey = ApiKeyPasswordBox.Password,
            TimeoutSeconds = Settings.Translation.TimeoutSeconds
        };

        if (int.TryParse(TimeoutTextBox.Text, out var timeoutSeconds))
        {
            settings.TimeoutSeconds = timeoutSeconds;
        }

        settings.Normalize();
        return settings;
    }

    private static void ShowValidationError(string message)
    {
        System.Windows.MessageBox.Show(message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string FormatDouble(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }
}
