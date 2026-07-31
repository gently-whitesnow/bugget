namespace Bugget.Application.Users.Options;

public sealed class MattermostBotOptions
{
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "";
    public string BotAccessToken { get; init; } = "";
}
