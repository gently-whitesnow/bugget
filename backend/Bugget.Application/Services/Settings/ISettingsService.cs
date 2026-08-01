using Bugget.Application.Results.Settings;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services;

public interface ISettingsService
{
    Task<(WorkspaceSettingView? Value, Error? Error)> UpdateWorkspaceSettingAsync(string organizationId, string sectionId, string settingId, string[] values);
    Task<(TeamSettingView? Value, Error? Error)> UpdateTeamSettingAsync(string teamId, string sectionId, string settingId, string[] values);
    Task<(UserSettingView? Value, Error? Error)> UpdateUserSettingAsync(string userId, string sectionId, string settingId, string[] values);
    Task<SettingsSectionsView> GetSettingsSectionsAsync(string organizationId, string teamId, string userId);
}
