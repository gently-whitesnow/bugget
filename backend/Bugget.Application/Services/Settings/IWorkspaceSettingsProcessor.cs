using Bugget.Contracts.Views.Settings;
using Bugget.Domain.Errors;
using Bugget.Domain.Settings;

namespace Bugget.Application.Services.Settings;

public interface IWorkspaceSettingsProcessor
{
    string SectionId { get; }
    Task<(WorkspaceSettingView? Value, Error? Error)> UpdateSettingAsync(string organizationId, string settingId, string[] values);
    WorkspaceSettingsSectionView ExtractSettings(WorkspaceSetting[] workspaceSettings);
}
