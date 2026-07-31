using Authentication;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.Interfaces;

namespace Users.Api.Controllers.Workspaces;

/// <summary>
/// Административные операции над рабочим пространством. Маршруты и формы приходят
/// из <c>specs/contracts/users/openapi.yaml</c> через
/// <see cref="WorkspacesAdminControllerBase"/>.
/// </summary>
[ApiController]
[Auth(Roles = "admin")]
[WorkspaceRequired]
public sealed class WorkspacesAdminController(IWorkspacesService workspacesService) : WorkspacesAdminControllerBase
{
    /// <summary>
    /// Переименовать рабочую область
    /// </summary>
    public override Task<ActionResult<Workspace>> UpdateWorkspace(
        int workspaceId,
        WorkspaceUpdateRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        return workspacesService.UpdateWorkspaceAsync(user.Id, workspaceId, body.Name)
            .AsContractResultAsync(HttpContext, model => model.ToContract());
    }

    /// <summary>
    /// Удалить рабочую область
    /// </summary>
    /// <remarks>
    /// Удаляется текущая область пользователя, а не та, что в пути: так было и до
    /// contract-first, идентификатор в адресе оставлен ради формы URL.
    /// </remarks>
    public override Task<IActionResult> DeleteWorkspace(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        return workspacesService.DeleteWorkspaceAsync(user.Id, user.WorkspaceId!.Value)
            .AsActionResultAsync(HttpContext);
    }
}
