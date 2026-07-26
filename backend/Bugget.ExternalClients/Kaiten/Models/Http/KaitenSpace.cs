namespace Bugget.ExternalClients.Kaiten.Models;

public sealed class KaitenSpaceResponse
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public bool Archived { get; init; }
    public KaitenBoardResponse[] Boards { get; init; } = [];
}
