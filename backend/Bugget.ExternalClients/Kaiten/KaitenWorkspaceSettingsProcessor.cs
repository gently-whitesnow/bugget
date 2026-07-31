using Bugget.BO.Errors;
using Bugget.BO.Services.Settings;
using Bugget.DA.Interfaces;
using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.Errors;
using Bugget.Entities.Views.Settings;

namespace Bugget.ExternalClients.Kaiten;

public sealed class KaitenWorkspaceSettingsProcessor(
    ISettingsDbClient settingsDbClient,
    KaitenBoardsLoaderService boardsLoaderService) : IWorkspaceSettingsProcessor
{
    public string SectionId => KaitenConstants.FeatureKey;

    // Все описания сеттингов Kaiten для workspace в одном месте
    private static readonly IReadOnlyDictionary<string, KaitenWorkspaceSetting> SettingsMap =
        new KaitenWorkspaceSetting[]
        {
            new SingleValueWorkspaceSetting(
                Id: KaitenConstants.DomainFieldKey,
                Title: "Домен",
                Description: "Домен рабочего пространства Kaiten"
            ),
            new SingleValueWorkspaceSetting(
                Id: KaitenConstants.AccessTokenFieldKey,
                Title: "Токен доступа",
                Description: "Токен доступа к Kaiten рабочему пространству"
            )
        }.ToDictionary(s => s.Id, StringComparer.Ordinal);

    public WorkspaceSettingsSectionView ExtractSettings(WorkspaceSettingDbModel[] workspaceSettings)
    {
        var kaitenSettings = workspaceSettings
            .Where(s => s.FeatureKey == KaitenConstants.FeatureKey)
            .ToArray();

        return new WorkspaceSettingsSectionView
        {
            Id = KaitenConstants.FeatureKey,
            Title = "Kaiten",
            Settings = SettingsMap
                .Values
                .Select(def => def.BuildView(kaitenSettings))
                .ToArray()
        };
    }

    async Task<(WorkspaceSettingView? Value, Error? Error)> IWorkspaceSettingsProcessor.UpdateSettingAsync(
        string workspaceId,
        string settingId,
        string[] values)
    {
        if (!SettingsMap.TryGetValue(settingId, out var definition))
        {
            return (null, BoErrors.WorkspaceSettingNotFound);
        }

        var result = await definition.UpdateAsync(workspaceId, values, KaitenConstants.FeatureKey, settingsDbClient);

        // Всегда инвалидируем кэш при изменении workspace настроек Kaiten
        if (result.Error is null)
        {
            boardsLoaderService.InvalidateCache(workspaceId);
        }

        return result;
    }

    // Базовое описание любого Kaiten-сеттинга для workspace
    private abstract record KaitenWorkspaceSetting(
        string Id,
        string Title,
        string Description)
    {
        public abstract WorkspaceSettingView BuildView(WorkspaceSettingDbModel[] kaitenSettings);

        public abstract Task<(WorkspaceSettingView? Value, Error? Error)> UpdateAsync(
            string workspaceId,
            string[] values,
            string sectionId,
            ISettingsDbClient settingsDbClient);
    }

    /// <summary>
    /// Обычный строковый сеттинг с одним значением.
    /// Если потом понадобится валидировать домен/токен — логика будет тут.
    /// </summary>
    private sealed record SingleValueWorkspaceSetting(
        string Id,
        string Title,
        string Description)
        : KaitenWorkspaceSetting(Id, Title, Description)
    {
        public override WorkspaceSettingView BuildView(WorkspaceSettingDbModel[] kaitenSettings)
        {
            var value = kaitenSettings
                .FirstOrDefault(s => s.FieldKey == Id)?
                .FieldValue ?? string.Empty;

            if (Id == KaitenConstants.AccessTokenFieldKey && !string.IsNullOrEmpty(value))
            {
                value = "********";
            }

            return new WorkspaceSettingView
            {
                Id = Id,
                Title = Title,
                Description = Description,
                IsArray = false,
                Values = new[] { value }
            };
        }

        public override async Task<(WorkspaceSettingView? Value, Error? Error)> UpdateAsync(
            string workspaceId,
            string[] values,
            string sectionId,
            ISettingsDbClient settingsDbClient)
        {
            if (values.Length != 1)
            {
                return (null, BoErrors.WorkspaceSettingInvalidValues);
            }

            var setting = await settingsDbClient.UpsertWorkspaceSettingAsync(
                workspaceId,
                sectionId,
                Id,
                values[0]);

            var value = setting.FieldValue;
            if (Id == KaitenConstants.AccessTokenFieldKey && !string.IsNullOrEmpty(value))
            {
                value = "********";
            }

            return (new WorkspaceSettingView
            {
                Id = Id,
                Title = Title,
                Description = Description,
                IsArray = false,
                Values = new[] { value }
            }, null);
        }
    }
}
