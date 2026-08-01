using Bugget.Api.Extensions;
using Bugget.Api.Generated.Users;
using Bugget.Api.Users.Authentication;
using Bugget.Api.Users.Mappers;
using Bugget.Application.Users.Interfaces;
using Bugget.Contracts.Users.Generated;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Users.Controllers.Workspaces;

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
