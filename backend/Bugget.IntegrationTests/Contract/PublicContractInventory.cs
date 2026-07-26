namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Инвентарь публичной поверхности: по строке на каждый маршрут приложения — кто его
/// зовёт и чем он покрыт. Список сверяется с таблицей маршрутов живого хоста
/// (<see cref="PublicSurface"/>), поэтому новый эндпоинт нельзя добавить молча:
/// тест упадёт, пока путь не появится здесь с решением по покрытию.
/// </summary>
internal static class PublicContractInventory
{
    public const string Frontend = "фронт";
    public const string Nginx = "nginx";
    public const string Internal = "внутренний";

    /// <summary>Маршрут → кто зовёт, покрытие, причина.</summary>
    public static readonly IReadOnlyDictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal)
    {
        // --- reports ---
        ["POST /v2/reports"] = new(Frontend, "ReportsContractTests"),
        ["GET /v2/reports"] = new(Frontend, "ReportsContractTests"),
        ["GET /v2/reports/{aliasId}"] = new(Frontend, "ReportsContractTests"),
        ["PATCH /v2/reports/{aliasId}"] = new(Frontend, "ReportsContractTests"),
        ["GET /v2/reports/legacy/{legacyId:int}"] = new(Frontend, "ReportsContractTests"),
        ["POST /v2/reports/counts:batch"] = new(Frontend, "ReportsContractTests"),
        ["GET /v2/reports/{id:long}/analytics"] = new(Frontend, "AnalyticsContractTests"),

        // --- bugs и шаги ---
        ["POST /v2/reports/{aliasId}/bugs"] = new(Frontend, "BugsContractTests"),
        ["PATCH /v2/reports/{aliasId}/bugs/{bugId}"] = new(Frontend, "BugsContractTests"),
        ["POST /v2/reports/{aliasId}/bugs/{bugId}/steps"] = new(Frontend, "BugsContractTests"),
        ["PATCH /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}"] = new(Frontend, "BugsContractTests"),
        ["DELETE /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}"] = new(Frontend, "BugsContractTests"),
        ["PUT /v2/reports/{aliasId}/bugs/{bugId}/steps/order"] = new(Frontend, "BugsContractTests"),

        // --- комментарии и ссылки ---
        ["POST /v2/reports/{aliasId}/bugs/{bugId}/comments"] = new(Frontend, "CommentsAndLinksContractTests"),
        ["PUT /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}"] = new(Frontend, "CommentsAndLinksContractTests"),
        ["DELETE /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}"] = new(Frontend, "CommentsAndLinksContractTests"),
        ["POST /v2/reports/{aliasId}/links"] = new(Frontend, "CommentsAndLinksContractTests"),
        ["PUT /v2/reports/{aliasId}/links/{linkId}"] = new(Frontend, "CommentsAndLinksContractTests"),
        ["DELETE /v2/reports/{aliasId}/links/{linkId}"] = new(Frontend, "CommentsAndLinksContractTests"),

        // --- вложения ---
        ["POST /v2/reports/{aliasId}/bugs/{bugId}/attachments"] = new(Frontend, "AttachmentsContractTests"),
        ["PATCH /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}"] = new(Frontend, "AttachmentsContractTests"),
        ["DELETE /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}"] = new(Frontend, "AttachmentsContractTests"),
        ["GET /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content"] = new(Frontend, "AttachmentsContractTests"),
        ["GET /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content/preview"] = new(Frontend, "AttachmentsContractTests"),
        ["POST /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments"] = new(Frontend, "AttachmentsContractTests"),
        ["PATCH /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}"] = new(Frontend, "AttachmentsContractTests"),
        ["DELETE /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}"] = new(Frontend, "AttachmentsContractTests"),
        ["GET /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content"] = new(Frontend, "AttachmentsContractTests"),
        ["GET /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content/preview"] = new(Frontend, "AttachmentsContractTests"),
        ["POST /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments"] = new(Frontend, "AttachmentsContractTests"),
        ["PATCH /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}"] = new(Frontend, "AttachmentsContractTests"),
        ["DELETE /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}"] = new(Frontend, "AttachmentsContractTests"),
        ["GET /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content"] = new(Frontend, "AttachmentsContractTests"),
        ["GET /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content/preview"] = new(Frontend, "AttachmentsContractTests"),

        // --- аналитика, поиск, внешние источники, настройки ---
        ["GET /v2/analytics/summary"] = new(Frontend, "AnalyticsContractTests"),
        ["GET /v2/analytics/responsible/{userId}"] = new(Frontend, "AnalyticsContractTests"),
        ["GET /v1/reports/search"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["GET /v1/external/search"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["POST /v1/external/search/apply"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["GET /v1/external/kaiten/boards"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["POST /v1/external/kaiten/boards/batch-get"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["GET /v1/settings-sections"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["PUT /v1/workspace-settings-sections/{sectionId}/settings/{settingId}"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["PUT /v1/team-settings-sections/{sectionId}/settings/{settingId}"] = new(Frontend, "SettingsAndSearchContractTests"),
        ["PUT /v1/user-settings-sections/{sectionId}/settings/{settingId}"] = new(Frontend, "SettingsAndSearchContractTests"),

        // --- SignalR ---
        ["* /v1/report-page-hub"] = new(Frontend, "ReportPageHubContractTests", "проверяется handshake; сам обмен сообщениями — вне контракта HTTP"),
        ["* /v1/report-page-hub/negotiate"] = new(Frontend, "ReportPageHubContractTests"),

        // --- users: рабочие пространства и команды ---
        ["POST /v1/workspaces"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["GET /v1/workspaces"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["PUT /v1/workspaces/{workspaceId}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["POST /v1/workspaces/{workspaceId}/members/join"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["PUT /v1/workspaces/{workspaceId}/teams/{teamId}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams/batch/list"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/autocomplete"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/members"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams/{teamId}/members/join"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/members"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/members/{userId}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams/{teamId}/invites"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/invites"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["PUT /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}"] = new(Frontend, "UsersWorkspacesContractTests"),
        ["POST /v1/invites/accept"] = new(Frontend, "UsersWorkspacesContractTests"),

        // --- users: профиль в контексте workspace/team (по этим путям ходит фронт) ---
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/users"] = new(Frontend, "UsersProfileContractTests"),
        ["PUT /v1/workspaces/{workspaceId}/teams/{teamId}/users"] = new(Frontend, "UsersProfileContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users"] = new(Frontend, "UsersProfileContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/batch/list"] = new(Frontend, "UsersProfileContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/autocomplete"] = new(Frontend, "UsersProfileContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar"] = new(Frontend, "UsersProfileContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar"] = new(Frontend, "UsersProfileContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar/content"] = new(Frontend, "UsersProfileContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/{userId:long}/avatar/content"] = new(Frontend, "UsersProfileContractTests"),
        ["GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links"] = new(Frontend, "UsersProfileContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links/{provider}"] = new(Frontend, "UsersProfileContractTests"),
        ["PUT /v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost"] = new(Frontend, "UsersProfileContractTests"),
        ["DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost"] = new(Frontend, "UsersProfileContractTests"),
        ["POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/merge"] = new(Frontend, "UsersProfileContractTests"),

        // --- users: те же действия без контекста в пути ---
        ["GET /v1/users"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант; фронт ходит с контекстом"),
        ["PUT /v1/users"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант; фронт ходит с контекстом"),
        ["DELETE /v1/users"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант; фронт ходит с контекстом"),
        ["POST /v1/users/batch/list"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["GET /v1/users/autocomplete"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["POST /v1/users/avatar"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["DELETE /v1/users/avatar"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["GET /v1/users/avatar/content"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["GET /v1/users/{userId:long}/avatar/content"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["GET /v1/users/external-links"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["DELETE /v1/users/external-links/{provider}"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["PUT /v1/users/mattermost"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["DELETE /v1/users/mattermost"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),
        ["POST /v1/users/merge"] = new(Frontend, Uncovered, "тот же контроллер, что и контекстный вариант"),

        // --- авторизация ---
        ["GET /_internal/auth"] = new(Nginx, "AuthorizationContractTests"),
        ["GET /_internal/anon/auth"] = new(Nginx, "AuthorizationContractTests"),
        ["GET /v1/auth"] = new(Frontend, "AuthorizationContractTests"),
        ["GET /v1/flags"] = new(Frontend, "AuthorizationContractTests"),
        ["POST /v1/logout"] = new(Frontend, "AuthorizationContractTests"),
        ["GET /v1/external/token/callback"] = new(Frontend, Uncovered, "OIDC-callback: нужен внешний провайдер, в тестовом хосте не поднимается"),
        ["GET /v1/fake/login"] = new(Frontend, Uncovered, "провайдер входа для локальной разработки; в тестовом хосте контроллер FakeAuth не попадает в application parts — см. итог MAIN-13"),
        ["GET /v1/users/mattermost/connect"] = new(Frontend, Uncovered, "OAuth Mattermost: нужен внешний провайдер"),
        ["GET /v1/users/mattermost/callback"] = new(Frontend, Uncovered, "OAuth Mattermost: нужен внешний провайдер"),
        ["GET /_internal/admin"] = new(Nginx, Uncovered, "админский auth_request; в текущем nginx-конфиге не используется"),
        ["GET /v1/admin/authenticate"] = new(Internal, Uncovered, "админский вход, фронт не зовёт"),
        ["POST /_internal/auth/authenticate"] = new(Internal, Uncovered, "служебный вход, фронт не зовёт"),
        ["DELETE /_internal/users/{userId}/cache"] = new(Internal, Uncovered, "сброс кэша пользователя, вызывается модулем users в процессе"),

        // --- внутренние ручки модуля users (зовёт модуль authorization) ---
        ["POST /_internal/users"] = new(Internal, "UsersScenario: на нём заводятся пользователи во всех контрактных тестах"),
        ["POST /_internal/users/batch-get"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["DELETE /_internal/users/{userId}"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["GET /_internal/users/context/{userId}"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["GET /_internal/users/context/by-external-id/{externalId}"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["GET /_internal/users/by-provider/{provider}/{externalId}"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["GET /_internal/users/{userId}/admin-access"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["GET /_internal/users/{userId}/external-links"] = new(Internal, Uncovered, "внутренний вызов между модулями"),
        ["POST /_internal/users/{userId}/external-links"] = new(Internal, Uncovered, "внутренний вызов между модулями"),

        // --- внутренний API модуля reports (боты, не фронт) ---
        ["GET /v2/_internal/reports"] = new(Internal, "InternalReportsControllerTests"),
        ["POST /v2/_internal/bugs"] = new(Internal, "InternalBugsControllerTests"),
        ["GET /v2/_internal/bugs/{bugId:int}"] = new(Internal, "InternalBugsControllerTests"),
        ["POST /v2/_internal/bugs/{bugId:int}/comments"] = new(Internal, "InternalCommentsControllerTests"),
        ["GET /v2/_internal/bugs/{bugId:int}/external-comments"] = new(Internal, "InternalCommentsControllerTests"),
        ["POST /v2/_internal/bugs/{bugId:int}/steps"] = new(Internal, "InternalBugStepsControllerTests"),
        ["POST /v2/_internal/attachments"] = new(Internal, "InternalAttachmentsControllerTests"),
        ["GET /v2/_internal/attachments/{attachmentId:int}/content"] = new(Internal, "InternalAttachmentsControllerTests"),
        ["GET /v2/_internal/domain-events"] = new(Internal, "InternalDomainEventsControllerTests"),
        ["GET /v2/_internal/domain-events/latest-id"] = new(Internal, "InternalDomainEventsControllerTests"),
        ["GET /v2/_internal/ping"] = new(Internal, Uncovered, "проверка живости для ботов"),
        ["* /_internal/ping"] = new(Internal, Uncovered, "проверка живости"),
        ["* /health"] = new(Internal, Uncovered, "healthcheck для оркестратора"),
    };

    public const string Uncovered = "—";

    /// <summary>Одна строка инвентаря: кто зовёт путь и чем он покрыт.</summary>
    /// <param name="Consumer">Потребитель: фронт, nginx или внутренний вызов.</param>
    /// <param name="CoveredBy">Тест-класс или <see cref="Uncovered"/>.</param>
    /// <param name="Note">Почему не покрыт либо что именно проверяется.</param>
    internal sealed record Entry(string Consumer, string CoveredBy, string Note = "");
}
