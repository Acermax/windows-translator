using System.Globalization;
using System.Net.Http;
using System.Windows;
using WindowsTranslator.App.Features.Codex;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Settings;

public partial class SettingsWindow : Window
{
    private static readonly string[] DefaultCodexModels =
    [
        "gpt-5.5",
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.3-codex",
        "gpt-5.2"
    ];

    private CancellationTokenSource? _modelsCancellation;
    private readonly CodexOAuthTokenStore _codexTokenStore = new();
    private readonly CodexOAuthLoginService _codexLoginService;

    public SettingsWindow(AppSettings settings)
    {
        Settings = settings;
        Settings.Normalize();
        _codexLoginService = new CodexOAuthLoginService(_codexTokenStore);
        InitializeComponent();
        LoadSettings();
        Loaded += SettingsWindow_Loaded;
    }

    public AppSettings Settings { get; }

    private void LoadSettings()
    {
        ProviderComboBox.SelectedValue = Settings.Translation.Provider;
        if (ProviderComboBox.SelectedValue is null)
        {
            ProviderComboBox.SelectedIndex = 0;
        }

        EndpointTextBox.Text = Settings.Translation.Endpoint;
        ApiKeyPasswordBox.Password = Settings.Translation.ApiKey;
        ModelComboBox.Items.Add(Settings.Translation.Model);
        ModelComboBox.Text = Settings.Translation.Model;
        ModelsStatusTextBlock.Text = "Refresh to load models from the configured endpoint.";
        LoadCodexModels(Settings.Translation.CodexOAuth.Model);
        CodexReasoningEffortComboBox.SelectedValue = Settings.Translation.CodexOAuth.ReasoningEffort;
        if (CodexReasoningEffortComboBox.SelectedValue is null)
        {
            CodexReasoningEffortComboBox.SelectedIndex = 0;
        }

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
        ClipboardFallbackCheckBox.IsChecked = Settings.Capture.UseClipboardFallback;
        ClipboardFallbackDelayTextBox.Text = Settings.Capture.ClipboardFallbackDelayMs.ToString();

        CtrlCheckBox.IsChecked = Settings.Hotkey.Ctrl;
        AltCheckBox.IsChecked = Settings.Hotkey.Alt;
        ShiftCheckBox.IsChecked = Settings.Hotkey.Shift;
        WinCheckBox.IsChecked = Settings.Hotkey.Win;
        HotkeyKeyTextBox.Text = Settings.Hotkey.Key;
        UpdateProviderPanels();
        UpdateCodexStatus();
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (SelectedProvider == TranslationProvider.OpenAiCompatible)
        {
            await RefreshModelsAsync(showSuccess: false);
        }
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync(showSuccess: true);
    }

