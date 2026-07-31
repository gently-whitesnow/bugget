using System.Collections.Concurrent;
using Bugget.Infrastructure.ExternalClients.Kaiten.Models;
using Bugget.Infrastructure.ExternalClients.Kaiten.Services;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

/// <summary>
/// Сервис для lazy загрузки и перезагрузки кэша досок Kaiten по workspace.
/// </summary>
public sealed class KaitenBoardsLoaderService(
    KaitenClientFactory clientFactory,
    KaitenBoardsProvider boardsProvider,
    ILogger<KaitenBoardsLoaderService> logger,
    KaitenConfigService kaitenConfigService)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadLocks = new();

    /// <summary>
    /// Загружает кэш досок для workspace, если ещё не загружен.
    /// </summary>
    public async Task EnsureLoadedAsync(string workspaceId)
    {
        if (boardsProvider.HasCache(workspaceId))
        {
            return;
        }

        await LoadBoardsAsync(workspaceId);
    }

    /// <summary>
    /// Инвалидирует кэш для workspace без перезагрузки.
    /// </summary>
    public void InvalidateCache(string workspaceId)
    {
        boardsProvider.InvalidateCache(workspaceId);
    }

    private async Task LoadBoardsAsync(string workspaceId)
    {
        var lockObj = _loadLocks.GetOrAdd(workspaceId, _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync();
        try
        {
            // Double-check после получения lock
            if (boardsProvider.HasCache(workspaceId))
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

            logger.LogInformation("Loading Kaiten boards and spaces for workspace {WorkspaceId}...", workspaceId);

            var spaces = await client.GetSpacesAsync();
            var storedBoards = new Dictionary<int, StoredBoard>();

            foreach (var space in spaces.Where(s => !s.Archived))
            {
                foreach (var board in space.Boards)
                {
                    storedBoards.TryAdd(board.Id, new StoredBoard
                    {
                        Id = board.Id,
                        Title = board.Title,
                        SpaceTitle = space.Title,
                        SpaceId = space.Id
                    });
                }
            }

            boardsProvider.SetBoards(workspaceId, storedBoards);
            logger.LogInformation(
                "Loaded {BoardCount} Kaiten boards for workspace {WorkspaceId}",
                storedBoards.Count,
                workspaceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Kaiten boards for workspace {WorkspaceId}", workspaceId);
        }
        finally
        {
            lockObj.Release();
        }
    }
}
