using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.Core.Translation;

public sealed class OpenAiCompatibleModelService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TranslationSettings _settings;

    public OpenAiCompatibleModelService(HttpClient httpClient, TranslationSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _settings.Normalize();
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_settings.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new TranslationException($"Invalid translation endpoint: {_settings.Endpoint}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, OpenAiCompatibleEndpointBuilder.BuildModelsUri(endpoint));
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationException($"Model endpoint returned {(int)response.StatusCode}: {TrimForError(body)}");
        }

        ModelListResponse? modelList;
        try
        {
            modelList = JsonSerializer.Deserialize<ModelListResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new TranslationException("Model endpoint returned invalid JSON.", ex);
        }

        var modelIds = modelList?.Data?
            .Select(model => model.Id?.Trim())
            .OfType<string>()
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return modelIds is { Length: > 0 }
            ? modelIds
            : throw new TranslationException("Model endpoint returned no models.");
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

    private sealed record ModelListResponse(
        [property: JsonPropertyName("data")] ModelItem[]? Data);

    private sealed record ModelItem(
        [property: JsonPropertyName("id")] string? Id);
}
