# ADR-0012: Публичный неотрицательный Int64 — каноническая decimal string

- **Статус:** Accepted
- **Дата:** 2026-08-02
- **Решение принял:** тимлид

## Контекст

OpenAPI описывал часть идентификаторов и счётчиков как `integer` с
`format: int64`. Генератор C# сохранял их как `long`, но TypeScript представлял
тем же `number`. Единственный клиент API работает с IEEE-754 double: значение
`9007199254740993` там округляется до `9007199254740992`. В результате UI мог
показать, положить в key, сравнить или отправить в следующий URL идентификатор
соседней записи.

Изменение ломает публичный HTTP-контракт. По правилу `ROOT.md` поставщик и
единственный потребитель должны перейти атомарно, а решение — остаться в ADR.
Внутри Application, Domain и PostgreSQL значения уже представлены `long`; менять
их ради формы провода не требуется.

## Решение

**Передавать публичный неотрицательный Int64 канонической десятичной строкой
`Int64String`, меняя backend, OpenAPI, генерацию C#/TypeScript и frontend одним
атомарным rollout.**

Канон `Int64String` определён в `specs/contracts/shared.yaml`: `0` либо число без
знака, ведущих нулей и разделителей в диапазоне
`0..9223372036854775807`. Поле обязательное и не допускает `null`.

Scope перехода ограничен девятью response-полями:

- `reports`: `ReportList.total`, `ReportCountsItem.count`,
  `AnalyticsReport.report_id`;
- `analytics`: `TopRegressionReport.report_id`,
  `AnalyticsResponsibleParticipatedReport.report_id`,
  `AnalyticsResponsibleCompletedReport.report_id`;
- `external`: `ExternalSearchResult.total`;
- `users`: `WorkspaceMember.user_id`, `UserProfile.id`.

Две path-границы меняются тем же rollout: report analytics `{id}` и удаление
участника `{userId}`. На frontend значения остаются строками в store, key,
сравнениях, отображении и URL. Для числового порядка и арифметики используется
`BigInt`, а не `Number`.

`Bugget.Api.Http.WireInt64` владеет обеими backend-границами: `TryParse` принимает
только канонический path и проверяет диапазон, `ToWire` форматирует response и
fail-fast отвергает отрицательный внутренний `long`. Application, Domain, схема
БД и внутренние `long` не меняются.

Переход строгий: tolerant reader, `string | number`, двойная сериализация и
другие union-формы для старого и нового провода запрещены. Старый и новый клиент
отдельно не выкатываются.

## Последствия

- Значения за `Number.MAX_SAFE_INTEGER` проходят response → store → UI → URL без
  округления.
- Contract generation остаётся единственным источником C#/TypeScript типов;
  ручной generated-дифф запрещён ADR-0005.
- Гейт `backend-contracts-int64` рекурсивно проверяет AST контрактов и запрещает
  публичный `format: int64`; self-test удерживает эквивалентные YAML-записи и
  границы канонического pattern.
- Contract, unit и frontend consumer-тесты защищают required/non-null форму,
  входную/выходную HTTP-границу и точность реальных UI-потоков.
- Цена решения — осознанно ломающий wire-формат и необходимость атомарного
  обновления единственного клиента вместе с API.

## Отброшенные альтернативы

- **Оставить JSON number и проверять safe integer.** Не представляет весь Int64 и
  либо теряет данные, либо искусственно сужает внутренний диапазон.
- **Принимать одновременно number и string.** Продлевает неоднозначный контракт,
  сохраняет unsafe-путь и требует tolerant reader на каждой границе.
- **Перевести Application, Domain и БД на string.** Форма транспорта протекает во
  внутренние слои без продуктовой причины и ухудшает числовые операции.
- **Выпустить backend и frontend раздельно.** Между релизами единственный клиент и
  API несовместимы; атомарный PR выбран именно для устранения этого окна.
