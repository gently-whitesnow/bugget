using Bugget.Entities.BO;

namespace Bugget.DA.Interfaces;

public interface IUsersClient
{
    Task<User> GetUserAsync(string userId);

    Task<IEnumerable<User>> GetUsersAsync(IEnumerable<string> userIds);
}
