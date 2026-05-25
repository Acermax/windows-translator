namespace WindowsTranslator.Core.Translation;

public sealed record TranslationRequest(
    string Text,
    string? TargetLanguage = null,
    TextProcessingAction Action = TextProcessingAction.Translate);
