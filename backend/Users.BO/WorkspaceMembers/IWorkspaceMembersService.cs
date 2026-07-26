using Flow;
using Users.Entities.DbModels.Members;

namespace Users.BO.WorkspaceMembers;

public interface IWorkspaceMembersService
{
    Task<ResultStruct<WorkspaceMemberDbModel>> CreateWorkspaceMemberAsync(long userId, int workspaceId);
}
