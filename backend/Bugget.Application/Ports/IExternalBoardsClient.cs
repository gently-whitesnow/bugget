using Bugget.Application.ExternalSearch.Models;

namespace Bugget.Application.Ports;

/// <summary>
/// Доски внешнего трекера, из которых пользователь выбирает цель при переносе репорта.
/// Реализация ходит в конкретного поставщика и живёт в инфраструктуре; прикладной слой
/// и транспорт видят только этот контракт и <see cref="ExternalBoard"/>.
/// </summary>
public interface IExternalBoardsClient
{
    /// <summary>Доски рабочей области с фильтром по названию доски или пространства.</summary>
    Task<ExternalBoard[]> GetBoardsAsync(string workspaceId, string? query = null, uint skip = 0, uint take = 10);

    /// <summary>Доски рабочей области по списку идентификаторов.</summary>
    Task<ExternalBoard[]> BatchGetBoardsAsync(string workspaceId, int[] ids);
}
