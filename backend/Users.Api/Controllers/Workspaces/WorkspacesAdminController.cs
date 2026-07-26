using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.BO.Interfaces;
using Users.Entities.DbModels.Workspaces;
using Users.Entities.Dto.Workspaces;

namespace Users.Api.Controllers.Workspaces;

[Route("v1/workspaces")]
[Auth(Roles = "admin")]
[WorkspaceRequired]
public sealed class WorkspacesAdminController(IWorkspacesService workspacesService) : ApiController
{
    /// <summary>
    /// Переименовать рабочую область
    /// </summary>
    [HttpPut("{workspaceId}")]
    [ProducesResponseType(typeof(WorkspaceDbModel), 200)]
    public Task<IActionResult> UpdateWorkspaceAsync([FromRoute] int workspaceId, [FromBody] UpdateWorkspaceDto dto)
    {
        var user = User.GetIdentity();

        return workspacesService.UpdateWorkspaceAsync(user.Id, workspaceId, dto.Name)
        .AsActionResultAsync();
    }

    /// <summary>
    /// Удалить рабочую область
    /// </summary>
    [HttpDelete("{workspaceId}")]
    [ProducesResponseType(200)]
    public Task<IActionResult> DeleteWorkspaceAsync()
    {
        var user = User.GetIdentity();

        return workspacesService.DeleteWorkspaceAsync(user.Id, user.WorkspaceId!.Value)
        .AsActionResultAsync();
    }
}
