using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Errors;
using Bugget.Entities.Views.Settings;

namespace Bugget.BO.Services.Settings;

public interface ITeamSettingsProcessor
{
    string SectionId { get; }
    Task<(TeamSettingView? Value, Error? Error)> UpdateSettingAsync(string teamId, string settingId, string[] values);
    TeamSettingsSectionView ExtractSettings(TeamSettingDbModel[] teamSettings);
}
