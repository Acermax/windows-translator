namespace WindowsTranslator.Core.Translation;

public static class OpenAiCompatibleEndpointBuilder
{
    public static Uri BuildChatCompletionsUri(Uri endpoint)
    {
        var builder = CreateCleanBuilder(endpoint);
        var segments = GetSegments(builder);

        if (EndsWith(segments, "chat", "completions"))
        {
            return builder.Uri;
        }

        if (segments.Count == 0)
        {
            segments.Add("v1");
        }
        else if (EndsWith(segments, "models"))
        {
            segments.RemoveAt(segments.Count - 1);
        }
        else if (EndsWith(segments, "completions"))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        segments.Add("chat");
        segments.Add("completions");
        builder.Path = "/" + string.Join('/', segments);
        return builder.Uri;
    }

    public static Uri BuildModelsUri(Uri endpoint)
    {
        var builder = CreateCleanBuilder(endpoint);
        var segments = GetSegments(builder);

        if (EndsWith(segments, "models"))
        {
            return builder.Uri;
        }

        if (segments.Count == 0)
        {
            segments.Add("v1");
        }
        else if (EndsWith(segments, "chat", "completions"))
        {
            segments.RemoveRange(segments.Count - 2, 2);
        }
        else if (EndsWith(segments, "completions"))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        segments.Add("models");
        builder.Path = "/" + string.Join('/', segments);
        return builder.Uri;
    }

    private static UriBuilder CreateCleanBuilder(Uri endpoint)
    {
        return new UriBuilder(endpoint)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };
    }

    private static List<string> GetSegments(UriBuilder builder)
    {
        return builder.Path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static bool EndsWith(List<string> segments, string segment)
    {
        return segments.Count > 0
            && string.Equals(segments[^1], segment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EndsWith(List<string> segments, string penultimate, string ultimate)
    {
        return segments.Count >= 2
            && string.Equals(segments[^2], penultimate, StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[^1], ultimate, StringComparison.OrdinalIgnoreCase);
    }
}
