using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsTranslator.App.Features.Codex;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Translation;

internal sealed class CodexOAuthTranslationService : ITranslationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly TranslationSettings _settings;
    private readonly CodexOAuthCredentialProvider _credentialProvider;

    public CodexOAuthTranslationService(
        HttpClient httpClient,
        TranslationSettings settings,
        CodexOAuthCredentialProvider credentialProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _settings.Normalize();
        _credentialProvider = credentialProvider;
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Text);

        if (request.Text.Length > _settings.MaxInputCharacters)
        {
            throw new TranslationException($"Selected text is too long. Limit: {_settings.MaxInputCharacters} characters.");
        }

        if (!Uri.TryCreate(_settings.CodexOAuth.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new TranslationException($"Invalid Codex endpoint: {_settings.CodexOAuth.Endpoint}");
        }

        var targetLanguage = string.IsNullOrWhiteSpace(request.TargetLanguage)
            ? _settings.TargetLanguage
            : request.TargetLanguage.Trim();
        var userPrompt = TranslationPromptBuilder.BuildUserPrompt(request.Text, targetLanguage, request.Action);
        var payload = new ResponsesRequest(
            _settings.CodexOAuth.Model,
            TranslationPromptBuilder.SystemPrompt,
            [
                new ResponsesInputMessage(
                    "user",
                    [
                        new ResponsesInputContent("input_text", userPrompt)
                    ])
            ],
            new ResponsesReasoning(_settings.CodexOAuth.ReasoningEffort),
            false,
            true);

        var tokens = await _credentialProvider.GetTokensAsync(_settings.CodexOAuth, cancellationToken).ConfigureAwait(false);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        httpRequest.Headers.TryAddWithoutValidation("originator", "codex_cli_rs");
        if (!string.IsNullOrWhiteSpace(tokens.AccountId))
        {
            httpRequest.Headers.TryAddWithoutValidation("chatgpt-account-id", tokens.AccountId);
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationException($"Codex endpoint returned {(int)response.StatusCode}: {TrimForError(body)}");
        }

        var text = CodexResponseTextExtractor.ExtractText(body);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new TranslationException("Codex endpoint returned no translated text.");
        }

        return new TranslationResult(text.Trim(), _settings.CodexOAuth.Model, stopwatch.Elapsed);
    }

    private static string TrimForError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "empty response body";
        }

        const int limit = 1_000;
        return body.Length <= limit ? body : body[..limit] + "...";
    }

    private sealed record ResponsesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("instructions")] string Instructions,
        [property: JsonPropertyName("input")] ResponsesInputMessage[] Input,
        [property: JsonPropertyName("reasoning")] ResponsesReasoning Reasoning,
        [property: JsonPropertyName("store")] bool Store,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record ResponsesReasoning(
        [property: JsonPropertyName("effort")] string Effort);

    private sealed record ResponsesInputMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] ResponsesInputContent[] Content);

    private sealed record ResponsesInputContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);
}
