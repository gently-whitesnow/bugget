using System;

namespace Bugget.Application.Authorization;

public sealed record WorkspaceMember(
    int WorkspaceId,
    string Role,
    int[] TeamIds
);
