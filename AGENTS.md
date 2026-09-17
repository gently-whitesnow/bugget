# AGENTS.md

Bugget — self-hosted инструмент баг-репортов. Что за продукт и как поднять —
[README.md](README.md). Здесь — навигация и правила, которые нельзя нарушать молча.

## Главное ограничение

Продуктом пользуются у заказчика, у API один клиент — фронтенд этого же монорепозитория.
**Публичный HTTP-контракт ломать нельзя.** Если ломаем — фронт и бекенд правятся одним PR,
решение фиксируется ADR.

## Проверки перед сдачей хода

Два входа, оба обязательны и оба крутятся в CI:

```sh
harness check                        # рамка репозитория: .harness.json
./scripts/verify.sh          # сборка, тесты, контракты, линтеры
./scripts/verify.sh frontend # одна область: backend | frontend
```

Перед работой выполни `harness guide` — он объясняет, как читать `.harness.json`, цикл
`check → --only <id> --verbose → explain <id>` и формат коммита. Один раз на клон —
`harness setup` (хук и шаблон сообщения коммита). Установка harness —
[README.md](README.md#проверки-качества).

Шаги `verify.sh` перечислены в самом скрипте. `apps/landing` в проверки не входит — его держит
`.github/workflows/deploy-landing.yml`.

## Где что искать

| Нужно | Где |
| --- | --- |
| Запуск, профили compose, конфигурация, публикация образов | [deploy/README.md](deploy/README.md) |
| Структура backend и внешние маршруты | [backend/README.md](backend/README.md) |
| Фронтенд: стек, команды | [frontend/README.md](frontend/README.md) |
| Дизайн-система: токены, палитра, компоненты | [frontend/DESIGN.md](frontend/DESIGN.md) |
| Почему архитектура такая | [adrs/REGISTRY.md](adrs/REGISTRY.md) |
| Контракты API (источник правды) | `specs/contracts/<module>/openapi.yaml`, общее — `specs/contracts/shared.yaml` |
| Realtime-события SignalR | `specs/contracts/events.yaml` |
| Перегенерация кода из контрактов | `scripts/contracts/openapi-generate.sh` (C#), `scripts/contracts/frontend-openapi-generate.sh` (TS) |
| Публичные HTTP-пути и их покрытие | `backend/Bugget.IntegrationTests/Contract/PublicContractInventory.cs` |
| Как фронт ходит в API | `scripts/contracts/frontend-api-inventory.mjs` |
| Дисциплина PR, вклад, безопасность | [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md), [docs/SECURITY.md](docs/SECURITY.md) |

Код: `backend/` — .NET, решение `backend/Bugget.slnx`; `frontend/` — Vite + React +
TypeScript; `deploy/` — compose и nginx.

## Правила

**Падает проверка — чини причину, а не проверку.** Политику, settings и answers в
`.harness.json` меняет только владелец репозитория. Точечных подавлений нет: диагностика
выключается на весь репозиторий в `backend/.editorconfig` с причиной либо чинится
(ADR-0016).

**Сгенерированное руками не правим.** `*.g.cs` и `frontend/src/shared/api/generated/*.d.ts`
собираются из `specs/contracts/**/openapi.yaml`; дифф ловят гейты `backend-contracts` и
`frontend-contracts` (ADR-0005).

**Realtime-событие — сначала в `specs/contracts/events.yaml`, потом в код.** Форму
сообщений менять нельзя (ADR-0007); держат гейты `*-realtime-contract`.

**Полное описание провода — только OpenAPI.** Golden-снимков ответа нет и заводить их
нельзя; contract-тесты проверяют поведение явными assertions (ADR-0010). Новый эндпоинт
обязан появиться в `PublicContractInventory.cs`.

**Новый код пишется по DDD**, легаси мигрирует только при касании (ADR-0003).

**Граф проектов — DAG**, `Domain` и `Application` не зависят от persistence, транспорта
и ASP.NET (ADR-0001). Порты внешних зависимостей живут в `Bugget.Application/**/Ports`,
use-case пересекает границу слоя только через интерфейс (ADR-0017). Держит это
`backend/Bugget.Architecture.Tests`; известные отступления — поимённо в
`KnownDeviations.cs`, список только сокращается.

**Reuse-first.** Новый эндпоинт, схема, stored function, колонка или таблица — последний
вариант, а не первый.

**Не угадывай молча.** Требование читается двояко, затрагивается схема БД или публичный
контракт, или лучше иное решение — скажи об этом до кода.

## Дисциплина PR

Бюджет ревью — примерно 600 строк бизнес-логики на PR; сгенерированный код, тесты,
документация, форматирование и механические перемещения не в счёт. Больше — режь на
слайсы. Сообщение коммита — по `harness commit-message template`: Контекст, Решение,
Границы, Последствия; проверяет хук `commit-msg`.

## Известный долг

| Долг | Состояние |
| --- | --- |
| Архитектура `sliced-dotnet/1` | Проверки `architecture.*` в `.harness.json` выключены до переезда backend — ADR-0016. |
| DDD-миграция легаси | Только при касании — ADR-0003. |
| Mutation testing | Отложен до rich-домена — ADR-0003. |
| Скрепка принимает любой файл | Бекенд пропускает белый список MIME (`AttachmentConstants.cs`), а `ComposerInput` берёт любой файл: нужен `accept`, проверка при вставке и решение по PDF. |
