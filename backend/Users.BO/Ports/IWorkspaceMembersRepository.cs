using Bugget.Entities.Errors;
using Users.Entities.BO;

namespace Users.BO.Ports;

public interface IWorkspaceMembersRepository
{
    Task<(WorkspaceMember? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId, string role, int sizeLimit);
    Task<WorkspaceMember> CreateWorkspaceMemberAsync(long userId, int workspaceId, string role);
    Task<WorkspaceMember> UpdateWorkspaceMemberAsync(long userId, int workspaceId, string role);
    Task<WorkspaceMember[]> ListWorkspaceMembersAsync(int workspaceId);
    Task DeleteWorkspaceMemberAsync(long userId, int workspaceId);
}
