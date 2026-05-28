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

    public bool UseClipboardFallback { get; set; }

    public int ClipboardFallbackDelayMs { get; set; } = 160;

    public string Method { get; set; } = "UIAutomation";

    public void Normalize()
    {
        FocusSettleDelayMs = Math.Clamp(FocusSettleDelayMs, 0, 2_000);
        MaxSelectedTextCharacters = Math.Clamp(MaxSelectedTextCharacters, 100, 1_000_000);
        MaxAutomationElements = Math.Clamp(MaxAutomationElements, 10, 20_000);
        ClipboardFallbackDelayMs = Math.Clamp(ClipboardFallbackDelayMs, 40, 2_000);
        Method = string.IsNullOrWhiteSpace(Method) ? "UIAutomation" : Method.Trim();
    }
}

public sealed class TranslationSettings
{
    public string Provider { get; set; } = TranslationProvider.OpenAiCompatible;

    public string Endpoint { get; set; } = "http://localhost:8000/v1/chat/completions";

    public string Model { get; set; } = "local-model";

    public string ApiKey { get; set; } = string.Empty;

    public CodexOAuthSettings CodexOAuth { get; set; } = new();

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
        Provider = TranslationProvider.Normalize(Provider);
        CodexOAuth ??= new CodexOAuthSettings();
        CodexOAuth.Normalize();

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

public static class TranslationProvider
{
    public const string OpenAiCompatible = "OpenAiCompatible";

    public const string CodexOAuth = "CodexOAuth";

    public static string Normalize(string? provider)
    {
        if (string.Equals(provider, CodexOAuth, StringComparison.OrdinalIgnoreCase))
        {
            return CodexOAuth;
        }

        return OpenAiCompatible;
    }
}

public sealed class CodexOAuthSettings
{
    public string Issuer { get; set; } = "https://auth.openai.com";

    public string ClientId { get; set; } = "app_EMoamEEZ73f0CkXaXp7hrann";

    public string Endpoint { get; set; } = "https://chatgpt.com/backend-api/codex/responses";

    public string Model { get; set; } = "gpt-5.5";

    public string ReasoningEffort { get; set; } = CodexReasoningEffort.None;

    public int CallbackPort { get; set; } = 1455;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            Issuer = "https://auth.openai.com";
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            Endpoint = "https://chatgpt.com/backend-api/codex/responses";
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            Model = "gpt-5.5";
        }

        Issuer = Issuer.Trim().TrimEnd('/');
        ClientId = ClientId.Trim();
        Endpoint = Endpoint.Trim();
        Model = Model.Trim();
        if (string.Equals(Model, "gpt-5.2-codex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Model, "gpt-5-codex", StringComparison.OrdinalIgnoreCase))
        {
            Model = "gpt-5.5";
        }

        ReasoningEffort = CodexReasoningEffort.Normalize(ReasoningEffort);
        CallbackPort = Math.Clamp(CallbackPort, 1024, 65_535);
    }
}

public static class CodexReasoningEffort
{
    public const string None = "none";

    public const string Low = "low";

    public const string Medium = "medium";

    public const string High = "high";

    public const string ExtraHigh = "xhigh";

    public static string Normalize(string? effort)
    {
        return effort?.Trim().ToLowerInvariant() switch
        {
            Low => Low,
            Medium => Medium,
            High => High,
            ExtraHigh => ExtraHigh,
            _ => None
        };
    }
}
