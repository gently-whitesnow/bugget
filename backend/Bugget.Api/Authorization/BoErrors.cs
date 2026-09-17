using Bugget.Domain.Errors;

namespace Bugget.Api.Authorization;

public static class BoErrors
{
    public static readonly NotFoundError UserNotFound = new NotFoundError("user_not_found", "User not found");
    public static readonly UnauthorizedError ExpiredRefreshToken = new UnauthorizedError("expired_refresh_token", "Expired refresh token");
    public static readonly UnauthorizedError InvalidRefreshToken = new UnauthorizedError("invalid_refresh_token", "Invalid refresh token");
    public static readonly UnauthorizedError InvalidAccessToken = new UnauthorizedError("invalid_access_token", "Invalid access token");
    public static readonly UnauthorizedError ExpiredAccessToken = new UnauthorizedError("expired_access_token", "Expired access token");
    public static readonly UnauthorizedError InvalidToken = new UnauthorizedError("invalid_token", "Invalid token");
    public static readonly UnauthorizedError UserNotActive = new UnauthorizedError("user_not_active", "User not active");

}
