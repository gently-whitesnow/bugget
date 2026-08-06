namespace Bugget.Domain.Authentication;

public static class AuthClaims
{
    public const string UserId = "user_id";
    public const string UserIdHeaderConfigured = "user_id_header_configured";
    public const string TeamId = "team_id";
    public const string TeamIdHeaderConfigured = "team_id_header_configured";
    public const string OrganizationId = "organization_id";
    public const string OrganizationIdHeaderConfigured = "organization_id_header_configured";
    public const string SignalRConnectionId = "signalr_connection_id";

    /// <summary>
    /// Способ входа: <see cref="AuthMethods.Pat"/> или <see cref="AuthMethods.Jwt"/>.
    /// Отсутствие claim'а трактуется как интерактивная (браузерная) сессия.
    /// </summary>
    public const string AuthMethod = "auth_method";
}
