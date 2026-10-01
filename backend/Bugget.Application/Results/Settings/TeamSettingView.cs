namespace Bugget.Application.Results.Settings;

public sealed class TeamSettingView
{
    /// <summary>Id настройки (kaiten_url)</summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>Является ли настройка массивом</summary>
    public bool IsArray { get; init; }

    /// <summary>Является ли настройка булевым значением</summary>
    public bool IsBool { get; init; }

    public required string[] Values { get; init; }
}
