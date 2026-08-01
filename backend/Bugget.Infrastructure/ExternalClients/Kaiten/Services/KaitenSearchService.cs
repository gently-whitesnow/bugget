using Bugget.Application.ExternalSearch.Models;
using Bugget.Application.ExternalSearch.Ports;
using Bugget.Application.Ports;
using Bugget.Infrastructure.ExternalClients.Kaiten.Models;
using Bugget.Infrastructure.ExternalClients.Kaiten.Services;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

public sealed class KaitenSearchService(
    KaitenClientFactory clientFactory,
    ISettingsDbClient settingsDbClient,
    KaitenConfigService kaitenConfigService) : IExternalSearchRepository
{

    public async Task<ExternalSearchResult> SearchAsync(
    string workspaceId,
    string teamId,
    string? query,
    uint skip,
    uint take)
    {
        var config = await kaitenConfigService.GetWorkspaceConfigAsync(workspaceId);
        if (config is null)
        {
            return new ExternalSearchResult { Total = 0, Items = [] };
        }

        var client = clientFactory.CreateClient(config);

        var boardIds = await GetTeamBoardIdsAsync(teamId);
        if (boardIds.Length == 0)
        {
            return new ExternalSearchResult { Total = 0, Items = [] };
        }

        var boardsCount = (uint)boardIds.Length;

        // Делим skip/take ровно один раз
        var perBoardSkip = skip / boardsCount;
        var perBoardTake = Math.Max(1u, take / boardsCount); // чтобы не получить 0

        var cardsTasks = boardIds
            .Select(boardId => client.SearchAsync(query, perBoardSkip, perBoardTake, boardId))
            .ToArray();

        var cardsPerBoard = await Task.WhenAll(cardsTasks);

        var items = cardsPerBoard
            .SelectMany(c => c)
            .Select(KaitenSearchItem.FromCard)
            .ToArray();

        return new ExternalSearchResult
        {
            Total = items.Length,
            Items = items
        };
    }

    private async Task<int[]> GetTeamBoardIdsAsync(string teamId)
    {
        var teamSettings = await settingsDbClient.GetTeamSettingsAsync(teamId);

        var boardIdsStrings = teamSettings
            .Where(s => s.FeatureKey == KaitenConstants.FeatureKey && s.FieldKey == KaitenConstants.BoardIdsFieldKey)
            .Select(s => s.FieldValue)
            .ToArray();

        return boardIdsStrings
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToArray();
    }
}
