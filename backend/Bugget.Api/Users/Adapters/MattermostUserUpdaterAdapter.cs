using Bugget.Api.Users.MattermostOAuth;
using Bugget.Application.Users.Interfaces;

namespace Bugget.Api.Users.Adapters;

public sealed class MattermostUserUpdaterAdapter(IUsersService usersService) : IMattermostUserUpdater
{
    public Task UpdateMattermostUserIdAsync(long userId, string mattermostUserId)
        => usersService.UpdateMattermostUserIdAsync(userId, mattermostUserId);
}
