using Bugget.Api.Contracts.External.Generated;
using Bugget.ExternalClients.Kaiten.Models;
// Одноимённые типы: контрактная схема и BO-модель. Берём по алиасу, чтобы
// не гадать, кто победил в разрешении имени.
using BoSearchResult = Bugget.BO.ExternalSearch.Models.ExternalSearchResult;
using ContractSearchResult = Bugget.Api.Contracts.External.Generated.ExternalSearchResult;

namespace Bugget.Mappers;

/// <summary>
/// BO → Contracts для <c>/v1/external/**</c>. Имя <c>ExternalSearchResult</c>
/// занято и в BO, и в контракте — BO-тип берётся по алиасу.
/// </summary>
internal static class ExternalMapper
{
    public static ContractSearchResult ToContract(this BoSearchResult result) => new()
    {
        Total = result.Total,
        Items = [.. result.Items.Select(item => new ExternalSearchItem
        {
            Id = item.Id,
            Text = item.Text,
            Source = item.Source,
        })],
    };

    public static ICollection<KaitenBoard> ToContract(this IEnumerable<StoredBoard> boards) =>
        [.. boards.Select(board => new KaitenBoard
        {
            Id = board.Id,
            Title = board.Title,
            Space_title = board.SpaceTitle,
            Space_id = board.SpaceId,
        })];
}
