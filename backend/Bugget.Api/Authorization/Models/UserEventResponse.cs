using System;

namespace Bugget.Api.Authorization.Models;

public sealed record UserEventResponse
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public int? TeamId { get; set; }
    public int? WorkspaceId { get; set; }
    public int Role { get; set; }
    public string EventType { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
