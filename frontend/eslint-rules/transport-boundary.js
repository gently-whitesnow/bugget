/**
 * Сборка правил `no-restricted-syntax`, запрещающих прямой HTTP-вызов путей
 * модуля, у которого есть транспортная граница на операциях контракта.
 *
 * Модули переводятся на generated по одному, и запрет у каждого свой — меняется
 * только шаблон пути и текст сообщения. Форма же вызова одна на всех, и держать
 * её в одном месте дешевле, чем размножать одинаковые селекторы: правило,
 * закрывающее не все формы, выглядит как выполненный инвариант.
 *
 * Закрыты:
 *   * shorthand — путь первым аргументом (`appApi.get("/v2/reports")`);
 *   * config-form — путь в поле `url` объекта (`appApi.request({ url, method })`,
 *     `appApi({ url })`, `appApi.get(url, config)` с путём внутри конфига).
 *     Ищется по всему поддереву вызова: `url` может лежать и в разложенном
 *     объекте (`{ ...config, url: "/v2/reports" }`);
 *   * обход axios через `fetch` — он ушёл бы мимо интерсепторов, то есть мимо
 *     case-границы и обработки 401.
 *
 * Вызов операции (`request("/v2/reports/{aliasId}", "get", …)`) под правило не
 * попадает: у него нет member-callee вида `<инстанс>.<метод>` и нет поля `url`.
 */

const httpMethods = "^(get|post|put|patch|delete|head|options|request)$";
const apiInstance = "Api$";

/**
 * Формы вызова: `<инстанс>.<метод>(…)`, вызов самого инстанса `<инстанс>(…)`
 * и `fetch(…)`.
 */
const axiosCalls = [
  `CallExpression[callee.property.name=/${httpMethods}/]`,
  `CallExpression[callee.name=/${apiInstance}/]`,
  'CallExpression[callee.name="fetch"]',
];

/** Путь строкой или template-строкой с подстановкой. */
const pathNodes = (prefix, pathPattern) => [
  `${prefix} > Literal[value=/${pathPattern}/]`,
  `${prefix} > TemplateLiteral > TemplateElement[value.raw=/${pathPattern}/]`,
];

/**
 * @param {string} pathPattern регулярка адреса модуля в виде строки
 * @param {string} message что показать вместо вызова
 */
export const transportBoundaryOptions = (pathPattern, message) =>
  axiosCalls
    .flatMap((call) => [
      // shorthand: путь — прямой аргумент вызова
      ...pathNodes(call, pathPattern),
      // config-form: путь в поле `url` где-то внутри аргументов вызова
      ...pathNodes(`${call} Property[key.name="url"]`, pathPattern),
    ])
    .map((selector) => ({ selector, message }));
