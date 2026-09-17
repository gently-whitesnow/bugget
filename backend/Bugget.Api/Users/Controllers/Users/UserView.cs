using Bugget.Domain.Users;

namespace Bugget.Api.Users.Controllers.Users;

public sealed record UserView(string Id, string Name, string? ImageUrl, string WorkspaceRole, string? MattermostUserId);
