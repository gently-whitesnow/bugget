import { defineConfig } from "steiger";
import fsd from "@feature-sliced/steiger-plugin";

export default defineConfig([
  ...fsd.configs.recommended,
  {
    // Сгенерированные из OpenAPI типы правятся кодгеном, а не руками, — правила FSD к ним неприменимы.
    ignores: ["./src/shared/api/generated/**"],
  },
]);
