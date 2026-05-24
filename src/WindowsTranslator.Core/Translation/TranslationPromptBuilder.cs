namespace WindowsTranslator.Core.Translation;

public static class TranslationPromptBuilder
{
    public const string SystemPrompt = "You are a precise translation engine. Return only the translated text, with no explanations.";

    public static string BuildUserPrompt(string text, string targetLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        return $$"""
Translate the following text to {{targetLanguage}}.
Preserve meaning, tone, names, URLs, code identifiers, and basic formatting.
Return only the translation.

Text:
{{text}}
""";
    }
}
