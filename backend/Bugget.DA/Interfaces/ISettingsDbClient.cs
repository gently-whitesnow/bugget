using Bugget.Entities.DbModels.Settings;

namespace Bugget.DA.Interfaces;

public interface ISettingsDbClient
{
    Task<(WorkspaceSettingDbModel[], TeamSettingDbModel[], UserSettingDbModel[])> GetSettingsAsync(string workspaceId, string teamId, string userId);

    Task<WorkspaceSettingDbModel[]> GetWorkspaceSettingsAsync(string workspaceId);
    Task<TeamSettingDbModel[]> GetTeamSettingsAsync(string teamId);

    Task<WorkspaceSettingDbModel> UpsertWorkspaceSettingAsync(string workspaceId, string sectionId, string settingId, string value);

    Task<TeamSettingDbModel> UpsertTeamSettingAsync(string teamId, string sectionId, string settingId, string value);

    Task<UserSettingDbModel> UpsertUserSettingAsync(string userId, string sectionId, string settingId, string value);

    Task<WorkspaceSettingDbModel[]> UpsertWorkspaceSettingsAsync(string workspaceId, string sectionId, string settingId, string[] value);

    Task<TeamSettingDbModel[]> UpsertTeamSettingsAsync(string teamId, string sectionId, string settingId, string[] value);

    Task<UserSettingDbModel[]> UpsertUserSettingsAsync(string userId, string sectionId, string settingId, string[] value);
}

