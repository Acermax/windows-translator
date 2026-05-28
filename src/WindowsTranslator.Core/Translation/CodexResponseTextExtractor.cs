using System.Text;
using System.Text.Json;

namespace WindowsTranslator.Core.Translation;

public static class CodexResponseTextExtractor
{
    public static string? ExtractText(string body)
    {
        if (LooksLikeServerSentEvents(body))
        {
            var sseText = ExtractServerSentEventText(body);
            if (!string.IsNullOrWhiteSpace(sseText))
            {
                return sseText;
            }
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            {
                return outputText.GetString();
            }

            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                var builder = new StringBuilder();
                foreach (var item in output.EnumerateArray())
                {
                    AppendOutputItemText(item, builder);
                }

                if (builder.Length > 0)
                {
                    return builder.ToString();
                }
            }

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                return ExtractChatCompletionsText(choices);
            }

            if (root.TryGetProperty("error", out var error) && IsErrorValue(error))
            {
                var message = ExtractErrorMessage(error) ?? error.GetRawText();
                throw new TranslationException($"Codex endpoint returned an error: {message}");
            }
        }
        catch (JsonException ex)
        {
            throw new TranslationException("Codex endpoint returned invalid JSON.", ex);
        }

        return null;
    }

    private static bool LooksLikeServerSentEvents(string body)
    {
        var trimmed = body.TrimStart();
        return trimmed.StartsWith("event:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || body.Contains("\ndata:", StringComparison.OrdinalIgnoreCase)
            || body.Contains("\nevent:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractServerSentEventText(string body)
    {
        var builder = new StringBuilder();
        foreach (var line in body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = trimmed["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(data) || string.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;
                var eventType = root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                    ? type.GetString()
                    : null;

                if (string.Equals(eventType, "response.output_text.delta", StringComparison.Ordinal)
                    && root.TryGetProperty("delta", out var delta)
                    && delta.ValueKind == JsonValueKind.String)
                {
                    builder.Append(delta.GetString());
                }
                else if (string.Equals(eventType, "response.completed", StringComparison.Ordinal)
                    && root.TryGetProperty("response", out var response))
                {
                    var completedText = ExtractText(response.GetRawText());
                    if (!string.IsNullOrWhiteSpace(completedText))
                    {
                        return completedText;
                    }
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    private static void AppendOutputItemText(JsonElement item, StringBuilder builder)
    {
        if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }
        }
        else if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            builder.Append(text.GetString());
        }
    }

    private static string? ExtractChatCompletionsText(JsonElement choices)
    {
        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }
        }

        return null;
    }

    private static bool IsErrorValue(JsonElement error)
    {
        if (error.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (error.ValueKind == JsonValueKind.String)
        {
            return !string.IsNullOrWhiteSpace(error.GetString());
        }

        return true;
    }

    private static string? ExtractErrorMessage(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String)
        {
            return message.GetString();
        }

        return null;
    }
}
