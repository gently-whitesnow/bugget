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
 * Ловится именно связка «axios-метод + строковый путь reports»: вызов операции
 * (`request("/v2/reports/{aliasId}", "get", …)`) под правило не попадает, потому
 * что у него нет member-callee вида `<инстанс>.<метод>`.
 *
 * Использование в eslint.config:
 *   "no-restricted-syntax": ["error", ...noDirectReportsTransportOptions]
 */

const message =
  "Путь модуля reports вызывается напрямую. Транспорт этого модуля живёт в src/shared/api/reports (операции сгенерированного контракта) — добавьте или используйте операцию там.";

const httpMethods = "^(get|post|put|patch|delete|request)$";
const reportsPath = "^\\/v[12]\\/reports";

export const noDirectReportsTransportOptions = [
  {
    selector: `CallExpression[callee.property.name=/${httpMethods}/] > Literal[value=/${reportsPath}/]`,
    message,
  },
  {
    selector: `CallExpression[callee.property.name=/${httpMethods}/] > TemplateLiteral > TemplateElement[value.raw=/${reportsPath}/]`,
    message,
  },
];
