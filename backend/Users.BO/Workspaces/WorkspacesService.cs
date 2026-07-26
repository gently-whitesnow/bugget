using Flow;
using Microsoft.Extensions.Options;
using Users.BO.Interfaces;
using Users.DA.Interfaces;
using Users.Entities.BO;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Workspaces;
using Users.Entities.Options;

namespace Users.BO;

public sealed class WorkspacesService(
    IWorkspacesRepository workspacesDbClient,
    ITeamsRepository teamsRepository,
    IMembersRepository membersRepository,
    IAuthorizationRepository authorizationRepository,
    IOptions<SelfHostedOptions> hostingOptions) : IWorkspacesService
{
    public async Task<ResultStruct<WorkspaceDbModel>> CreateWorkspaceAsync(long userId, string name)
    {
        if (hostingOptions.Value.Enabled)
        {
            return BoErrors.SelfHostedModeError;
        }

        var workspace = await workspacesDbClient.CreateWorkspaceAsync(userId, name);

        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return workspace;
    }

    public Task<WorkspaceDbModel> InternalCreateWorkspaceAsync(string name)
    {
        return workspacesDbClient.CreateWorkspaceAsync(name);
    }

    public Task<WorkspaceDbModel[]> ListWorkspacesAsync()
    {
        return workspacesDbClient.ListWorkspacesAsync();
    }

    public async Task<(Workspace[] Workspaces, WorkspaceMemberDbModel[] WorkspacesMember, TeamMemberDbModel[] TeamsMember)> GetWorkspacesContextAsync(long userId)
    {
        var workspacesDbModels = await workspacesDbClient.ListWorkspacesAsync(userId);
        if (workspacesDbModels.Length == 0)
        {
            return (Array.Empty<Workspace>(), Array.Empty<WorkspaceMemberDbModel>(), Array.Empty<TeamMemberDbModel>());
        }
        var workspaceIds = workspacesDbModels.Select(e => e.Id).ToArray();
        var teamsTask = teamsRepository.ListTeamsAsync(workspaceIds);
        var membersTask = membersRepository.ListMembersAsync(userId);
        await Task.WhenAll(teamsTask, membersTask);
        var teams = teamsTask.Result;
        var (workspacesMember, teamsMember) = membersTask.Result;

        var teamsLookup = teams.GroupBy(e => e.WorkspaceId).ToDictionary(e => e.Key, e => e.ToList());

        var workspaces = new List<Workspace>(workspacesDbModels.Length);

        foreach (var workspaceDbModel in workspacesDbModels)
        {
            var workspace = new Workspace
            {
                Id = workspaceDbModel.Id,
                Name = workspaceDbModel.Name,
                CreatedAt = workspaceDbModel.CreatedAt,
                UpdatedAt = workspaceDbModel.UpdatedAt,
                Teams = null,
            };
            if (teamsLookup.TryGetValue(workspaceDbModel.Id, out var workspaceTeams))
            {
                workspace.Teams = workspaceTeams.Select(e => new Team
                {
                    Id = e.Id,
                    WorkspaceId = e.WorkspaceId,
                    Name = e.Name,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt,
                }).ToArray();
            }
            workspaces.Add(workspace);
        }

        return (workspaces.ToArray(), workspacesMember, teamsMember);
    }

    public async Task<ResultStruct<WorkspaceDbModel>> UpdateWorkspaceAsync(long userId, int workspaceId, string name)
    {
        if (hostingOptions.Value.Enabled)
        {
            return BoErrors.SelfHostedModeError;
        }

        var workspace = await workspacesDbClient.UpdateWorkspaceAsync(workspaceId, name);
        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return workspace;
    }

    public async Task<ResultStruct> DeleteWorkspaceAsync(long userId, int workspaceId)
    {
        if (hostingOptions.Value.Enabled)
        {
            return BoErrors.SelfHostedModeError;
        }

        await workspacesDbClient.DeleteWorkspaceAsync(workspaceId);
        await authorizationRepository.InvalidateUserCacheAsync(userId);
        return ResultStruct.Success;
    }
}
