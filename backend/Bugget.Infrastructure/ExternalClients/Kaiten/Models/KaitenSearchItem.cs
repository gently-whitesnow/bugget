using Bugget.Application.ExternalSearch.Models;

namespace Bugget.Infrastructure.ExternalClients.Kaiten.Models;

public sealed class KaitenSearchItem : IExternalSearchItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public string Source => "kaiten";

    public static KaitenSearchItem FromCard(KaitenCardResponse card) => new()
    {
        Id = card.Id.ToString(),
        Text = card.Title,
    };
}
