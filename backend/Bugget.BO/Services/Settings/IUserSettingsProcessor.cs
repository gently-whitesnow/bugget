using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Views.Settings;
using Monade;

namespace Bugget.BO.Services.Settings;

public interface IUserSettingsProcessor
{
    string SectionId { get; }
    Task<MonadeStruct<UserSettingView>> UpdateSettingAsync(string userId, string settingId, string[] values);
    UserSettingsSectionView ExtractSettings(UserSettingDbModel[] userSettings);
}
