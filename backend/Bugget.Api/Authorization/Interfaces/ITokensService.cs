using System.Security.Claims;
using System.Threading.Tasks;

namespace Bugget.Api.Authorization.Interfaces;

/// <summary>Генерация access и refresh токенов.</summary>
public interface ITokensService
{
    /// <summary>Новая пара токенов для пользователя.</summary>
    Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(long userId);

    /// <summary>Новая пара токенов по существующему refresh токену.</summary>
    Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(long userId, string refreshToken);

    /// <summary>Валидирует refresh токен и возвращает principal.</summary>
    Task<ClaimsPrincipal> ValidateRefreshTokenAsync(string token);
}
