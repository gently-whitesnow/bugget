using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Errors;
using Bugget.Entities.Views.Settings;

namespace Bugget.BO.Services.Settings;

public interface IUserSettingsProcessor
{
    string SectionId { get; }
    Task<(UserSettingView? Value, Error? Error)> UpdateSettingAsync(string userId, string settingId, string[] values);
    UserSettingsSectionView ExtractSettings(UserSettingDbModel[] userSettings);
}
