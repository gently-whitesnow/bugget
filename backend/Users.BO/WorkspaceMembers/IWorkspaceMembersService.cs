using Bugget.Entities.Errors;
using Users.Entities.BO;

namespace Users.BO.WorkspaceMembers;

public interface IWorkspaceMembersService
{
    Task<(WorkspaceMember? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId);
}
