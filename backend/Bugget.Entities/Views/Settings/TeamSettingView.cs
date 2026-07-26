namespace Bugget.Entities.Views.Settings;

public sealed class TeamSettingView
{
    /// <summary>
    /// Id настройки (kaiten_url)
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Название настройки
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Описание настройки
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Является ли настройка массивом
    /// </summary>
    public bool IsArray { get; init; } = false;

    /// <summary>
    /// Является ли настройка булевым значением
    /// </summary>
    public bool IsBool { get; init; } = false;

    /// <summary>
    /// Значения настройки
    /// </summary>
    public required string[] Values { get; init; }
}
