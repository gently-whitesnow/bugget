using System.Text.RegularExpressions;

namespace Bugget.Api.Authorization.Authentication;

/// <summary>
/// Workspace/team из <c>X-Original-URI</c>, который nginx кладёт в auth_request.
/// Общий разбор для JWT (мягкая проверка) и PAT (жёсткое совпадение со scope токена).
/// </summary>
internal static partial class OriginalUriScope
{
    public static (int? WorkspaceId, int? TeamId) ParseOptional(string? originalUri)
    {
        if (string.IsNullOrEmpty(originalUri))
        {
            return (null, null);
        }

        var workspaceMatch = WorkspaceIdRegex().Match(originalUri);
        var teamMatch = TeamIdRegex().Match(originalUri);

        int? workspaceId = workspaceMatch.Success
            ? int.Parse(workspaceMatch.Groups["wid"].Value)
            : null;
        int? teamId = teamMatch.Success
            ? int.Parse(teamMatch.Groups["tid"].Value)
            : null;

        return (workspaceId, teamId);
    }

    public static bool TryParse(string? originalUri, out int workspaceId, out int teamId)
    {
        var (parsedWorkspaceId, parsedTeamId) = ParseOptional(originalUri);
        if (parsedWorkspaceId is null || parsedTeamId is null)
        {
            workspaceId = default;
            teamId = default;
            return false;
        }

        workspaceId = parsedWorkspaceId.Value;
        teamId = parsedTeamId.Value;
        return true;
    }

    [GeneratedRegex(@"workspaces/(?<wid>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex WorkspaceIdRegex();

    [GeneratedRegex(@"teams/(?<tid>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex TeamIdRegex();
}
