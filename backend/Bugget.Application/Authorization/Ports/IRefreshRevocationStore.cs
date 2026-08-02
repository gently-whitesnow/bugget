using System;
using System.Threading.Tasks;

namespace Bugget.Application.Authorization.Ports;

public interface IRefreshRevocationStore
{
    /// <summary>
    /// Отозван ли токен на текущий момент времени.
    /// </summary>
    Task<bool> IsRevokedAsync(string jti);

    /// <summary>
    /// Помечает токен отозванным включительно до <paramref name="revokedUntil"/> —
    /// границы, до которой его ещё принимает lifetime-валидатор
    /// (см. <see cref="RefreshTokenRevocation.RevokedUntil"/>). Дальше этой границы
    /// запись держать не нужно: токен уже отклоняется по времени жизни.
    /// </summary>
    Task RevokeAsync(string jti, DateTimeOffset revokedUntil);
}
