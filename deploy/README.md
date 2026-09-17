# Развёртывание

Compose-контур и nginx. Команда запуска — в [корневом README](../README.md#быстрый-старт).

Флаги в команде, каждый обязателен:

| Флаг | Что даёт | Что будет без него |
| --- | --- | --- |
| `-f docker-compose.yml` | образы, тома, сеть | `service "redis" has neither an image nor a build context specified` |
| `-f docker-compose.dev.yml` | порты наружу | контейнеры поднимутся, но с хоста недоступны |
| `--env-file env/dev.env` | `ASPNETCORE_ENVIRONMENT=Development` | fake-логин выключен |
| `--profile full` | выбирает сервисы | не поднимется ничего |

Профили:

| Профиль | Сервисы | Когда |
| --- | --- | --- |
| `full` | postgres, redis, app-api, app-ui, nginx | приложение целиком в docker |
| `back` | postgres, redis, app-api, nginx | фронт локально в vite |
| `front` | — | нерабочий: `nginx` зависит от `app-api`, которого в профиле нет |

Порты на хост:

| Сервис | Порт |
| --- | --- |
| nginx | 80 |
| app-api | 7777 |
| app-ui | 1337 |
| postgres | 5432 |
| redis | 6379 |

## Конфигурация

Настройки читаются из `appsettings.json`, переменных окружения и внешнего
`external_settings.json` (монтируется в контейнер) — последний перекрывает остальные.

Контекст workspace/team обязателен для работы приложения. В
`ExternalSettings:Authentication` должны быть заданы непустые
`OrganizationIdHeaderName` (organization identity используется как внешний workspace) и
`TeamIdHeaderName`. Приложение проверяет оба имени на старте и не запускается с пустой
конфигурацией; штатный compose монтирует готовые значения из
`deploy/external-settings/bugget-api/external_settings.json`.

Переменные окружения:

| Переменная | Назначение |
| --- | --- |
| `POSTGRES_CONNECTION_STRING` | база модуля reports (`app_db`) |
| `USERS_POSTGRES_CONNECTION_STRING` | база модуля users (`users_db`) |
| `REDIS_CONNECTION_STRING` | Redis для токенов и кэша |
| `APP_DOMAIN` | внешний адрес приложения, используется в редиректах после логина |

Фоновая оптимизация видео (`OptimizatorSettings`) — самая прожорливая часть процесса:
ffmpeg живёт отдельным процессом и не виден в памяти `dotnet`. Загрузка от неё не зависит —
оригинал сохраняется и отдаётся сразу, оптимизация догоняет в фоне и может ждать сколько
нужно. Значения проверяются на старте: ноль, отрицательное и бюджет потоков сверх числа
ядер валят запуск, а не всплывают OOM'ом.

| Ключ | Умолчание | Назначение |
| --- | --- | --- |
| `VideoOptimizationEnabled` | `true` | выключает перекодирование целиком: оригинал остаётся как есть, превью не строится |
| `VideoMaxConcurrency` | `1` | сколько ffmpeg-процессов разрешено одновременно; остальные ждут в очереди |
| `VideoEncoderThreads` | `1` | `-threads` выходного кодировщика |
| `VideoDecoderThreads` | `1` | `-threads` входа: на 4K именно декодер держит основную память |
| `VideoFilterThreads` | `1` | `-filter_threads`, глобальный потолок потоков фильтров |
| `VideoTimeoutSeconds` | `900` | потолок одного вызова ffmpeg; по истечении убивается всё дерево процессов |

Профиль и версия ffmpeg пишутся в лог один раз на процесс. Метрики уезжают в общий
Prometheus-конвейер: `bugget_video_optimize_queued`, `bugget_video_optimize_active`,
`bugget_video_optimize_duration` (метка `result`) и `bugget_video_ffmpeg_peak_rss` (Linux).

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

Несовместимые frontend/backend образы выкладываются и откатываются согласованной парой.
