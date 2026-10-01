#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import ts from "../../frontend/node_modules/typescript/lib/typescript.js";

const root = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../..",
);
const sourceRoot = path.join(root, "frontend/src");
const modules = [
  "reports",
  "users",
  "settings",
  "analytics",
  "external",
  "authorization",
];
const rawInstances = new Set(["appApi", "usersApi", "authorizationApi"]);

const relative = (file) =>
  path.relative(sourceRoot, file).replaceAll(path.sep, "/");
const isTest = (file) => /\.(test|spec)\.[cm]?[jt]sx?$/.test(file);

const valueImports = (node) => {
  const clause = node.importClause;
  if (!clause || clause.isTypeOnly) return [];

  const names = [];
  if (clause.name) names.push({ imported: "default", local: clause.name.text });
  const bindings = clause.namedBindings;
  if (bindings && ts.isNamespaceImport(bindings)) {
    names.push({ imported: "*", local: bindings.name.text });
  } else if (bindings) {
    for (const element of bindings.elements) {
      if (!element.isTypeOnly) {
        names.push({
          imported: element.propertyName?.text ?? element.name.text,
          local: element.name.text,
        });
      }
    }
  }
  return names;
};

const isFetchCall = (node) => {
  if (!ts.isCallExpression(node)) return false;
  if (ts.isIdentifier(node.expression)) return node.expression.text === "fetch";
  return (
    ts.isPropertyAccessExpression(node.expression) &&
    node.expression.name.text === "fetch"
  );
};

const isAllowedFetch = (file, node) => {
  const name = relative(file);
  const first = node.arguments[0];
  if (
    name === "shared/ui/FilePreview/FilePreview.tsx" &&
    first &&
    ts.isCallExpression(first) &&
    ts.isIdentifier(first.expression) &&
    first.expression.text === "getImageUrl"
  ) {
    return true;
  }
  return (
    name === "shared/ui/notifications/NotificationDemoForm.tsx" &&
    first &&
    ts.isStringLiteral(first) &&
    first.text === "/api/demo-endpoint"
  );
};

const locationHrefAssignment = (node) =>
  ts.isBinaryExpression(node) &&
  node.operatorToken.kind === ts.SyntaxKind.EqualsToken &&
  ts.isPropertyAccessExpression(node.left) &&
  node.left.name.text === "href" &&
  ts.isPropertyAccessExpression(node.left.expression) &&
  ts.isIdentifier(node.left.expression.expression) &&
  node.left.expression.expression.text === "window" &&
  node.left.expression.name.text === "location";

const isAllowedRedirect = (file, node) => {
  const name = relative(file);
  const value = node.right;
  if (name === "pages/Login/ui/Login.tsx") {
    if (ts.isIdentifier(value) && value.text === "callbackUrl") return true;
    return (
      ts.isTemplateExpression(value) &&
      value.head.text === "/api/authorization/v1/fake/login?"
    );
  }
  return (
    name === "pages/Settings/ui/hooks/useExternalLinks.ts" &&
    ts.isTemplateExpression(value) &&
    value.head.text === "/api/authorization/v1/"
  );
};

const isAllowedLocationCall = (file, node) => {
  const name = relative(file);
  const first = node.arguments[0];
  return (
    (name === "shared/api/instances/base.ts" &&
      node.expression.name.text === "replace" &&
      first &&
      ts.isIdentifier(first) &&
      first.text === "redirectUrl") ||
    (name ===
      "widgets/custom-left-sidebar/ui/CustomLeftSidebar/components/LogoutButton.tsx" &&
      node.expression.name.text === "replace" &&
      first &&
      ts.isCallExpression(first) &&
      ts.isIdentifier(first.expression) &&
      first.expression.text === "getPostLogoutRedirectUrl")
  );
};

const isInstancesImport = (file, specifier) => {
  if (specifier.startsWith("@/shared/api/instances")) return true;
  if (!specifier.startsWith(".")) return false;

  const resolved = path
    .relative(sourceRoot, path.resolve(path.dirname(file), specifier))
    .replaceAll(path.sep, "/");
  return (
    resolved === "shared/api/instances" ||
    resolved.startsWith("shared/api/instances/")
  );
};

