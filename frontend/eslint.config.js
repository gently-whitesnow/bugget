import js from "@eslint/js";
import effector from "eslint-plugin-effector";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";

import { noUnsafeInnerHtmlOption } from "./eslint-rules/no-unsafe-inner-html.js";
import { noDirectReportsTransportOptions } from "./eslint-rules/no-direct-reports-transport.js";

export default tseslint.config(
  // src/shared/api/generated — вывод openapi-typescript из specs/contracts/**.
  // Правится он перегенерацией (scripts/quality/frontend-openapi-generate.sh),
  // а не руками, поэтому замечания линтера по нему нечинимы и только шумят.
  // Так же он исключён из prettier (.prettierignore) и из LOC-бюджета
  // (.quality/frontend-loc.json).
  { ignores: ["dist", "src/shared/api/generated"] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      effector,
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh,
    },
    settings: {
      "import/resolver": {
        alias: {
          map: [["@/", "./src/"]],
          // не забудьте указать расширения, если необходимо
          extensions: [".js", ".jsx", ".ts", ".tsx"],
        },
      },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // eslint-plugin-react-hooks 7 добавил в recommended правила React Compiler.
      // Они находят реальный долг (22 setState в эффектах и три точечных места),
      // но чинится он переписыванием компонентов, а не обновлением зависимости,
      // ради которого плагин подняли. Отключены здесь, чтобы долг не смешивался с
      // security-обновлением; включать обратно по одному правилу за проход.
      "react-hooks/set-state-in-effect": "off",
      "react-hooks/purity": "off",
      "react-hooks/refs": "off",
      "react-hooks/static-components": "off",
      "react-refresh/only-export-components": [
        "warn",
        { allowConstantExport: true },
      ],
      "no-restricted-syntax": [
        "error",
        noUnsafeInnerHtmlOption,
        // Модуль reports переведён на операции контракта: прямой вызов его пути
        // мимо src/shared/api/reports снова разводит адрес и контракт.
        ...noDirectReportsTransportOptions,
      ],
      "max-len": [
        "error",
        { code: 1000, ignoreStrings: true, ignoreUrls: true },
      ],
      "effector/mandatory-scope-binding": "warn",
      "effector/prefer-useUnit": "warn",
    },
  },
  {
    // Два исключения из запрета на прямой вызов путей reports, оба узкие:
    //   * транспортная граница модуля — то самое единственное место;
    //   * тесты интерсепторов (`shared/api/instances`), где адрес и есть предмет
    //     проверки: ADR-0009 требует, чтобы форма данных не зависела от URL, и
    //     доказывается это сравнением ответов по разным адресам.
    // Остальные ограничения (innerHTML) здесь остаются в силе.
    files: ["src/shared/api/reports/**/*.ts", "src/shared/api/instances/*.test.ts"],
    rules: {
      "no-restricted-syntax": ["error", noUnsafeInnerHtmlOption],
    },
  }
);
