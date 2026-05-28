namespace WindowsTranslator.App.Features.Codex;

internal sealed class CodexOAuthTokenSet
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string IdToken { get; set; } = string.Empty;

    public string? AccountId { get; set; }

    public string? Email { get; set; }

    public string? PlanType { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool CanRefresh => !string.IsNullOrWhiteSpace(RefreshToken);

    public bool ExpiresSoon(TimeSpan margin) => ExpiresAtUtc <= DateTimeOffset.UtcNow.Add(margin);
}
