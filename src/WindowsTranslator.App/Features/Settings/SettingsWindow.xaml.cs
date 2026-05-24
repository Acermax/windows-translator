using System.Globalization;
using System.Windows;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.App.Features.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        Settings = settings;
        Settings.Normalize();
        InitializeComponent();
        LoadSettings();
    }

    public AppSettings Settings { get; }

    private void LoadSettings()
    {
        EndpointTextBox.Text = Settings.Translation.Endpoint;
        ModelTextBox.Text = Settings.Translation.Model;
        ApiKeyPasswordBox.Password = Settings.Translation.ApiKey;
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
        Settings.Translation.Model = ModelTextBox.Text;
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
