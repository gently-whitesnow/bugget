using Bugget.BO.Services.ReportLinks;
using Bugget.Entities.Authentication;
using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.DTO.Link;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Reports;

/// <summary>
/// Api для работы со ссылками репорта
/// </summary>
[Route("/v2/reports/{aliasId}/links")]
public sealed class ReportLinksController(ReportLinksService reportLinksService) : ApiController
{
    /// <summary>
    /// Добавить ссылку к репорту
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReportLinkDbModel), 201)]
    public async Task<IActionResult> CreateReportLinkAsync([FromRoute] string aliasId, [FromBody] ReportLinkDto createDto)
    {
        var user = User.GetIdentity();
        return await reportLinksService.CreateReportLinkAsync(user, aliasId, createDto).AsActionResultAsync(201);
    }

    /// <summary>
    /// Обновить ссылку репорта
    /// </summary>
    [HttpPut("{linkId}")]
    [ProducesResponseType(typeof(ReportLinkDbModel), 200)]
    public Task<IActionResult> UpdateReportLinkAsync([FromRoute] string aliasId, [FromRoute] int linkId, [FromBody] ReportLinkDto updateDto)
    {
        var user = User.GetIdentity();
        return reportLinksService.UpdateReportLinkAsync(user, aliasId, linkId, updateDto).AsActionResultAsync();
    }

    /// <summary>
    /// Удалить ссылку репорта
    /// </summary>
    [HttpDelete("{linkId}")]
    [ProducesResponseType(200)]
    public Task<IActionResult> DeleteReportLinkAsync([FromRoute] string aliasId, [FromRoute] int linkId)
    {
        var user = User.GetIdentity();
        return reportLinksService.DeleteReportLinkAsync(user, aliasId, linkId).AsActionResultAsync();
    }
}
