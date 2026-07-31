using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Users;

public sealed class WorkspacesService(
    IWorkspacesRepository workspacesDbClient,
    ITeamsRepository teamsRepository,
    IMembersRepository membersRepository,
    IAuthorizationRepository authorizationRepository,
    IOptions<SelfHostedOptions> hostingOptions) : IWorkspacesService
{
    public async Task<(Workspace? Value, Error? Error)> CreateWorkspaceAsync(long userId, string name)
    {
        if (hostingOptions.Value.Enabled)
        {
            return (null, BoErrors.SelfHostedModeError);
        }

        var workspace = await workspacesDbClient.CreateWorkspaceAsync(userId, name);

        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return (workspace, null);
    }

    public Task<Workspace> InternalCreateWorkspaceAsync(string name)
    {
        return workspacesDbClient.CreateWorkspaceAsync(name);
    }

    public Task<Workspace[]> ListWorkspacesAsync()
    {
        return workspacesDbClient.ListWorkspacesAsync();
    }

    public async Task<(Workspace[] Workspaces, WorkspaceMember[] WorkspacesMember, TeamMember[] TeamsMember)> GetWorkspacesContextAsync(long userId)
    {
        var workspaces = await workspacesDbClient.ListWorkspacesAsync(userId);
        if (workspaces.Length == 0)
        {
            return (Array.Empty<Workspace>(), Array.Empty<WorkspaceMember>(), Array.Empty<TeamMember>());
        }
        var workspaceIds = workspaces.Select(e => e.Id).ToArray();
        var teamsTask = teamsRepository.ListTeamsAsync(workspaceIds);
        var membersTask = membersRepository.ListMembersAsync(userId);
        await Task.WhenAll(teamsTask, membersTask);
        var teams = teamsTask.Result;
        var (workspacesMember, teamsMember) = membersTask.Result;

        var teamsLookup = teams.GroupBy(e => e.WorkspaceId).ToDictionary(e => e.Key, e => e.ToArray());

        foreach (var workspace in workspaces)
        {
            workspace.Teams = teamsLookup.TryGetValue(workspace.Id, out var workspaceTeams)
                ? workspaceTeams
                : null;
        }

        return (workspaces, workspacesMember, teamsMember);
    }

    public async Task<(Workspace? Value, Error? Error)> UpdateWorkspaceAsync(long userId, int workspaceId, string name)
    {
        if (hostingOptions.Value.Enabled)
        {
            return (null, BoErrors.SelfHostedModeError);
        }

        var workspace = await workspacesDbClient.UpdateWorkspaceAsync(workspaceId, name);
        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return (workspace, null);
    }

    public async Task<Error?> DeleteWorkspaceAsync(long userId, int workspaceId)
    {
        if (hostingOptions.Value.Enabled)
        {
            return BoErrors.SelfHostedModeError;
        }

        await workspacesDbClient.DeleteWorkspaceAsync(workspaceId);
        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return null;
    }
}
