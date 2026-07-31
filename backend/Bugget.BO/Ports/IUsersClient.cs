using Bugget.Entities.BO;

namespace Bugget.BO.Ports;

public interface IUsersClient
{
    Task<User> GetUserAsync(string userId);

    Task<IEnumerable<User>> GetUsersAsync(IEnumerable<string> userIds);
}
