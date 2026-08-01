using Bugget.Application.ExternalSearch.Models;
using Bugget.Contracts.External.Generated;
// Одноимённые типы: контрактная схема и BO-модель. Берём по алиасу, чтобы
// не гадать, кто победил в разрешении имени.
using BoSearchResult = Bugget.Application.ExternalSearch.Models.ExternalSearchResult;
using ContractSearchResult = Bugget.Contracts.External.Generated.ExternalSearchResult;

namespace Bugget.Api.Mappers;

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

    public static ICollection<KaitenBoard> ToContract(this IEnumerable<ExternalBoard> boards) =>
        [.. boards.Select(board => new KaitenBoard
        {
            Id = board.Id,
            Title = board.Title,
            Space_title = board.SpaceTitle,
            Space_id = board.SpaceId,
        })];
}
