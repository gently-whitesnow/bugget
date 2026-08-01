using System.Security.Claims;
using System.Threading.Tasks;

namespace Bugget.Api.Authorization.Interfaces;

/// <summary>
/// Предоставляет методы для генерации access и refresh токенов.
/// </summary>
public interface ITokensService
{
    /// <summary>
    /// Генерирует новый access токен и refresh токен для указанного идентификатора пользователя.
    /// </summary>
    public Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(long userId);

    /// <summary>
    /// Генерирует новый access токен и refresh токен для указанного идентификатора пользователя, используя существующий refresh токен.
    /// </summary>
    public Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(long userId, string refreshToken);

    /// <summary>
    /// Валидирует refresh токен и возвращает principal.
    /// </summary>
    public Task<ClaimsPrincipal> ValidateRefreshTokenAsync(string token);
}
