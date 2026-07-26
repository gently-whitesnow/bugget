using System.ComponentModel.DataAnnotations;
using System.Net;
using Flow.Extensions;
using Microsoft.AspNetCore.Mvc;
using Users.Api.Controllers.Users;
using Users.BO;
using Users.BO.Interfaces;
using Users.Entities.DbModels.Users;
using Users.Entities.Dto.Users;

namespace Users.Api.Controllers;

[Route("_internal/users")]
public sealed class InternalUsersController(
    IUsersService usersService,
    IUserExternalLinksService externalLinksService) : ApiController
{
    /// <summary>
    /// Добавить или обновить пользователя сервисом авторизации
    /// </summary>
    /// <param name="createUserDto"></param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(typeof(UserDbModel), (int)HttpStatusCode.OK)]
    public Task<UserDbModel> InsertOrUpdateUserAsync([FromBody] CreateUserDto createUserDto)
    {
        return usersService.TryInsertUserAsync(createUserDto);
    }

    /// <summary>
    /// Получить контекст пользователя сервисом авторизации
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("context/{userId}")]
    [ProducesResponseType(typeof(UserContext), (int)HttpStatusCode.OK)]
    public Task<IActionResult> GetUserAsync([FromRoute] long userId)
    {
        return usersService.GetUserContextAsync(userId).AsActionResultAsync();
    }

    /// <summary>
    /// Получить контекст пользователя по externalId (для OIDC Bearer)
    /// </summary>
    [HttpGet("context/by-external-id/{externalId}")]
    [ProducesResponseType(typeof(UserContext), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public Task<IActionResult> GetUserContextByExternalIdAsync([FromRoute] string externalId)
    {
        return usersService.GetUserContextByExternalIdAsync(externalId).AsActionResultAsync();
    }

    /// <summary>
    /// Проверить, есть ли у пользователя доступ к SaaS-админке
    /// </summary>
    [HttpGet("{userId}/admin-access")]
    [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
    public Task<bool> HasAdminAccessAsync([FromRoute] long userId)
    {
        return usersService.IsAdminAsync(userId);
    }

    /// <summary>
    /// Получить пользователей по id
    /// </summary>
    /// <returns></returns>
    [HttpPost("batch-get")]
    [ProducesResponseType(typeof(UserDbModel[]), (int)HttpStatusCode.OK)]
    public Task<UserDbModel[]> ListUsersAsync([FromBody][MinLength(1)][MaxLength(1000)] long[] userIds)
    {
        return usersService.ListUsersAsync(userIds, null);
    }

    /// <summary>
    /// Удалить пользователя по id
    /// </summary>
    /// <returns></returns>
    [HttpDelete("{userId}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public Task DeleteUserAsync([FromRoute] long userId)
    {
        return usersService.DeleteUserAsync(userId);
    }

    /// <summary>
    /// Поиск пользователя по провайдеру и внешнему ID
    /// </summary>
    [HttpGet("by-provider/{provider}/{externalId}")]
    [ProducesResponseType(typeof(long), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> FindUserByProviderAsync([FromRoute] string provider, [FromRoute] string externalId)
    {
        var userId = await externalLinksService.FindUserByProviderAndExternalIdAsync(provider, externalId);
        if (userId is null)
        {
            return NotFound();
        }

        return Ok(userId.Value);
    }

    /// <summary>
    /// Список привязок пользователя
    /// </summary>
    [HttpGet("{userId}/external-links")]
    [ProducesResponseType(typeof(UserExternalLinkDbModel[]), (int)HttpStatusCode.OK)]
    public Task<UserExternalLinkDbModel[]> GetExternalLinksAsync([FromRoute] long userId)
    {
        return externalLinksService.GetLinksAsync(userId);
    }

    /// <summary>
    /// Добавить привязку провайдера (вызывается из authorization-api при mode=link)
    /// </summary>
    [HttpPost("{userId}/external-links")]
    [ProducesResponseType(typeof(ExternalLinkView), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    public async Task<IActionResult> AddExternalLinkAsync(
        [FromRoute] long userId,
        [FromBody] AddExternalLinkDto dto)
    {
        var existing = await externalLinksService.FindUserByProviderAndExternalIdAsync(dto.Provider, dto.ExternalId);
        if (existing is not null)
        {
            return Conflict(new { error = "external_id_taken", ownerId = existing.Value.ToString() });
        }

        var link = await externalLinksService.AddLinkAsync(userId, dto.Provider, dto.ExternalId, dto.Email);
        return Ok(new ExternalLinkView(link.Provider, link.ExternalId, link.Email, link.LinkedAt));
    }
}
