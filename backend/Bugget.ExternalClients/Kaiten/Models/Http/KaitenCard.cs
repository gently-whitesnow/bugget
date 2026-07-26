namespace Bugget.ExternalClients.Kaiten.Models;

public sealed class KaitenCardResponse
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required int BoardId { get; init; }
}

