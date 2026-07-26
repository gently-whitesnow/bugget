using Bugget.ExternalClients.Kaiten.Models;

namespace Bugget.ExternalClients.Kaiten;

/// <summary>
/// Сервис для работы с досками Kaiten.
/// </summary>
public sealed class KaitenBoardsService(
    KaitenBoardsProvider boardsProvider,
    KaitenBoardsLoaderService loaderService)
{
    public async Task<StoredBoard[]> GetBoardsAsync(string workspaceId, string? query = null, uint skip = 0, uint take = 10)
    {
        await loaderService.EnsureLoadedAsync(workspaceId);

        var boards = boardsProvider.GetBoards(workspaceId);

        if (string.IsNullOrWhiteSpace(query))
        {
            return boards;
        }

        return boards
            .Where(b => b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       b.SpaceTitle.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Skip((int)skip)
            .Take((int)take)
            .ToArray();
    }

    public async Task<StoredBoard[]> BatchGetBoardsAsync(string workspaceId, int[] ids)
    {
        await loaderService.EnsureLoadedAsync(workspaceId);

        return boardsProvider.BatchGetBoards(workspaceId, ids);
    }
}

