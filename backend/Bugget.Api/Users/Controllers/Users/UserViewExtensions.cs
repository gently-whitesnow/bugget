using Bugget.Domain.Users;

namespace Bugget.Api.Users.Controllers.Users;

public static class UserViewExtensions
{
    public static UserView ToUserView(this User user, string workspaceRole)
    {
        return new UserView(user.Id.ToString(), user.Name, user.ImageUrl, workspaceRole, user.MattermostUserId);
    }
}
