using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Errors;
using Bugget.Entities.Views.Settings;

namespace Bugget.BO.Services.Settings;

public interface IWorkspaceSettingsProcessor
{
    string SectionId { get; }
    Task<(WorkspaceSettingView? Value, Error? Error)> UpdateSettingAsync(string organizationId, string settingId, string[] values);
    WorkspaceSettingsSectionView ExtractSettings(WorkspaceSettingDbModel[] workspaceSettings);
}
