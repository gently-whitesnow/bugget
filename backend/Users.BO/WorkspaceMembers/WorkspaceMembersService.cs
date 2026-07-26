using Flow;
using Microsoft.Extensions.Options;
using Users.DA.Interfaces;
using Users.Entities.BO;
using Users.Entities.DbModels.Members;
using Users.Entities.Options;

namespace Users.BO.WorkspaceMembers;

public sealed class WorkspaceMembersService(
    IWorkspaceMembersRepository workspaceMembersRepository,
    IOptions<SelfHostedOptions> selfHostedOptions,
    IAuthorizationRepository authorizationRepository) : IWorkspaceMembersService
{
    public async Task<ResultStruct<WorkspaceMemberDbModel>> CreateWorkspaceMemberAsync(long userId, int workspaceId)
    {
        if (!selfHostedOptions.Value.Enabled)
        {
            return BoErrors.SelfHostedModeRequiredError;
        }

        var workspaceMember = await workspaceMembersRepository.CreateWorkspaceMemberAsync(userId, workspaceId, WorkspaceRole.Member);
        var workspaceMembers = await workspaceMembersRepository.ListWorkspaceMembersAsync(workspaceId);
        if (workspaceMembers.Length > 1)
        {
            await authorizationRepository.InvalidateUserCacheAsync(userId);
            return workspaceMember;
        }

        var upgradedWorkspaceMember = await workspaceMembersRepository.UpdateWorkspaceMemberAsync(userId, workspaceId, WorkspaceRole.Admin);
        await authorizationRepository.InvalidateUserCacheAsync(userId);

        return upgradedWorkspaceMember;
    }
}
