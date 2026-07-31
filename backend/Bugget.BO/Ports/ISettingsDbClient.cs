using Bugget.Entities.BO.Settings;

namespace Bugget.BO.Ports;

public interface ISettingsDbClient
{
    Task<(WorkspaceSetting[], TeamSetting[], UserSetting[])> GetSettingsAsync(string workspaceId, string teamId, string userId);

    Task<WorkspaceSetting[]> GetWorkspaceSettingsAsync(string workspaceId);
    Task<TeamSetting[]> GetTeamSettingsAsync(string teamId);

    Task<WorkspaceSetting> UpsertWorkspaceSettingAsync(string workspaceId, string sectionId, string settingId, string value);

    Task<TeamSetting> UpsertTeamSettingAsync(string teamId, string sectionId, string settingId, string value);

    Task<UserSetting> UpsertUserSettingAsync(string userId, string sectionId, string settingId, string value);

    Task<WorkspaceSetting[]> UpsertWorkspaceSettingsAsync(string workspaceId, string sectionId, string settingId, string[] value);

    Task<TeamSetting[]> UpsertTeamSettingsAsync(string teamId, string sectionId, string settingId, string[] value);

    Task<UserSetting[]> UpsertUserSettingsAsync(string userId, string sectionId, string settingId, string[] value);
}

