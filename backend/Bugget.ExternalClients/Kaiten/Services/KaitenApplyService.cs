using Bugget.BO.ExternalSearch.Models;
using Bugget.BO.Interfaces;
using Bugget.BO.Services.ReportLinks;
using Bugget.DA.Interfaces;
using Bugget.Entities.Authentication;
using Bugget.Entities.DbModels.Settings;
using Bugget.Entities.DTO.Link;
using Bugget.Entities.Options;
using Bugget.ExternalClients.Kaiten.Services;
using Bugget.ExternalClients.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.ExternalClients.Kaiten;

/// <summary>
/// Сервис для применения результатов поиска Kaiten (прикрепление ссылки и отправка комментария).
/// </summary>
public sealed class KaitenApplyService(
    KaitenClientFactory clientFactory,
    ILogger<KaitenApplyService> logger,
    ReportLinksService reportLinksService,
    KaitenConfigService kaitenConfigService,
    KaitenBoardsProvider boardsProvider,
    IOptions<ReportAliasOptions> reportAliasOptions) : IExternalApplyRepository
{
    private static readonly string? BuggetBaseUrl = Environment.GetEnvironmentVariable(NotificationsConstants.BuggetBaseUrlKey);

    public async Task ApplySearchResultAsync(UserIdentity user, string workspaceId, string teamId, ExternalSearchApply searchApply)
    {
        // Проверяем, что источник - Kaiten
        if (!string.Equals(searchApply.Source, KaitenConstants.FeatureKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var config = await kaitenConfigService.GetWorkspaceConfigAsync(workspaceId);
        if (config is null)
        {
            logger.LogWarning("Kaiten settings not configured for workspace {WorkspaceId}", workspaceId);
            return;
        }

        var client = clientFactory.CreateClient(config);

        var (useReportLinking, sendReportLinkToComments) = await kaitenConfigService.GetTeamSettingsAsync(teamId);

        var cardId = searchApply.Id;
        var reportPath = reportAliasOptions.Value.AliasMode == ReportAliasMode.Team
            && !string.IsNullOrEmpty(searchApply.reportIdContext.TeamId)
            ? $"/teams/{searchApply.reportIdContext.TeamId}/reports/{searchApply.reportIdContext.AliasId}"
            : $"/reports/{searchApply.reportIdContext.AliasId}";

        var reportUrl = $"{BuggetBaseUrl}{reportPath}";

        if (useReportLinking)
        {
            await SafeAddExternalLinkAsync(client, cardId, reportUrl, $"Баг-репорт #{searchApply.reportIdContext.AliasId}");
        }

        if (sendReportLinkToComments)
        {
            var commentText = $"Создан баг-репорт: {reportUrl}";
            await SafeAddCommentAsync(client, cardId, commentText);
        }

        var card = await client.GetCardAsync(cardId);
        if (card is null)
        {
            logger.LogError("Карточка {CardId} не найдена", cardId);
            return;
        }
        var space = boardsProvider.GetBoard(workspaceId, card.BoardId);
        if (space is null)
        {
            logger.LogError("Доска {BoardId} не найдена", card.BoardId);
            return;
        }

        var kaitenLink = $"{config.Domain}/space/{space.SpaceId}/boards/card/{cardId}";
        await reportLinksService.CreateReportLinkInternalAsync(user, searchApply.reportIdContext, new ReportLinkDto { Link = kaitenLink, Name = $"Задача" });
    }

    private async Task SafeAddExternalLinkAsync(KaitenClient client, string cardId, string url, string description)
    {
        try
        {
            await client.AddExternalLinkAsync(cardId, url, description);
            logger.LogInformation("Внешняя ссылка добавлена к карточке {CardId}: {Url}", cardId, url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при добавлении внешней ссылки к карточке {CardId}", cardId);
        }
    }

    private async Task SafeAddCommentAsync(KaitenClient client, string cardId, string text)
    {
        try
        {
            await client.AddCommentAsync(cardId, text);
            logger.LogInformation("Комментарий добавлен к карточке {CardId}", cardId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при добавлении комментария к карточке {CardId}", cardId);
        }
    }
}
