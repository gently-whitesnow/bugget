using Bugget.Domain;

namespace Bugget.Application.Ports;

public interface IUsersClient
{
    Task<User> GetUserAsync(string userId);

    Task<IEnumerable<User>> GetUsersAsync(IEnumerable<string> userIds);
}
