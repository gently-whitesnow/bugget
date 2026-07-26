namespace MattermostOAuth;

public sealed class MattermostOAuthOptions
{
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string SuccessRedirectPath { get; init; } = "/settings";
}
