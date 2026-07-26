using System.Threading.Tasks;
using Authorization.Api.Models;

namespace Authorization.Api.Interfaces;

public interface IUserCache
{
    Task<UserContext?> GetUserAsync(string idKey);
    Task SetUserAsync(UserContext user, string idKey);
    Task DeleteUserAsync(string idKey);
}
