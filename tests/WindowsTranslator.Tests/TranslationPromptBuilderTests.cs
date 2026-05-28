using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.Tests;

public sealed class TranslationPromptBuilderTests
{
    [Fact]
    public void BuildUserPrompt_IncludesTargetLanguageAndText()
    {
        var prompt = TranslationPromptBuilder.BuildUserPrompt("Hello world", "Spanish");

        Assert.Contains("Spanish", prompt);
        Assert.Contains("Hello world", prompt);
        Assert.Contains("Return only the translation", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ForEnglishTranslation_AlwaysTargetsEnglish()
    {
        var prompt = TranslationPromptBuilder.BuildUserPrompt(
            "Hola mundo",
            "Spanish",
            TextProcessingAction.TranslateToEnglish);

        Assert.Contains("Translate the following text to English", prompt);
        Assert.Contains("Return only the English translation", prompt);
        Assert.Contains("Hola mundo", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ForMultilineText_PreservesLineBreaksAndParagraphs()
    {
        const string text = "Linea uno\r\n\r\n- punto dos\r\n  detalle";

        var prompt = TranslationPromptBuilder.BuildUserPrompt(
            text,
            "English",
            TextProcessingAction.TranslateToEnglish);

        Assert.Contains("Do not merge separate lines or paragraphs", prompt);
        Assert.Contains("Keep empty lines empty", prompt);
        Assert.Contains(text, prompt);
    }

    [Fact]
    public void BuildUserPrompt_ForGrammarCorrection_KeepsOriginalLanguage()
    {
        var prompt = TranslationPromptBuilder.BuildUserPrompt(
            "This are wrong.",
            "Spanish",
            TextProcessingAction.CorrectGrammar);

        Assert.Contains("Correct grammar", prompt);
        Assert.Contains("Keep the original language", prompt);
        Assert.Contains("This are wrong.", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ForImproveWriting_KeepsOriginalLanguage()
    {
        var prompt = TranslationPromptBuilder.BuildUserPrompt(
            "This text is okay but flat.",
            "Spanish",
            TextProcessingAction.ImproveWriting);

        Assert.Contains("Improve the following text", prompt);
        Assert.Contains("Keep the original language", prompt);
        Assert.Contains("This text is okay but flat.", prompt);
    }
}
