using Bugget.Contracts.Views.Settings;
using Bugget.Domain.Errors;
using Bugget.Domain.Settings;

namespace Bugget.Application.Services.Settings;

public interface IUserSettingsProcessor
{
    string SectionId { get; }
    Task<(UserSettingView? Value, Error? Error)> UpdateSettingAsync(string userId, string settingId, string[] values);
    UserSettingsSectionView ExtractSettings(UserSetting[] userSettings);
}
