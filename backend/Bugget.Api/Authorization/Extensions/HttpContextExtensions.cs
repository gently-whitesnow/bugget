using System;
using System.Linq;
using Bugget.Api.Authorization.Authentication;
using Bugget.Api.Authorization.Models;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Bugget.Api.Authorization.Extensions;

public static class HttpContextExtensions
{
    public const string AuthMethodHeaderName = "Auth-Request-Auth-Method";

    public static bool IsDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static string BuildCookieHeader(string name, string value, TimeSpan lifetime)
    {
        var cookie = new SetCookieHeaderValue(name, value)
        {
            Secure = !IsDevelopment,
            HttpOnly = true,
            // Strict убрал, так как не работают прямые переходы по ссылке с других сайтов/приложений
            SameSite = Microsoft.Net.Http.Headers.SameSiteMode.Lax,
            Path = "/",
            MaxAge = lifetime,
            Expires = DateTimeOffset.UtcNow.Add(lifetime)
        };
        return cookie.ToString();
    }

    public static void SetJsonWebTokensCookie(
            this HttpContext ctx, string access, string refresh, TimeSpan accessLifetime, TimeSpan refreshLifetime)
    {
        var accessCookie = BuildCookieHeader("access_token", access, accessLifetime);
        var refreshCookie = BuildCookieHeader("refresh_token", refresh, refreshLifetime);

        ctx.Response.Headers.Append("Set-Cookie", accessCookie);
        ctx.Response.Headers.Append("Set-Cookie", refreshCookie);
    }

    public static void SetAuthHeaders(this HttpContext ctx, UserContext user, string authMethod = AuthMethods.Jwt)
    {
        ctx.Response.Headers["Auth-Request-User-Id"] = user.User.Id.ToString();
        ctx.Response.Headers[AuthMethodHeaderName] = authMethod;

        var logger = ctx.RequestServices?.GetService<ILogger<HttpContext>>();

        var origUri = ctx.Request.Headers["X-Original-URI"].FirstOrDefault();
        logger?.LogInformation("X-Original-URI: {Uri}", origUri);

        var (wid, tid) = OriginalUriScope.ParseOptional(origUri);

        logger?.LogInformation("Extracted from URI: wid={Wid}, tid={Tid}", wid, tid);

        // Логирование для отладки
        logger?.LogInformation("SetAuthHeaders: UserId={UserId}, WorkspaceId={WorkspaceId}, TeamId={TeamId}, WorkspacesCount={WorkspacesCount}",
            user.User.Id, wid, tid, user.Workspaces?.Length ?? 0);

        // Пытаемся найти workspace по ID из URL
        if (wid != null && user.Workspaces != null)
        {
            var workspace = user.Workspaces.FirstOrDefault(w => w.WorkspaceId == wid);
            if (workspace != null)
            {
                logger?.LogInformation("SetAuthHeaders: Found workspace {WorkspaceId} with role {Role}", wid, workspace.Role);
                ctx.Response.Headers["Auth-Request-Workspace-Id"] = wid.ToString();
                ctx.Response.Headers["Auth-Request-Workspace-Role"] = workspace.Role;

                // Если указан team ID, проверяем, что команда принадлежит этому workspace
                if (tid != null)
                {
                    // Проверяем, что команда существует в этом workspace
                    if (workspace.TeamIds.Contains(tid.Value))
                    {
                        ctx.Response.Headers["Auth-Request-Team-Id"] = tid.ToString();
                        logger?.LogInformation("SetAuthHeaders: Set team ID {TeamId}", tid);
                    }
                    else
                    {
                        logger?.LogWarning("SetAuthHeaders: Team {TeamId} not found in workspace {WorkspaceId} teams: {TeamIds}",
                            tid, wid, string.Join(",", workspace.TeamIds));
                    }
                    // Если команда не принадлежит workspace - не устанавливаем заголовок
                    // Это приведет к 403 на уровне бизнес-логики
                }
            }
            else
            {
                logger?.LogWarning("SetAuthHeaders: User {UserId} not found in workspace {WorkspaceId}. Available workspaces: {Workspaces}",
                    user.User.Id, wid, string.Join(",", user.Workspaces.Select(w => $"{w.WorkspaceId}:{w.Role}")));
            }
            // Если пользователь не является членом workspace - не устанавливаем заголовки
            // Это приведет к 403
        }
        else
        {
            logger?.LogWarning("SetAuthHeaders: Invalid workspace ID or no workspaces. WorkspaceId={WorkspaceId}, Workspaces={Workspaces}",
                wid, user.Workspaces?.Length ?? 0);
        }
    }
}
