# Реестр ADR

Архитектурные решения Bugget. Один файл — одно решение, номер сквозной и четырёхзначный.

Новый ADR: скопировать `.template.md` в `NNNN-kebab-заголовок.md`, заполнить, добавить
строку сюда. ADR не редактируют задним числом — устаревшее решение получает статус
`Superseded by ADR-NNNN`, новое пишется отдельным файлом.

| Номер | Заголовок | Статус | Дата |
| --- | --- | --- | --- |
| [0001](0001-clean-architecture-target.md) | Целевая архитектура — квартет `Bugget.{Api,Application,Domain,Infrastructure}` + `Contracts` | Accepted | 2026-07-26 |
| [0002](0002-quality-harness-ratchet-first.md) | Quality harness — единый `verify.sh`, ratchet поверх legacy-профиля, ставка на CI | Accepted | 2026-07-26 |
| [0003](0003-ddd-for-new-features.md) | DDD обязателен для нового кода, легаси мигрируем только при касании | Accepted | 2026-07-26 |
| [0004](0004-drop-monade-and-flow.md) | Отказ от `Monade` и `Flow` в пользу нативных кортежей | Accepted | 2026-07-26 |
| [0005](0005-contract-first-openapi.md) | Contract-first — источник правды `specs/contracts/**/openapi.yaml` | Accepted | 2026-07-26 |
| [0006](0006-integration-tests-in-ci.md) | Интеграционные тесты в полном прогоне и в CI, категория по имени проекта | Accepted | 2026-07-26 |
| [0007](0007-realtime-events-contract.md) | Контракт realtime-событий — `specs/contracts/events.yaml` и гейт покрытия по четырём сторонам | Accepted | 2026-07-27 |
| [0008](0008-problem-details.md) | RFC 9457 для HTTP-ошибок | Accepted | 2026-07-29 |
| [0009](0009-wire-ui-case-boundary.md) | Единая граница регистров wire↔UI на фронте | Accepted | 2026-07-30 |
| [0010](0010-contract-tests-without-snapshots.md) | Поведенческие contract-тесты с явными assertions, полное описание провода — только OpenAPI | Accepted | 2026-07-31 |
| [0011](0011-created-report-location.md) | Создание репорта возвращает 201, тело и внешний origin-relative Location | Accepted | 2026-08-01 |
| [0012](0012-public-int64-decimal-string.md) | Публичный неотрицательный Int64 — каноническая decimal string | Accepted | 2026-08-02 |
| [0013](0013-string-enum-wire-contract.md) | Enum-like значения публичного HTTP API — строки `snake_case`, домен и БД остаются числовыми | Accepted | 2026-08-01 |

Идентификатор `ADR-20260518`, который встречается в описаниях контрактов и в
сгенерированных `.g.cs`, — из старой схемы нумерации по дате. Он относится к решению
о формате ошибки, зафиксированному в ADR-0005; подробности — в самом ADR-0005.
