using MattermostOAuth;
using Users.BO.Interfaces;

namespace Users.Api.Adapters;

public sealed class MattermostUserUpdaterAdapter(IUsersService usersService) : IMattermostUserUpdater
{
    public Task UpdateMattermostUserIdAsync(long userId, string mattermostUserId)
        => usersService.UpdateMattermostUserIdAsync(userId, mattermostUserId);
}
