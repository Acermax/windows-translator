using System.Text;
using System.Text.Json;

namespace WindowsTranslator.App.Features.Codex;

internal sealed class CodexJwtClaims
{
    public string? AccountId { get; private init; }

    public string? Email { get; private init; }

    public string? PlanType { get; private init; }

    public DateTimeOffset? ExpiresAtUtc { get; private init; }

    public static CodexJwtClaims Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CodexJwtClaims();
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return new CodexJwtClaims();
        }

        try
        {
            var payload = DecodeBase64Url(parts[1]);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            string? accountId = null;
            string? planType = null;
            if (root.TryGetProperty("https://api.openai.com/auth", out var auth) && auth.ValueKind == JsonValueKind.Object)
            {
                accountId = ReadString(auth, "chatgpt_account_id");
                planType = ReadString(auth, "chatgpt_plan_type") ?? ReadString(auth, "chatgpt_plan");
            }

            DateTimeOffset? expiresAt = null;
            if (root.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var expSeconds))
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            }

            return new CodexJwtClaims
            {
                AccountId = accountId,
                Email = ReadString(root, "email"),
                PlanType = planType,
                ExpiresAtUtc = expiresAt
            };
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return new CodexJwtClaims();
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Convert.FromBase64String(normalized);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
