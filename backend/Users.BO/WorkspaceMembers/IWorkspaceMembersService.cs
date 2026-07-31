using Bugget.Entities.Errors;
using Users.Entities.DbModels.Members;

namespace Users.BO.WorkspaceMembers;

public interface IWorkspaceMembersService
{
    Task<(WorkspaceMemberDbModel? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId);
}
