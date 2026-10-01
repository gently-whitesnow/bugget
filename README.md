# Bugget

Инструмент для баг-репортов: тестировщик заводит репорт, разработчик видит шаги, факт и ожидание,
переписка и вложения лежат рядом с багом. Self-hosted, MIT.

**Миссия:** создавать процесс работы с багами, делая упор на удобство и эффективность.

Собираетесь что-то править? Правила разработки — в [AGENTS.md](AGENTS.md), дисциплина PR и
формат коммита — в [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md).

## Что внутри

| Компонент | Образ | Описание |
| --- | --- | --- |
| `backend/` | `ghcr.io/gently-whitesnow/bugget-api` | API: репорты, пользователи и авторизация в одном процессе |
| `frontend/` | `ghcr.io/gently-whitesnow/bugget-ui` | SPA на React |
| `deploy/nginx/` | `ghcr.io/gently-whitesnow/bugget-nginx-self-hosted` | Точка входа: маршрутизация и проверка авторизации |

Хранилища: PostgreSQL (две базы — `app_db` и `users_db`) и Redis (ротация refresh-токенов, кэш
пользователей).

## Быстрый старт

Из зависимостей нужен только Docker с compose v2: весь контур собирается из исходников
в контейнерах. Node 24 и .NET SDK 9 (версия зафиксирована в `global.json`) понадобятся
дальше, для локальной разработки фронта и бэкенда.

```sh
cd deploy
docker compose -f docker-compose.yml -f docker-compose.dev.yml \
  --env-file env/dev.env --profile full up --build
```

Приложение поднимется на http://localhost. В dev-режиме доступен fake-логин без внешнего провайдера:

```
http://localhost/api/authorization/v1/fake/login?externalId=user-1&name=Tester
```

Остановить и удалить данные:

```sh
cd deploy
docker compose -f docker-compose.yml -f docker-compose.dev.yml \
  --env-file env/dev.env --profile full down -v
```

Флаги, профили compose, порты и конфигурация — [deploy/README.md](deploy/README.md).

## Разработка

Фронтенд локально с hot reload, бекенд в docker — [frontend/README.md](frontend/README.md).
Структура и команды бекенда — [backend/README.md](backend/README.md).

## Проверки качества

Два входа — оба запускают человек, агент и CI:

```sh
harness check                                     # рамка репозитория (.harness.json)
./scripts/verify.sh                               # сборка, тесты, контракты, линтеры
./scripts/verify.sh backend                       # только бекенд
./scripts/verify.sh frontend                      # только фронтенд
```

[harness](https://github.com/gently-whitesnow/harness-cli) — CLI, который проверяет сам
репозиторий: связность и циклы зависимостей, дубли, долю комментариев, подавленные
диагностики, настройки сборки .NET, документацию и формат коммитов. Тесты и сборку он не
запускает — за них отвечает `verify.sh`. Установка и подготовка клона:

```sh
curl -fsSL https://raw.githubusercontent.com/gently-whitesnow/harness-cli/master/install.sh | sh
export PATH="$HOME/.local/bin:$PATH"
harness setup    # хук commit-msg и шаблон сообщения коммита, один раз на клон
harness guide    # как работать с рамкой: цикл check → explain, exit-коды, коммиты
```

`scripts/verify.sh` — обычный bash-скрипт: шаги идут подряд, первый упавший останавливает
прогон. Новый шаг добавляется прямо в него. Нужен `python3`; интеграционные тесты бекенда
поднимают Postgres и Keycloak в Docker (Testcontainers).

## Контракты и архитектурные решения

Контракты API — `specs/contracts/<module>/openapi.yaml`, общие схемы —
`specs/contracts/shared.yaml`. Это источник правды: `*.g.cs` и фронтовые `*.d.ts` только
генерируются (`scripts/contracts/openapi-generate.sh`, `frontend-openapi-generate.sh`).
Контроллеры наследуют сгенерированные базы, поэтому маршрут или форма ответа мимо контракта
не компилируются. Realtime-события SignalR описаны в `specs/contracts/events.yaml` (ADR-0007).

Архитектурные решения и причины — [adrs/REGISTRY.md](adrs/REGISTRY.md).

## Лицензия

[MIT](LICENSE)
