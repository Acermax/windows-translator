using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.Core.Translation;

public sealed class OpenAiCompatibleTranslationService : ITranslationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly TranslationSettings _settings;

    public OpenAiCompatibleTranslationService(HttpClient httpClient, TranslationSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _settings.Normalize();
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

        if (!Uri.TryCreate(_settings.Endpoint, UriKind.Absolute, out var configuredEndpoint))
        {
            throw new TranslationException($"Invalid translation endpoint: {_settings.Endpoint}");
        }

        var endpoint = OpenAiCompatibleEndpointBuilder.BuildChatCompletionsUri(configuredEndpoint);

        var targetLanguage = string.IsNullOrWhiteSpace(request.TargetLanguage)
            ? _settings.TargetLanguage
            : request.TargetLanguage.Trim();

        var payload = new ChatCompletionRequest(
            _settings.Model,
            [
                new ChatMessage("system", TranslationPromptBuilder.SystemPrompt),
                new ChatMessage("user", TranslationPromptBuilder.BuildUserPrompt(request.Text, targetLanguage))
            ],
            _settings.Temperature,
            _settings.TopP,
            _settings.TopK,
            _settings.MinP,
            _settings.PresencePenalty,
            _settings.RepetitionPenalty,
            _settings.IncludeReasoning,
            new ChatTemplateKwargs(_settings.EnableThinking),
            _settings.MaxOutputTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationException($"Translation endpoint returned {(int)response.StatusCode}: {TrimForError(body)}");
        }

        ChatCompletionResponse? completion;
        try
        {
            completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new TranslationException("Translation endpoint returned invalid JSON.", ex);
        }

        var firstChoice = completion?.Choices?.FirstOrDefault();
        var translatedText = firstChoice?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            var reasoning = firstChoice?.Message?.Reasoning;
            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                var suffix = string.Equals(firstChoice?.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
                    ? " The response stopped because max output tokens were exhausted; increase maxOutputTokens or disable thinking."
                    : " The model returned reasoning but no final answer in message.content.";
                throw new TranslationException("Translation endpoint returned reasoning but no translated text." + suffix);
            }

            throw new TranslationException("Translation endpoint returned no translated text in message.content.");
        }

        var model = string.IsNullOrWhiteSpace(completion?.Model) ? _settings.Model : completion.Model;
        return new TranslationResult(translatedText, model, stopwatch.Elapsed);
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

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("top_p")] double TopP,
        [property: JsonPropertyName("top_k")] int TopK,
        [property: JsonPropertyName("min_p")] double MinP,
        [property: JsonPropertyName("presence_penalty")] double PresencePenalty,
        [property: JsonPropertyName("repetition_penalty")] double RepetitionPenalty,
        [property: JsonPropertyName("include_reasoning")] bool IncludeReasoning,
        [property: JsonPropertyName("chat_template_kwargs")] ChatTemplateKwargs ChatTemplateKwargs,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatTemplateKwargs(
        [property: JsonPropertyName("enable_thinking")] bool EnableThinking);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("reasoning")] string? Reasoning = null);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] ChatChoice[]? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);
}
