using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface IWorkspacesRepository
{
    Task<Workspace> CreateWorkspaceAsync(long userId, string name);
    Task<Workspace> CreateWorkspaceAsync(string name);
    Task<Workspace[]> ListWorkspacesAsync(long userId);
    Task<Workspace[]> ListWorkspacesAsync();
    Task<Workspace> UpdateWorkspaceAsync(int workspaceId, string name);
    Task DeleteWorkspaceAsync(int workspaceId);
}
