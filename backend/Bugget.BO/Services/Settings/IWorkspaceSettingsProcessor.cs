using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Views.Settings;
using Monade;

namespace Bugget.BO.Services.Settings;

public interface IWorkspaceSettingsProcessor
{
    string SectionId { get; }
    Task<MonadeStruct<WorkspaceSettingView>> UpdateSettingAsync(string organizationId, string settingId, string[] values);
    WorkspaceSettingsSectionView ExtractSettings(WorkspaceSettingDbModel[] workspaceSettings);
}
