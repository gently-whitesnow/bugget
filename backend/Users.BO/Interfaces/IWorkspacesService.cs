using Bugget.Entities.Errors;
using Users.Entities.BO;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Workspaces;

namespace Users.BO.Interfaces;

public interface IWorkspacesService
{
    Task<(WorkspaceDbModel? Value, Error? Error)> CreateWorkspaceAsync(long userId, string name);
    Task<WorkspaceDbModel> InternalCreateWorkspaceAsync(string name);
    Task<(Workspace[] Workspaces, WorkspaceMemberDbModel[] WorkspacesMember, TeamMemberDbModel[] TeamsMember)> GetWorkspacesContextAsync(long userId);
    Task<WorkspaceDbModel[]> ListWorkspacesAsync();
    Task<(WorkspaceDbModel? Value, Error? Error)> UpdateWorkspaceAsync(long userId, int workspaceId, string name);
    Task<Error?> DeleteWorkspaceAsync(long userId, int workspaceId);
}
