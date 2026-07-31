using Authentication;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.WorkspaceMembers;

namespace Users.Api.Controllers.TeamMembers;

/// <summary>
/// Членство в рабочем пространстве. Маршрут и форма приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через
/// <see cref="WorkspaceMembersControllerBase"/>.
/// </summary>
[ApiController]
[Auth]
public sealed class WorkspaceMembersController(IWorkspaceMembersService workspaceMembersService) : WorkspaceMembersControllerBase
{
    /// <summary>
    /// Вступить в рабочую область
    /// </summary>
    public override Task<ActionResult<WorkspaceMember>> JoinWorkspace(
        int workspaceId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return workspaceMembersService.CreateWorkspaceMemberAsync(user.Id, workspaceId)
            .AsContractResultAsync(HttpContext, model => model.ToContract());
    }
}
