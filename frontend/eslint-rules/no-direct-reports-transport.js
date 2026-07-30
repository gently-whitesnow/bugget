/**
 * Правило no-restricted-syntax: запрет прямых HTTP-вызовов путей модуля `reports`
 * вне единственной транспортной границы (`src/shared/api/reports`).
 *
 * Модуль переведён на операции сгенерированного контракта: путь, метод, query,
 * тело и тип ответа связаны там в одном месте и выведены из
 * `specs/contracts/reports/openapi.yaml`. Новый `appApi.get("/v2/reports/...")`
 * рядом с моделью снова разводит адрес и контракт, и такое расхождение опять
 * находится в рантайме у заказчика, а не на сборке.
 *
 * Закрыты обе формы вызова axios:
 *   * shorthand — путь первым аргументом (`appApi.get("/v2/reports")`);
 *   * config-form — путь в поле `url` объекта (`appApi.request({ url, method })`,
 *     `appApi({ url })`, `appApi.get(url, config)` с путём внутри конфига).
 * Config-form ищется по всему поддереву вызова: `url` может лежать и в
 * разложенном объекте (`{ ...config, url: "/v2/reports" }`).
 *
 * Вызов операции (`request("/v2/reports/{aliasId}", "get", …)`) под правило не
 * попадает: у него нет member-callee вида `<инстанс>.<метод>` и нет поля `url`.
 * Краснота правила закреплена тестом
 * `src/shared/api/reports/transportBoundary.gate.test.ts`.
 *
 * Использование в eslint.config:
 *   "no-restricted-syntax": ["error", ...noDirectReportsTransportOptions]
 */

const message =
  "Путь модуля reports вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/reports (операции сгенерированного контракта) — добавьте или используйте операцию там.";

const httpMethods = "^(get|post|put|patch|delete|head|options|request)$";
const apiInstance = "Api$";
const reportsPath = "^\\/v[12]\\/reports";

/**
 * Формы вызова: `<инстанс>.<метод>(…)`, вызов самого инстанса `<инстанс>(…)`
 * и `fetch(…)` — последний мимо axios обошёл бы и интерсепторы, то есть
 * case-границу и обработку 401.
 */
const axiosCalls = [
  `CallExpression[callee.property.name=/${httpMethods}/]`,
  `CallExpression[callee.name=/${apiInstance}/]`,
  'CallExpression[callee.name="fetch"]',
];

/** Путь строкой или template-строкой с подстановкой. */
const pathNodes = (prefix) => [
  `${prefix} > Literal[value=/${reportsPath}/]`,
  `${prefix} > TemplateLiteral > TemplateElement[value.raw=/${reportsPath}/]`,
];

export const noDirectReportsTransportOptions = axiosCalls.flatMap((call) => [
  // shorthand: путь — прямой аргумент вызова
  ...pathNodes(call),
  // config-form: путь в поле `url` где-то внутри аргументов вызова
  ...pathNodes(`${call} Property[key.name="url"]`),
]).map((selector) => ({ selector, message }));
