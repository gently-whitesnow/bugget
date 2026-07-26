using System.Linq;
using Bugget.DA.Interfaces;
using Bugget.Entities.DbModels.Settings;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class SettingsDbClient : PostgresClient, ISettingsDbClient
{
    public async Task<(WorkspaceSettingDbModel[], TeamSettingDbModel[], UserSettingDbModel[])> GetSettingsAsync(string workspaceId, string teamId, string userId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        await using var multi = await connection.QueryMultipleAsync(@"
            SELECT * FROM public.list_workspace_settings(@workspace_id);
            SELECT * FROM public.list_team_settings(@team_id);
            SELECT * FROM public.list_user_settings(@user_id);
        ", new { team_id = teamId, workspace_id = workspaceId, user_id = userId });

        var workspaceSettings = await multi.ReadAsync<WorkspaceSettingDbModel>();
        var teamSettings = await multi.ReadAsync<TeamSettingDbModel>();
        var userSettings = await multi.ReadAsync<UserSettingDbModel>();

        return (workspaceSettings.ToArray(), teamSettings.ToArray(), userSettings.ToArray());
    }

    public async Task<WorkspaceSettingDbModel[]> GetWorkspaceSettingsAsync(string workspaceId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var workspaceSettings = await connection.QueryAsync<WorkspaceSettingDbModel>(
            "SELECT * FROM public.list_workspace_settings(@workspace_id);",
            new { workspace_id = workspaceId });

        return workspaceSettings.ToArray();
    }

    public async Task<TeamSettingDbModel[]> GetTeamSettingsAsync(string teamId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        var teamSettings = await connection.QueryAsync<TeamSettingDbModel>(
            "SELECT * FROM public.list_team_settings(@team_id);",
            new { team_id = teamId });

        return teamSettings.ToArray();
    }

    public async Task<WorkspaceSettingDbModel> UpsertWorkspaceSettingAsync(string workspaceId, string sectionId, string settingId, string value)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<WorkspaceSettingDbModel>(
            "SELECT * FROM public.upsert_workspace_setting(@workspace_id, @feature_key, @field_key, @field_value);",
            new
            {
                workspace_id = workspaceId,
                feature_key = sectionId,
                field_key = settingId,
                field_value = value
            });
    }

    public async Task<TeamSettingDbModel> UpsertTeamSettingAsync(string teamId, string sectionId, string settingId, string value)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<TeamSettingDbModel>(
            "SELECT * FROM public.upsert_team_setting(@team_id, @feature_key, @field_key, @field_value);",
            new
            {
                team_id = teamId,
                feature_key = sectionId,
                field_key = settingId,
                field_value = value
            });
    }

    public async Task<UserSettingDbModel> UpsertUserSettingAsync(string userId, string sectionId, string settingId, string value)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        return await connection.QuerySingleAsync<UserSettingDbModel>(
            "SELECT * FROM public.upsert_user_setting(@user_id, @feature_key, @field_key, @field_value);",
            new
            {
                user_id = userId,
                feature_key = sectionId,
                field_key = settingId,
                field_value = value
            });
    }

    public async Task<WorkspaceSettingDbModel[]> UpsertWorkspaceSettingsAsync(string workspaceId, string sectionId, string settingId, string[] value)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        var fieldValues = value ?? Array.Empty<string>();

        var updated = await connection.QueryAsync<WorkspaceSettingDbModel>(
            "SELECT * FROM public.replace_workspace_settings_section(@workspace_id, @feature_key, @field_key, @field_values);",
            new
            {
                workspace_id = workspaceId,
                feature_key = sectionId,
                field_key = settingId,
                field_values = fieldValues
            });

        return updated.ToArray();
    }

    public async Task<TeamSettingDbModel[]> UpsertTeamSettingsAsync(string teamId, string sectionId, string settingId, string[] value)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        var fieldValues = value ?? Array.Empty<string>();

        var updated = await connection.QueryAsync<TeamSettingDbModel>(
            "SELECT * FROM public.replace_team_settings_section(@team_id, @feature_key, @field_key, @field_values);",
            new
            {
                team_id = teamId,
                feature_key = sectionId,
                field_key = settingId,
                field_values = fieldValues
            });

        return updated.ToArray();
    }

    public async Task<UserSettingDbModel[]> UpsertUserSettingsAsync(string userId, string sectionId, string settingId, string[] value)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        var fieldValues = value ?? Array.Empty<string>();

        var updated = await connection.QueryAsync<UserSettingDbModel>(
            "SELECT * FROM public.replace_user_settings_section(@user_id, @feature_key, @field_key, @field_values);",
            new
            {
                user_id = userId,
                feature_key = sectionId,
                field_key = settingId,
                field_values = fieldValues
            });

        return updated.ToArray();
    }
}

