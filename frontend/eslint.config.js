import js from "@eslint/js";
import effector from "eslint-plugin-effector";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";

import { noUnsafeInnerHtmlOption } from "./eslint-rules/no-unsafe-inner-html.js";

export default tseslint.config(
  { ignores: ["dist"] },
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
      "react-refresh/only-export-components": [
        "warn",
        { allowConstantExport: true },
      ],
      "no-restricted-syntax": ["error", noUnsafeInnerHtmlOption],
      "max-len": [
        "error",
        { code: 1000, ignoreStrings: true, ignoreUrls: true },
      ],
      "effector/mandatory-scope-binding": "warn",
      "effector/prefer-useUnit": "warn",
    },
  }
);
