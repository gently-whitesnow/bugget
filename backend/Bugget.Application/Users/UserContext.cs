using Bugget.Domain.Users;

namespace Bugget.Application.Users;

public sealed record UserContext(User User, WorkspaceContext[] Workspaces);
