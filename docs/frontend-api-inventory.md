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

Отдельный гейт `frontend-api-inventory` закрывает формы, в которых строка пути
спрятана от синтаксического правила: raw-инстанс с URL через переменную,
`window.fetch`, runtime-import `axios`, `XMLHttpRequest` и `sendBeacon`. Он же
проверяет, что у всех шести модулей есть `client.ts` с импортом своего
`generated/<module>` и общей `createOperationRequest`, а каждый оставшийся
`fetch` и browser redirect входит в список исключений ниже. Краснота обходов
доказана встроенной самопроверкой скрипта
`scripts/quality/frontend-api-inventory.mjs --self-test`.

Страницы и сущности зовут ручки через границу и держат у себя только то, что к
контракту отношения не имеет: пагинацию по умолчанию, журналирование отказа,
адаптеры имён. Формы данных выводятся из операций (`Camelized<T>` учитывает
case-границу ADR-0009), рукописных DTO в `frontend/src` не осталось.

## Исключения

| Место | Что делает | Почему не операция контракта |
| --- | --- | --- |
| `pages/Login/ui/Login.tsx` | `window.location.href` на `/api/authorization/v1/external/token/callback` и `/api/authorization/v1/fake/login` | Это не запрос фронта, а навигация браузера: ответ — редирект на провайдера входа и установка cookie, читать тело некому. Типизированная операция здесь неприменима — она вернула бы данные вместо перехода. Самих путей в контракте нет: `specs/contracts/authorization/openapi.yaml` описывает только ту поверхность, которую фронт зовёт как API. |
| `pages/Settings/ui/hooks/useExternalLinks.ts` | `window.location.href` на `/api/authorization/v1/{provider}/login?mode=link` | То же: переход на провайдера для привязки аккаунта. |
| `shared/api/instances/base.ts` | `window.location.replace(redirectUrl)` после HTTP 401 | Это UI-навигация на auth entry (`/login` для self-hosted или `/` для SaaS), а не API-вызов. URL собирает общий `buildAuthRedirectUrl`, который сохраняет текущий путь в `next`. |
| `widgets/custom-left-sidebar/ui/CustomLeftSidebar/components/LogoutButton.tsx` | `window.location.replace(getPostLogoutRedirectUrl())` после успешного logout | Переход на UI auth entry после контрактной операции `POST /v1/logout`. Расхождение с полем ответа `redirect_url` отдельно зафиксировано ниже и этим инвентарём не меняется. |
| `shared/ui/FilePreview/FilePreview.tsx` | `fetch` за содержимым вложения и `src`/`href` картинки | Тело бинарное и уезжает в браузер, а не в код; axios-интерсепторы (конверсия регистра) ему не нужны. Исключение здесь только в способе запроса, но не в адресе: его строит `reportsApi.attachmentContentUrl` из шести шаблонов `shared/api/reports/urls.ts`, объявленных `satisfies keyof paths`. Оригинал и превью — два разных пути контракта, а не путь с дописанным суффиксом, поэтому переименование любого из них в `specs/contracts/reports/openapi.yaml` валит `frontend-typecheck`, а не уезжает в 404 в рантайме. |
| `shared/lib/buildFullUrl.ts` | дописывает `/api/app/workspaces/{id}/teams/{id}` к пути для браузерных адресов | Это шов проксирования, а не ручка: тот же префикс для axios дописывает интерсептор `shared/api/instances/app.ts`. Путей модулей здесь нет. |
| `shared/model/socket` | SignalR-хаб (`/v1/report-page-hub`) | Realtime описан отдельным контрактом `specs/contracts/events.yaml`, из него ничего не генерируется (ADR-0007), дрейф держит гейт `frontend-realtime-contract`. |
| `shared/ui/notifications/NotificationDemoForm.tsx` | `fetch("/api/demo-endpoint")` | Демонстрационная форма UI-кита; такого пути у бекенда нет и в контракте быть не может. |

Отдельно зафиксировано расхождение, которое исправляется не здесь: `POST /v1/logout`
отдаёт `redirect_url`, но фронт уводит пользователя по собственному
`getPostLogoutRedirectUrl` (`shared/lib/auth`). Поведение сохранено 1:1; переход на
адрес из ответа — смена поведения и отдельное решение.
