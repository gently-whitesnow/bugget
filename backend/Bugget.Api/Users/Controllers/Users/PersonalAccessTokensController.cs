using Bugget.Api.Generated.Users;
using Bugget.Api.Http;
using Bugget.Api.Users.Authentication;
using Bugget.Api.Users.Mappers;
using Bugget.Application.Users.Interfaces;
using Bugget.Contracts.Users.Generated;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Users.Controllers;

/// <summary>
/// Personal access tokens текущего пользователя. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через
/// <see cref="PersonalAccessTokensControllerBase"/>.
/// </summary>
/// <remarks>
/// Токены строго личные: владелец берётся из identity, admin-доступа к чужим токенам
/// нет. Сегменты <c>workspaceId</c>/<c>teamId</c> из адреса не используются — токен
/// привязывается к контексту identity, как и везде в модуле users.
/// </remarks>
[ApiController]
[Auth]
public sealed class PersonalAccessTokensController(
    IPersonalAccessTokensService tokensService) : PersonalAccessTokensControllerBase
{
    /// <summary>
    /// Токены пользователя по всем его командам — без секрета
    /// </summary>
    public override async Task<ActionResult<ICollection<PersonalAccessToken>>> ListInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();
        var tokens = await tokensService.ListAsync(identity.Id);
        return tokens.Select(t => t.ToContract()).ToList();
    }

    /// <summary>
    /// Выпустить токен: значение возвращается один раз
    /// </summary>
    [WorkspaceRequired]
    [TeamRequired]
    public override async Task<ActionResult<PersonalAccessTokenCreated>> CreateInContext(
        string workspaceId,
        string teamId,
        PersonalAccessTokenCreateRequest body,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();
        var issued = await tokensService.IssueAsync(
            identity.Id,
            identity.WorkspaceId!.Value,
            identity.TeamId!.Value,
            body.Label,
            body.Expires_at);

        return new PersonalAccessTokenCreated
        {
            Token = issued.SecretValue,
            Personal_access_token = issued.Token.ToContract(),
        };
    }

    /// <summary>
    /// Отозвать свой токен: чужой и несуществующий неразличимы — 404
    /// </summary>
    [RouteParameterConstraint("tokenId", "long")]
    public override async Task<IActionResult> RevokeInContext(
        string workspaceId,
        string teamId,
        string tokenId,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();

        var invalidTokenId = WireInt64.TryBindRouteValue(HttpContext, "tokenId", tokenId, out var id);
        if (invalidTokenId is not null)
        {
            return invalidTokenId;
        }

        var revoked = await tokensService.RevokeAsync(id, identity.Id);
        return revoked ? NoContent() : NotFound();
    }
}
