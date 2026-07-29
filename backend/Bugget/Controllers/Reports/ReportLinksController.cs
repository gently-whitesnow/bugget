using Bugget.Api.Generated.Reports;
using Bugget.BO.Services.ReportLinks;
using Bugget.Entities.Authentication;
using Bugget.Entities.DTO.Link;
using Bugget.Extensions;
using Bugget.Mappers;
using Bugget.Reports.Contracts.Generated;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Reports;

/// <summary>
/// Api для работы со ссылками репорта. Маршруты и формы приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="ReportLinksControllerBase"/>.
/// </summary>
[ApiController]
public sealed class ReportLinksController(ReportLinksService reportLinksService) : ReportLinksControllerBase
{
    public override Task<ActionResult<ReportLink>> CreateReportLink(
        string aliasId,
        ReportLinkRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return reportLinksService.CreateReportLinkAsync(user, aliasId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToContract(), 201);
    }

    public override Task<ActionResult<ReportLink>> UpdateReportLink(
        string aliasId,
        int linkId,
        ReportLinkRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return reportLinksService.UpdateReportLinkAsync(user, aliasId, linkId, ToDto(body))
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToContract());
    }

    public override Task<IActionResult> DeleteReportLink(
        string aliasId,
        int linkId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return reportLinksService.DeleteReportLinkAsync(user, aliasId, linkId).AsActionResultAsync(HttpContext);
    }

    private static ReportLinkDto ToDto(ReportLinkRequest body) => new()
    {
        Link = body.Link,
        Name = body.Name
    };
}
