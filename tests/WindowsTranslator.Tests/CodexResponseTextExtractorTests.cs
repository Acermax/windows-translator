using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.Tests;

public sealed class CodexResponseTextExtractorTests
{
    [Fact]
    public void ExtractText_ParsesSseThatStartsWithEventLines()
    {
        var body = """
event: response.created
data: {"type":"response.created","response":{"id":"resp_123"}}

event: response.output_text.delta
data: {"type":"response.output_text.delta","delta":"O"}

event: response.output_text.delta
data: {"type":"response.output_text.delta","delta":"K"}

event: response.completed
data: {"type":"response.completed","response":{"id":"resp_123","output":[],"error":null}}

""";

        var text = CodexResponseTextExtractor.ExtractText(body);

        Assert.Equal("OK", text);
    }

    [Fact]
    public void ExtractText_ParsesJsonOutputText()
    {
        var text = CodexResponseTextExtractor.ExtractText("""
{
  "output_text": "Hello"
}
""");

        Assert.Equal("Hello", text);
    }

    [Fact]
    public void ExtractText_IgnoresNullError()
    {
        var text = CodexResponseTextExtractor.ExtractText("""
{
  "id": "resp_123",
  "output": [],
  "error": null
}
""");

        Assert.Null(text);
    }

    [Fact]
    public void ExtractText_ThrowsFriendlyErrorForRealErrorObject()
    {
        var exception = Assert.Throws<TranslationException>(() => CodexResponseTextExtractor.ExtractText("""
{
  "error": {
    "message": "Bad request"
  }
}
"""));

        Assert.Equal("Codex endpoint returned an error: Bad request", exception.Message);
    }

    [Fact]
    public void ExtractText_ThrowsFriendlyErrorForInvalidJson()
    {
        var exception = Assert.Throws<TranslationException>(() => CodexResponseTextExtractor.ExtractText("eventually not json"));

        Assert.Equal("Codex endpoint returned invalid JSON.", exception.Message);
    }
}
