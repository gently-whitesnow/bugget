using System.ComponentModel.DataAnnotations;
using Bugget.Api.Extensions;
using Bugget.Api.Generated.External;
using Bugget.Api.Mappers;
using Bugget.Application.Errors;
using Bugget.Application.Services.External;
using Bugget.Contracts.External.Generated;
using Bugget.Domain.Authentication;
using Bugget.Infrastructure.ExternalClients.Kaiten;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Controllers;

/// <summary>
/// Api для работы с внешними источниками. Маршруты и формы приходят из
/// <c>specs/contracts/external/openapi.yaml</c> через <see cref="ExternalControllerBase"/>.
/// </summary>
[ApiController]
public sealed class ExternalController(
    ExternalSearchService externalSearchService,
    KaitenBoardsService kaitenBoardsService) : ExternalControllerBase
{
    public override async Task<ActionResult<ExternalSearchResult>> SearchExternal(
        string? query = null,
        // Неотрицательность skip/take генератор в атрибуты не переносит: до
        // contract-first параметры были uint, и отрицательное значение отсекалось
        // связыванием.
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(0, int.MaxValue)] int? take = 10,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BoErrors.OrganizationIdRequired.ToProblemDetails(HttpContext);
        }

        if (user.TeamId is null)
        {
            return BoErrors.TeamIdRequired.ToProblemDetails(HttpContext);
        }

        var searchResult = await externalSearchService.SearchAsync(
            user.OrganizationId,
            user.TeamId,
            query,
            (uint)(skip ?? 0),
            (uint)(take ?? 10));

        return Ok(searchResult.ToContract());
    }

    public override async Task<IActionResult> ApplyExternalSearchResult(
        ExternalSearchApplyRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BoErrors.OrganizationIdRequired.ToProblemDetails(HttpContext);
        }

        return await externalSearchService.ApplySearchResultAsync(
            user,
            user.OrganizationId,
            user.TeamId!,
            body.Id,
            body.Source,
            body.Report_id).AsActionResultAsync(HttpContext);
    }

    public override async Task<ActionResult<ICollection<KaitenBoard>>> ListKaitenBoards(
        string? query = null,
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(0, int.MaxValue)] int? take = 10,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BoErrors.OrganizationIdRequired.ToProblemDetails(HttpContext);
        }

        var boards = await kaitenBoardsService.GetBoardsAsync(
            user.OrganizationId,
            query,
            (uint)(skip ?? 0),
            (uint)(take ?? 10));

        return Ok(boards.ToContract());
    }

    public override async Task<ActionResult<ICollection<KaitenBoard>>> BatchGetKaitenBoards(
        KaitenBoardsBatchGetRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BoErrors.OrganizationIdRequired.ToProblemDetails(HttpContext);
        }

        var boards = await kaitenBoardsService.BatchGetBoardsAsync(user.OrganizationId, [.. body.Ids]);
        return Ok(boards.ToContract());
    }
}
