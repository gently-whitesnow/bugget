using Users.Entities.DbModels.Members;
using Users.Entities.Errors;

namespace Users.BO.WorkspaceMembers;

public interface IWorkspaceMembersService
{
    Task<(WorkspaceMemberDbModel? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId);
}
