using Bugget.Api.Extensions;
using Bugget.Api.Generated.Users;
using Bugget.Api.Users.Authentication;
using Bugget.Api.Users.Mappers;
using Bugget.Application.Users.WorkspaceMembers;
using Bugget.Contracts.Users.Generated;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Users.Controllers.TeamMembers;

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
