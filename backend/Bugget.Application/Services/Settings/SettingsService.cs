using Bugget.Application.Ports;
using Bugget.Application.Results.Settings;
using Bugget.Application.Services.Settings;
using Bugget.Domain.Errors;

namespace Bugget.Application.Services;

public sealed class SettingsService(SettingsProcessorProvider settingsProcessorProvider, ISettingsDbClient settingsDbClient) : ISettingsService
{
    public Task<(WorkspaceSettingView? Value, Error? Error)> UpdateWorkspaceSettingAsync(string organizationId, string sectionId, string settingId, string[] values)
    {
        var processor = settingsProcessorProvider.GetWorkspaceSettingsProcessor(sectionId);
        if (processor.Error is not null)
        {
            return Task.FromResult<(WorkspaceSettingView? Value, Error? Error)>((null, processor.Error));
        }

        return processor.Value!.UpdateSettingAsync(organizationId, settingId, values);
    }

    public Task<(TeamSettingView? Value, Error? Error)> UpdateTeamSettingAsync(string teamId, string sectionId, string settingId, string[] values)
    {
        var processor = settingsProcessorProvider.GetTeamSettingsProcessor(sectionId);
        if (processor.Error is not null)
        {
            return Task.FromResult<(TeamSettingView? Value, Error? Error)>((null, processor.Error));
        }

        return processor.Value!.UpdateSettingAsync(teamId, settingId, values);
    }

    public Task<(UserSettingView? Value, Error? Error)> UpdateUserSettingAsync(string userId, string sectionId, string settingId, string[] values)
    {
        var processor = settingsProcessorProvider.GetUserSettingsProcessor(sectionId);
        if (processor.Error is not null)
        {
            return Task.FromResult<(UserSettingView? Value, Error? Error)>((null, processor.Error));
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
