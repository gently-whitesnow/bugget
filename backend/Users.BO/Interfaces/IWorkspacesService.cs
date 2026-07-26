using Flow;
using Users.Entities.BO;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Workspaces;

namespace Users.BO.Interfaces;

public interface IWorkspacesService
{
    Task<ResultStruct<WorkspaceDbModel>> CreateWorkspaceAsync(long userId, string name);
    Task<WorkspaceDbModel> InternalCreateWorkspaceAsync(string name);
    Task<(Workspace[] Workspaces, WorkspaceMemberDbModel[] WorkspacesMember, TeamMemberDbModel[] TeamsMember)> GetWorkspacesContextAsync(long userId);
    Task<WorkspaceDbModel[]> ListWorkspacesAsync();
    Task<ResultStruct<WorkspaceDbModel>> UpdateWorkspaceAsync(long userId, int workspaceId, string name);
    Task<ResultStruct> DeleteWorkspaceAsync(long userId, int workspaceId);
}
