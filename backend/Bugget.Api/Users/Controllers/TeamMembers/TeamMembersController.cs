using Bugget.Api.Generated.Users;
using Bugget.Api.Users.Authentication;
using Bugget.Api.Users.Mappers;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.TeamMembers;
using Bugget.Contracts.Users.Generated;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Users.Controllers.TeamMembers;

/// <summary>Участники команды. Маршруты и формы — из <c>specs/contracts/users/openapi.yaml</c>.</summary>
[ApiController]
[Auth]
[WorkspaceRequired]
public sealed class TeamMembersController(
    ITeamMembersService teamMembersService,
    IOptions<TeamsOptions> teamsOptions,
    IOptions<SelfHostedOptions> selfHostedOptions) : TeamMembersControllerBase
{
    public override async Task<IActionResult> JoinTeam(
        string workspaceId,
        int teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await teamMembersService.CreateTeamMemberAsync(teamId, user.Id);
        return Ok();
    }

    public override async Task<ActionResult<Bugget.Contracts.Users.Generated.TeamMembers>> ListTeamMembers(
        string workspaceId,
        int teamId,
        CancellationToken cancellationToken = default)
    {
        var members = await teamMembersService.ListTeamMembersAsync(teamId);
        var sizeLimit = selfHostedOptions.Value.Enabled ? 0 : teamsOptions.Value.DefaultSizeLimit;
        return members.ToView(sizeLimit).ToContract();
    }

    public override async Task<IActionResult> LeaveTeam(
        string workspaceId,
        int teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await teamMembersService.DeleteTeamMemberAsync(user.Id, teamId);
        return Ok();
    }
}
