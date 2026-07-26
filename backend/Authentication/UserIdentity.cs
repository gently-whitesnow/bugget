using System.Security.Claims;

namespace Authentication;

public class UserIdentity(ClaimsPrincipal principal)
{
    public long Id { get; init; } = long.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : -1;
    public string WorkspaceRole { get; init; } = principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    public int? TeamId { get; init; } = int.TryParse(principal.FindFirst(ClaimKey.Team)?.Value, out var teamId) ? teamId : null;
    public int? WorkspaceId { get; init; } = int.TryParse(principal.FindFirst(ClaimKey.Workspace)?.Value, out var workspaceId) ? workspaceId : null;
}
