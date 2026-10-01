namespace Bugget.Application.Results.Settings;

public sealed class UserSettingView
{
    /// <summary>Id настройки, например <c>kaiten_url</c>.</summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public bool IsArray { get; init; }

    public bool IsBool { get; init; }

    public required string[] Values { get; init; }
}
