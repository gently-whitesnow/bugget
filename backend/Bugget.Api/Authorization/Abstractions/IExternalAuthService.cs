using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bugget.Api.Authorization.Abstractions;

public interface IExternalAuthService
{
    /// <summary>
    /// Authorizes an external user and optionally links the external identity.
    /// </summary>
    /// <remarks>
    /// Obsolete migration note:
    /// this contract still allows <paramref name="provider"/> to be null. That legacy path writes only
    /// <c>users.external_id</c> and does not create a row in <c>user_external_links</c>, which breaks
    /// new users in provider-based auth flows.
    ///
    /// Next steps:
    /// 1. Move every caller to an explicit provider (<c>oidc</c>, <c>telegram</c>, <c>google</c>,
    ///    <c>yandex</c>, <c>fake</c>, etc.).
    /// 2. Split or replace this method with a provider-required API.
    /// 3. Remove the provider-null branch from the implementation.
    /// 4. Stop using <c>users.external_id</c> for auth lookup and remove the column after backfill.
    /// </remarks>
    [Obsolete("This contract still allows provider=null. Migrate callers to a provider-required API backed by user_external_links.")]
    public Task AuthorizeAsync(
        HttpContext context,
        IExternalUser externalUser,
        bool useExternalTokens = false,
        string? provider = null,
        string? email = null);

    /// <summary>
    /// Привязать внешний аккаунт к текущему пользователю (из access_token cookie).
    /// Возвращает (success, errorCode, conflictOwnerId).
    /// errorCode: null | "not_authenticated" | "external_id_taken".
    /// conflictOwnerId: ID пользователя-владельца при конфликте external_id.
    /// </summary>
    public Task<(bool Success, string? ErrorCode, string? ConflictOwnerId)> LinkAccountAsync(
        HttpContext context, string provider, string externalId, string? email);
}
