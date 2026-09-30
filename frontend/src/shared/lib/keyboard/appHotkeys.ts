import type { Hotkey } from "./hotkey";

/** Сочетания уровня приложения. Одна таблица для обработчиков и шпаргалки. */
export const appHotkeys = {
  search: { code: "KeyK", mod: true },
  searchSlash: { code: "Slash" },
  attachFile: { code: "KeyU", mod: true },
  help: { code: "Slash", mod: true },
  helpQuestion: { code: "Slash", shift: true },
} satisfies Record<string, Hotkey>;