    private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateProviderPanels();
    }

    private async void CodexLoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateCodexSettingsFromUi(out var codexSettings))
        {
            return;
        }

        CodexLoginButton.IsEnabled = false;
        CodexLogoutButton.IsEnabled = false;
        CodexStatusTextBlock.Text = "Opening browser for ChatGPT sign-in...";

        try
        {
            var tokens = await _codexLoginService.SignInAsync(codexSettings, CancellationToken.None);
            Settings.Translation.CodexOAuth = codexSettings;
            UpdateCodexStatus(tokens);
        }
        catch (Exception ex) when (ex is TranslationException or HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException)
        {
            CodexStatusTextBlock.Text = $"Could not sign in: {ex.Message}";
        }
        finally
        {
            CodexLoginButton.IsEnabled = true;
            CodexLogoutButton.IsEnabled = HasCodexTokens();
        }
    }

    private void CodexLogoutButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _codexTokenStore.Delete();
            UpdateCodexStatus();
        }
        catch (TranslationException ex)
        {
            CodexStatusTextBlock.Text = ex.Message;
        }
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

        if (!int.TryParse(ClipboardFallbackDelayTextBox.Text, out var clipboardFallbackDelayMs))
        {
            ShowValidationError("Clipboard fallback delay ms must be a number.");
            return;
        }

        Settings.Translation.Provider = SelectedProvider;
        Settings.Translation.Endpoint = EndpointTextBox.Text;
        Settings.Translation.Model = ModelComboBox.Text;
        Settings.Translation.ApiKey = ApiKeyPasswordBox.Password;
        Settings.Translation.CodexOAuth.Model = CodexModelComboBox.Text;
        Settings.Translation.CodexOAuth.ReasoningEffort = SelectedCodexReasoningEffort;
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
        Settings.Capture.UseClipboardFallback = ClipboardFallbackCheckBox.IsChecked == true;
        Settings.Capture.ClipboardFallbackDelayMs = clipboardFallbackDelayMs;

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
        if (SelectedProvider != TranslationProvider.OpenAiCompatible)
        {
            ModelsStatusTextBlock.Text = "Codex OAuth models are selected from the Codex settings section.";
            return;
        }

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

    private string SelectedProvider => ProviderComboBox.SelectedValue?.ToString() ?? TranslationProvider.OpenAiCompatible;

    private void UpdateProviderPanels()
    {
        if (OpenAiProviderPanel is null
            || CodexProviderPanel is null
            || OpenAiAdvancedPanel is null
            || SettingsHintTextBlock is null)
        {
            return;
        }

        var isOpenAiCompatible = SelectedProvider == TranslationProvider.OpenAiCompatible;
        OpenAiProviderPanel.Visibility = isOpenAiCompatible ? Visibility.Visible : Visibility.Collapsed;
        OpenAiAdvancedPanel.Visibility = isOpenAiCompatible ? Visibility.Visible : Visibility.Collapsed;
        CodexProviderPanel.Visibility = isOpenAiCompatible ? Visibility.Collapsed : Visibility.Visible;
        SettingsHintTextBlock.Text = isOpenAiCompatible
            ? "For local vLLM without auth, leave API key empty. Use Refresh to load served models, or type a model name manually."
            : "Codex OAuth uses your ChatGPT sign-in. Set Thinking to Off for faster translation.";

        if (!isOpenAiCompatible)
        {
            UpdateCodexStatus();
        }
    }

    private void LoadCodexModels(string currentModel)
    {
        CodexModelComboBox.Items.Clear();
        foreach (var model in DefaultCodexModels)
        {
            CodexModelComboBox.Items.Add(model);
        }

        CodexModelComboBox.Text = DefaultCodexModels.Contains(currentModel)
            ? currentModel
            : DefaultCodexModels[0];
    }

    private string SelectedCodexReasoningEffort =>
        CodexReasoningEffortComboBox.SelectedValue?.ToString() ?? CodexReasoningEffort.None;

    private bool TryCreateCodexSettingsFromUi(out CodexOAuthSettings codexSettings)
    {
        codexSettings = new CodexOAuthSettings
        {
            Issuer = Settings.Translation.CodexOAuth.Issuer,
            ClientId = Settings.Translation.CodexOAuth.ClientId,
            Endpoint = Settings.Translation.CodexOAuth.Endpoint,
            Model = CodexModelComboBox.Text,
            ReasoningEffort = SelectedCodexReasoningEffort,
            CallbackPort = Settings.Translation.CodexOAuth.CallbackPort
        };
        codexSettings.Normalize();
        return true;
    }

    private void UpdateCodexStatus()
    {
        try
        {
            UpdateCodexStatus(_codexTokenStore.Load());
        }
        catch (TranslationException ex)
        {
            CodexStatusTextBlock.Text = ex.Message;
            CodexLogoutButton.IsEnabled = true;
        }
    }

    private void UpdateCodexStatus(CodexOAuthTokenSet? tokens)
    {
        if (tokens is null)
        {
            CodexStatusTextBlock.Text = "Not signed in.";
            CodexLogoutButton.IsEnabled = false;
            return;
        }

        var identity = !string.IsNullOrWhiteSpace(tokens.Email)
            ? tokens.Email
            : tokens.AccountId;
        var expires = tokens.ExpiresAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        CodexStatusTextBlock.Text = string.IsNullOrWhiteSpace(identity)
            ? $"Signed in. Token expires {expires}."
            : $"Signed in as {identity}. Token expires {expires}.";
        CodexLogoutButton.IsEnabled = true;
    }

    private bool HasCodexTokens()
    {
        try
        {
            return _codexTokenStore.Load() is not null;
        }
        catch (TranslationException)
        {
            return true;
        }
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
