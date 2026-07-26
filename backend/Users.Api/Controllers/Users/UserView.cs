using Users.Entities.DbModels.Users;

namespace Users.Api.Controllers.Users;

public sealed record UserView(string Id, string Name, string? ImageUrl, string WorkspaceRole, string? MattermostUserId);

public static class UserViewExtensions
{
    public static UserView ToUserView(this UserDbModel user, string workspaceRole)
    {
        return new UserView(user.Id.ToString(), user.Name, user.ImageUrl, workspaceRole, user.MattermostUserId);
    }
}
