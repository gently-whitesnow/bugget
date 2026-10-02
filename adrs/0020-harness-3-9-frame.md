# ADR-0020: Рамка harness 3.9 — новые проверки required, потолок связности фронта по замеру

## Статус

Дата: 2026-10-02

Accepted. Дополняет ADR-0016.

Решение принял: владелец продукта.

## Контекст

Релизы harness 3.6–3.9 добавили проверки, которых нет в `.harness.json`: `complexity.typescript`,
`dependencies.typescript`, `duplication.typescript`, `functions.csharp`, `adrs.shape`. Проверка,
не названная в `policy`, не запускается. `warning-suppressions.dotnet` стояла в `advisory`.

Первый прогон с этими проверками в `required` показал:

- `adrs.shape` — 45 находок: шапка ADR не в форме harness, нестандартные H2, ADR-0005 на 2200 слов;
- `dependencies.typescript` — пять циклов каталогов, `duplication.typescript` — четыре повтора;
- `functions.csharp` — `SenderLoopAsync` на 100 строк при лимите 80;
- `warning-suppressions.dotnet` — одиннадцать выключений без объявленной причины;
- `complexity.typescript` — средняя достижимость 32.16 файла при дефолте 8.0, ядро из двух файлов.

## Решение

Внести все проверки релиза в `policy` как `required` и исправить код. Исключений два.

- Восемь проверок `architecture.sliced-dotnet.*` остаются в `off`, как в ADR-0016: без зоны
  sliced-dotnet их нечем мерить, переезд backend — отдельная задача.
- `complexity.typescript` — `required`, ядро — 0, а потолок `averageReachableFiles` равен
  замеру после правок: 32.2 вместо 8.0. Число нельзя превышать, рост связности фронта блокирует
  CI. Понижать потолок можно по мере развязки фронта, поднимать — только новым ADR.

Что сделано в коде:

- циклы разорваны переносом типов в `types.ts` и выносом контекста `ReportSidebar`;
  `useSelfHostedAutoJoin` переехал из `shared/lib` в `shared/model`;
- дубли: удалена мёртвая копия `pages/Home/ui/components`, `domain.ts` остался один
  в `shared/lib/url`, правка вложений шага и комментария — общий `model-owner-attachments.ts`;
- `SenderLoopAsync` разбит на чтение пачки, отправку и расчёт бэкоффа;
- причины выключений перенесены из `backend/.editorconfig` в
  `settings.warning-suppressions.dotnet.repositoryWide`;
- ADR приведены к форме `adrs.shape`, шаблон переименован в `.template.md`, из ADR-0005
  выделены ADR-0018 и ADR-0019.

## Последствия

- Рамка без жёлтых проверок: всё названное в `policy` блокирует, кроме явно выключенной архитектуры.
- Фронт держит текущую связность, но не стремится к 8.0: потолок — ограничитель роста,
  а не цель. Новая сквозная зависимость в `shared` или `app` упрётся в него.
- Тело ADR теперь ограничено 1000 словами и 10 строками кода; длинная спецификация живёт
  в контракте или делится на несколько ADR.

## Альтернативы

- **`complexity.typescript` в `advisory` или `off`:** число видно, но не удерживает ничего (ADR-0016).
- **Развязать фронт до 8.0 в этой же задаче:** переписывание фронта, отдельная работа.
