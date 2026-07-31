using Users.Entities.BO;

namespace Users.BO.Ports;

public interface IMembersRepository
{
    Task<(WorkspaceMember[], TeamMember[])> ListMembersAsync(long userId);
}
