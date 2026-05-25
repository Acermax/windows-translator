namespace WindowsTranslator.Core.Translation;

public static class TranslationPromptBuilder
{
    public const string SystemPrompt = "You are a precise text editing and translation engine. Return only the requested text, with no explanations.";

    public static string BuildUserPrompt(string text, string targetLanguage)
    {
        return BuildUserPrompt(text, targetLanguage, TextProcessingAction.Translate);
    }

    public static string BuildUserPrompt(string text, string targetLanguage, TextProcessingAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        return action switch
        {
            TextProcessingAction.Translate => BuildTranslationPrompt(text, targetLanguage),
            TextProcessingAction.TranslateToEnglish => BuildEnglishTranslationPrompt(text),
            TextProcessingAction.CorrectGrammar => BuildGrammarPrompt(text),
            TextProcessingAction.ImproveWriting => BuildWritingPrompt(text),
            _ => BuildTranslationPrompt(text, targetLanguage)
        };
    }

    private static string BuildTranslationPrompt(string text, string targetLanguage)
    {
        return $$"""
Translate the following text to {{targetLanguage}}.
Preserve meaning, tone, names, URLs, code identifiers, and basic formatting.
Return only the translation.

Text:
{{text}}
""";
    }

    private static string BuildEnglishTranslationPrompt(string text)
    {
        return $$"""
Translate the following text to English.
Preserve meaning, tone, names, URLs, code identifiers, and basic formatting.
Do not keep the original language unless the text is already English.
Return only the English translation.

Text:
{{text}}
""";
    }

    private static string BuildGrammarPrompt(string text)
    {
        return $$"""
Correct grammar, spelling, punctuation, and small typos in the following text.
Keep the original language, meaning, tone, names, URLs, code identifiers, and basic formatting.
Do not rewrite style unless needed for correctness.
Return only the corrected text.

Text:
{{text}}
""";
    }

    private static string BuildWritingPrompt(string text)
    {
        return $$"""
Improve the following text for clarity, natural phrasing, and readability.
Keep the original language, meaning, tone, names, URLs, code identifiers, and basic formatting.
Return only the improved text.

Text:
{{text}}
""";
    }
}
