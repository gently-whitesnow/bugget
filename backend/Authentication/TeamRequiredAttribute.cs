using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authentication;

/// <summary>
/// Атрибут для проверки наличия TeamId в UserIdentity
/// Возвращает 404, если TeamId отсутствует
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class TeamRequiredAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userIdentity = context.HttpContext.User.Identity;

        if (userIdentity?.IsAuthenticated != true)
        {
            context.Result = new NotFoundResult();
            return;
        }

        var teamIdClaim = context.HttpContext.User.FindFirst(ClaimKey.Team);

        if (teamIdClaim == null || !int.TryParse(teamIdClaim.Value, out var teamId) || teamId <= 0)
        {
            context.Result = new NotFoundResult();
            return;
        }
    }
}
