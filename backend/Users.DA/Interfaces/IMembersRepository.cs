using Users.Entities.DbModels.Members;

namespace Users.DA.Interfaces;

public interface IMembersRepository
{
    Task<(WorkspaceMemberDbModel[], TeamMemberDbModel[])> ListMembersAsync(long userId);
}
