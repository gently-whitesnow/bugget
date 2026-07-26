using Users.Entities.DbModels.Workspaces;

namespace Users.DA.Interfaces;

public interface IWorkspacesRepository
{
    Task<WorkspaceDbModel> CreateWorkspaceAsync(long userId, string name);
    Task<WorkspaceDbModel> CreateWorkspaceAsync(string name);
    Task<WorkspaceDbModel[]> ListWorkspacesAsync(long userId);
    Task<WorkspaceDbModel[]> ListWorkspacesAsync();
    Task<WorkspaceDbModel> UpdateWorkspaceAsync(int workspaceId, string name);
    Task DeleteWorkspaceAsync(int workspaceId);
}
