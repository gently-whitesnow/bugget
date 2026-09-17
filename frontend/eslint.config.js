import js from "@eslint/js";
import effector from "eslint-plugin-effector";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";

import { noUnsafeInnerHtmlOption } from "./eslint-rules/no-unsafe-inner-html.js";
import { noDirectReportsTransportOptions } from "./eslint-rules/no-direct-reports-transport.js";
import { noDirectUsersTransportOptions } from "./eslint-rules/no-direct-users-transport.js";
import { noDirectAnalyticsTransportOptions } from "./eslint-rules/no-direct-analytics-transport.js";
import { noDirectExternalTransportOptions } from "./eslint-rules/no-direct-external-transport.js";
import { noDirectSettingsTransportOptions } from "./eslint-rules/no-direct-settings-transport.js";
import { noDirectAuthorizationTransportOptions } from "./eslint-rules/no-direct-authorization-transport.js";

export default tseslint.config(
  // src/shared/api/generated — вывод openapi-typescript, правится только
  // перегенерацией (scripts/contracts/frontend-openapi-generate.sh).
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
      // Правила React Compiler из react-hooks 7 находят реальный долг, но он
      // чинится переписыванием компонентов; включать по одному за проход.
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
        // Модули переведены на операции контракта: прямой вызов их путей мимо
        // src/shared/api/<модуль> снова разводит адрес и контракт.
        ...noDirectReportsTransportOptions,
        ...noDirectUsersTransportOptions,
        ...noDirectAnalyticsTransportOptions,
        ...noDirectExternalTransportOptions,
        ...noDirectSettingsTransportOptions,
        ...noDirectAuthorizationTransportOptions,
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
    // Узкие исключения: сама транспортная граница модуля и тесты интерсепторов,
    // где адрес — предмет проверки (ADR-0009). Запрет innerHTML остаётся в силе.
    files: [
      "src/shared/api/reports/**/*.ts",
      "src/shared/api/users/**/*.ts",
      "src/shared/api/analytics/**/*.ts",
      "src/shared/api/external/**/*.ts",
      "src/shared/api/settings/**/*.ts",
      "src/shared/api/authorization/**/*.ts",
      "src/shared/api/instances/*.test.ts",
    ],
    rules: {
      "no-restricted-syntax": ["error", noUnsafeInnerHtmlOption],
    },
  }
);
