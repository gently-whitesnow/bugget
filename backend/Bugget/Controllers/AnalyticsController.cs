using Bugget.Analytics.Contracts.Generated;
using Bugget.Api.Generated.Analytics;
using Bugget.BO.Services.Analytics;
using Bugget.Entities.Authentication;
using Bugget.Extensions;
using Bugget.Http;
using Bugget.Mappers;
using Microsoft.AspNetCore.Mvc;
using HttpProblemDetailsFactory = Bugget.Http.ProblemDetailsFactory;

namespace Bugget.Controllers;

/// <summary>
/// v2 API для аналитики. Наследует <see cref="AnalyticsControllerBase"/> —
/// маршруты/HTTP-методы и <c>[Authorize]</c> приходят оттуда. Тонкий маппер
/// UserIdentity → <see cref="AnalyticsService"/> → Contracts.
///
/// Контракт после R6:
///   * <c>GET /v2/analytics/summary?period=...&amp;teamId=...</c> — единый summary;
///     <c>teamId</c> опциональный (фильтр <c>creator_team_id</c>).
///   * <c>GET /v2/analytics/responsible/{userId}?period=...</c> — отдельный
///     shape <c>AnalyticsResponsible</c>.
/// Detail-эндпоинт <c>/v2/analytics/reports/{id}</c> переехал на sub-resource
/// <c>/v2/reports/{id}/analytics</c> (см. ReportsController).
/// </summary>
[ApiController]
public sealed class AnalyticsController(AnalyticsService analyticsService) : AnalyticsControllerBase
{
    public override async Task<ActionResult<AnalyticsSummary>> GetAnalyticsSummary(
        [FromQuery] string period,
        [FromQuery] string? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        if (string.IsNullOrEmpty(user.OrganizationId))
        {
            return Unauthorized();
        }

        try
        {
            var bo = await analyticsService.GetSummaryAsync(
                user.OrganizationId, period, teamId, cancellationToken);
            return Ok(bo.ToContract());
        }
        catch (ArgumentException ex) when (ex.ParamName == "period")
        {
            // PeriodResolver.Resolve бросает ArgumentException на невалидный period —
            // отдаём 400 c человекочитаемым сообщением (без stack-трейса).
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.InvalidPeriod, ex.Message);
        }
    }

    public override async Task<ActionResult<AnalyticsResponsible>> GetAnalyticsByResponsible(
        string userId,
        [FromQuery] string period,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        if (string.IsNullOrEmpty(user.OrganizationId))
        {
            return Unauthorized();
        }

        try
        {
            var bo = await analyticsService.GetByResponsibleAsync(
                user.OrganizationId, userId, period, cancellationToken);
            return Ok(bo.ToContract());
        }
        catch (ArgumentException ex) when (ex.ParamName == "period")
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.InvalidPeriod, ex.Message);
        }
    }
}
