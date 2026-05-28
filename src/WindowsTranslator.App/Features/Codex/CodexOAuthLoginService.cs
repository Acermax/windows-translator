using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using WindowsTranslator.Core.Settings;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Codex;

internal sealed class CodexOAuthLoginService
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);
    private readonly CodexOAuthTokenStore _tokenStore;

    public CodexOAuthLoginService(CodexOAuthTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public async Task<CodexOAuthTokenSet> SignInAsync(CodexOAuthSettings settings, CancellationToken cancellationToken)
    {
        settings.Normalize();

        using var listener = StartListener(settings.CallbackPort, out var actualPort);
        var redirectUri = $"http://localhost:{actualPort}/auth/callback";
        var codeVerifier = CreatePkceValue(32);
        var codeChallenge = CreateCodeChallenge(codeVerifier);
        var state = CreatePkceValue(24);
        var authorizeUrl = BuildAuthorizeUrl(settings, redirectUri, codeChallenge, state);

        OpenBrowser(authorizeUrl);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LoginTimeout);

        var context = await listener.GetContextAsync().WaitAsync(timeout.Token).ConfigureAwait(false);
        var query = ParseQuery(context.Request.Url?.Query);

        if (!string.Equals(query.GetValueOrDefault("state"), state, StringComparison.Ordinal))
        {
            await WriteHtmlAsync(context.Response, "Codex sign-in failed", "The OAuth state did not match. You can close this tab.").ConfigureAwait(false);
            throw new TranslationException("Codex OAuth sign-in failed because the callback state did not match.");
        }

        if (query.TryGetValue("error", out var error))
        {
            var description = query.GetValueOrDefault("error_description");
            await WriteHtmlAsync(context.Response, "Codex sign-in failed", WebUtility.HtmlEncode(description ?? error)).ConfigureAwait(false);
            throw new TranslationException($"Codex OAuth sign-in failed: {description ?? error}");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            await WriteHtmlAsync(context.Response, "Codex sign-in failed", "No authorization code was returned. You can close this tab.").ConfigureAwait(false);
            throw new TranslationException("Codex OAuth sign-in failed because no authorization code was returned.");
        }

        using var httpClient = new HttpClient();
        var tokens = await CodexOAuthTokenExchange.ExchangeCodeAsync(
            httpClient,
            settings,
            code,
            redirectUri,
            codeVerifier,
            timeout.Token).ConfigureAwait(false);

        _tokenStore.Save(tokens);
        await WriteHtmlAsync(context.Response, "Codex sign-in complete", "You can close this tab and return to Windows Translator.").ConfigureAwait(false);
        return tokens;
    }

    private static HttpListener StartListener(int preferredPort, out int actualPort)
    {
        foreach (var port in new[] { preferredPort, 1457 })
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/auth/");
            try
            {
                listener.Start();
                actualPort = port;
                return listener;
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        throw new TranslationException("Could not start the local Codex OAuth callback server. Check that ports 1455 and 1457 are available.");
    }

    private static string BuildAuthorizeUrl(CodexOAuthSettings settings, string redirectUri, string codeChallenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid profile email offline_access",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["id_token_add_organizations"] = "true",
            ["codex_cli_simplified_flow"] = "true",
            ["originator"] = "codex_cli_rs"
        };

        return $"{settings.Issuer}/oauth/authorize?{BuildQueryString(parameters)}";
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private static string CreatePkceValue(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string BuildQueryString(Dictionary<string, string> parameters)
    {
        return string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return values;
        }

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pieces[0].Replace('+', ' '));
            var value = pieces.Length > 1
                ? Uri.UnescapeDataString(pieces[1].Replace('+', ' '))
                : string.Empty;
            values[key] = value;
        }

        return values;
    }

    private static async Task WriteHtmlAsync(HttpListenerResponse response, string title, string message)
    {
        var html = $"""
<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>{WebUtility.HtmlEncode(title)}</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif;margin:32px;">
<h1>{WebUtility.HtmlEncode(title)}</h1>
<p>{message}</p>
</body>
</html>
""";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }
}
