using Bugget.Application.Ports;
using Bugget.Application.Users.Interfaces;
using Bugget.Domain;

namespace Bugget.Api.Modules.InProcess;

/// <summary>
/// Доступ модуля reports к пользователям. Раньше был HTTP-вызовом в users-api
/// (<c>POST _internal/users/batch-get</c>), после объединения сервисов — прямой вызов.
/// </summary>
public sealed class UsersClientAdapter(IUsersService usersService) : IUsersClient
{
    public async Task<User> GetUserAsync(string userId)
    {
        if (!long.TryParse(userId, out var id))
        {
            return Unknown(userId);
        }

        var users = await usersService.ListUsersAsync([id], null);
        var user = users.FirstOrDefault();

        return user is null ? Unknown(userId) : Map(userId, user);
    }

    public async Task<IEnumerable<User>> GetUsersAsync(IEnumerable<string> userIds)
    {
        var raw = userIds.ToArray();
        var parseable = new List<long>(raw.Length);
        foreach (var id in raw)
        {
            if (long.TryParse(id, out var parsed))
            {
                parseable.Add(parsed);
            }
        }

        if (parseable.Count == 0)
        {
            return raw.Select(Unknown);
        }

        var users = await usersService.ListUsersAsync([.. parseable], null);
        var byId = users.ToDictionary(u => u.Id.ToString(), u => u);

        return raw.Select(id => byId.TryGetValue(id, out var user) ? Map(id, user) : Unknown(id));
    }

    /// <summary>
    /// Пользователь не найден в users — отдаём id вместо имени, как это делал HTTP-клиент.
    /// </summary>
    private static User Unknown(string userId) => new() { Id = userId, Name = userId };

    private static User Map(string userId, Bugget.Domain.Users.User user) => new()
    {
        Id = userId,
        Name = user.Name ?? userId,
        MattermostUserId = user.MattermostUserId,
        ImageUrl = user.ImageUrl,
    };
}
