using Bugget.Contracts.Settings.Generated;
using Bugget.Contracts.Views.Settings;

namespace Bugget.Api.Mappers;

/// <summary>
/// View → Contracts для <c>/v1/settings-sections</c> и обновления настроек.
///
/// Три уровня настроек (рабочее пространство, команда, пользователь) описаны в
/// коде тремя одинаковыми по форме типами, а в контракте — одной схемой
/// <see cref="Setting"/>: наружу они и уходили одинаковыми, что зафиксировано
/// снимками контракта.
/// </summary>
internal static class SettingsMapper
{
    public static SettingsSections ToContract(this SettingsSectionsView view) => new()
    {
        Workspace_sections = [.. view.WorkspaceSections.Select(ToContract)],
        Team_sections = [.. view.TeamSections.Select(ToContract)],
        User_sections = [.. view.UserSections.Select(ToContract)],
    };

    public static Setting ToContract(this WorkspaceSettingView view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Description = view.Description,
        Is_array = view.IsArray,
        Is_bool = view.IsBool,
        Values = view.Values,
    };

    public static Setting ToContract(this TeamSettingView view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Description = view.Description,
        Is_array = view.IsArray,
        Is_bool = view.IsBool,
        Values = view.Values,
    };

    public static Setting ToContract(this UserSettingView view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Description = view.Description,
        Is_array = view.IsArray,
        Is_bool = view.IsBool,
        Values = view.Values,
    };

    private static SettingsSection ToContract(this WorkspaceSettingsSectionView view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Settings = [.. view.Settings.Select(ToContract)],
    };

    private static SettingsSection ToContract(this TeamSettingsSectionView view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Settings = [.. view.Settings.Select(ToContract)],
    };

    private static SettingsSection ToContract(this UserSettingsSectionView view) => new()
    {
        Id = view.Id,
        Title = view.Title,
        Settings = [.. view.Settings.Select(ToContract)],
    };
}
