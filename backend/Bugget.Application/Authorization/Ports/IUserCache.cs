using System.Threading.Tasks;

namespace Bugget.Application.Authorization.Ports;

public interface IUserCache
{
    Task<UserContext?> GetUserAsync(string idKey);
    Task SetUserAsync(UserContext user, string idKey);
    Task DeleteUserAsync(string idKey);
}
