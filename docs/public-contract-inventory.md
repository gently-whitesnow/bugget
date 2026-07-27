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
| `GET /_internal/auth` | nginx | да — `AuthorizationContractTests` |  |
| `GET /v1/external/kaiten/boards` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/external/search` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/external/token/callback` | фронт | нет | OIDC-callback: нужен внешний провайдер, в тестовом хосте не поднимается |
| `GET /v1/fake/login` | фронт | нет | провайдер входа для локальной разработки; в тестовом хосте контроллер FakeAuth не попадает в application parts — см. итог MAIN-13 |
| `GET /v1/reports/search` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/settings-sections` | фронт | да — `SettingsAndSearchContractTests` |  |
| `GET /v1/users/mattermost/callback` | фронт | нет | OAuth Mattermost: нужен внешний провайдер |
| `GET /v1/users/mattermost/connect` | фронт | нет | OAuth Mattermost: нужен внешний провайдер |
| `GET /v1/workspaces` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/autocomplete` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/invites` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/members` | фронт | да — `UsersWorkspacesContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/autocomplete` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar/content` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v1/workspaces/{workspaceId}/teams/{teamId}/users/{userId:long}/avatar/content` | фронт | да — `UsersProfileContractTests` |  |
| `GET /v2/analytics/responsible/{userId}` | фронт | да — `AnalyticsContractTests` |  |
| `GET /v2/analytics/summary` | фронт | да — `AnalyticsContractTests` |  |
| `GET /v2/reports` | фронт | да — `ReportsContractTests` |  |
| `GET /v2/reports/legacy/{legacyId:int}` | фронт | да — `ReportsContractTests` |  |
| `GET /v2/reports/{aliasId}` | фронт | да — `ReportsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content/preview` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}/content/preview` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}/content/preview` | фронт | да — `AttachmentsContractTests` |  |
| `GET /v2/reports/{id:long}/analytics` | фронт | да — `AnalyticsContractTests` |  |
| `PATCH /v2/reports/{aliasId}` | фронт | да — `ReportsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}` | фронт | да — `BugsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}` | фронт | да — `BugsContractTests` |  |
| `PATCH /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}` | фронт | да — `AttachmentsContractTests` |  |
| `POST /v1/external/kaiten/boards/batch-get` | фронт | да — `SettingsAndSearchContractTests` |  |
| `POST /v1/external/search/apply` | фронт | да — `SettingsAndSearchContractTests` |  |
| `POST /v1/invites/accept` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/logout` | фронт | да — `AuthorizationContractTests` |  |
| `POST /v1/workspaces` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/members/join` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/batch/list` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/invites` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/members/join` | фронт | да — `UsersWorkspacesContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar` | фронт | да — `UsersProfileContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/batch/list` | фронт | да — `UsersProfileContractTests` |  |
| `POST /v1/workspaces/{workspaceId}/teams/{teamId}/users/merge` | фронт | да — `UsersProfileContractTests` |  |
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
| `PUT /v1/workspace-settings-sections/{sectionId}/settings/{settingId}` | фронт | да — `SettingsAndSearchContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}` | фронт | да — `UsersWorkspacesContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}/users` | фронт | да — `UsersProfileContractTests` |  |
| `PUT /v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost` | фронт | да — `UsersProfileContractTests` |  |
| `PUT /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}` | фронт | да — `CommentsAndLinksContractTests` |  |
| `PUT /v2/reports/{aliasId}/bugs/{bugId}/steps/order` | фронт | да — `BugsContractTests` |  |
| `PUT /v2/reports/{aliasId}/links/{linkId}` | фронт | да — `CommentsAndLinksContractTests` |  |
