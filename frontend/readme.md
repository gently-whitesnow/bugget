# Фронтенд

## Стэк

- vite + ts + effector
- lucide – иконки
- tailwind – css-framework
- [daisyui](https://daisyui.com/) – plugin для tailwind готовыми компонентами

TODO писать сюда все новые пакеты что добавляем и что они делают, почему добавляем, чтобы не было дубликатов

- date-fns
- simple-git hooks – прекоммит хуки
- lint-staged – запуск комманд только для "staged" файлов
- eslint+prettier – линтер+форматтер

## Локальная разработка

### Перед началом работы

- npm ci – установка зависимостей и активация прекоммит-хуков (ставятся в корневой `.git/hooks`)

### Команды для локального запуска

- npm run dev – запуск фронтового приложения на дев-сервере
- npm run start-backend – запуск локальной БД и локального бэкенда
- npm start – запуск двух предыдущих команд последовательно

### Precommit-хуки

- если не работают (должны активироваться командой `npm ci`), переустановить: `npm run prepare`
- если раньше запускали `npx simple-git-hooks` из `frontend` и появился `frontend/.git/` — удалить `frontend/.git` (это артефакт, Git его не использует)

## Идеи на будущее

- **Переход на ProseMirror или Lexical для текстовых полей.** Сейчас `MarkdownTextarea` — это `contentEditable` с ручной обработкой ввода, вставки и undo (через deprecated `document.execCommand`). При росте требований (коллаборативное редактирование, слэш-команды, вложенные блоки, drag-n-drop блоков) стоит перейти на специализированный фреймворк с собственной undo-моделью. Кандидаты: [ProseMirror](https://prosemirror.net/) (гибкий, низкоуровневый), [TipTap](https://tiptap.dev/) (обёртка над ProseMirror), [Lexical](https://lexical.dev/) (от Meta, React-first).
