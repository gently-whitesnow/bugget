namespace Bugget.Contracts.Views.Settings;

public sealed class TeamSettingsSectionView
{
    /// <summary>
    /// Id раздела настроек (kaiten)
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Название раздела настроек
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Настройки раздела
    /// </summary>
    public required TeamSettingView[] Settings { get; init; }
}
