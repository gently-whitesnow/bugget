# ADR-0011: Создание репорта возвращает 201, тело и внешний origin-relative Location

- **Статус:** Accepted
- **Дата:** 2026-08-01
- **Решение принял:** владелец продукта

## Контекст

`POST /v2/reports` создаёт ресурс, но исторически отвечал `200 OK` с `ReportSummary`.
Внешний запрос приходит через nginx по пути с workspace/team, после чего nginx срезает
этот префикс. Backend получает те же идентификаторы как `OrganizationId` и `TeamId` в
identity. Единственный клиент API находится в этом монорепозитории и после создания
использует тело ответа.

## Решение

Возвращать из `POST /v2/reports` `201 Created`, прежнее JSON-тело `ReportSummary` и
обязательный `Location` с внешним origin-relative путём
`/api/app/workspaces/{workspaceId}/teams/{teamId}/v2/reports/{aliasId}`.

`workspaceId` и `teamId` берутся из identity, а `aliasId` — из `id` тела ответа.
Frontend продолжает потреблять тело: не читает `Location`, не переходит по нему и не
выполняет дополнительный GET. OpenAPI, backend и frontend переключаются атомарно.

## Последствия

- Семантика успешного create соответствует HTTP, а `Location` указывает путь, видимый
  клиенту до nginx rewrite.
- Форма `ReportSummary`, ошибки и валидация не меняются.
- OpenAPI и contract-тест принуждают статус, media type, заголовок и связь alias с
  телом; frontend wire-тест принуждает прежний POST и единственное потребление ответа.
- Другие create-операции и навигация frontend этим решением не меняются.

## Отброшенные альтернативы

- **Вернуть внутренний `/v2/reports/{aliasId}`.** После nginx rewrite это путь backend,
  а не внешний API-путь клиента.
- **Вернуть только `Location` и сделать GET.** Ломает текущий сценарий и добавляет
  сетевой запрос без продуктовой пользы.
- **Восстановить исходный URL запроса.** Workspace/team уже срезаны nginx; канонические
  значения доступны напрямую в identity.
