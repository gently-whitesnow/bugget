using Users.Entities.Errors;

namespace Authorization.Api;

public static class BoErrors
{
    public static NotFoundError UserNotFound = new NotFoundError("user_not_found", "User not found");
    public static UnauthorizedError ExpiredRefreshToken = new UnauthorizedError("expired_refresh_token", "Expired refresh token");
    public static UnauthorizedError InvalidRefreshToken = new UnauthorizedError("invalid_refresh_token", "Invalid refresh token");
    public static UnauthorizedError InvalidAccessToken = new UnauthorizedError("invalid_access_token", "Invalid access token");
    public static UnauthorizedError ExpiredAccessToken = new UnauthorizedError("expired_access_token", "Expired access token");
    public static UnauthorizedError InvalidToken = new UnauthorizedError("invalid_token", "Invalid token");
    public static UnauthorizedError UserNotActive = new UnauthorizedError("user_not_active", "User not active");

}
