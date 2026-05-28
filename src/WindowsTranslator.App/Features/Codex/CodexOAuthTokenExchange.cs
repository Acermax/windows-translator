using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Codex;

internal static class CodexOAuthTokenExchange
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<CodexOAuthTokenSet> ExchangeCodeAsync(
        HttpClient httpClient,
        CodexOAuthSettings settings,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = settings.ClientId,
            ["code_verifier"] = codeVerifier
        };

        return await SendTokenRequestAsync(httpClient, settings, fields, null, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CodexOAuthTokenSet> RefreshAsync(
        HttpClient httpClient,
        CodexOAuthSettings settings,
        CodexOAuthTokenSet currentTokens,
        CancellationToken cancellationToken)
    {
        if (!currentTokens.CanRefresh)
        {
            throw new TranslationException("Codex OAuth credentials cannot be refreshed. Sign in again from Settings.");
        }

        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = currentTokens.RefreshToken,
            ["client_id"] = settings.ClientId
        };

        return await SendTokenRequestAsync(httpClient, settings, fields, currentTokens, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CodexOAuthTokenSet> SendTokenRequestAsync(
        HttpClient httpClient,
        CodexOAuthSettings settings,
        Dictionary<string, string> fields,
        CodexOAuthTokenSet? fallbackTokens,
        CancellationToken cancellationToken)
    {
        settings.Normalize();
        var tokenEndpoint = $"{settings.Issuer}/oauth/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(fields)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationException($"Codex OAuth token endpoint returned {(int)response.StatusCode}: {TrimForError(body)}");
        }

        TokenResponse? tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new TranslationException("Codex OAuth token endpoint returned invalid JSON.", ex);
        }

        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new TranslationException("Codex OAuth token endpoint returned no access token.");
        }

        var accessToken = tokenResponse.AccessToken.Trim();
        var refreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? fallbackTokens?.RefreshToken ?? string.Empty
            : tokenResponse.RefreshToken.Trim();
        var idToken = string.IsNullOrWhiteSpace(tokenResponse.IdToken)
            ? fallbackTokens?.IdToken ?? string.Empty
            : tokenResponse.IdToken.Trim();

        var claims = CodexJwtClaims.Parse(idToken);
        var accessClaims = CodexJwtClaims.Parse(accessToken);
        var expiresAtUtc = accessClaims.ExpiresAtUtc
            ?? claims.ExpiresAtUtc
            ?? DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600);

        return new CodexOAuthTokenSet
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IdToken = idToken,
            AccountId = claims.AccountId ?? accessClaims.AccountId ?? fallbackTokens?.AccountId,
            Email = claims.Email ?? accessClaims.Email ?? fallbackTokens?.Email,
            PlanType = claims.PlanType ?? accessClaims.PlanType ?? fallbackTokens?.PlanType,
            ExpiresAtUtc = expiresAtUtc
        };
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

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }
}
