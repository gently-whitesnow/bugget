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
}