const analyze = (file, text) => {
  const source = ts.createSourceFile(
    file,
    text,
    ts.ScriptTarget.Latest,
    true,
    file.endsWith("x") ? ts.ScriptKind.TSX : ts.ScriptKind.TS,
  );
  const violations = [];
  const inventory = { fetch: 0, redirects: 0, axios: 0, rawInstances: 0 };

  const fail = (node, message) => {
    const { line } = source.getLineAndCharacterOfPosition(
      node.getStart(source),
    );
    violations.push(`${relative(file)}:${line + 1}: ${message}`);
  };

  const visit = (node) => {
    if (
      ts.isImportDeclaration(node) &&
      ts.isStringLiteral(node.moduleSpecifier)
    ) {
      const specifier = node.moduleSpecifier.text;
      const imports = valueImports(node);

      if (specifier === "axios" && imports.length > 0) {
        inventory.axios += 1;
        if (
          relative(file) !== "shared/api/instances/base.ts" &&
          !isTest(file)
        ) {
          fail(
            node,
            "runtime-import axios разрешён только общей транспортной границе",
          );
        }
      }

      if (isInstancesImport(file, specifier)) {
        const importedInstances = imports.filter(
          ({ imported }) => imported === "*" || rawInstances.has(imported),
        );
        inventory.rawInstances += importedInstances.length;
        const inBoundary = modules.some((module) =>
          relative(file).startsWith(`shared/api/${module}/`),
        );
        if (importedInstances.length > 0 && !inBoundary && !isTest(file)) {
          fail(
            node,
            `сырой API-инстанс (${importedInstances
              .map(({ imported }) => imported)
              .join(", ")}) импортирован вне контрактной границы`,
          );
        }
      }

      if (specifier === "@/shared/api") {
        const appInstance = imports.find(
          ({ imported }) => imported === "appApi",
        );
        if (appInstance) {
          inventory.rawInstances += 1;
          if (!isTest(file)) {
            fail(
              node,
              "appApi из публичного индекса предназначен только для wire-тестов",
            );
          }
        }
      }
    }

    if (isFetchCall(node)) {
      inventory.fetch += 1;
      if (!isAllowedFetch(file, node)) {
        fail(
          node,
          "fetch не входит в документированный инвентарь браузерных исключений",
        );
      }
    }

    if (
      ts.isIdentifier(node) &&
      node.text === "fetch" &&
      !(ts.isCallExpression(node.parent) && node.parent.expression === node) &&
      !(
        ts.isPropertyAccessExpression(node.parent) &&
        node.parent.name === node &&
        ts.isCallExpression(node.parent.parent) &&
        node.parent.parent.expression === node.parent
      )
    ) {
      fail(node, "алиас глобального fetch обходит инвентарь вызовов");
    }

    if (
      ts.isNewExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === "XMLHttpRequest"
    ) {
      fail(node, "XMLHttpRequest обходит единственную транспортную границу");
    }

    if (
      ts.isCallExpression(node) &&
      ts.isPropertyAccessExpression(node.expression) &&
      node.expression.name.text === "sendBeacon"
    ) {
      fail(node, "sendBeacon обходит единственную транспортную границу");
    }

    if (
      ts.isCallExpression(node) &&
      ts.isPropertyAccessExpression(node.expression) &&
      ["assign", "replace"].includes(node.expression.name.text) &&
      ts.isPropertyAccessExpression(node.expression.expression) &&
      ts.isIdentifier(node.expression.expression.expression) &&
      node.expression.expression.expression.text === "window" &&
      node.expression.expression.name.text === "location"
    ) {
      inventory.redirects += 1;
      if (!isAllowedLocationCall(file, node)) {
        fail(
          node,
          "window.location redirect не входит в документированный API-инвентарь",
        );
      }
    }

    if (locationHrefAssignment(node)) {
      inventory.redirects += 1;
      if (!isAllowedRedirect(file, node)) {
        fail(node, "redirect не входит в документированный API-инвентарь");
      }
    }

    ts.forEachChild(node, visit);
  };

  visit(source);
  return { violations, inventory };
};

