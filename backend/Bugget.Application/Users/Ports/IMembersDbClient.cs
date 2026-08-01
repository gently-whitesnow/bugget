using Bugget.Domain.Users;

namespace Bugget.Application.Users.Ports;

public interface IMembersDbClient
{
    Task<(WorkspaceMember[], TeamMember[])> ListMembersAsync(long userId);
}
