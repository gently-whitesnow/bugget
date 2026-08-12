---
version: alpha
name: bugget-design-system
description: "Двухтемная продуктовая дизайн-система bugget/frontend: Tailwind CSS v4 + DaisyUI v5, светлая тема по умолчанию и тёмная по prefers-color-scheme. Бренд-цвет — приглушённый изумруд #009966, одинаковый в обеих темах; вся остальная палитра переопределяется потемново. Плотный рабочий интерфейс баг-трекера, а не маркетинговая страница: узкая шкала радиусов (4–14px), границы вместо теней, отсутствие градиентов, шрифт Verdana. Ключевая идиома контролов — «прозрачный в покое, фон только на hover/focus»: селекты и автосаджесты не рисуют рамку, пока их не тронули. Иерархия строится ступенью поверхности base-100 → base-200 → base-300 и границами base-300 / base-content с альфой. Раскладка — не брейкпойнты вьюпорта, а container queries по контейнерам app-layout и app-header."

colors:
  # ── Роли светлой темы (default: true). Это имена, которыми оперируют компоненты.
  # ── В тёмной теме каждая роль разрешается в свой dark-* близнец ниже.
  base-100: "#ffffff"
  base-200: "#f7f9fa"
  base-300: "#eef2f6"
  base-content: "#0d1529"
  primary: "#009966"
  primary-content: "#ffffff"
  secondary: "#8fa0b8"
  secondary-content: "#010515"
  accent: "#71d1fe"
  accent-content: "#042e49"
  neutral: "#0d1529"
  neutral-content: "#f7f9fa"
  info: "#00bafe"
  info-content: "#eff6ff"
  success: "#00a43b"
  success-content: "#effcf3"
  warning: "#ffdc3b"
  warning-content: "#342305"
  error: "#ea003e"
  error-content: "#fceeef"

  # ── Роли тёмной темы (prefersdark: true).
  dark-base-100: "#1d232a"
  dark-base-200: "#191e24"
  dark-base-300: "#15191e"
  dark-base-content: "#ecf9ff"
  dark-primary: "#009966"
  dark-primary-content: "#edf1fe"
  dark-secondary: "#f43098"
  dark-secondary-content: "#f9e4f0"
  dark-accent: "#00d3bb"
  dark-accent-content: "#084d49"
  dark-neutral: "#09090b"
  dark-neutral-content: "#e4e4e7"
  dark-info: "#00bafe"
  dark-info-content: "#042e49"
  dark-success: "#00d390"
  dark-success-content: "#004c39"
  dark-warning: "#f0d03c"
  dark-warning-content: "#261700"
  dark-error: "#ff627d"
  dark-error-content: "#4d0218"

typography:
  display:
    fontFamily: Verdana
    fontSize: 36px
    fontWeight: 700
    lineHeight: 1.11
    letterSpacing: 0
  h1:
    fontFamily: Verdana
    fontSize: 30px
    fontWeight: 600
    lineHeight: 1.20
    letterSpacing: 0
  h2:
    fontFamily: Verdana
    fontSize: 24px
    fontWeight: 600
    lineHeight: 1.33
    letterSpacing: 0
  h3:
    fontFamily: Verdana
    fontSize: 20px
    fontWeight: 600
    lineHeight: 1.40
    letterSpacing: 0
  title:
    fontFamily: Verdana
    fontSize: 18px
    fontWeight: 500
    lineHeight: 1.56
    letterSpacing: 0
  body:
    fontFamily: Verdana
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.50
    letterSpacing: 0
  body-sm:
    fontFamily: Verdana
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.43
    letterSpacing: 0
  field-label:
    fontFamily: Verdana
    fontSize: 13px
    fontWeight: 400
    lineHeight: 1.54
    letterSpacing: 0
  caption:
    fontFamily: Verdana
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.33
    letterSpacing: 0
  button:
    fontFamily: Verdana
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.20
    letterSpacing: 0
  button-sm:
    fontFamily: Verdana
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.20
    letterSpacing: 0
  eyebrow:
    fontFamily: Verdana
    fontSize: 12px
    fontWeight: 500
    lineHeight: 1.33
    letterSpacing: 0.4px

rounded:
  sm: 4px
  md: 6px
  field: 8px
  dark-field: 4px
  box: 8px
  lg: 8px
  xl: 12px
  popup: 14px
  xxl: 16px
  selector: 32px
  dark-selector: 8px
  full: 9999px

spacing:
  xxs: 4px
  xs: 6px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px
  xxl: 32px
  section: 24px
  page-padding-inline: clamp(16px, 3cqi, 24px)
  page-padding-block: clamp(16px, 3cqi, 32px)
  header-height: 64px
  content-max: 1144px
  content-narrow-max: 800px

