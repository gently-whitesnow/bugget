using Authentication;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Controllers.Workspaces;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.Interfaces;

namespace Users.Api.Controllers;

/// <summary>
/// Рабочие пространства пользователя. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="WorkspacesControllerBase"/>.
/// </summary>
[ApiController]
[Auth]
public sealed class WorkspacesController(IWorkspacesService workspacesService) : WorkspacesControllerBase
{
    /// <summary>
    /// Создать рабочую область
    /// </summary>
    public override Task<ActionResult<Workspace>> CreateWorkspace(
        WorkspaceCreateRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        return workspacesService.CreateWorkspaceAsync(user.Id, body.Name)
            .AsContractResultAsync(HttpContext, model => model.ToContract());
    }

    /// <summary>
    /// Получить рабочие области
    /// </summary>
    public override async Task<ActionResult<WorkspacesContext>> GetWorkspacesContext(
        CancellationToken cancellationToken = default)
    {
        var (workspaces, workspacesMember, teamsMember) = await workspacesService.GetWorkspacesContextAsync(User.GetIdentity().Id);
        return new WorkspacesContextView
        {
            Workspaces = workspaces.Select(e =>
            new WorkspaceView
            {
                Id = e.Id.ToString(),
                Name = e.Name,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                Teams = e.Teams?.Select(t =>
                new TeamView
                {
                    Id = t.Id.ToString(),
                    Name = t.Name,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToArray()
            }).ToArray(),
            TeamsMember = teamsMember?.Select(m => new TeamMemberView
            {
                TeamId = m.TeamId.ToString(),
                UserId = m.UserId.ToString(),
                CreatedAt = m.CreatedAt
            }).ToArray(),
            WorkspacesMember = workspacesMember?.Select(m => new WorkspaceMemberView
            {
                WorkspaceId = m.WorkspaceId.ToString(),
                UserId = m.UserId.ToString(),
                Role = m.Role,
                CreatedAt = m.CreatedAt
            }).ToArray()
        }.ToContract();
    }
}
