# Bugget

Инструмент для баг-репортов: тестировщик заводит репорт, разработчик видит шаги, факт и ожидание,
переписка и вложения лежат рядом с багом. Self-hosted, MIT.

## Что внутри

| Компонент | Образ | Описание |
| --- | --- | --- |
| `backend/` | `ghcr.io/gently-whitesnow/bugget-api` | API: репорты, пользователи и авторизация в одном процессе |
| `frontend/` | `ghcr.io/gently-whitesnow/bugget-ui` | SPA на React |
| `deploy/nginx/` | `ghcr.io/gently-whitesnow/bugget-nginx-self-hosted` | Точка входа: маршрутизация и проверка авторизации |

Хранилища: PostgreSQL (две базы — `app_db` и `users_db`) и Redis (ротация refresh-токенов, кэш
пользователей).

## Быстрый старт

```sh
cd deploy
docker compose -f docker-compose.yml -f docker-compose.dev.yml \
  --env-file env/dev.env --profile full up --build
```

Приложение поднимется на http://localhost. В dev-режиме доступен fake-логин без внешнего провайдера:

```
http://localhost/api/authorization/v1/fake/login?externalId=user-1&name=Tester
```

Остановить и удалить данные: `docker compose ... --profile full down -v`.

### Разработка фронтенда

Бэкенд поднимается в docker, фронт — локально с hot reload:

```sh
cd frontend
npm ci
npm run start-backend   # postgres + redis + api + nginx
npm run dev             # vite на :5173, проксирует /api на nginx
```

### Разработка бэкенда

```sh
cd backend
dotnet build Bugget.sln
dotnet test Bugget.Tests/Bugget.Tests.csproj          # unit
dotnet test Bugget.Architecture.Tests/Bugget.Architecture.Tests.csproj   # границы слоёв
dotnet test Bugget.IntegrationTests/Bugget.IntegrationTests.csproj   # нужен docker (Testcontainers)
```

### Проверки качества

Одна команда для человека, агента и CI — она же крутится на pull request:

```sh
./scripts/quality/verify.sh                       # все гейты
./scripts/quality/verify.sh --list                # что вообще проверяется
./scripts/quality/verify.sh --dry-run             # план без запуска
./scripts/quality/verify.sh --fast                # без медленных гейтов
./scripts/quality/verify.sh --scope backend       # только бекенд
./scripts/quality/verify.sh --only frontend-lint  # ровно один гейт
```

Набор гейтов лежит в `.quality/quality.config.json` — новая проверка добавляется туда,
а не в workflow. Гейты прогоняются все, даже если что-то упало: в конце печатается сводка
со статусом и временем. Нужны `jq` и `python3` (на нём написаны бекендовые гейты
поддерживаемости, дубликатов и послаблений).

Полный прогон поднимает Postgres и Keycloak в Docker (Testcontainers) — без демона Docker
гейт `backend-test-integration` падает. `--fast` его пропускает и Docker не требует.

Тестовые проекты бекенда делятся по имени: `*.IntegrationTests` идут в медленный гейт,
остальные — в быстрый. Списки нигде не ведутся, проекты находятся поиском, так что новый
тестовый проект запускается сам.

### Ratchet-гейты и снимки

Часть гейтов сравнивает код не с идеалом, а со снимком в `.quality/`: существующий долг
зафиксирован и не блокирует, но расти ему нельзя, а новое нарушение — красный гейт
(ADR-0002). Так работают `frontend-maintainability`, `backend-maintainability` и
`backend-suppressions`.

```sh
python3 scripts/quality/backend-maintainability.py --update            # пересобрать снимок бюджета
python3 scripts/quality/backend-maintainability.py --profile strict    # посмотреть целевой профиль
python3 scripts/quality/backend-suppressions.py --list                 # все послабления и их обоснования
python3 scripts/quality/backend-suppressions.py --update               # пересобрать снимок послаблений
python3 scripts/quality/backend-duplicates.py --max 0                  # все группы дубликатов
```

Снимок вниз (отрефакторили — стало меньше) пересобирается свободно. Снимок вверх — это
ослабление гейта: отдельный коммит, в теле которого сказано, зачем и когда снимется.

## Контракты и архитектурные решения

Контракты API описаны в `specs/contracts/<module>/openapi.yaml`, общие схемы —
в `specs/contracts/shared.yaml`. Это источник правды: файлы `*.g.cs` только генерируются
(`./scripts/quality/openapi-generate.sh`) и правке руками не подлежат. Контроллеры
наследуют сгенерированные абстрактные базы, поэтому маршрут или форма ответа мимо
контракта не компилируются, а расхождение кода с yaml валит гейт `backend-contracts`.

Realtime-события SignalR описаны в `specs/contracts/events.yaml`. Этот контракт
описательный: из него ничего не генерируется, форма сообщений — уже публичный контракт
(ADR-0007). Соответствие контракта, интерфейса публикации, обработчика и подписок фронта
держит гейт `backend-realtime-contract` / `frontend-realtime-contract`; разобранный
контракт показывает `python3 scripts/quality/realtime-contract.py --list`.

Архитектурные решения и причины — [specs/ADR/REGISTRY.md](specs/ADR/REGISTRY.md).
Точка входа для агента — [ROOT.md](ROOT.md).

## Структура backend

Один процесс и один образ, внутри — три модуля с сохранёнными границами:

- `Bugget*` — репорты, баги, комментарии, вложения, аналитика (`app_db`);
- `Users.*` — пользователи, команды, рабочие пространства (`users_db`);
- `Authorization.*` — выпуск и ротация JWT, проверка авторизации для nginx (Redis).

Внешние маршруты разведены по префиксам, которые снимает nginx:

| Внешний путь | Модуль |
| --- | --- |
| `/api/app/workspaces/{ws}/teams/{team}/...` | reports |
| `/api/users/...` | users |
| `/api/authorization/...` | authorization |
| `/_internal/auth` (субзапрос nginx) | authorization |

Модули не ходят друг к другу по HTTP: адаптеры в `backend/Bugget/Modules/InProcess` подменяют
бывшие межсервисные вызовы прямыми.

## Конфигурация

Настройки читаются из `appsettings.json`, переменных окружения и внешнего
`external_settings.json` (монтируется в контейнер) — последний перекрывает остальные.

Переменные окружения:

| Переменная | Назначение |
| --- | --- |
| `POSTGRES_CONNECTION_STRING` | база модуля reports (`app_db`) |
| `USERS_POSTGRES_CONNECTION_STRING` | база модуля users (`users_db`) |
| `REDIS_CONNECTION_STRING` | Redis для токенов и кэша |
| `APP_DOMAIN` | внешний адрес приложения, используется в редиректах после логина |

Что важно поменять перед боевой установкой:

- `KeyStoreOptions:PemFilePath` — путь к RSA-ключам подписи JWT. Ключ генерируется при первом
  старте, каталог обязан быть на постоянном томе: иначе каждый рестарт разлогинивает всех.
- `TeamsOptions:Pepper` — соль для инвайт-кодов, значение по умолчанию публичное.
- `OidcAuthOptions` — включите для входа через свой OIDC-провайдер; иначе доступен только
  fake-логин, и он работает лишь в `ASPNETCORE_ENVIRONMENT=Development`.

## Публикация образов

Workflow'ы `publish-backend`, `publish-frontend`, `publish-nginx-self-hosted` запускаются вручную
(`workflow_dispatch`) и принимают версию вида `v1.2.3`. Версия без суффикса собирается под
amd64 + arm64 и получает тег `latest`, с суффиксом — только amd64.

## Лицензия

[MIT](LICENSE)