components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.primary-content}"
    typography: "{typography.button}"
    rounded: "{rounded.field}"
    padding: 0 16px
    height: 35px
  button-info:
    backgroundColor: "{colors.info}"
    textColor: "{colors.info-content}"
    typography: "{typography.button}"
    rounded: "{rounded.field}"
    padding: 8px 16px
  button-outline-secondary:
    backgroundColor: transparent
    textColor: "{colors.secondary}"
    typography: "{typography.button}"
    rounded: "{rounded.field}"
    padding: 8px 16px
  button-ghost:
    backgroundColor: transparent
    textColor: "{colors.base-content}"
    typography: "{typography.button}"
    rounded: "{rounded.field}"
    padding: 0 16px
  button-error:
    backgroundColor: "{colors.error}"
    textColor: "{colors.error-content}"
    typography: "{typography.button}"
    rounded: "{rounded.field}"
    padding: 0 16px
  text-input:
    backgroundColor: "{colors.base-100}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.field}"
    padding: 0 12px
    height: 42px
  text-input-focused:
    backgroundColor: "{colors.base-100}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.field}"
    height: 42px
  field-label:
    backgroundColor: transparent
    textColor: color-mix(in oklab, {colors.base-content} 72%, transparent)
    typography: "{typography.field-label}"
  select-trigger:
    backgroundColor: transparent
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.box}"
    padding: 6px 6px
  select-trigger-hover:
    backgroundColor: color-mix(in oklab, {colors.base-200} 70%, transparent)
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.box}"
    padding: 6px 6px
  select-trigger-focused:
    backgroundColor: transparent
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.box}"
    padding: 6px 6px
  select-trigger-compact:
    backgroundColor: transparent
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.box}"
    padding: 2px 4px
  select-menu:
    backgroundColor: "{colors.base-100}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.popup}"
    padding: 6px
  select-menu-item:
    backgroundColor: transparent
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.box}"
    padding: 8px 16px
  select-menu-item-selected:
    backgroundColor: "{colors.base-200}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.box}"
    padding: 8px 16px
  select-placeholder:
    backgroundColor: transparent
    textColor: color-mix(in oklab, {colors.base-content} 50%, transparent)
    typography: "{typography.body-sm}"
  app-header:
    backgroundColor: "{colors.base-200}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    padding: 0 16px
    height: 64px
  sidebar-container:
    backgroundColor: "{colors.base-100}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.sm}"
    padding: 32px 24px
  section-header-chip:
    backgroundColor: "{colors.base-200}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.lg}"
    padding: 8px
  section-header-chip-hover:
    backgroundColor: "{colors.base-300}"
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.lg}"
    padding: 8px
  card:
    backgroundColor: "{colors.base-100}"
    textColor: "{colors.base-content}"
    typography: "{typography.body}"
    rounded: "{rounded.xl}"
    padding: 16px
  callout-error:
    backgroundColor: color-mix(in oklab, {colors.error} 10%, transparent)
    textColor: "{colors.error}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.lg}"
    padding: 12px
  callout-info:
    backgroundColor: color-mix(in oklab, {colors.info} 15%, transparent)
    textColor: "{colors.base-content}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.lg}"
    padding: 12px
  status-badge:
    backgroundColor: color-mix(in oklab, {colors.info} 20%, transparent)
    textColor: "{colors.base-content}"
    typography: "{typography.caption}"
    rounded: "{rounded.full}"
    padding: 2px 8px
  avatar:
    backgroundColor: "{colors.base-200}"
    rounded: "{rounded.full}"
    size: 32px
---

## Overview

`bugget/frontend` — интерфейс баг-трекера, а не лендинг. Система оптимизирована под плотный
рабочий экран: репорт с двумя сайдбарами, лента багов, комментарии, фильтры. Отсюда все
решения ниже.

### Откуда формат

