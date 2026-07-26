using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Views.Settings;
using Monade;

namespace Bugget.BO.Services.Settings;

public interface ITeamSettingsProcessor
{
    string SectionId { get; }
    Task<MonadeStruct<TeamSettingView>> UpdateSettingAsync(string teamId, string settingId, string[] values);
    TeamSettingsSectionView ExtractSettings(TeamSettingDbModel[] teamSettings);
}
