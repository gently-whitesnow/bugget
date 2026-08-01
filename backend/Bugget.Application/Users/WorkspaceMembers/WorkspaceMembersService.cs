using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Users.WorkspaceMembers;

public sealed class WorkspaceMembersService(
    IWorkspaceMembersDbClient workspaceMembersDbClient,
    IOptions<SelfHostedOptions> selfHostedOptions,
    IUserCacheInvalidator userCacheInvalidator) : IWorkspaceMembersService
{
    public async Task<(WorkspaceMember? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId)
    {
        if (!selfHostedOptions.Value.Enabled)
        {
            return (null, BoErrors.SelfHostedModeRequiredError);
        }

        var workspaceMember = await workspaceMembersDbClient.CreateWorkspaceMemberAsync(userId, workspaceId, WorkspaceRole.Member);
        var workspaceMembers = await workspaceMembersDbClient.ListWorkspaceMembersAsync(workspaceId);
        if (workspaceMembers.Length > 1)
        {
            await userCacheInvalidator.InvalidateUserCacheAsync(userId);
            return (workspaceMember, null);
        }

        var upgradedWorkspaceMember = await workspaceMembersDbClient.UpdateWorkspaceMemberAsync(userId, workspaceId, WorkspaceRole.Admin);
        await userCacheInvalidator.InvalidateUserCacheAsync(userId);

        return (upgradedWorkspaceMember, null);
    }
}
