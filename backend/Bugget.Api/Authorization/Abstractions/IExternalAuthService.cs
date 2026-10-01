using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bugget.Api.Authorization.Abstractions;

public interface IExternalAuthService
{
    /// <summary>
    /// Долг: provider=null — легаси-путь, пишет только users.external_id без строки в
    /// user_external_links. Цель — provider-required API; [Obsolete] снят, потому что все
    /// вызывающие всё равно глушили CS0618 точечно.
    /// </summary>
    Task AuthorizeAsync(
        HttpContext context,
        IExternalUser externalUser,
        bool useExternalTokens = false,
        string? provider = null,
        string? email = null);

    /// <summary>
    /// Привязать внешний аккаунт к текущему пользователю (из access_token cookie).
    /// errorCode: null | "not_authenticated" | "external_id_taken"; conflictOwnerId — владелец при конфликте external_id.
    /// </summary>
    Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> LinkAccountAsync(
        HttpContext context, string provider, string externalId, string? email);
}