Структура файла — не наша выдумка: это открытая спецификация
[DESIGN.md](https://stitch.withgoogle.com/docs/design-md/specification). Формат придумали
в Google Labs для своего инструмента Stitch, а потом открыли; мы **сам Stitch не
используем и зависимости от него не имеем** — нам нужен только формат.

Смысл чужого формата в том, что его понимают чужие инструменты. Практически это уже даёт
две вещи: файл проверяется линтером (`npx @google/design.md lint DESIGN.md`), и любой
агент, знающий формат, находит в нём нужные разделы без объяснений. Ради второго
заголовки разделов оставлены на английском — это канонические имена спецификации. Проза —
русская, как и вся остальная документация репозитория.

Спецификация рассчитана на одну тему и на маркетинговый сайт, а у нас две темы и
продуктовое приложение. Где пришлось от неё отступить — перечислено в Known Gaps.

Технически система — **Tailwind CSS v4 + DaisyUI v5** с двумя кастомными темами. Светлая
объявлена `default: true`, тёмная — `prefersdark: true`, то есть переключение идёт по
системной настройке пользователя, ручного тумблера тем в продукте нет. Источник значений —
[`src/shared/styles/tailwind.css`](src/shared/styles/tailwind.css) (темы, override примитивов
DaisyUI) и [`src/app/global.css`](src/app/global.css) (reset, шрифт, раскладка).

Порядок подключения важен и легко ломается: `global.css` импортируется первым — из
[`src/app/main.tsx`](src/app/main.tsx), а `tailwind.css` — позже, из
[`src/app/App.tsx`](src/app/App.tsx). Значит reset, шрифт и токены раскладки применяются
до утилит Tailwind и классов DaisyUI, и последние их перебивают. Импорт из другого места
меняет каскад.

Палитра задана в **oklch**; hex в front matter — это её пересчёт для машин, которые не
читают oklch. При правках всегда правится oklch в `tailwind.css`, hex здесь — производная.

Бренд-цвет — приглушённый изумруд `{colors.primary}` (#009966), единственный токен,
**одинаковый в обеих темах**. Он держит идентичность, пока вокруг него переворачивается вся
остальная палитра.

Иерархия строится не тенями, а **ступенью поверхности**: `{colors.base-100}` →
`{colors.base-200}` → `{colors.base-300}` плюс границы `{colors.base-300}` и
`{colors.base-content}` с альфой 15–30%. Теней в системе три штуки, и все — на всплывающих
слоях.

Главная поведенческая идиома — **«прозрачный в покое»**. Селекты, автосаджесты и
инлайн-контролы не рисуют ни рамки, ни фона, пока на них не навели курсор или не поставили
фокус. Экран репорта плотно набит контролами; если каждый обвести полем ввода, интерфейс
превратится в решётку. Фон появляется только на `hover` / `focus-within` — и он же служит
единственным индикатором интерактивности.

Раскладка — **container queries**, а не брейкпойнты вьюпорта. Контейнеры `app-layout` и
`app-header` объявлены явно, сайдбары и действия хедера реагируют на ширину своего
контейнера. Это нужно потому, что одна и та же страница живёт и на широком мониторе с двумя
сайдбарами, и внутри узкой колонки.

**Ключевые характеристики:**

- Двухтемная система с единственным сквозным брендовым токеном (`{colors.primary}` #009966).
- Плотный продуктовый ритм: доминирующий отступ — `{spacing.sm}` 8px, не 24px.
- Узкая шкала радиусов: 4 → 6 → 8 → 12 → 14px. Больше 16px не используется нигде.
- Границы вместо теней. Тень только у popup-слоёв и хедера.
- Шрифт — системный стек с Verdana во главе. Своей типографики у продукта нет.
- Контролы прозрачны в покое; фон — сигнал взаимодействия.
- `1.5` — базовый ритм внутреннего паддинга компактных контролов (`p-1.5` = 6px).
- Никаких градиентов, никакого glassmorphism, никаких декоративных заливок.

## Colors

> Источник: [`src/shared/styles/tailwind.css`](src/shared/styles/tailwind.css), блоки
> `@plugin "daisyui/theme"`. Имена ролей совпадают с CSS-переменными DaisyUI
> (`--color-primary` и т.д.), поэтому `{colors.primary}` читается напрямую как
> `bg-primary` / `text-primary` в разметке.

### Поверхности

- **Base 100** (`{colors.base-100}` #ffffff → `{colors.dark-base-100}` #1d232a): фон страницы
  и фон карточек. В светлой теме — чистый белый; в тёмной — тёплый графит, **не** чёрный.
- **Base 200** (`{colors.base-200}` #f7f9fa → `{colors.dark-base-200}` #191e24): ступень
  выше — хедер, чипы, hover-фон контролов, выбранная опция меню.
- **Base 300** (`{colors.base-300}` #eef2f6 → `{colors.dark-base-300}` #15191e): третья
  ступень и основной цвет границ (`border-base-300` — 45 употреблений, самый частый бордер).
- **Base Content** (`{colors.base-content}` #0d1529 → `{colors.dark-base-content}` #ecf9ff):
  весь текст. Градации задаются альфой, а не отдельными токенами (см. ниже).

Заметь инверсию: в светлой теме ступени идут **вниз** по светлоте (100 → 300 темнеют),
в тёмной — тоже вниз (100 → 300 темнеют). То есть `base-300` всегда контрастнее фона в
обе стороны, и `border-base-300` работает без развилок по теме.

### Текст и его градации

Отдельных токенов «secondary text» нет — используется `{colors.base-content}` с альфой.
Фактически закрепившаяся шкала:

| Утилита | Роль |
| --- | --- |
| `text-base-content` | Основной текст, заголовки |
| `text-base-content/80` | Слегка приглушённый текст внутри плотных блоков |
| `text-base-content/70` | Вторичный текст, подписи к полям |
| `text-base-content/60` | Метаданные: авторы, даты, счётчики |
| `text-base-content/50` | Плейсхолдеры, неактивные подсказки |
| `text-base-content/40` | Иконки-заглушки, disabled |

`.field-label` и `.label-text` зашиты в `@layer components` на 72% — это отдельная точка,
не совпадающая ни с 70, ни с 80.

### Бренд и акцент

- **Primary** (`{colors.primary}` #009966, идентичен в тёмной теме): бренд. Основные CTA,
  активные состояния, кольцо фокуса (`ring-primary/25`).
- **Secondary** (`{colors.secondary}` #8fa0b8 → `{colors.dark-secondary}` #f43098): в светлой
  теме это нейтральный серо-синий, в тёмной — насыщенная маджента. Роли расходятся; сейчас
  secondary используется почти исключительно как `btn-outline btn-secondary` на кнопке
  «Отменить».
- **Accent** (`{colors.accent}` #71d1fe → `{colors.dark-accent}` #00d3bb): практически не
  используется (1 употребление `bg-accent`, 1 — `text-accent`). Держится в теме про запас.

### Семантика

- **Info** (`{colors.info}` #00bafe, идентичен в тёмной теме): информационные блоки,
  подсветка, и — исторически — кнопка «Сохранить» (`btn btn-info`).
- **Success** (`{colors.success}` #00a43b → `{colors.dark-success}` #00d390): успешные статусы.
- **Warning** (`{colors.warning}` #ffdc3b → `{colors.dark-warning}` #f0d03c): предупреждения,
  «ожидает проверки».
- **Error** (`{colors.error}` #ea003e → `{colors.dark-error}` #ff627d): ошибки, деструктивные
  действия, невалидные поля.

Семантические цвета в продукте почти всегда идут **с альфой на фоне и в полную силу на
тексте**: `bg-error/10` + `text-error` + `border-error/30`. Плашки семантики на сплошной
заливке — редкость и требуют осознанного решения.

## Typography

### Семейство

```css
font-family: Verdana, "Dejavu Sans", Geneva, Tahoma, sans-serif;
```

Собственного шрифта у продукта нет, веб-шрифты не подключаются. Verdana выбран за широкие
знаки и высокую читаемость на плотном экране; `Dejavu Sans` — Linux-эквивалент,
`Geneva` / `Tahoma` — исторические фолбэки.

Практическое следствие: **Verdana шире почти всех гротесков**. Строка на 14px занимает
заметно больше места, чем в Inter или системном стеке. Не переносить сюда межбуквенные
интервалы и размеры из макетов, свёрстанных под Inter, — поедет.

Моноширинного токена в системе нет; код и `id` рендерятся тем же стеком.

### Иерархия

Шкала — дефолтная тайлвиндовская, свою не заводили. Ниже — то, что реально используется,
с частотой употребления.

| Токен | Утилита | Размер | Вес | Интерлиньяж | Употребление |
| --- | --- | --- | --- | --- | --- |
| `{typography.display}` | `text-4xl` | 36px | 700 | 1.11 | 1 место — экран входа |
| `{typography.h1}` | `text-3xl` | 30px | 600 | 1.20 | Заголовки страниц (4) |
| `{typography.h2}` | `text-2xl` | 24px | 600 | 1.33 | Заголовки секций (12) |
| `{typography.h3}` | `text-xl` | 20px | 600 | 1.40 | Редко (1) |
| `{typography.title}` | `text-lg` | 18px | 500 | 1.56 | Заголовки карточек (15) |
| `{typography.body}` | `text-base` | 16px | 400 | 1.50 | Основной текст (212) |
| `{typography.body-sm}` | `text-sm` | 14px | 400 | 1.43 | Контролы, меню, плотные блоки (109) |
| `{typography.field-label}` | `.field-label` | 13px | 400 | 1.54 | Подписи к полям формы |
| `{typography.caption}` | `text-xs` | 12px | 400 | 1.33 | Метаданные, счётчики (75) |
| `{typography.button}` | `btn` | 14px | 400 | 1.20 | Кнопки обычного размера |
| `{typography.button-sm}` | `btn-sm` | 12px | 400 | 1.20 | Кнопки в плотных местах (39) |
| `{typography.eyebrow}` | `text-xs tracking-wide` | 12px | 500 | 1.33 | Надзаголовки секций (13) |

### Принципы

- **Три размера несут 95% интерфейса**: `{typography.body}` 16px, `{typography.body-sm}` 14px,
  `{typography.caption}` 12px. Всё остальное — акценты.
- **14px — размер контролов.** Плейсхолдеры, значения селектов, строки меню, подписи —
  все `text-sm`. Плейсхолдер не должен быть мельче поля.
- **Веса ограничены четырьмя**: 400 (текст), 500 `font-medium` (акцент в тексте, метки),
  600 `font-semibold` (заголовки), 700 `font-bold` (только display).
- **Межбуквенный интервал не трогаем.** Единственное исключение — `tracking-wide` на
  надзаголовках-эйбрау; отрицательного трекинга в системе нет вообще.
- **`.field-label` — не `<label>` в вакууме, а класс.** Он задаёт 13px/72% и объявлен в
  `@layer components`, поэтому переопределяется утилитами без `!important`.

## Layout

### Шкала отступов

База — 4px (Tailwind). Реально используемый набор, по убыванию частоты:

| Токен | Значение | Утилита | Где |
| --- | --- | --- | --- |
| `{spacing.sm}` | 8px | `gap-2`, `p-2` | Доминирующий шаг: иконка↔текст, строки списков |
| `{spacing.xxs}` | 4px | `gap-1`, `p-1` | Внутри чипов, между иконкой и счётчиком |
| `{spacing.md}` | 12px | `gap-3`, `px-3` | Между группами контролов |
| `{spacing.lg}` | 16px | `gap-4`, `p-4` | Паддинг карточек, зазор блоков |
| `{spacing.xs}` | 6px | `p-1.5`, `gap-1.5` | **Ритм компактных контролов** |
| `{spacing.xl}` | 24px | `px-6`, `gap-6` | Внутренний паддинг сайдбара |
| `{spacing.xxl}` | 32px | `py-8` | Вертикальный паддинг сайдбара |

Отдельно про `{spacing.xs}` 6px: `p-1.5` — это **осознанный ритм триггеров селектов и
автосаджестов**, а не случайное значение. Он делает контрол визуально «тесным по контенту»,
что и требуется для прозрачных в покое элементов. Не заменять на `p-1` или `p-2` без причины.

### Токены раскладки

```css
:root {
  --layout-content-max: 71.5rem;          /* 1144px */
  --layout-content-narrow-max: 50rem;     /* 800px */
  --layout-page-padding-block: clamp(1rem, 3cqi, 2rem);
  --layout-page-padding-inline: clamp(1rem, 3cqi, 1.5rem);
  --layout-section-gap: 1.5rem;           /* 24px */
}
```

Высота хедера — `4rem` (64px), вынесена в `src/shared/config/const.ts` как `headerHeight`,
потому что от неё считается высота липких сайдбаров.

### Оболочка страницы

Все роутовые страницы живут внутри общей оболочки `.app-main`. Она уже задаёт
внешний паддинг страницы, максимальную ширину и `gap: 1rem` между секциями.

- **Не добавляй `px-*` / `py-*` / `p-*` на корневой обёртке страницы** — получишь двойной
  паддинг. Оболочка это уже сделала.
- Для узких центрированных страниц (формы настроек) — `.layout-content-narrow`, 800px.

### Сетка приложения

Одна грид-раскладка уровня приложения с именованными областями `main` / `left` / `right`.
Контейнер `app-layout` объявлен на `.app-layout-scroll`.

```css
.app-layout-scroll { container: app-layout / inline-size; }

.app-layout-grid {
  display: grid;
  grid-template-areas: "main" "left" "right";
  grid-template-columns: minmax(0, 1fr);
}
```

Порядок областей по умолчанию — **сначала main**: на узком контейнере контент важнее
сайдбаров, и сайдбары уходят под него. Раскладка в строку включается на 54rem (один сайдбар)
и 74rem (оба), ширина сайдбара — `clamp(15rem, 20–24cqi, 18.75rem)`.

Сложенные сайдбары обязаны занимать всю доступную ширину, включая внутренний липкий слой
(`.sidebar-container`, `.sidebar-container-inner` → `width: 100%`).

### Утилиты адаптивной раскладки

Вместо ручных брейкпойнтов в `global.css` лежат четыре готовых примитива:
`.responsive-inline` (flex-wrap с настраиваемым минимумом элемента),
`.responsive-fill` (растущая колонка), `.responsive-sidebar` (сжимающаяся колонка),
`.responsive-card-grid` (`auto-fit` / `minmax`, карточка от 18rem). Каждый параметризуется
CSS-переменной — сначала ищи подходящий, потом пиши свой медиазапрос.

### Философия воздуха

Воздух здесь дешёвый по вертикали и дорогой по горизонтали. Между секциями —
`{spacing.section}` 24px, внутри секции — `{spacing.lg}` 16px, внутри строки —
`{spacing.sm}` 8px. Больших пустых зон нет: это рабочий инструмент, экран должен
быть насыщен, но не тесен.

## Elevation & Depth

Глубина держится **границами и ступенью поверхности**. Тени — исключение, а не система.

| Уровень | Приём | Где |
| --- | --- | --- |
| 0 (плоский) | Без границы и тени | Текст, инлайн-контролы в покое |
| 1 (ступень) | `bg-base-200` на `bg-base-100` | Чипы, hover-фон, выбранная опция меню |
| 2 (граница) | 1px `{colors.base-300}` | Карточки, разделители, таблицы |
| 3 (сильная граница) | 1px `base-content/15` … `/30` | Popup-слои, контур сайдбара |
| 4 (хедер) | `bg-base-200` + `shadow-sm` | `.app-header`, `z-[200]` |
| 5 (popup) | `bg-base-100` + 1px `base-content/15` + `shadow-xl` | Меню селектов, автосаджест |
| Фокус | `ring-2 ring-primary/25`, `outline: none` | Триггеры селектов |

Итого в коде: `shadow-lg` (13), `shadow-xl` (11), `shadow-sm` (7). Всё остальное — границы.

Ключевое правило popup-слоёв: **меню визуально отделено от триггера**. Триггер прозрачен и
без рамки, меню — с фоном `{colors.base-100}`, границей и `shadow-xl`, отступом `mt-1`
(4px) и радиусом `{rounded.popup}` 14px, который заметно мягче радиуса самого триггера
(`{rounded.box}` 8px). Разница радиусов — намеренная: она читается как «другой слой».

### Фокус и доступность

- Кольцо фокуса — `focus-visible:ring-2 ring-primary/25` при `focus-visible:outline-none`.
- Нативный `outline` полей формы снят в `@layer components`, но **цвет границы при фокусе
  не меняется** — фокус на полях ввода сейчас читается слабо (см. Known Gaps).
- `prefers-reduced-motion: reduce` обрезает все анимации и переходы до 0.01ms глобально.

### Движение

Стандартная длительность перехода — `duration-200` (14 употреблений), она же у
скрытия/показа хедера и смены высоты сайдбара. `duration-300` и `duration-500` —
единичные случаи для появления контента. Переходят **цвет и трансформация**, не размеры.

## Shapes

### Шкала радиусов

Радиусы задаются двумя источниками: токены DaisyUI (`--radius-*`, зависят от темы) и
утилиты Tailwind.

| Токен | Значение | Утилита | Где |
| --- | --- | --- | --- |
| `{rounded.sm}` | 4px | `rounded-sm` | Контур сайдбара (`rounded-l-sm` / `rounded-r-sm`) |
| `{rounded.md}` | 6px | `rounded-md` | Мелкие элементы, скелетоны (33) |
| `{rounded.box}` | 8px | `rounded-box` | **Триггеры селектов и строки опций** (20) |
| `{rounded.field}` | 8px / 4px в тёмной | `rounded-field`, `btn`, `input` | Кнопки, поля ввода |
| `{rounded.lg}` | 8px | `rounded-lg` | Чипы, плашки, коллауты (17) |
| `{rounded.xl}` | 12px | `rounded-xl` | Карточки (6) |
| `{rounded.popup}` | 14px | `rounded-[14px]` | **Только popup-поверхности** (4) |
| `{rounded.xxl}` | 16px | `rounded-2xl` | Крупные панели (2) |
| `{rounded.selector}` | 32px / 8px в тёмной | `--radius-selector` | Чекбоксы, тогглы, радио |
| `{rounded.full}` | 9999px | `rounded-full` | Аватары, иконки-кружки, бейджи (20) |

Потолок шкалы — 16px. Пилюльная форма (`{rounded.full}`) зарезервирована за круглыми
элементами: аватарами, иконками в кружке, счётчиками. Кнопки-пилюли системе чужды.

`{rounded.popup}` 14px не входит ни в одну шкалу намеренно — это единственное значение, по
которому popup-слой отличается от всего остального. Если видишь `rounded-[14px]` — это
всплывающая поверхность.

### Границы

`--border: 1.5px` в обеих темах — толщина рамки компонентов DaisyUI (кнопки, поля).
Разделители и контуры карточек рисуются обычным 1px `border-base-300`.

Спецификация DESIGN.md не знает под-токена `border`, поэтому границы компонентов не влезли
во front matter и живут здесь:

| Компонент | Граница |
| --- | --- |
| `button-outline-secondary` | 1.5px `{colors.secondary}` |
| `text-input`, `text-input-focused` | 1.5px `base-content` 15% (при фокусе **не меняется**) |
| `select-trigger` | нет — прозрачен в покое |
| `select-trigger-focused` | `ring-2` `primary` 25%, `outline: none` |
| `select-menu` | 1px `base-content` 15% |
| `sidebar-container` | 1px `base-content` 30%, только со стороны контента |
| `card` | 1px `{colors.base-300}` |
| `callout-error` | 1px `error` 30% |
| `callout-info` | 1px `info` 30% |

### Геометрия изображений и аватаров

- Аватары — `{rounded.full}`, размер задаётся в шагах по 0.25rem (проп `width`, по умолчанию
  8 → 32px).
- Пока аватар грузится, на его месте стоит `RoundedSkeleton` того же размера — сдвига
  раскладки нет.
- Превью вложений и ссылок используют радиус карточки, не пилюлю.

## Components

> Полные исходники — `src/shared/ui/`. Ниже — контракт: что компонент обязан выглядеть
> так, а не иначе.

### Кнопки

**`button-primary`** — брендовый CTA.

- `btn btn-primary`. Фон `{colors.primary}`, текст `{colors.primary-content}`,
  тип `{typography.button}`, радиус `{rounded.field}`.
- Высота считается DaisyUI как `--size-field × 10`: **35px в светлой теме, 40px в тёмной**
  (`--size-field` = 0.21875rem / 0.25rem). Это расхождение тем, а не задумка — см. Known Gaps.

**`button-info`** — кнопка «Сохранить» (`SaveButton`).

- `btn btn-info px-4 py-2`. Фон `{colors.info}`.
- Заблокирована, пока `isChanged === false`. В состоянии загрузки текст заменяется на
  `<span className="loading loading-spinner" />`, ширина кнопки не скачет.

**`button-outline-secondary`** — кнопка «Отменить» (`CancelButton`).

- `btn btn-outline btn-secondary px-4 py-2`. Прозрачный фон, рамка 1.5px.
- Всегда идёт в паре с `button-info` и блокируется по тому же `isChanged`.

**`button-ghost`** — иконочные и низкоприоритетные действия (31 употребление).

- `btn btn-ghost`, часто с `btn-sm` / `btn-square` / `btn-circle`.

**`button-error`** — деструктивные действия. `btn btn-error`.

Размеры: `btn-sm` (39) — рабочая лошадка в плотных местах, `btn-xs` (18) — внутри строк
списков, размер по умолчанию — на формах.

### Поля ввода

**`text-input`** / **`text-input-focused`**

- Высота **42px** (`min-height: 2.625rem`), зашита в `@layer components` для
  `.input`, `.select`, `.file-input` и их `-sm` вариантов. То есть `input-sm` **не делает
  поле ниже** — размер контролов в системе один.
- Граница — `color-mix(in oklab, base-content 15%, transparent)`, фон `{colors.base-100}`.
- При фокусе граница **не меняется**, нативный `outline` снят.

**`field-label`** — подпись над контролом.

- Класс `.field-label` (он же `.label-text`): 13px, вес 400, цвет `base-content` на 72%.

Правило: **плейсхолдер не заменяет подпись.** Для фильтров сайдбара, фильтров поиска, форм
и автосаджестов подпись обязательна. Плейсхолдер — вторичная подсказка, не имя поля.
Опустить подпись можно только в плотном инлайновом контексте (панель хедера), где смысл уже
задан окружением, — и там контролу всё равно нужно доступное имя через ARIA.

```tsx
<div className="flex flex-col gap-1.5">
  <div className="field-label">Участник</div>
  <Autosuggest placeholder="Начните вводить" />
</div>
```

### Селекты

Примитивы: `SelectTrigger`, `SelectFieldLayout`, `SelectMenu`, `SelectMenuItem`
(`src/shared/ui/SelectPrimitives/`).

**`select-trigger`** — прозрачный в покое.

- `group inline-flex cursor-pointer rounded-box p-1.5 transition-colors duration-200`.
- Без рамки, без фона. Радиус `{rounded.box}` 8px, паддинг `{spacing.xs}` 6px по кругу.
- **Без шеврона.** Стрелки вниз в стандартных триггерах нет: аффорданс несут курсор,
  подпись и анимация меню.
- Высота **не фиксируется**. Триггер сжимается по контенту — это не поле ввода.
- Ширина управляется пропом `fullWidth`, не семантическим вариантом.

**`select-trigger-hover`** — фон `bg-base-200/70` появляется на `hover` и `focus-within`.
Это единственный визуальный сигнал интерактивности.

**`select-trigger-focused`** — `focus-visible:ring-2 ring-primary/25`, `outline: none`.

**`select-trigger-compact`** — исключение для `BugHeader`: `px-1 py-0.5` на триггере плюс
`bg-transparent px-2.5 py-1 group-hover:bg-base-200/70` на контенте. Осознанно теснее
базового ритма, потому что стоит в одну строку с заголовком бага.

**`select-menu`** — всплывающий слой.

- `absolute left-0 top-full z-20 mt-1 w-full rounded-[14px] border border-base-content/15 bg-base-100 p-1.5 shadow-xl`.
- Радиус 14px, отступ от триггера 4px, `z-20`.

**`select-menu-item`** — строка опции.

- `w-full cursor-pointer rounded-box px-4 py-2 text-left text-sm transition-colors hover:bg-base-200`.
- Выбранная — `bg-base-200`.
- `cursor-pointer` обязателен. Ховер меняет **только цвет**: никакого сдвига раскладки.
- Длинные подписи в меню не обрезаются, если полная читаемость важна для смысла.

**`select-placeholder`** — `text-sm text-base-content/50`, тот же размер, что у значения.

**Владение стилями.** Общие примитивы могут отдавать наружу `triggerClassName`,
`triggerContentClassName`, `menuClassName`. Доменные обёртки — **не могут**, пока у них не
появилось реального второго визуального режима. Если у компонента в продукте один стабильный
вид, этот вид зашивается внутрь.

### Селект статуса

- В сайдбарах и фильтрах закрытый триггер статуса **остаётся нейтральным**. Семантический
  цвет живёт в опциях меню, не в закрытом триггере.
- `ReportStatusSelect` использует один и тот же паттерн во всех местах появления; разница
  между одиночным и множественным выбором — поведенческая, не визуальная. Стили —
  внутренние, `triggerClassName` наружу не выставляется.
- В `BugHeader` вспомогательные состояния вроде «ожидает проверки» рендерятся **отдельным
  бейджем рядом** с селектом, а не внутри выбранного значения и не тултипом. Закрытый
  триггер от этого не должен расти в высоту.

### Множественный выбор

- Тот же корпус триггера: прозрачный в покое, фон на hover/focus.
- Сброс/очистка — компактное действие **внутри** триггера справа.
- Выбранные чипы рендерятся **под** триггером, не внутри него: иначе триггер прыгает по
  высоте при каждом выборе.
- Мультиселект статусов в фильтрах поиска совпадает по отступам и ховеру с сайдбарным.

### Автосаджест

Тот же язык, что у селектов, — это принципиально, а не совпадение.

- Закрытый триггер: прозрачный, фон только на ховере, тот же ритм паддинга.
- Открытое состояние: фон hover/focus несёт **обёртка**, сам `input` остаётся прозрачным и
  без рамки.
- Popup — `{rounded.popup}` 14px, `border-base-content/15`, `bg-base-100`,
  `shadow-lg`/`shadow-xl`, `mt-1`.
- Строки подсказок — `{rounded.box}`, обязательный `cursor-pointer`, та же подсветка, что у
  опций селекта.
- Кнопка очистки (`.autosuggest-clear`) — абсолютная, 24×24, круглая, `opacity: .5` → `.8`
  на ховере.

### Хедер

**`app-header`** (`HeaderContainer`)

- Высота 64px (`headerHeight`), `bg-base-200`, `shadow-sm`, `px-4`, `z-[200]`.
- Объявляет контейнер: `container: app-header / inline-size`.
- Скрывается сдвигом `-translate-y-full` с `duration-200`, а не `display: none`.

**Триггеры мобильных шторок живут в области действий хедера** — не плавающей кнопкой и не
липким элементом поверх контента. Визуально они не отличаются от прочих иконочных кнопок
хедера и показываются только на той ширине контейнера, где сайдбар превращается в шторку:

```css
.header-sidebar-action { display: none; flex-shrink: 0; }

@container app-header (max-width: 53.999rem) {
  .header-sidebar-action { display: flex; }
}
```

Состояние открытости шторки шарится между триггером в хедере и самой шторкой через
локальный провайдер/контекст. Escape-закрытие, блокировка скролла body, клик по подложке и
`aria-expanded` / `aria-modal` живут рядом с этим состоянием, а не размазаны по компонентам.

### Сайдбар

**`sidebar-container`** (`SidebarContainer`)

- Липкий внутренний слой высотой `calc(100vh - 4rem)` (или `100vh`, если хедер скрыт),
  `px-6 py-8`, `flex-col justify-between gap-4`.
- Граница со стороны контента: `border-r rounded-r-sm` для левого, `border-l rounded-l-sm`
  для правого, цвет `border-base-content/30`.
- При складывании сайдбаров под контент **двойная горизонтальная граница убирается**: боковые
  рамки и радиусы обнуляются, остаётся одна разделительная линия сверху.

```css
@container app-layout (max-width: 73.999rem) {
  .app-layout-grid--left-right .app-layout-left .sidebar-container,
  .app-layout-grid--left-right .app-layout-right .sidebar-container {
    border-inline-width: 0;
    border-radius: 0;
    border-block-start-width: 1px;
  }
}
```

### Чипы, плашки, бейджи

**`section-header-chip`** (`SectionHeaderChip`) — заголовок секции со счётчиком.

- `flex items-center gap-2 p-2 bg-base-200 rounded-lg w-fit mb-2`.
- Иконка — в кружке 20×20 `rounded-full bg-info/20`.
- Подпись — `text-sm font-medium`, с русской плюрализацией (zero/one/few/many).
- Кликабельный — `cursor-pointer hover:bg-base-300 transition-colors`;
  заблокированный — `opacity-50 cursor-not-allowed`.

**`card`** — контентная карточка (DaisyUI `card` / `card-body` / `card-title`).

- Фон `{colors.base-100}`, граница 1px `{colors.base-300}`, радиус `{rounded.xl}` 12px,
  паддинг `{spacing.lg}` 16px. Без тени.

**`callout-error`** / **`callout-info`** — семантические плашки.

- Фон семантики с альфой 5–20%, граница той же семантики 25–50%, текст — в полную силу.
- Радиус `{rounded.lg}` 8px.

**`status-badge`** — `{rounded.full}`, `{typography.caption}`, семантический фон с альфой.

**`StatusIndicator`** — иконка 16×16 в семантическом цвете + `text-sm` подпись,
`space-x-2`. Для неизвестного статуса — `text-base-content/50`, без иконки.

### Аватар

**`avatar`** (`Avatar`) — `{rounded.full}`, размер `width × 0.25rem` (по умолчанию 32px),
до загрузки — `RoundedSkeleton` того же размера.

### Иконки

**Lucide React** (`lucide-react`), единственная библиотека иконок.

```tsx
import { Bug, Settings } from "lucide-react";

<Bug className="w-4 h-4" />   // рядом с text-sm
<Bug className="w-5 h-5" />   // рядом с text-base
```

Размер иконки привязан к размеру соседнего текста: 16px к `text-sm`, 20px к `text-base`.
Цвет — от семантики контекста, не задаётся отдельно.

## Do's and Don'ts

### Do

- Строй иерархию ступенью поверхности `base-100 → base-200 → base-300` и границами.
- Оставляй триггеры селектов и автосаджестов **прозрачными в покое**; фон — только на
  `hover` / `focus-within`.
- Держи компактные контролы на ритме `p-1.5` (6px).
- Ставь видимую подпись над контролом в сайдбарах, фильтрах и формах.
- Отделяй popup от триггера: `{rounded.popup}` 14px + граница + `shadow-xl` + `mt-1`.
- Давай семантику как «фон с альфой + текст в полную силу»: `bg-error/10 text-error`.
- Задавай градации текста альфой `base-content`, а не новыми цветовыми токенами.
- Используй `.app-main` для внешнего паддинга страницы и ничего не добавляй поверх.
- Предпочитай container queries (`cqi`, `clamp`, `auto-fit`) брейкпойнтам вьюпорта.
- Держи `cursor-pointer` на каждой кликабельной строке меню и подсказки.
- Правь цвета в oklch в `tailwind.css`; hex в этом файле — производная.
- Зашивай стабильный вид внутрь доменного компонента, а не выставляй `triggerClassName`.

### Don't

- Не рисуй шеврон в стандартном триггере селекта.
- Не задавай триггеру селекта фиксированную высоту — он сжимается по контенту.
- Не тонируй закрытый триггер статуса семантическим цветом; цвет живёт в опциях меню.
- Не клади выбранные чипы мультиселекта внутрь триггера — он начнёт прыгать по высоте.
- Не добавляй `px-*` / `py-*` / `p-*` на корневую обёртку роутовой страницы.
- Не выставляй `triggerClassName` у доменных обёрток вроде `ReportStatusSelect`.
- Не заменяй подпись поля плейсхолдером и не делай плейсхолдер мельче `text-sm`.
- Не подключай веб-шрифты и не переноси метрики макетов, свёрстанных под Inter.
- Не добавляй градиенты, glassmorphism, свечения и декоративные заливки.
- Не превышай радиус 16px и не делай кнопки-пилюли.
- Не заменяй границы тенями: `shadow-*` — только у popup-слоёв и хедера.
- Не двигай раскладку на ховере: меняется цвет, не размеры.
- Не заводи новый цветовой токен, пока не исчерпал существующие 10 семантических ролей.
- Не полагайся на `input-sm` для уменьшения высоты поля — она зафиксирована на 42px.

## Responsive Behavior

Система адаптируется **по ширине контейнера**, а не вьюпорта. Медиазапросы к вьюпорту
в раскладке не используются.

### Контрольные точки контейнеров

| Контейнер | Ширина | Что меняется |
| --- | --- | --- |
| `app-header` | < 53.999rem (864px) | Появляется `.header-sidebar-action` — триггер шторки |
| `app-layout` | ≥ 54rem (864px) | Один сайдбар встаёт в строку: `"left main"` или `"main right"` |
| `app-layout` | < 73.999rem (1184px) | У сложенных сайдбаров снимаются боковые рамки и радиусы |
| `app-layout` | ≥ 74rem (1184px) | Оба сайдбара в строку: `"left main right"` |

Ширина сайдбара в строке — `minmax(14rem, clamp(15rem, 20–24cqi, 18.75rem))`.

### Стратегия сворачивания

- **Сайдбары**: строка → стопка **под** контентом (`main` идёт первым в grid-areas).
  На узком контейнере они превращаются в шторки, триггеры уезжают в хедер.
- **Сложенные сайдбары** занимают всю ширину, включая внутренний липкий слой.
- **Сетки карточек**: `.responsive-card-grid` через `auto-fit` / `minmax(18rem, 1fr)` —
  количество колонок считает браузер, ручных ступеней нет.
- **Отступы страницы**: `clamp(1rem, 3cqi, 1.5rem)` по горизонтали и
  `clamp(1rem, 3cqi, 2rem)` по вертикали — непрерывное масштабирование, не ступени.

### Тач-цели

- Поля ввода — 42px, выше минимальных 44px не поднимаются, но и не опускаются ниже.
- Кнопки размера по умолчанию — 35px (светлая) / 40px (тёмная); `btn-sm` — 28px / 32px.
  На тач-устройствах `btn-sm` и `btn-xs` в одиночных действиях не годятся — только внутри
  строк списка, где вся строка кликабельна.
- Триггеры селектов сжимаются по контенту; в тач-контексте им нужен внешний паддинг.

### Движение и предпочтения

`prefers-reduced-motion: reduce` глобально обрезает анимации и переходы до 0.01ms.
Ничего дополнительно оборачивать не нужно.

### Изображения

Аватары держат квадрат и не кропятся; на время загрузки место занимает скелетон того же
размера, чтобы не было сдвига раскладки.

## Iteration Guide

1. Правь **один компонент за раз** и называй его именем токена из `components:`.
2. Перед новой секцией реши, на какой ступени поверхности она живёт: `base-100` (фон
   страницы), `base-200` (поднятая), `base-300` (граница/третий уровень).
3. Текст по умолчанию — `{typography.body}` 16px; всё, что внутри контролов, — 14px.
4. Новый цвет заводи только после того, как убедился, что ни одна из 10 семантических
   ролей не подходит. Правь oklch в `tailwind.css`, потом обнови hex здесь.
5. Новый вариант компонента — **отдельная запись** в `components:`, а не проп-развилка
   внутри существующей.
6. Прежде чем добавить проп стилизации доменному компоненту, проверь, есть ли реальный
   второй визуальный режим. Нет — зашей стиль внутрь.
7. Проверяй обе темы. `{colors.secondary}` и `{colors.accent}` меняют роль между темами
   сильнее всего — на них ломается чаще всего.
8. После правок прогони проверки фронтенда: `npm run lint`, `npm run build`,
   `npx steiger ./src --fail-on-warnings`.
9. Провалидируй сам этот файл официальным линтером спецификации:
   `npx @google/design.md lint DESIGN.md`. Ошибок быть не должно; про ожидаемые
   предупреждения — в Known Gaps.

## Known Gaps

- **Основные кнопки не проходят WCAG AA по контрасту.** Проверено
  `npx @google/design.md lint`: `button-primary` — белый на `{colors.primary}` даёт
  **3.65:1**, `button-info` — `{colors.info-content}` на `{colors.info}` даёт **2.04:1**,
  `button-error` — **4.08:1**. Порог AA для обычного текста — 4.5:1. Хуже всех
  `btn-info`, то есть кнопка «Сохранить». Это фактическое состояние продукта, не опечатка
  в документе; чинится сменой `*-content` на тёмный либо понижением светлоты фона в oklch.
- **Расхождение размеров контролов между темами.** `--size-field` равен `0.21875rem` в
  светлой теме и `0.25rem` в тёмной, `--radius-field` — `0.5rem` и `0.25rem`. Из-за этого
  кнопки в тёмной теме выше (40px против 35px) и острее по углам. То же с
  `--radius-selector`: 32px против 8px — чекбоксы и тогглы в светлой теме круглые, в
  тёмной почти квадратные. Похоже на артефакт, а не на решение; здесь зафиксировано как
  есть.
- **Две «главные» кнопки.** Бренд — `{colors.primary}` (`btn-primary`, 33 употребления), но
  «Сохранить» в формах исторически `btn-info` (`SaveButton`). Какая из них главный CTA —
  системой не зафиксировано.
- **Фокус на полях ввода почти не виден.** В `@layer components` при `:focus` цвет границы
  выставлен в то же значение, что и в покое, а `outline` снят. Кольцо `ring-primary/25` есть
  только у триггеров селектов.
- **`{colors.accent}` практически не используется** (по одному употреблению фона и текста) и
  меняет характер между темами: голубой #71d1fe в светлой, бирюзовый #00d3bb в тёмной. Роль
  не определена.
- **`{colors.secondary}` меняет роль между темами**: нейтральный серо-синий #8fa0b8 в
  светлой, насыщенная маджента #f43098 в тёмной. Кнопка «Отменить» в тёмной теме выглядит
  ярче, чем задумано.
- **Шкала градаций текста не канонизирована**: в коде живут 40/50/55/60/70/72/80/90/100.
  Значения 55, 90 и 100 — единичные.
- **Моноширинного токена нет.** Код, идентификаторы и хеши рендерятся Verdana.
- **Тёмная тема не переключается вручную** — только `prefers-color-scheme`. Тумблера в
  интерфейсе нет.
- **Состояний ошибки и валидации полей формы система не описывает** — они собираются
  из семантических утилит по месту.
- Токены `--depth: 1` и `--noise: 0` оставлены дефолтными DaisyUI; вставка тени и
  текстовой тени у кнопок приходит оттуда и специально не настраивалась.
- **Отклонения от спецификации DESIGN.md, сделанные осознанно.** Спецификация рассчитана
  на одну тему, поэтому тёмная выражена префиксом `dark-*` в `colors:`. Линтер помечает
  эти 29 токенов как `orphaned-tokens` — они и не могут быть отреферены из `components:`,
  где значение одно. Он же даёт `contrast-ratio` на компонентах с `backgroundColor:
  transparent`: `transparent` разбирается как `#00000000`, из-за чего прозрачные в покое
  триггеры выглядят провалом контраста. Оба класса предупреждений ожидаемы;
  ошибок (`errors`) быть не должно.
