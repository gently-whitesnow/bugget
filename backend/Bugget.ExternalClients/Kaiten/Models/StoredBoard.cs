namespace Bugget.ExternalClients.Kaiten.Models;

public sealed class StoredBoard
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string SpaceTitle { get; init; }
    public required int SpaceId { get; init; }
}
