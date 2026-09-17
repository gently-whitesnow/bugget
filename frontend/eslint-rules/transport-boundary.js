/**
 * Сборка правил `no-restricted-syntax` против прямого HTTP-вызова путей модуля
 * с транспортной границей. Закрыты: shorthand (`appApi.get("/v2/reports")`),
 * config-form (`url` где угодно в поддереве аргументов, включая spread) и
 * `fetch` — он ушёл бы мимо интерсепторов (case-граница, обработка 401).
 * Вызов операции `request(path, method, …)` под правило не попадает.
 */

const httpMethods = "^(get|post|put|patch|delete|head|options|request)$";
const apiInstance = "Api$";

const axiosCalls = [
  `CallExpression[callee.property.name=/${httpMethods}/]`,
  `CallExpression[callee.name=/${apiInstance}/]`,
  'CallExpression[callee.name="fetch"]',
];

const pathNodes = (prefix, pathPattern) => [
  `${prefix} > Literal[value=/${pathPattern}/]`,
  `${prefix} > TemplateLiteral > TemplateElement[value.raw=/${pathPattern}/]`,
];

export const transportBoundaryOptions = (pathPattern, message) =>
  axiosCalls
    .flatMap((call) => [
      ...pathNodes(call, pathPattern),
      // config-form: `url` где-то внутри аргументов вызова
      ...pathNodes(`${call} Property[key.name="url"]`, pathPattern),
    ])
    .map((selector) => ({ selector, message }));
