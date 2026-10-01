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
/// v2 API аналитики: маршруты и <c>[Authorize]</c> — из <see cref="AnalyticsControllerBase"/>, здесь только маппинг
/// UserIdentity → <see cref="IAnalyticsService"/> → Contracts. Detail по репорту живёт в <c>/v2/reports/{id}/analytics</c>.
/// </summary>
[ApiController]
public sealed class AnalyticsController(IAnalyticsService analyticsService) : AnalyticsControllerBase
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
    /// Причина отказа собирается из публичного списка допустимых значений, а не из текста исключения:
    /// тот — внутренняя деталь и отражает обратно присланное клиентом значение.
    /// </summary>
    private ObjectResult InvalidPeriod() =>
        HttpProblemDetailsFactory.Create(
            HttpContext,
            ProblemDescriptors.InvalidPeriod,
            $"Допустимые значения: {string.Join(", ", PeriodResolver.AllowedValues)}.");
}
