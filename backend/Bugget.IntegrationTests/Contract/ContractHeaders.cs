namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Имена заголовков identity — те же, что nginx проставляет из ответа
/// <c>/_internal/auth</c> (deploy/nginx/snippets/includes/auth-proxy-response.conf)
/// и что читает бекенд в боевом контуре
/// (deploy/external-settings/bugget-api/external_settings.json).
/// Переименование любого из них ломает авторизацию целиком.
/// </summary>
internal static class ContractHeaders
{
    public const string UserId = "Auth-Request-User-Id";
    public const string TeamId = "Auth-Request-Team-Id";
    public const string WorkspaceId = "Auth-Request-Workspace-Id";
    public const string WorkspaceRole = "Auth-Request-Workspace-Role";

    /// <summary>
    /// Шаблон маршрута, по которому запрос был обслужен. В боевом контуре такого
    /// заголовка нет: его проставляет только тестовый хост
    /// (<see cref="MatchedRouteStartupFilter"/>), чтобы снимок контракта писал шаблон
    /// (<c>/v2/reports/{aliasId}</c>), а не конкретный путь с идентификаторами сида.
    /// </summary>
    public const string MatchedRoute = "X-Contract-Matched-Route";
}
