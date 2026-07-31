using Bugget.Contracts.Views.Settings;
using Bugget.Domain.Errors;
using Bugget.Domain.Settings;

namespace Bugget.Application.Services.Settings;

public interface ITeamSettingsProcessor
{
    string SectionId { get; }
    Task<(TeamSettingView? Value, Error? Error)> UpdateSettingAsync(string teamId, string settingId, string[] values);
    TeamSettingsSectionView ExtractSettings(TeamSetting[] teamSettings);
}
