using System.Collections.Concurrent;
using System.Collections.Frozen;
using Bugget.Infrastructure.ExternalClients.Kaiten.Models;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

/// <summary>
/// Провайдер кэша досок Kaiten по workspaceId.
/// </summary>
public sealed class KaitenBoardsProvider
{
    private readonly ConcurrentDictionary<string, FrozenDictionary<int, StoredBoard>> _boardsByWorkspace = new();

    /// <summary>
    /// Устанавливает кэш досок для workspace.
    /// </summary>
    public void SetBoards(string workspaceId, Dictionary<int, StoredBoard> boards)
    {
        _boardsByWorkspace[workspaceId] = boards.ToFrozenDictionary();
    }

    /// <summary>
    /// Проверяет, загружен ли кэш для workspace.
    /// </summary>
    public bool HasCache(string workspaceId)
    {
        return _boardsByWorkspace.ContainsKey(workspaceId);
    }

    /// <summary>
    /// Получает список всех досок для workspace.
    /// </summary>
    public StoredBoard[] GetBoards(string workspaceId)
    {
        if (!_boardsByWorkspace.TryGetValue(workspaceId, out var boards))
        {
            return [];
        }

        return boards.Values.ToArray();
    }

    /// <summary>
    /// Получает список досок по идентификаторам.
    /// </summary>
    public StoredBoard[] BatchGetBoards(string workspaceId, int[] ids)
    {
        if (!_boardsByWorkspace.TryGetValue(workspaceId, out var boards))
        {
            return [];
        }

        return boards.Values.Where(b => ids.Contains(b.Id)).ToArray();
    }

    public StoredBoard? GetBoard(string workspaceId, int id)
    {
        if (!_boardsByWorkspace.TryGetValue(workspaceId, out var boards))
        {
            return null;
        }

        return boards.TryGetValue(id, out var board) ? board : null;
    }

    /// <summary>
    /// Инвалидирует кэш для workspace.
    /// </summary>
    public void InvalidateCache(string workspaceId)
    {
        _boardsByWorkspace.TryRemove(workspaceId, out _);
    }
}
