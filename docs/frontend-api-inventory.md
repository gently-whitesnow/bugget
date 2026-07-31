# Инвентарь обращений фронта к API

Итог программы «фронт на сгенерированный TS-клиент» (MAIN-16). Каждый HTTP-модуль
фронта имеет ровно одну транспортную границу в `frontend/src/shared/api/<модуль>`:
путь берётся из `paths` сгенерированного клиента, метод — из объявленных у этого
пути, тело, query и тип ответа — из той же операции. Общая механика вызова —
`frontend/src/shared/api/operation.ts`, второго типового пути вызова нет ни у
одного модуля.

Ниже — что где живёт и полный список осознанных исключений. Список только
сокращается: новая строка здесь — тот же осознанный долг, что и отключённый гейт.

## Границы модулей

| Модуль | Граница | Контракт | Инстанс |
| --- | --- | --- | --- |
| reports | `shared/api/reports` | `specs/contracts/reports/openapi.yaml` | `appApi` |
| users | `shared/api/users` | `specs/contracts/users/openapi.yaml` | `usersApi` (префикс `/api/users`) |
| settings | `shared/api/settings` | `specs/contracts/settings/openapi.yaml` | `appApi` |
| analytics | `shared/api/analytics` | `specs/contracts/analytics/openapi.yaml` | `appApi` |
| external | `shared/api/external` | `specs/contracts/external/openapi.yaml` | `appApi` |
| authorization | `shared/api/authorization` | `specs/contracts/authorization/openapi.yaml` | `authorizationApi` (префикс `/api/authorization`) |

Обход границы краснит гейт `frontend-lint`: правила
`frontend/eslint-rules/no-direct-<модуль>-transport.js` ловят и shorthand
(`appApi.get("/v1/...")`), и config-форму (`{ url }`), и обход axios через
`fetch`. Краснота каждого правила закреплена тестом
`shared/api/<модуль>/transportBoundary.gate.test.ts`.

Страницы и сущности зовут ручки через границу и держат у себя только то, что к
контракту отношения не имеет: пагинацию по умолчанию, журналирование отказа,
адаптеры имён. Формы данных выводятся из операций (`Camelized<T>` учитывает
case-границу ADR-0009), рукописных DTO в `frontend/src` не осталось.

## Исключения

| Место | Что делает | Почему не операция контракта |
| --- | --- | --- |
| `pages/Login/ui/Login.tsx` | `window.location.href` на `/api/authorization/v1/external/token/callback` и `/api/authorization/v1/fake/login` | Это не запрос фронта, а навигация браузера: ответ — редирект на провайдера входа и установка cookie, читать тело некому. Типизированная операция здесь неприменима — она вернула бы данные вместо перехода. Самих путей в контракте нет: `specs/contracts/authorization/openapi.yaml` описывает только ту поверхность, которую фронт зовёт как API. |
| `pages/Settings/ui/hooks/useExternalLinks.ts` | `window.location.href` на `/api/authorization/v1/{provider}/login?mode=link` | То же: переход на провайдера для привязки аккаунта. |
| `shared/ui/FilePreview/FilePreview.tsx` | `fetch` за содержимым вложения и `src`/`href` картинки | Тело бинарное и уезжает в браузер, а не в код; axios-интерсепторы (конверсия регистра) ему не нужны. Адрес при этом contract-bound: `reportsApi.attachmentContentPath` строит его из шаблона контракта. |
| `shared/lib/buildFullUrl.ts` | дописывает `/api/app/workspaces/{id}/teams/{id}` к пути для браузерных адресов | Это шов проксирования, а не ручка: тот же префикс для axios дописывает интерсептор `shared/api/instances/app.ts`. Путей модулей здесь нет. |
| `shared/model/socket` | SignalR-хаб (`/v1/report-page-hub`) | Realtime описан отдельным контрактом `specs/contracts/events.yaml`, из него ничего не генерируется (ADR-0007), дрейф держит гейт `frontend-realtime-contract`. |
| `shared/ui/notifications/NotificationDemoForm.tsx` | `fetch("/api/demo-endpoint")` | Демонстрационная форма UI-кита; такого пути у бекенда нет и в контракте быть не может. |

Отдельно зафиксировано расхождение, которое исправляется не здесь: `POST /v1/logout`
отдаёт `redirect_url`, но фронт уводит пользователя по собственному
`getPostLogoutRedirectUrl` (`shared/lib/auth`). Поведение сохранено 1:1; переход на
адрес из ответа — смена поведения и отдельное решение.
