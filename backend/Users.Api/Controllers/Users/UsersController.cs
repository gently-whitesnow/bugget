using System.ComponentModel.DataAnnotations;
using Authentication;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts.Generated;
using Users.Api.Controllers.Users;
using Users.Api.Generated;
using Users.Api.Mappers;
using Users.BO.Interfaces;

namespace Users.Api.Controllers;

/// <summary>
/// Профиль пользователя. Маршруты и формы приходят из
/// <c>specs/contracts/users/openapi.yaml</c> через <see cref="UsersControllerBase"/>.
/// </summary>
/// <remarks>
/// Каждая ручка объявлена дважды: с контекстом
/// <c>/v1/workspaces/{workspaceId}/teams/{teamId}/users/**</c> и без него. Так
/// исторически объявлены маршруты этого контроллера, и фронт ходит по варианту
/// с контекстом. Идентификаторы из пути не используются — пользователь всегда
/// берётся из identity, поэтому контекстные операции делегируют бесконтекстным.
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
    public override async Task<ActionResult<User>> GetUser(CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var userDbModel = await userService.GetUserAsync(user.Id);
        if (userDbModel is null)
        {
            return NotFound();
        }

        return Ok(userDbModel.ToUserView(user.WorkspaceRole).ToContract());
    }

    /// <summary>
    /// Обновить данные пользователя
    /// </summary>
    public override async Task<ActionResult<UserProfile>> PutUser(
        UserUpdateRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var updated = await userService.PutUserAsync(user.Id, new Entities.Dto.Users.PutUserDto { Name = body.Name });
        return updated.ToContract();
    }

    /// <summary>
    /// Удалить пользователя
    /// </summary>
    public override async Task<IActionResult> DeleteUser(CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        await userService.DeleteUserAsync(user.Id);
        return Ok();
    }

    /// <summary>
    /// Получение пользователей по id
    /// </summary>
    [WorkspaceRequired]
    public override async Task<ActionResult<ICollection<User>>> ListUsers(
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
    public override async Task<ActionResult<AutocompleteUsers>> AutocompleteUsers(
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

        return Ok(new Entities.View.Users.AutocompleteUsersView
        {
            Users = users.Select(e => new Entities.View.Users.AutocompleteUserView
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
    public override async Task<ActionResult<ICollection<ExternalLink>>> GetExternalLinks(
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
    public override async Task<IActionResult> UnlinkProvider(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var links = await externalLinksService.GetLinksAsync(user.Id);
        if (links.Length <= 1)
        {
            return BadRequest("Нельзя отвязать единственный способ входа");
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
    public override async Task<IActionResult> MergeUsers(
        MergeUsersRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (!long.TryParse(body.Source_user_id, out var sourceUserId))
        {
            return BadRequest("Некорректный sourceUserId");
        }

        if (sourceUserId == user.Id)
        {
            return BadRequest("Нельзя объединить аккаунт сам с собой");
        }

        var (success, errorCode) = await userService.MergeUsersAsync(user.Id, sourceUserId);
        if (!success)
        {
            return errorCode switch
            {
                "source_not_found" => NotFound("Исходный аккаунт не найден"),
                "source_owns_workspaces" => Conflict(new { error = errorCode }),
                _ => BadRequest(errorCode)
            };
        }

        return Ok();
    }

    /// <summary>
    /// Привязать Mattermost аккаунт вручную
    /// </summary>
    public override async Task<IActionResult> LinkMattermost(
        LinkMattermostRequest body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Mattermost_user_id) || body.Mattermost_user_id.Length > 64)
        {
            return BadRequest("Некорректный Mattermost User ID");
        }

        var user = User.GetIdentity();
        await userService.UpdateMattermostUserIdAsync(user.Id, body.Mattermost_user_id.Trim());
        return NoContent();
    }

    /// <summary>
    /// Отвязать Mattermost аккаунт
    /// </summary>
    public override async Task<IActionResult> UnlinkMattermost(CancellationToken cancellationToken = default)
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

    // --- те же операции по адресу с контекстом рабочего пространства и команды ---

    public override Task<ActionResult<User>> GetUserInContext(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default) => GetUser(cancellationToken);

    public override Task<ActionResult<UserProfile>> PutUserInContext(
        int workspaceId,
        int teamId,
        UserUpdateRequest body,
        CancellationToken cancellationToken = default) => PutUser(body, cancellationToken);

    public override Task<IActionResult> DeleteUserInContext(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default) => DeleteUser(cancellationToken);

    [WorkspaceRequired]
    public override Task<ActionResult<ICollection<User>>> ListUsersInContext(
        int workspaceId,
        int teamId,
        [MinLength(1)][MaxLength(1000)] IEnumerable<string> body,
        CancellationToken cancellationToken = default) => ListUsers(body, cancellationToken);

    [WorkspaceRequired]
    public override Task<ActionResult<AutocompleteUsers>> AutocompleteUsersInContext(
        int workspaceId,
        int teamId,
        string? searchString = null,
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(1, 100)] int? take = 10,
        CancellationToken cancellationToken = default) => AutocompleteUsers(searchString, skip, take, cancellationToken);

    public override Task<ActionResult<ICollection<ExternalLink>>> GetExternalLinksInContext(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default) => GetExternalLinks(cancellationToken);

    public override Task<IActionResult> UnlinkProviderInContext(
        int workspaceId,
        int teamId,
        string provider,
        CancellationToken cancellationToken = default) => UnlinkProvider(provider, cancellationToken);

    public override Task<IActionResult> MergeUsersInContext(
        int workspaceId,
        int teamId,
        MergeUsersRequest body,
        CancellationToken cancellationToken = default) => MergeUsers(body, cancellationToken);

    public override Task<IActionResult> LinkMattermostInContext(
        int workspaceId,
        int teamId,
        LinkMattermostRequest body,
        CancellationToken cancellationToken = default) => LinkMattermost(body, cancellationToken);

    public override Task<IActionResult> UnlinkMattermostInContext(
        int workspaceId,
        int teamId,
        CancellationToken cancellationToken = default) => UnlinkMattermost(cancellationToken);
}
