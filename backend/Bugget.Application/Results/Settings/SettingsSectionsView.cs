namespace Bugget.Application.Results.Settings;

public sealed class SettingsSectionsView
{
    /// <summary>
    /// Секции настроек рабочего пространства
    /// </summary>
    public required WorkspaceSettingsSectionView[] WorkspaceSections { get; init; }

    /// <summary>
    /// Секции настроек команды
    /// </summary>
    public required TeamSettingsSectionView[] TeamSections { get; init; }

    /// <summary>
    /// Секции настроек пользователя
    /// </summary>
    public required UserSettingsSectionView[] UserSections { get; init; }
}
