# Фронтенд

## Стэк

- vite + ts + effector
- lucide – иконки
- tailwind – css-framework
- [daisyui](https://daisyui.com/) – plugin для tailwind готовыми компонентами

TODO писать сюда все новые пакеты что добавляем и что они делают, почему добавляем, чтобы не было дубликатов

- date-fns
- eslint+prettier – линтер+форматтер
- steiger – линтер архитектуры (Feature-Sliced Design)

## Локальная разработка

### Перед началом работы

- npm ci – установка зависимостей

### Команды для локального запуска

- npm run dev – запуск фронтового приложения на дев-сервере
- npm run start-backend – запуск локальной БД и локального бэкенда
- npm start – запуск двух предыдущих команд последовательно

### Проверки качества

Git-хуков в проекте нет: проверки живут в CI и запускаются той же командой локально.

- `../scripts/quality/verify.sh --scope frontend` – все фронтовые гейты, ровно как в CI
- `../scripts/quality/verify.sh --scope frontend --fast` – без медленных (без `npm audit`)
- `../scripts/quality/verify.sh --list` – какие вообще есть гейты и что каждый проверяет

Набор гейтов описан в `.quality/quality.config.json` — новая проверка добавляется туда.

- LOC-бюджет: лимит и зафиксированные превышения в `.quality/frontend-loc.json`,
  пересобрать после рефакторинга – `../scripts/quality/frontend-loc.sh --update`
- `npm audit`: принятые уязвимости с причинами в `.quality/frontend-audit-allowlist.json`
- точечные отключения правил FSD – в `steiger.config.js`, каждое с причиной

## Идеи на будущее

- **Переход на ProseMirror или Lexical для текстовых полей.** Сейчас `MarkdownTextarea` — это `contentEditable` с ручной обработкой ввода, вставки и undo (через deprecated `document.execCommand`). При росте требований (коллаборативное редактирование, слэш-команды, вложенные блоки, drag-n-drop блоков) стоит перейти на специализированный фреймворк с собственной undo-моделью. Кандидаты: [ProseMirror](https://prosemirror.net/) (гибкий, низкоуровневый), [TipTap](https://tiptap.dev/) (обёртка над ProseMirror), [Lexical](https://lexical.dev/) (от Meta, React-first).
