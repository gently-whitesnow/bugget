using Bugget.Api.Contracts.Settings.Generated;
using Bugget.Api.Generated.Settings;
using Bugget.BO.Errors;
using Bugget.BO.Services;
using Bugget.Entities.Authentication;
using Bugget.Extensions;
using Bugget.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers;

/// <summary>
/// Api для управления пользовательскими, командными и рабочими настройками.
/// Маршруты и формы приходят из <c>specs/contracts/settings/openapi.yaml</c>
/// через <see cref="SettingsControllerBase"/>.
/// </summary>
/// <remarks>
/// Авторизация по политикам (RequireOrganizationIdHeader и соседи) здесь
/// намеренно не навешана — так было и до contract-first: в self-hosted-контуре
/// заголовки приходят не всегда. Уровень доступа проверяется по identity в теле
/// метода.
/// </remarks>
[ApiController]
public sealed class SettingsController(SettingsService settingsService) : SettingsControllerBase
{
    public override async Task<ActionResult<SettingsSections>> GetSettingsSections(
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BoErrors.OrganizationIdRequired.ToProblemDetails();
        }

        if (user.TeamId is null)
        {
            return BoErrors.TeamIdRequired.ToProblemDetails();
        }

        var sections = await settingsService.GetSettingsSectionsAsync(user.OrganizationId, user.TeamId, user.Id);
        return Ok(sections.ToContract());
    }

    public override async Task<ActionResult<Setting>> UpdateWorkspaceSetting(
        string sectionId,
        string settingId,
        IEnumerable<string> body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BoErrors.OrganizationIdRequired.ToProblemDetails();
        }

        return await settingsService
            .UpdateWorkspaceSettingAsync(user.OrganizationId, sectionId, settingId, [.. body])
            .AsContractResultAsync(view => view.ToContract());
    }

    public override async Task<ActionResult<Setting>> UpdateTeamSetting(
        string sectionId,
        string settingId,
        IEnumerable<string> body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        if (user.TeamId is null)
        {
            return BoErrors.TeamIdRequired.ToProblemDetails();
        }

        return await settingsService
            .UpdateTeamSettingAsync(user.TeamId, sectionId, settingId, [.. body])
            .AsContractResultAsync(view => view.ToContract());
    }

    public override Task<ActionResult<Setting>> UpdateUserSetting(
        string sectionId,
        string settingId,
        IEnumerable<string> body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        return settingsService
            .UpdateUserSettingAsync(user.Id, sectionId, settingId, [.. body])
            .AsContractResultAsync(view => view.ToContract());
    }
}
