using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.TeamInvites;

namespace Users.Api.Controllers.TeamInvites;

/// <summary>
/// Приём приглашения в команду. Маршрут и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="TeamInvitesControllerBase"/>.
/// </summary>
[ApiController]
[Auth]
public sealed class TeamInvitesController(ITeamInvitesService teamInvitesService) : TeamInvitesControllerBase
{
    /// <summary>
    /// Принять инвайт
    /// </summary>
    public override Task<ActionResult<AcceptedInvite>> AcceptTeamInvite(
        AcceptInviteRequest body,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetIdentity().Id;
        return teamInvitesService.AcceptTeamInviteAsync(body.Token, userId)
            .AsContractResultAsync(result => result.ToAcceptView().ToContract());
    }
}
