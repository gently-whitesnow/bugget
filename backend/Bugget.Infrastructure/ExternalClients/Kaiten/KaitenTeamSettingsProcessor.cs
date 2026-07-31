using Bugget.Application.Errors;
using Bugget.Application.Ports;
using Bugget.Application.Services.Settings;
using Bugget.Contracts.Views.Settings;
using Bugget.Domain.Errors;
using Bugget.Domain.Settings;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

public sealed class KaitenTeamSettingsProcessor(ISettingsDbClient settingsDbClient) : ITeamSettingsProcessor
{
    public string SectionId => KaitenConstants.FeatureKey;

    private const string BoardIdsSettingTitle = "Рабочие доски";
    private const string BoardIdsSettingDescription = "Не нашли нужную доску? Добавьте бота в пространство, где она находится";

    private const string UseReportLinkingSettingTitle = "Связать репорт с задачей";
    private const string UseReportLinkingSettingDescription = "При создании репорта ссылка на него будет прикреплена к задаче";

    private const string SendReportLinkToCommentsSettingTitle = "Отправлять ссылку на репорт в комментарии";
    private const string SendReportLinkToCommentsSettingDescription = "При создании репорта ссылка на него будет отправлена в комментарии к задаче";

    // Один реестр всех настроек секции Kaiten
    private static readonly IReadOnlyDictionary<string, KaitenSetting> SettingsMap =
        new KaitenSetting[]
        {
            new BoardIdsKaitenSetting(
                Id: KaitenConstants.BoardIdsFieldKey,
                Title: BoardIdsSettingTitle,
                Description: BoardIdsSettingDescription),
            new BoolKaitenSetting(
                Id: KaitenConstants.UseReportLinkingFieldKey,
                Title: UseReportLinkingSettingTitle,
                Description: UseReportLinkingSettingDescription,
                ErrorCode: "use_report_linking_invalid_values_error",
                ErrorMessage: "Значение должно быть одно и быть булевым",
                DefaultValue: true
            ),
            new BoolKaitenSetting(
                Id: KaitenConstants.SendReportLinkToCommentsFieldKey,
                Title: SendReportLinkToCommentsSettingTitle,
                Description: SendReportLinkToCommentsSettingDescription,
                ErrorCode: "send_report_link_to_comments_invalid_values_error",
                ErrorMessage: "Значение должно быть одно и быть булевым",
                DefaultValue: true
            )
        }.ToDictionary(x => x.Id, StringComparer.Ordinal);

    public TeamSettingsSectionView ExtractSettings(TeamSetting[] teamSettings)
    {
        var kaitenSettings = teamSettings
            .Where(s => s.FeatureKey == KaitenConstants.FeatureKey)
            .ToArray();

        return new TeamSettingsSectionView
        {
            Id = KaitenConstants.FeatureKey,
            Title = "Kaiten",
            Settings = SettingsMap
                .Values
                .Select(definition => definition.BuildView(kaitenSettings))
                .ToArray()
        };
    }

    async Task<(TeamSettingView? Value, Error? Error)> ITeamSettingsProcessor.UpdateSettingAsync(
        string teamId,
        string settingId,
        string[] values)
    {
        if (!SettingsMap.TryGetValue(settingId, out var definition))
        {
            return (null, BoErrors.TeamSettingNotFound);
        }

        return await definition.UpdateAsync(teamId, values, KaitenConstants.FeatureKey, settingsDbClient);
    }

    // Базовое описание любого сеттинга Kaiten
    private abstract record KaitenSetting(
        string Id,
        string Title,
        string Description)
    {
        public abstract TeamSettingView BuildView(TeamSetting[] kaitenSettings);

        public abstract Task<(TeamSettingView? Value, Error? Error)> UpdateAsync(
            string teamId,
            string[] values,
            string sectionId,
            ISettingsDbClient settingsDbClient);
    }

    // board_ids: массив значений с лимитом по количеству
    private sealed record BoardIdsKaitenSetting(
        string Id,
        string Title,
        string Description)
        : KaitenSetting(Id, Title, Description)
    {
        public override TeamSettingView BuildView(TeamSetting[] kaitenSettings)
        {
            // Если в БД несколько значений с одним ключом — вернем их все,
            // если нет ни одного — отдадим один пустой элемент.
            var values = kaitenSettings
                .Where(s => s.FieldKey == Id)
                .Select(s => s.FieldValue)
                .DefaultIfEmpty(string.Empty)
                .ToArray();

            return new TeamSettingView
            {
                Id = Id,
                Title = Title,
                Description = Description,
                IsArray = true,
                Values = values
            };
        }

        public override async Task<(TeamSettingView? Value, Error? Error)> UpdateAsync(
            string teamId,
            string[] values,
            string sectionId,
            ISettingsDbClient settingsDbClient)
        {
            if (values.Length > 10)
            {
                return (null, new BadRequestError("board_ids_max_count_error", "Максимальное количество досок - 10"));
            }

            var settings = await settingsDbClient.UpsertTeamSettingsAsync(teamId, sectionId, Id, values);

            return (new TeamSettingView
            {
                Id = Id,
                Title = Title,
                Description = Description,
                IsArray = true,
                Values = settings.Select(s => s.FieldValue).ToArray()
            }, null);
        }
    }

    // Общий тип для булевых настроек
    private sealed record BoolKaitenSetting(
        string Id,
        string Title,
        string Description,
        string ErrorCode,
        string ErrorMessage,
        bool DefaultValue)
        : KaitenSetting(Id, Title, Description)
    {
        public override TeamSettingView BuildView(TeamSetting[] kaitenSettings)
        {
            var raw = kaitenSettings.FirstOrDefault(s => s.FieldKey == Id)?.FieldValue;
            var value = raw ?? DefaultValue.ToString();

            return new TeamSettingView
            {
                Id = Id,
                Title = Title,
                Description = Description,
                IsBool = true,
                Values = new[] { value }
            };
        }

        public override async Task<(TeamSettingView? Value, Error? Error)> UpdateAsync(
            string teamId,
            string[] values,
            string sectionId,
            ISettingsDbClient settingsDbClient)
        {
            if (values.Length != 1 || !bool.TryParse(values[0], out var parsed))
            {
                return (null, new BadRequestError(ErrorCode, ErrorMessage));
            }

            var setting = await settingsDbClient.UpsertTeamSettingAsync(
                teamId,
                sectionId,
                Id,
                parsed.ToString());

            return (new TeamSettingView
            {
                Id = Id,
                Title = Title,
                Description = Description,
                IsBool = true,
                Values = new[] { setting.FieldValue }
            }, null);
        }
    }
}
