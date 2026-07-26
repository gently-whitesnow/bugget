using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Controllers.Workspaces;
using Users.BO;
using Users.BO.Interfaces;
using Users.Entities.BO;
using Users.Entities.DbModels.Workspaces;
using Users.Entities.Dto.Workspaces;

namespace Users.Api.Controllers;

[Route("v1/workspaces")]
[Auth]
public sealed class WorkspacesController(IWorkspacesService workspacesService) : ApiController
{
    /// <summary>
    /// Создать рабочую область
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceDbModel), 200)]
    public Task<IActionResult> CreateWorkspaceAsync([FromBody] CreateWorkspaceDto createWorkspaceDto)
    {
        var user = User.GetIdentity();

        return workspacesService.CreateWorkspaceAsync(user.Id, createWorkspaceDto.Name).AsActionResultAsync();
    }

    /// <summary>
    /// Получить рабочие области
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [ProducesResponseType(typeof(WorkspacesContextView), 200)]
    public async Task<WorkspacesContextView> GetWorkspacesContextAsync()
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
        };
    }
}
