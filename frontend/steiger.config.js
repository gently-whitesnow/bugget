import { defineConfig } from "steiger";
import fsd from "@feature-sliced/steiger-plugin";

export default defineConfig([
  ...fsd.configs.recommended,
  {
    // Сгенерированные из OpenAPI типы правятся кодгеном, а не руками, — правила FSD к ним неприменимы.
    ignores: ["./src/shared/api/generated/**"],
  },
  {
    // entities/beta-test — доменная сущность внешнего пользователя беты: свой api-сегмент,
    // модель и cross-import `@x/report`, который читает entities/report. Прямая ссылка на
    // слайс сейчас одна (pages/Report), из-за чего срабатывает insignificant-slice, но
    // слияние сущности в страницу сломало бы границу слоёв и разорвало cross-import.
    // Правило отключено точечно для этого слайса, глобально оно остаётся включённым.
    // Исключение временное: beta-test — SaaS-only фича, её выпил и снятие этого блока —
    // MAIN-25.
    files: ["./src/entities/beta-test/**"],
    rules: {
      "fsd/insignificant-slice": "off",
    },
  },
]);
