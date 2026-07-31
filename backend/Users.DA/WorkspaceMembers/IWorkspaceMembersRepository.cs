using Users.Entities.DbModels.Members;
using Users.Entities.Errors;

namespace Users.DA.Interfaces;

public interface IWorkspaceMembersRepository
{
    Task<(WorkspaceMemberDbModel? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId, string role, int sizeLimit);
    Task<WorkspaceMemberDbModel> CreateWorkspaceMemberAsync(long userId, int workspaceId, string role);
    Task<WorkspaceMemberDbModel> UpdateWorkspaceMemberAsync(long userId, int workspaceId, string role);
    Task<WorkspaceMemberDbModel[]> ListWorkspaceMembersAsync(int workspaceId);
    Task DeleteWorkspaceMemberAsync(long userId, int workspaceId);
}
