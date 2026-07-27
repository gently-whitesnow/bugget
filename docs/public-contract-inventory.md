# Инвентарь публичного контракта

<!-- Файл собирается тестом PublicContractInventoryTests из таблицы маршрутов
     приложения и из backend/Bugget.IntegrationTests/Contract/PublicContractInventory.cs.
     Руками не правится: пересобрать —
     UPDATE_CONTRACT_SNAPSHOTS=1 dotnet test backend/Bugget.IntegrationTests -->

Пути даны так, как их видит бекенд. Фронт ходит по ним через nginx с префиксами
`/api/app/workspaces/{id}/teams/{id}`, `/api/users` и `/api/authorization`, которые
срезаются при проксировании (deploy/nginx/snippets/locations).

| Путь | Зовёт | Покрыт | Комментарий |
| --- | --- | --- | --- |
| `* /_internal/ping` | внутренний | нет | проверка живости |
| `* /health` | внутренний | нет | healthcheck для оркестратора |
| `* /v1/report-page-hub` | фронт | да — `ReportPageHubContractTests` | проверяется handshake; сам обмен сообщениями — вне контракта HTTP |
| `* /v1/report-page-hub/negotiate` | фронт | да — `ReportPageHubContractTests` |  |
| `DELETE /_internal/users/{userId}` | внутренний | нет | внутренний вызов между модулями |
| `DELETE /_internal/users/{userId}/cache` | внутренний | нет | сброс кэша пользователя, вызывается модулем users в процессе |
| `DELETE /v1/users` | фронт | нет | тот же контроллер, что и контекстный вариант; фронт ходит с контекстом |
| `DELETE /v1/users/avatar` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `DELETE /v1/users/external-links/{provider}` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `DELETE /v1/users/mattermost` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `DELETE /v1/workspaces/{workspaceId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/members` | фронт | да — `UsersWorkspacesContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/members/{userId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users` | фронт | да — `UsersProfileContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar` | фронт | да — `UsersProfileContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links/{provider}` | фронт | да — `UsersProfileContractTests` |  |
| `DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost` | фронт | да — `UsersProfileContractTests` |  |
| `DELETE /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `DELETE /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}` | фронт | да — `CommentsAndLinksContractTests` |  |
| `DELETE /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `DELETE /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}` | фронт | да — `BugsContractTests` |  |
| `DELETE /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `DELETE /v2/reports/{aliasId}/links/{linkId}` | фронт | да — `CommentsAndLinksContractTests` |  |
| `GET /_internal/admin` | nginx | нет | админский auth_request; в текущем nginx-конфиге не используется |
| `GET /_internal/anon/auth` | nginx | да — `AuthorizationContractTests` |  |
| `GET /_internal/auth` | nginx | да — `AuthorizationContractTests` |  |
| `GET /_internal/users/by-provider/{provider}/{externalId}` | внутренний | нет | внутренний вызов между модулями |
| `GET /_internal/users/context/by-external-id/{externalId}` | внутренний | нет | внутренний вызов между модулями |
| `GET /_internal/users/context/{userId}` | внутренний | нет | внутренний вызов между модулями |
| `GET /_internal/users/{userId}/admin-access` | внутренний | нет | внутренний вызов между модулями |
| `GET /_internal/users/{userId}/external-links` | внутренний | нет | внутренний вызов между модулями |
| `GET /v1/admin/authenticate` | внутренний | нет | админский вход, фронт не зовёт |
| `GET /v1/auth` | фронт | да — `AuthorizationContractTests` |  |
| `GET /v1/external/kaiten/boards` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/external/search` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/external/token/callback` | фронт | нет | OIDC-callback: нужен внешний провайдер, в тестовом хосте не поднимается |
| `GET /v1/fake/login` | фронт | нет | провайдер входа для локальной разработки; в тестовом хосте контроллер FakeAuth не попадает в application parts — см. итог MAIN-13 |
| `GET /v1/flags` | фронт | да — `AuthorizationContractTests` |  |
| `GET /v1/reports/search` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/settings-sections` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/users` | фронт | нет | тот же контроллер, что и контекстный вариант; фронт ходит с контекстом |
| `GET /v1/users/autocomplete` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `GET /v1/users/avatar/content` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `GET /v1/users/external-links` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `GET /v1/users/mattermost/callback` | фронт | нет | OAuth Mattermost: нужен внешний провайдер |
| `GET /v1/users/mattermost/connect` | фронт | нет | OAuth Mattermost: нужен внешний провайдер |
| `GET /v1/users/{userId:long}/avatar/content` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `GET /v1/workspaces` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/autocomplete` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/invites` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/members` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/autocomplete` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar/content` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/{userId:long}/avatar/content` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v2/_internal/attachments/{attachmentId:int}/content` | внутренний | да — `InternalAttachmentsControllerTests` |  |
| `GET /v2/_internal/bugs/{bugId:int}` | внутренний | да — `InternalBugsControllerTests` |  |
| `GET /v2/_internal/bugs/{bugId:int}/external-comments` | внутренний | да — `InternalCommentsControllerTests` |  |
| `GET /v2/_internal/domain-events` | внутренний | да — `InternalDomainEventsControllerTests` |  |
| `GET /v2/_internal/domain-events/latest-id` | внутренний | да — `InternalDomainEventsControllerTests` |  |
| `GET /v2/_internal/ping` | внутренний | нет | проверка живости для ботов |
| `GET /v2/_internal/reports` | внутренний | да — `InternalReportsControllerTests` |  |
| `GET /v2/analytics/responsible/{userId}` | фронт | да — `AnalyticsContractTests` |  |
| `GET /v2/analytics/summary` | фронт | да — `AnalyticsContractTests` |  |
| `GET /v2/reports` | фронт | да — `ReportsContractTests` |  |
| `GET /v2/reports/legacy/{legacyId}` | фронт | да — `ReportsContractTests` |  |
| `GET /v2/reports/{aliasId}` | фронт | да — `ReportsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content/preview` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content/preview` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content/preview` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{id}/analytics` | фронт | да — `AnalyticsContractTests` |  |
| `PATCH /v2/reports/{aliasId}` | фронт | да — `ReportsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}` | фронт | да — `BugsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}` | фронт | да — `BugsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `POST /_internal/auth/authenticate` | внутренний | нет | служебный вход, фронт не зовёт |
| `POST /_internal/users` | внутренний | да — `UsersScenario: на нём заводятся пользователи во всех контрактных тестах` |  |
| `POST /_internal/users/batch-get` | внутренний | нет | внутренний вызов между модулями |
| `POST /_internal/users/{userId}/external-links` | внутренний | нет | внутренний вызов между модулями |
| `POST /v1/external/kaiten/boards/batch-get` | фронт | да — `SettingsAndSearchContractTests` |  |
| `POST /v1/external/search/apply` | фронт | да — `SettingsAndSearchContractTests` |  |
| `POST /v1/invites/accept` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/logout` | фронт | да — `AuthorizationContractTests` |  |
| `POST /v1/users/avatar` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `POST /v1/users/batch/list` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `POST /v1/users/merge` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `POST /v1/workspaces` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/members/join` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/batch/list` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/invites` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/members/join` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar` | фронт | да — `UsersProfileContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/batch/list` | фронт | да — `UsersProfileContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/merge` | фронт | да — `UsersProfileContractTests` |  |
| `POST /v2/_internal/attachments` | внутренний | да — `InternalAttachmentsControllerTests` |  |
| `POST /v2/_internal/bugs` | внутренний | да — `InternalBugsControllerTests` |  |
| `POST /v2/_internal/bugs/{bugId:int}/comments` | внутренний | да — `InternalCommentsControllerTests` |  |
| `POST /v2/_internal/bugs/{bugId:int}/steps` | внутренний | да — `InternalBugStepsControllerTests` |  |
| `POST /v2/reports` | фронт | да — `ReportsContractTests` |  |
| `POST /v2/reports/counts:batch` | фронт | да — `ReportsContractTests` |  |
| `POST /v2/reports/{aliasId}/bugs` | фронт | да — `BugsContractTests` |  |
| `POST /v2/reports/{aliasId}/bugs/{bugId}/attachments` | фронт | да — `AttachmentsContractTests` |  |
| `POST /v2/reports/{aliasId}/bugs/{bugId}/comments` | фронт | да — `CommentsAndLinksContractTests` |  |
| `POST /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments` | фронт | да — `AttachmentsContractTests` |  |
| `POST /v2/reports/{aliasId}/bugs/{bugId}/steps` | фронт | да — `BugsContractTests` |  |
| `POST /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments` | фронт | да — `AttachmentsContractTests` |  |
| `POST /v2/reports/{aliasId}/links` | фронт | да — `CommentsAndLinksContractTests` |  |
| `PUT /v1/team-settings-sections/{sectionId}/settings/{settingId}` | фронт | да — `SettingsAndSearchContractTests` |  |
| `PUT /v1/user-settings-sections/{sectionId}/settings/{settingId}` | фронт | да — `SettingsAndSearchContractTests` |  |
| `PUT /v1/users` | фронт | нет | тот же контроллер, что и контекстный вариант; фронт ходит с контекстом |
| `PUT /v1/users/mattermost` | фронт | нет | тот же контроллер, что и контекстный вариант |
| `PUT /v1/workspace-settings-sections/{sectionId}/settings/{settingId}` | фронт | да — `SettingsAndSearchContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}/users` | фронт | да — `UsersProfileContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost` | фронт | да — `UsersProfileContractTests` |  |
| `PUT /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}` | фронт | да — `CommentsAndLinksContractTests` |  |
| `PUT /v2/reports/{aliasId}/bugs/{bugId}/steps/order` | фронт | да — `BugsContractTests` |  |
| `PUT /v2/reports/{aliasId}/links/{linkId}` | фронт | да — `CommentsAndLinksContractTests` |  |
