using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bugget.Api.Users.Authentication;

/// <summary>
/// Атрибут для проверки наличия WorkspaceId в UserIdentity
/// Возвращает 404, если WorkspaceId отсутствует
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class WorkspaceRequiredAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userIdentity = context.HttpContext.User.Identity;

        if (userIdentity?.IsAuthenticated != true)
        {
            context.Result = new NotFoundResult();
            return;
        }

        var workspaceIdClaim = context.HttpContext.User.FindFirst(ClaimKey.Workspace);

        if (workspaceIdClaim == null || !int.TryParse(workspaceIdClaim.Value, out var workspaceId) || workspaceId <= 0)
        {
            context.Result = new NotFoundResult();
            return;
        }
    }
}
