using Bugget.Application.ExternalSearch.Models;
using Bugget.Application.Ports;
using Bugget.Infrastructure.ExternalClients.Kaiten.Models;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

/// <summary>
/// Реализация порта досок на Kaiten: наружу отдаёт нейтральный
/// <see cref="ExternalBoard"/>, внутренняя <see cref="StoredBoard"/> за границу не выходит.
/// </summary>
public sealed class KaitenBoardsService(
    KaitenBoardsProvider boardsProvider,
    KaitenBoardsLoaderService loaderService) : IExternalBoardsClient
{
    public async Task<ExternalBoard[]> GetBoardsAsync(string workspaceId, string? query = null, uint skip = 0, uint take = 10)
    {
        await loaderService.EnsureLoadedAsync(workspaceId);

        var boards = boardsProvider.GetBoards(workspaceId);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. boards.Select(ToApplication)];
        }

        return
        [
            .. boards
                .Where(b => b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            b.SpaceTitle.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Skip((int)skip)
                .Take((int)take)
                .Select(ToApplication)
        ];
    }

    public async Task<ExternalBoard[]> BatchGetBoardsAsync(string workspaceId, int[] ids)
    {
        await loaderService.EnsureLoadedAsync(workspaceId);

        return [.. boardsProvider.BatchGetBoards(workspaceId, ids).Select(ToApplication)];
    }

    private static ExternalBoard ToApplication(StoredBoard board) => new()
    {
        Id = board.Id,
        Title = board.Title,
        SpaceTitle = board.SpaceTitle,
        SpaceId = board.SpaceId,
    };
}
