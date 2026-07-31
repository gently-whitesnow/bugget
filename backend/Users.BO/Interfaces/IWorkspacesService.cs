using Bugget.Entities.Errors;
using Users.Entities.BO;

namespace Users.BO.Interfaces;

public interface IWorkspacesService
{
    Task<(Workspace? Value, Error? Error)> CreateWorkspaceAsync(long userId, string name);
    Task<Workspace> InternalCreateWorkspaceAsync(string name);
    Task<(Workspace[] Workspaces, WorkspaceMember[] WorkspacesMember, TeamMember[] TeamsMember)> GetWorkspacesContextAsync(long userId);
    Task<Workspace[]> ListWorkspacesAsync();
    Task<(Workspace? Value, Error? Error)> UpdateWorkspaceAsync(long userId, int workspaceId, string name);
    Task<Error?> DeleteWorkspaceAsync(long userId, int workspaceId);
}
