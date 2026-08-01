using System.Security.Claims;

namespace Bugget.Api.Users.Authentication;

public static class ClaimKey
{
    public const string Workspace = "workspace_id";
    public const string Team = "team_id";
}
