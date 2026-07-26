using System;
using System.Net;
using System.Threading.Tasks;
using Authentication;
using Flow.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.BO;
using Users.Entities.DbModels.Teams;
using Users.Entities.Dto.Teams;

namespace Users.Api.Controllers;

[Route("v1/teams")]
[Auth(Roles = "admin")]
public sealed class TeamsAdminObsoleteController(TeamsService teamsService) : ApiController
{
    // /// <summary>
    // /// [ACTUAL] [NOT_TESTED] Добавить команду
    // /// </summary>
    // /// <param name="request"></param>
    // /// <returns></returns>
    // [HttpPost]
    // [ProducesResponseType(typeof(TeamDbModel), (int)HttpStatusCode.OK)]
    // public async Task<IActionResult> CreateTeamAsync([FromBody] CreateTeamDto request)
    // {
    //     var user = User.GetIdentity();
    //     if (user.WorkspaceId is null)
    //     {
    //         return BadRequest("User not in organization");
    //     }

    //     return Ok(await teamsService.CreateTeamAsync(user.WorkspaceId!.Value, request.Name));
    // }

    // /// <summary>
    // /// Обновить команду
    // /// </summary>
    // /// <param name="teamId"></param>
    // /// <param name="request"></param>
    // /// <returns></returns>
    // [HttpPatch("{teamId}")]
    // [ProducesResponseType(typeof(TeamDbModel), (int)HttpStatusCode.OK)]
    // public Task<IActionResult> UpdateTeam(int teamId, [FromBody] UpdateTeamDto request)
    // {
    //     var user = User.GetIdentity();
    //     return teamsService.UpdateTeamAsync(user, teamId, request.Name).AsActionResultAsync();
    // }

    // /// <summary>
    // /// Удалить команду
    // /// </summary>
    // /// <param name="teamId"></param>
    // /// <returns></returns>
    // [HttpDelete("{teamId}")]
    // [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
    // public Task<IActionResult> DeleteTeam(int teamId)
    // {
    //     var user = User.GetIdentity();
    //     return teamsService.DeleteTeamAsync(user, teamId).AsActionResultAsync();
    // }

    // /// <summary>
    // /// Сгенерировать инвайт для вступления в команду
    // /// </summary>
    // /// <param name="teamId"></param>
    // /// <returns></returns>
    // [HttpPost("{teamId}/invites")]
    // [ProducesResponseType(typeof(TeamInviteDbModel), (int)HttpStatusCode.OK)]
    // public Task<IActionResult> GenerateInvite(int teamId)
    // {
    //     var user = User.GetIdentity();
    //     return teamsService.GenerateInviteLinkAsync(user, teamId).AsActionResultAsync();
    // }

    // /// <summary>
    // /// Удалить пользователя из команды
    // /// </summary>
    // /// <param name="fromTeamId"></param>
    // /// <param name="userId"></param>
    // /// <returns></returns>
    // [HttpDelete("{teamId}/users/{userId}")]
    // [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
    // public Task<IActionResult> RemoveTeamMember(int fromTeamId, long userId)
    // {
    //     var user = User.GetIdentity();
    //     return teamsService.RemoveTeamMemberAsync(user, fromTeamId, userId).AsActionResultAsync();
    // }

    // /// <summary>
    // /// Получить инвайты в команду
    // /// </summary>
    // /// <param name="teamId"></param>
    // /// <returns></returns>
    // [HttpGet("{teamId}/invites")]
    // [ProducesResponseType(typeof(TeamInviteDbModel[]), (int)HttpStatusCode.OK)]
    // public Task<IActionResult> GetTeamInvites(int teamId)
    // {
    //     var user = User.GetIdentity();
    //     return teamsService.GetTeamInvitesAsync(user, teamId).AsActionResultAsync();
    // }

    // /// <summary>
    // /// Удалить инвайт в команду
    // /// </summary>
    // /// <param name="teamId"></param>
    // /// <param name="token"></param>
    // /// <returns></returns>
    // [HttpDelete("{teamId}/invites/{token}")]
    // [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
    // public Task<IActionResult> DeleteTeamInvite(int teamId, Guid token)
    // {
    //     var user = User.GetIdentity();
    //     return teamsService.DeleteTeamInviteAsync(user, token, teamId).AsActionResultAsync(); 
    // }
}
