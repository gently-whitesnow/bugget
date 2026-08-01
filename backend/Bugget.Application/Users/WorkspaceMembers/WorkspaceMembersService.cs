using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Users.WorkspaceMembers;

public sealed class WorkspaceMembersService(
    IWorkspaceMembersRepository workspaceMembersRepository,
    IOptions<SelfHostedOptions> selfHostedOptions,
    IAuthorizationRepository authorizationRepository) : IWorkspaceMembersService
{
    public async Task<(WorkspaceMember? Value, Error? Error)> CreateWorkspaceMemberAsync(long userId, int workspaceId)
    {
        if (!selfHostedOptions.Value.Enabled)
        {
            return (null, BoErrors.SelfHostedModeRequiredError);
        }

        var workspaceMember = await workspaceMembersRepository.CreateWorkspaceMemberAsync(userId, workspaceId, WorkspaceRole.Member);
        var workspaceMembers = await workspaceMembersRepository.ListWorkspaceMembersAsync(workspaceId);
        if (workspaceMembers.Length > 1)
        {
            await authorizationRepository.InvalidateUserCacheAsync(userId);
            return (workspaceMember, null);
        }

        var upgradedWorkspaceMember = await workspaceMembersRepository.UpdateWorkspaceMemberAsync(userId, workspaceId, WorkspaceRole.Admin);
        await authorizationRepository.InvalidateUserCacheAsync(userId);

        return (upgradedWorkspaceMember, null);
    }
}
