using System.Net.Http;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Codex;

internal sealed class CodexOAuthCredentialProvider
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);
    private readonly CodexOAuthTokenStore _tokenStore;
    private readonly HttpClient _httpClient;

    public CodexOAuthCredentialProvider(CodexOAuthTokenStore tokenStore, HttpClient httpClient)
    {
        _tokenStore = tokenStore;
        _httpClient = httpClient;
    }

    public async Task<CodexOAuthTokenSet> GetTokensAsync(CodexOAuthSettings settings, CancellationToken cancellationToken)
    {
        var tokens = _tokenStore.Load();
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            throw new TranslationException("Codex OAuth is not connected. Open Settings and sign in with ChatGPT.");
        }

        if (!tokens.ExpiresSoon(RefreshMargin))
        {
            return tokens;
        }

        var refreshed = await CodexOAuthTokenExchange.RefreshAsync(_httpClient, settings, tokens, cancellationToken).ConfigureAwait(false);
        _tokenStore.Save(refreshed);
        return refreshed;
    }
}
