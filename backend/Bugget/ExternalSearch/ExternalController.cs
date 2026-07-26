using System.ComponentModel.DataAnnotations;
using Bugget.BO.Errors;
using Bugget.BO.ExternalSearch.Models;
using Bugget.BO.Services.External;
using Bugget.Entities.Authentication;
using Bugget.Extensions;
using Bugget.ExternalClients.Kaiten;
using Bugget.ExternalClients.Kaiten.Models;
using Bugget.ExternalSearch;
using Microsoft.AspNetCore.Mvc;
using Monade.Errors;

namespace Bugget.Controllers;

/// <summary>
/// Api для работы с внешними источниками
/// </summary>
[Route("/v1/external")]
public sealed class ExternalController(
    ExternalSearchService externalSearchService,
    KaitenBoardsService kaitenBoardsService) : ApiController
{
    /// <summary>
    /// Поиск по внешним источникам
    /// </summary>
    /// <returns></returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ExternalSearchView), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    public async Task<IActionResult> SearchReportsAsync(
        [FromQuery] string? query,
        [FromQuery] uint skip = 0,
        [FromQuery] uint take = 10
        )
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BadRequest(BoErrors.OrganizationIdRequired);
        }

        if (user.TeamId is null)
        {
            return BadRequest(BoErrors.TeamIdRequired);
        }

        var searchResult = await externalSearchService.SearchAsync(user.OrganizationId, user.TeamId, query, skip, take);
        return Ok(new ExternalSearchView
        {
            Total = searchResult.Total,
            Items = searchResult.Items.Select(item => new ExternalSearchItemView
            {
                Id = item.Id,
                Text = item.Text,
                Source = item.Source
            }).ToArray()
        });
    }

    /// <summary>
    /// Применение поискового результата
    /// </summary>
    /// <returns></returns>
    [HttpPost("search/apply")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    public async Task<IActionResult> ApplySearchResultAsync(
        [FromBody] ExternalSearchApplyDto searchApply)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BadRequest(BoErrors.OrganizationIdRequired);
        }

        return await externalSearchService.ApplySearchResultAsync(
            user,
            user.OrganizationId,
            user.TeamId!,
            searchApply.Id,
            searchApply.Source,
            searchApply.ReportId).AsActionResultAsync();
    }

    /// <summary>
    /// Получить список досок Kaiten для автокомплита
    /// </summary>
    [HttpGet("kaiten/boards")]
    [ProducesResponseType(typeof(StoredBoard[]), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    public async Task<IActionResult> GetKaitenBoardsAsync([FromQuery] string? query = null, [FromQuery] uint skip = 0, [FromQuery] uint take = 10)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BadRequest(BoErrors.OrganizationIdRequired);
        }

        return Ok(await kaitenBoardsService.GetBoardsAsync(user.OrganizationId, query, skip, take));
    }

    /// <summary>
    /// Получить список досок Kaiten по идентификаторам
    /// </summary>
    [HttpPost("kaiten/boards/batch-get")]
    [ProducesResponseType(typeof(StoredBoard[]), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    public async Task<IActionResult> BatchGetKaitenBoardsAsync([FromBody][Required] BatchGetBoardsDto dto)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BadRequest(BoErrors.OrganizationIdRequired);
        }

        return Ok(await kaitenBoardsService.BatchGetBoardsAsync(user.OrganizationId, dto.Ids));
    }
}
