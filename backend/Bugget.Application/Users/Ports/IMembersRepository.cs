using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface IMembersRepository
{
    Task<(WorkspaceMember[], TeamMember[])> ListMembersAsync(long userId);
}
