using Bugget.Api.Extensions;
using Bugget.Api.Generated.Analytics;
using Bugget.Api.Http;
using Bugget.Api.Mappers;
using Bugget.Application.Services.Analytics;
using Bugget.Contracts.Analytics.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;
using HttpProblemDetailsFactory = Bugget.Api.Http.ProblemDetailsFactory;

namespace Bugget.Api.Controllers;

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
            return InvalidPeriod();
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
            return InvalidPeriod();
        }
    }

    /// <summary>
    /// Причина отказа собирается из публичного списка допустимых значений, а не из
    /// текста исключения: сообщение исключения — внутренняя деталь, и вдобавок оно
    /// отражает обратно присланное клиентом значение. Наружу уходит только то, что
    /// и так записано в контракте.
    /// </summary>
    private ActionResult InvalidPeriod() =>
        HttpProblemDetailsFactory.Create(
            HttpContext,
            ProblemDescriptors.InvalidPeriod,
            $"Допустимые значения: {string.Join(", ", PeriodResolver.AllowedValues)}.");
}
