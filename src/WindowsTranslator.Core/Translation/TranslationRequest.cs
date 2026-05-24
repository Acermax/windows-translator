namespace WindowsTranslator.Core.Translation;

public sealed record TranslationRequest(string Text, string? TargetLanguage = null);
