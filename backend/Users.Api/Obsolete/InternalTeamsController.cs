// using System.Net;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Mvc;
// using Users.BO;
// using Users.Entities.DbModels.Teams;
// using Users.Entities.Dto.Teams;
// using System;

// namespace Users.Api.Controllers;

// [Route("_internal/teams/{teamId}/members")]
// public sealed class InternalTeamsController(TeamsService teamsService) : ApiController
// {
//     /// <summary>
//     /// Добавить пользователя в команду для сервиса авторизации
//     /// </summary>
//     /// <param name="teamId"></param>
//     /// <param name="addTeamMemberDto"></param>
//     /// <returns></returns>
//     [Obsolete("Не помню для чего")]
//     [HttpPost]
//     [ProducesResponseType(typeof(TeamDbModel), (int)HttpStatusCode.OK)]
//     public Task<TeamDbModel> AddTeamMemberAsync([FromRoute] int teamId, [FromBody] AddTeamMemberDto addTeamMemberDto)
//     {
//         return teamsService.AddTeamMemberAsync(teamId, addTeamMemberDto.UserId);
//     }
// }