const filesUnder = (directory) =>
  fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) return filesUnder(target);
    return /\.[cm]?[jt]sx?$/.test(entry.name) ? [target] : [];
  });

const checkModuleBoundaries = () => {
  const violations = [];
  for (const module of modules) {
    const file = path.join(sourceRoot, "shared/api", module, "client.ts");
    const text = fs.readFileSync(file, "utf8");
    if (!text.includes(`@/shared/api/generated/${module}`)) {
      violations.push(
        `${relative(file)}: нет импорта paths из generated/${module}`,
      );
    }
    if (!text.includes("createOperationRequest")) {
      violations.push(
        `${relative(file)}: модуль не использует общую границу createOperationRequest`,
      );
    }
  }
  return violations;
};

const selfTest = () => {
  const cases = [
    {
      name: "путь через переменную и raw appApi краснеет",
      file: "pages/Test.ts",
      text: 'import { appApi as client } from "@/shared/api"; const url = "/v2/reports"; client.get(url);',
    },
    {
      name: "window.fetch краснеет",
      file: "pages/Test.ts",
      text: 'window.fetch("/v2/reports");',
    },
    {
      name: "алиас fetch краснеет",
      file: "pages/Test.ts",
      text: 'const send = fetch; send("/v2/reports");',
    },
    {
      name: "namespace-import raw-инстансов краснеет",
      file: "pages/Test.ts",
      text: 'import * as instances from "@/shared/api/instances"; instances.appApi.get("/v2/reports");',
    },
    {
      name: "runtime axios вне границы краснеет",
      file: "pages/Test.ts",
      text: 'import axios from "axios"; axios.get("/v2/reports");',
    },
    {
      name: "XMLHttpRequest краснеет",
      file: "pages/Test.ts",
      text: "new XMLHttpRequest();",
    },
    {
      name: "sendBeacon краснеет",
      file: "pages/Test.ts",
      text: 'navigator.sendBeacon("/v2/reports", body);',
    },
    {
      name: "window.location.assign краснеет",
      file: "pages/Test.ts",
      text: 'window.location.assign("/api/authorization/v1/fake/login");',
    },
  ];

  let failed = false;
  for (const testCase of cases) {
    const file = path.join(sourceRoot, testCase.file);
    const result = analyze(file, testCase.text);
    if (result.violations.length === 0) {
      failed = true;
      console.error(`FAIL  ${testCase.name}`);
    } else {
      console.log(`ok    ${testCase.name}`);
    }
  }
  if (failed) process.exit(1);
  console.log(`самопроверка пройдена: ${cases.length} обходов краснеют`);
};

if (process.argv.includes("--self-test")) {
  selfTest();
  process.exit(0);
}

const totals = { fetch: 0, redirects: 0, axios: 0, rawInstances: 0 };
const violations = checkModuleBoundaries();
for (const file of filesUnder(sourceRoot)) {
  if (relative(file).startsWith("shared/api/generated/")) continue;
  const result = analyze(file, fs.readFileSync(file, "utf8"));
  violations.push(...result.violations);
  for (const key of Object.keys(totals)) totals[key] += result.inventory[key];
}

if (violations.length > 0) {
  console.error("Инвентарь HTTP-вызовов фронта разошёлся:");
  for (const violation of violations) console.error(`  ${violation}`);
  process.exit(1);
}

console.log(
  [
    `API-инвентарь сходится: ${modules.length} generated-границ`,
    `${totals.fetch} документированных fetch`,
    `${totals.redirects} redirect`,
    `${totals.axios} runtime axios import`,
    `${totals.rawInstances} импортов raw-инстансов (границы и wire-тесты)`,
  ].join("; "),
);
