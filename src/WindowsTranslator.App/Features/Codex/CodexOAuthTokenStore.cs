using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Codex;

internal sealed class CodexOAuthTokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WindowsTranslator.CodexOAuth.v1");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public CodexOAuthTokenStore(string? tokenPath = null)
    {
        TokenPath = tokenPath ?? GetDefaultTokenPath();
    }

    public string TokenPath { get; }

    public CodexOAuthTokenSet? Load()
    {
        if (!File.Exists(TokenPath))
        {
            return null;
        }

        try
        {
            var fileJson = File.ReadAllText(TokenPath, Encoding.UTF8);
            var envelope = JsonSerializer.Deserialize<TokenEnvelope>(fileJson, JsonOptions);
            if (envelope?.Protected == true && !string.IsNullOrWhiteSpace(envelope.Data))
            {
                var protectedBytes = Convert.FromBase64String(envelope.Data);
                var jsonBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<CodexOAuthTokenSet>(jsonBytes, JsonOptions);
            }

            return JsonSerializer.Deserialize<CodexOAuthTokenSet>(fileJson, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or CryptographicException)
        {
            throw new TranslationException("Could not read Codex OAuth credentials. Sign in again from Settings.", ex);
        }
    }

    public void Save(CodexOAuthTokenSet tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        try
        {
            var directory = Path.GetDirectoryName(TokenPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(tokens, JsonOptions);
            var protectedBytes = ProtectedData.Protect(jsonBytes, Entropy, DataProtectionScope.CurrentUser);
            var envelope = new TokenEnvelope(true, Convert.ToBase64String(protectedBytes));
            File.WriteAllText(TokenPath, JsonSerializer.Serialize(envelope, JsonOptions), Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
        {
            throw new TranslationException("Could not save Codex OAuth credentials.", ex);
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(TokenPath))
            {
                File.Delete(TokenPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TranslationException("Could not remove Codex OAuth credentials.", ex);
        }
    }

    private static string GetDefaultTokenPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.CurrentDirectory;
        }

        return Path.Combine(appData, "WindowsTranslator", "codex-oauth.json");
    }

    private sealed record TokenEnvelope(
        [property: JsonPropertyName("protected")] bool Protected,
        [property: JsonPropertyName("data")] string Data);
}
