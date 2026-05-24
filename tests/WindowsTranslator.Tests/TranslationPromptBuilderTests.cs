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
}
