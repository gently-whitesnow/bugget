using System.ComponentModel.DataAnnotations;
using Bugget.Api.Generated.Users;
using Bugget.Api.Http;
using Bugget.Api.Users.Authentication;
using Bugget.Api.Users.Controllers.Users;
using Bugget.Api.Users.Mappers;
using Bugget.Application.Users.Interfaces;
using Bugget.Contracts.Users.Generated;
using Microsoft.AspNetCore.Mvc;
using HttpProblemDetailsFactory = Bugget.Api.Http.ProblemDetailsFactory;

namespace Bugget.Api.Users.Controllers;

/// <summary>
/// Профиль пользователя. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="UsersControllerBase"/>.
/// </summary>
/// <remarks>
/// Все ручки живут по адресу с контекстом
/// <c>/v1/workspaces/{workspaceId}/teams/{teamId}/users/**</c> — по нему ходит фронт.
/// Идентификаторы из пути не используются: пользователь всегда берётся из identity,
/// поэтому <c>workspaceId</c> и <c>teamId</c> здесь игнорируются.
/// </remarks>
[ApiController]
[Auth]
public sealed class UsersController(
    IUsersService userService,
    IUserExternalLinksService externalLinksService) : UsersControllerBase
{
    /// <summary>
    /// Получить пользователя
    /// </summary>
    public override async Task<ActionResult<User>> GetUserInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();
        var user = await userService.GetUserAsync(identity.Id);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user.ToUserView(identity.WorkspaceRole).ToContract());
    }

    /// <summary>
    /// Обновить данные пользователя
    /// </summary>
    public override async Task<ActionResult<UserProfile>> PutUserInContext(
        string workspaceId,
        string teamId,
        UserUpdateRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var updated = await userService.PutUserAsync(user.Id, new Bugget.Application.Users.Commands.Users.PutUserDto { Name = body.Name });
        return updated.ToContract();
    }

    /// <summary>
    /// Удалить пользователя
    /// </summary>
    public override async Task<IActionResult> DeleteUserInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await userService.DeleteUserAsync(user.Id);
        return Ok();
    }

    /// <summary>
    /// Получение пользователей по id
    /// </summary>
    [WorkspaceRequired]
    public override async Task<ActionResult<ICollection<User>>> ListUsersInContext(
        string workspaceId,
        string teamId,
        [MinLength(1)][MaxLength(1000)] IEnumerable<string> body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var parsedIds = body
            .Where(id => long.TryParse(id, out _))
            .Select(long.Parse)
            .ToArray();
        if (parsedIds.Length == 0)
        {
            return new List<User>();
        }

        var users = await userService.ListUsersAsync(parsedIds, user.WorkspaceId);
        return users.Select(e => e.ToUserView(user.WorkspaceRole).ToContract()).ToList();
    }

    /// <summary>
    /// Поиск пользователей по имени
    /// </summary>
    /// <remarks>
    /// Диапазоны skip/take объявлены здесь: генератор minimum/maximum
    /// query-параметров в атрибуты не переносит.
    /// </remarks>
    [WorkspaceRequired]
    public override async Task<ActionResult<AutocompleteUsers>> AutocompleteUsersInContext(
        string workspaceId,
        string teamId,
        string? searchString = null,
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(1, 100)] int? take = 10,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var users = await userService.AutocompleteUsersAsync(
            user.WorkspaceId!.Value,
            searchString ?? string.Empty,
            skip ?? 0,
            take ?? 10,
            user.TeamId);

        return Ok(new Bugget.Application.Users.Results.Users.AutocompleteUsersView
        {
            Users = users.Select(e => new Bugget.Application.Users.Results.Users.AutocompleteUserView
            {
                Id = e.Id.ToString(),
                Name = e.Name,
                ImageUrl = e.ImageUrl
            }),
            Total = users.Length
        }.ToContract());
    }

    /// <summary>
    /// Список привязанных провайдеров текущего пользователя
    /// </summary>
    public override async Task<ActionResult<ICollection<ExternalLink>>> GetExternalLinksInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var links = await externalLinksService.GetLinksAsync(user.Id);
        return links
            .Select(l => new ExternalLinkView(l.Provider, l.ExternalId, l.Email, l.LinkedAt).ToContract())
            .ToList();
    }

    /// <summary>
    /// Отвязать провайдера (нельзя отвязать последний способ входа)
    /// </summary>
    public override async Task<IActionResult> UnlinkProviderInContext(
        string workspaceId,
        string teamId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var links = await externalLinksService.GetLinksAsync(user.Id);
        if (links.Length <= 1)
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.LastLoginMethod);
        }

        if (links.All(l => l.Provider != provider))
        {
            return NotFound();
        }

        await externalLinksService.RemoveLinkAsync(user.Id, provider);
        return NoContent();
    }

    /// <summary>
    /// Мёрж аккаунтов: перенести данные sourceUser → текущий пользователь
    /// </summary>
    public override async Task<IActionResult> MergeUsersInContext(
        string workspaceId,
        string teamId,
        MergeUsersRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (!long.TryParse(body.Source_user_id, out var sourceUserId))
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.InvalidSourceUserId);
        }

        if (sourceUserId == user.Id)
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.SameSourceUser);
        }

        var (success, errorCode) = await userService.MergeUsersAsync(user.Id, sourceUserId);
        if (!success)
        {
            return errorCode switch
            {
                "source_not_found" => HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.SourceNotFound),
                "source_owns_workspaces" => HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.SourceOwnsWorkspaces),
                _ => HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.MergeFailed)
            };
        }

        return Ok();
    }

    /// <summary>
    /// Привязать Mattermost аккаунт вручную
    /// </summary>
    public override async Task<IActionResult> LinkMattermostInContext(
        string workspaceId,
        string teamId,
        LinkMattermostRequest body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Mattermost_user_id) || body.Mattermost_user_id.Length > 64)
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.InvalidMattermostUserId);
        }

        var user = User.GetIdentity();
        await userService.UpdateMattermostUserIdAsync(user.Id, body.Mattermost_user_id.Trim());
        return NoContent();
    }

    /// <summary>
    /// Отвязать Mattermost аккаунт
    /// </summary>
    public override async Task<IActionResult> UnlinkMattermostInContext(
        string workspaceId,
        string teamId,
        CancellationToken cancellationToken = default)
    {
        var identity = User.GetIdentity();
        var userId = identity.Id;

        if (userId <= 0)
        {
            return Unauthorized();
        }

        await userService.UpdateMattermostUserIdAsync(userId, null);
        return Ok();
    }
}
