using System;

namespace Bugget.Application.Authorization;

public sealed record UserContext(User User, WorkspaceMember[] Workspaces);
