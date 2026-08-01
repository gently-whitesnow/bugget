using Bugget.Domain.Errors;
using Bugget.Domain.Users;

namespace Bugget.Application.Users.WorkspaceMembers;

public interface IWorkspaceMembersService
{
    Task<(WorkspaceMember? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId);
}
