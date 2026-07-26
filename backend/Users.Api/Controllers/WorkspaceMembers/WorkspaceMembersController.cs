using System.Net;
using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.BO.TeamMembers;
using Users.BO.WorkspaceMembers;
using Users.Entities.DbModels.Members;

namespace Users.Api.Controllers.TeamMembers;

[Auth]
[Route("v1/workspaces")]
public sealed class WorkspaceMembersController(IWorkspaceMembersService workspaceMembersService) : ApiController
{
    /// <summary>
    /// Вступить в рабочую область
    /// </summary>
    [HttpPost("{workspaceId}/members/join")]
    [ProducesResponseType(typeof(WorkspaceMemberDbModel), 200)]
    public Task<IActionResult> JoinWorkspaceAsync([FromRoute] int workspaceId)
    {
        var user = User.GetIdentity();
        return workspaceMembersService.CreateWorkspaceMemberAsync(user.Id, workspaceId).AsActionResultAsync();
    }
}
