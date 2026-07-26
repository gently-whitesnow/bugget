using System.ComponentModel.DataAnnotations;
using System.Net;
using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Controllers.Users;
using Users.BO;
using Users.BO.Interfaces;
using Users.Entities.DbModels.Users;
using Users.Entities.Dto.Users;
using Users.Entities.View.Users;


namespace Users.Api.Controllers;

[Auth]
[Route("v1/users")]
[Route("v1/workspaces/{workspaceId}/teams/{teamId}/users")]
public sealed class UsersController(
    IUsersService userService,
    IUserExternalLinksService externalLinksService) : ApiController
{
    /// <summary>
    /// Удалить пользователя
    /// </summary>
    /// <returns></returns>
    [HttpDelete]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public Task DeleteUserAsync()
    {
        var user = User.GetIdentity();
        return userService.DeleteUserAsync(user.Id);
    }

    /// <summary>
    /// Получить пользователя
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserView), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetUserAsync()
    {
        var user = User.GetIdentity();
        var userDbModel = await userService.GetUserAsync(user.Id);
        if (userDbModel is null)
        {
            return NotFound();
        }
        return Ok(userDbModel.ToUserView(user.WorkspaceRole));
    }

    /// <summary>
    /// Получение пользователей по id
    /// </summary>
    /// <param name="userIds"></param>
    /// <returns></returns>
    [WorkspaceRequired]
    [HttpPost("batch/list")]
    [ProducesResponseType(typeof(UserView[]), (int)HttpStatusCode.OK)]
    public async Task<UserView[]> ListUsersAsync(
        [FromBody]
        [MinLength(1)]
        [MaxLength(1000)]
        string[] userIds)
    {
        var user = User.GetIdentity();
        var parsedIds = userIds
            .Where(id => long.TryParse(id, out _))
            .Select(long.Parse)
            .ToArray();
        if (parsedIds.Length == 0)
        {
            return [];
        }

        var users = await userService.ListUsersAsync(parsedIds, user.WorkspaceId);
        return users.Select(e => e.ToUserView(user.WorkspaceRole)).ToArray();
    }

    /// <summary>
    /// Поиск пользователей по имени
    /// </summary>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(AutocompleteUsersView), 200)]
    [WorkspaceRequired]
    public async Task<IActionResult> AutocompleteUsersAsync(
        [FromQuery] string? searchString = null,
        [FromQuery][Range(0, int.MaxValue)] int skip = 0,
        [FromQuery][Range(1, 100)] int take = 10)
    {
        var user = User.GetIdentity();
        var users = await userService.AutocompleteUsersAsync(
            user.WorkspaceId!.Value,
            searchString ?? string.Empty,
            skip,
            take,
            user.TeamId);

        return Ok(new AutocompleteUsersView
        {
            Users = users.Select(e => new AutocompleteUserView
            {
                Id = e.Id.ToString(),
                Name = e.Name,
                ImageUrl = e.ImageUrl
            }),
            Total = users.Length
        });
    }

    /// <summary>
    /// Обновить данные пользователя
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserDbModel), 200)]
    public Task<UserDbModel> PutUserAsync([FromBody] PutUserDto putUserDto)
    {
        var user = User.GetIdentity();
        return userService.PutUserAsync(user.Id, putUserDto);
    }

    /// <summary>
    /// Список привязанных провайдеров текущего пользователя
    /// </summary>
    [HttpGet("external-links")]
    [ProducesResponseType(typeof(ExternalLinkView[]), (int)HttpStatusCode.OK)]
    public async Task<ExternalLinkView[]> GetExternalLinksAsync()
    {
        var user = User.GetIdentity();
        var links = await externalLinksService.GetLinksAsync(user.Id);
        return links.Select(l => new ExternalLinkView(l.Provider, l.ExternalId, l.Email, l.LinkedAt)).ToArray();
    }

    /// <summary>
    /// Отвязать провайдера (нельзя отвязать последний способ входа)
    /// </summary>
    [HttpDelete("external-links/{provider}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> UnlinkProviderAsync([FromRoute] string provider)
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
    [HttpPost("merge")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> MergeUsersAsync([FromBody] MergeUsersDto dto)
    {
        var user = User.GetIdentity();

        if (!long.TryParse(dto.SourceUserId, out var sourceUserId))
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
    [HttpPut("mattermost")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> LinkMattermostAsync([FromBody] LinkMattermostDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.MattermostUserId) || dto.MattermostUserId.Length > 64)
        {
            return BadRequest("Некорректный Mattermost User ID");
        }

        var user = User.GetIdentity();
        await userService.UpdateMattermostUserIdAsync(user.Id, dto.MattermostUserId.Trim());
        return NoContent();
    }

    [HttpDelete("mattermost")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> Disconnect()
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
