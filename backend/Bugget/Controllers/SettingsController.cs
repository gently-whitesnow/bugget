using Bugget.Authentication;
using Bugget.BO.Errors;
using Bugget.BO.Services;
using Bugget.Entities.Authentication;
using Bugget.Entities.Views.Settings;
using Bugget.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monade.Errors;

namespace Bugget.Controllers;

/// <summary>
/// Api для управления пользовательскими, командными и рабочими настройками.
/// </summary>
public sealed class SettingsController(SettingsService settingsService) : ApiController
{
    /// <summary>
    /// Обновить настройку рабочего пространства
    /// </summary>
    /// <param name="sectionId"></param>
    /// <param name="settingId"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    [HttpPut("v1/workspace-settings-sections/{sectionId}/settings/{settingId}")]
    [ProducesResponseType(typeof(WorkspaceSettingView), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(NotFoundError), 404)]
    // [Authorize(Policy = AuthPolicies.RequireOrganizationIdHeader)] TODO когда в self hosted пропадут костыли авторизации
    public async Task<IActionResult> UpdateWorkspaceSettingAsync(
        [FromRoute] string sectionId,
        [FromRoute] string settingId,
        [FromBody] string[] values)
    {
        var user = User.GetIdentity();

        if (user.OrganizationId is null)
        {
            return BadRequest(BoErrors.OrganizationIdRequired);
        }

        return await settingsService.UpdateWorkspaceSettingAsync(user.OrganizationId, sectionId, settingId, values).AsActionResultAsync();
    }

    /// <summary>
    /// Обновить настройку команды
    /// </summary>
    /// <param name="sectionId"></param>
    /// <param name="settingId"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    [HttpPut("v1/team-settings-sections/{sectionId}/settings/{settingId}")]
    [ProducesResponseType(typeof(TeamSettingView), 200)]
    [ProducesResponseType(typeof(NotFoundError), 404)]
    // [Authorize(Policy = AuthPolicies.RequireTeamIdHeader)] TODO когда в self hosted пропадут костыли авторизации 
    public async Task<IActionResult> UpdateTeamSettingAsync(
        [FromRoute] string sectionId,
        [FromRoute] string settingId,
        [FromBody] string[] values)
    {
        var user = User.GetIdentity();

        if (user.TeamId is null)
        {
            return BadRequest(BoErrors.TeamIdRequired);
        }

        return await settingsService.UpdateTeamSettingAsync(user.TeamId, sectionId, settingId, values).AsActionResultAsync();
    }

    /// <summary>
    /// Обновить настройку пользователя
    /// </summary>
    /// <param name="sectionId"></param>
    /// <param name="settingId"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    [HttpPut("v1/user-settings-sections/{sectionId}/settings/{settingId}")]
    [ProducesResponseType(typeof(UserSettingView), 200)]
    [ProducesResponseType(typeof(NotFoundError), 404)]
    // [Authorize(Policy = AuthPolicies.RequireUserIdHeader)] TODO когда в self hosted пропадут костыли авторизации
    public Task<IActionResult> UpdateUserSettingAsync(
        [FromRoute] string sectionId,
        [FromRoute] string settingId,
        [FromBody] string[] values)
    {
        var user = User.GetIdentity();

        return settingsService.UpdateUserSettingAsync(user.Id, sectionId, settingId, values).AsActionResultAsync();
    }

    /// <summary>
    /// Получить все секции настроек
    /// </summary>
    /// <returns></returns>
    [HttpGet("v1/settings-sections")]
    [ProducesResponseType(typeof(SettingsSectionsView), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    // [Authorize(Policy = AuthPolicies.RequireUserIdHeader)] TODO когда в self hosted пропадут костыли авторизации
    public async Task<IActionResult> GetSettingsSectionsAsync()
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

        return Ok(await settingsService.GetSettingsSectionsAsync(user.OrganizationId, user.TeamId, user.Id));
    }
}
