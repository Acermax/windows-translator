namespace WindowsTranslator.Core.Settings;

public sealed class AppSettings
{
    public HotkeySettings Hotkey { get; set; } = new();

    public CaptureSettings Capture { get; set; } = new();

    public TranslationSettings Translation { get; set; } = new();

    public static AppSettings CreateDefault() => new();

    public void Normalize()
    {
        Hotkey ??= new HotkeySettings();
        Capture ??= new CaptureSettings();
        Translation ??= new TranslationSettings();

        Hotkey.Normalize();
        Capture.Normalize();
        Translation.Normalize();
    }
}

public sealed class HotkeySettings
{
    public bool Ctrl { get; set; } = true;

    public bool Alt { get; set; }

    public bool Shift { get; set; } = true;

    public bool Win { get; set; }

    public string Key { get; set; } = "Z";

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            Key = "Z";
        }

        Key = Key.Trim().ToUpperInvariant();
    }
}

public sealed class CaptureSettings
{
    public int FocusSettleDelayMs { get; set; } = 50;

    public int MaxSelectedTextCharacters { get; set; } = 100_000;

    public int MaxAutomationElements { get; set; } = 1_500;

    public string Method { get; set; } = "UIAutomation";

    public void Normalize()
    {
        FocusSettleDelayMs = Math.Clamp(FocusSettleDelayMs, 0, 2_000);
        MaxSelectedTextCharacters = Math.Clamp(MaxSelectedTextCharacters, 100, 1_000_000);
        MaxAutomationElements = Math.Clamp(MaxAutomationElements, 10, 20_000);
        Method = string.IsNullOrWhiteSpace(Method) ? "UIAutomation" : Method.Trim();
    }
}

public sealed class TranslationSettings
{
    public string Endpoint { get; set; } = "http://localhost:8000/v1/chat/completions";

    public string Model { get; set; } = "local-model";

    public string ApiKey { get; set; } = string.Empty;

    public string TargetLanguage { get; set; } = "Spanish";

    public double Temperature { get; set; } = 0.6;

    public double TopP { get; set; } = 0.95;

    public int TopK { get; set; } = 20;

    public double MinP { get; set; } = 0.0;

    public double PresencePenalty { get; set; } = 0.0;

    public double RepetitionPenalty { get; set; } = 1.0;

    public bool IncludeReasoning { get; set; }

    public bool EnableThinking { get; set; }

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxInputCharacters { get; set; } = 12_000;

    public int MaxOutputTokens { get; set; } = 2_048;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            Endpoint = "http://localhost:8000/v1/chat/completions";
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            Model = "local-model";
        }

        if (string.IsNullOrWhiteSpace(TargetLanguage))
        {
            TargetLanguage = "Spanish";
        }

        Endpoint = Endpoint.Trim();
        Model = Model.Trim();
        TargetLanguage = TargetLanguage.Trim();
        ApiKey = ApiKey?.Trim() ?? string.Empty;
        Temperature = Math.Clamp(Temperature, 0, 2);
        TopP = Math.Clamp(TopP, 0, 1);
        TopK = Math.Clamp(TopK, 0, 100_000);
        MinP = Math.Clamp(MinP, 0, 1);
        PresencePenalty = Math.Clamp(PresencePenalty, -2, 2);
        RepetitionPenalty = Math.Clamp(RepetitionPenalty, 0.01, 10);
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 1, 300);
        MaxInputCharacters = Math.Clamp(MaxInputCharacters, 100, 100_000);
        MaxOutputTokens = Math.Clamp(MaxOutputTokens, 64, 32_768);
    }
}
