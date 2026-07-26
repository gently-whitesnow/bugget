using Bugget.BO.Services.Settings;
using Bugget.DA.Interfaces;
using Bugget.Entities.Views.Settings;
using Monade;

namespace Bugget.BO.Services;

public sealed class SettingsService(SettingsProcessorProvider settingsProcessorProvider, ISettingsDbClient settingsDbClient)
{
    public Task<MonadeStruct<WorkspaceSettingView>> UpdateWorkspaceSettingAsync(string organizationId, string sectionId, string settingId, string[] values)
    {
        var processor = settingsProcessorProvider.GetWorkspaceSettingsProcessor(sectionId);
        if (processor.HasError)
        {
            return Task.FromResult(new MonadeStruct<WorkspaceSettingView>(processor.Error!));
        }

        return processor.Value!.UpdateSettingAsync(organizationId, settingId, values);
    }

    public Task<MonadeStruct<TeamSettingView>> UpdateTeamSettingAsync(string teamId, string sectionId, string settingId, string[] values)
    {
        var processor = settingsProcessorProvider.GetTeamSettingsProcessor(sectionId);
        if (processor.HasError)
        {
            return Task.FromResult(new MonadeStruct<TeamSettingView>(processor.Error!));
        }

        return processor.Value!.UpdateSettingAsync(teamId, settingId, values);
    }

    public Task<MonadeStruct<UserSettingView>> UpdateUserSettingAsync(string userId, string sectionId, string settingId, string[] values)
    {
        var processor = settingsProcessorProvider.GetUserSettingsProcessor(sectionId);
        if (processor.HasError)
        {
            return Task.FromResult(new MonadeStruct<UserSettingView>(processor.Error!));
        }

        return processor.Value!.UpdateSettingAsync(userId, settingId, values);
    }

    public async Task<SettingsSectionsView> GetSettingsSectionsAsync(string organizationId, string teamId, string userId)
    {
        var (workspaceSettings, teamSettings, userSettings) = await settingsDbClient.GetSettingsAsync(organizationId, teamId, userId);
        var (workspaceProcessors, teamProcessors, userProcessors) = settingsProcessorProvider.GetSettingsProcessors();

        return new SettingsSectionsView
        {
            WorkspaceSections = workspaceProcessors.Select(p => p.ExtractSettings(workspaceSettings)).ToArray(),
            TeamSections = teamProcessors.Select(p => p.ExtractSettings(teamSettings)).ToArray(),
            UserSections = userProcessors.Select(p => p.ExtractSettings(userSettings)).ToArray()
        };
    }
}

