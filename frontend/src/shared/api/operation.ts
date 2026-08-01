import type { AxiosInstance } from "axios";
import type { Camelized } from "@/shared/lib/types";
import { buildQueryString } from "./buildQuery";
import type { QueryValue } from "./buildQuery";

/**
 * Типизированная граница «операция контракта → HTTP».
 *
 * Единственное место, где путь, метод, path-параметры, query, тело и тип ответа
 * соединяются вместе, и все они выведены из `paths`/`operations` сгенерированного
 * клиента. Call-site называет операцию — пару «ключ пути + метод», — а не строку
 * URL с методом axios рядом: путь проверяется как `keyof paths`, метод — как
 * объявленный у этого пути, а тип ответа берётся из этой же операции. Пропало
 * поле в схеме ответа, сменился метод или путь — код перестал компилироваться.
 *
 * Граница же держит и регистры (ADR-0009): тела запроса и ответа описаны здесь
 * `Camelized<T>` (в snake_case их перекладывает интерсептор
 * `shared/api/instances/base.ts`), query и path берутся из контракта как есть,
 * multipart не конвертируется вовсе, а имена его полей приходят из схемы.
 */

export type HttpMethod = "get" | "post" | "put" | "patch" | "delete";

export type ResponseValidator = (data: unknown) => void;

/** `never` и `undefined` в сгенерированных типах значат «этого у операции нет». */
type Present<T> = [NonNullable<T>] extends [never] ? never : NonNullable<T>;

/** Методы, реально объявленные у пути: у остальных в generated стоит `never`. */
export type MethodsOf<TPathItem> = {
  [M in Extract<keyof TPathItem, HttpMethod>]: [Present<TPathItem[M]>] extends [
    never,
  ]
    ? never
    : M;
}[Extract<keyof TPathItem, HttpMethod>];

type OperationOf<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = M extends keyof TPaths[P] ? Present<TPaths[P][M]> : never;

type ParametersOf<O> = O extends { parameters: infer P } ? P : never;

type PathParamsOf<O> = O extends { parameters: { path?: infer T } }
  ? Present<T>
  : never;

type QueryOf<O> = O extends { parameters: { query?: infer T } }
  ? Present<T>
  : never;

/**
 * Обязателен ли query у операции. Контракт различает две вещи: `query?: {...}` —
 * фильтры, которых может не быть (список, поиск), и `query: {...}` — параметр,
 * без которого ручка не имеет смысла (`attachType` у загрузки вложения). Если
 * стереть эту разницу, пропуск обязательного query пройдёт компиляцию и ручка
 * ответит 400 в рантайме.
 *
 * Признак — `undefined` в типе свойства: у необязательного свойства он есть,
 * у обязательного нет.
 */
type QueryIsOptional<O> = "query" extends keyof ParametersOf<O>
  ? undefined extends ParametersOf<O>["query"]
    ? true
    : false
  : true;

type BodyContentOf<O> = O extends { requestBody: { content: infer C } }
  ? C
  : never;

// Проверка `[X] extends [never]` обязательна: у операции без тела `BodyContentOf`
// равен `never`, а `never extends { ... infer B }` истинно и вывело бы `B = unknown` —
// «тело есть, тип неизвестен» вместо «тела нет».
type JsonBodyOf<O> = [BodyContentOf<O>] extends [never]
  ? never
  : BodyContentOf<O> extends { "application/json": infer B }
    ? B
    : never;

type MultipartBodyOf<O> = [BodyContentOf<O>] extends [never]
  ? never
  : BodyContentOf<O> extends { "multipart/form-data": infer B }
    ? B
    : never;

type ResponsesOf<O> = O extends { responses: infer R } ? R : never;

type SuccessResponseOf<O> = ResponsesOf<O>[Extract<
  keyof ResponsesOf<O>,
  200 | 201
>];

// Тот же случай, что и с телом запроса: ответ без тела (DELETE) не должен
// превращаться в `unknown`.
type JsonResponseOf<O> = [SuccessResponseOf<O>] extends [never]
  ? never
  : SuccessResponseOf<O> extends { content: { "application/json": infer D } }
    ? D
    : never;

/**
 * Поля multipart-тела: имена — из схемы контракта, значения — то, что кладётся
 * в `FormData`. Тело multipart регистр не конвертирует, поэтому имя поля здесь
 * то же, что на проводе.
 */
type MultipartFields<B> = { [K in keyof B]: File | Blob | string };

type PathArg<O> = [PathParamsOf<O>] extends [never]
  ? object
  : { path: PathParamsOf<O> };

type QueryArg<O> = [QueryOf<O>] extends [never]
  ? object
  : QueryIsOptional<O> extends true
    ? { query?: QueryOf<O> }
    : { query: QueryOf<O> };

type BodyArg<O> = [JsonBodyOf<O>] extends [never]
  ? object
  : { body: Camelized<JsonBodyOf<O>> };

type MultipartArg<O> = [MultipartBodyOf<O>] extends [never]
  ? object
  : { multipart: MultipartFields<MultipartBodyOf<O>> };

type OperationArgs<O> = PathArg<O> & QueryArg<O> & BodyArg<O> & MultipartArg<O>;

/** Тело успешного ответа в camelCase; `void` — если тела у ответа нет. */
type OperationResult<O> = [JsonResponseOf<O>] extends [never]
  ? void
  : Camelized<JsonResponseOf<O>>;

/**
 * Результат вызова операции. Экспортируется затем же, зачем `OperationCallArgs`:
 * обёртке над `request` (например context-форме адреса в модуле `users`) нужно
 * объявить свой возвращаемый тип тем же, что у самой операции, а не «шире».
 */
export type OperationCallResult<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = OperationResult<OperationOf<TPaths, P, M>>;

/** Форма тела запроса операции в том виде, в котором её пишет код фронта. */
export type OperationBody<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = Camelized<JsonBodyOf<OperationOf<TPaths, P, M>>>;

/** Форма успешного ответа операции в том виде, в котором его читает код фронта. */
export type OperationResponse<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = Camelized<JsonResponseOf<OperationOf<TPaths, P, M>>>;

/** Query-параметры операции: конверсию не проходят, берутся из контракта как есть. */
export type OperationQuery<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = QueryOf<OperationOf<TPaths, P, M>>;

/**
 * Аргументы вызова операции — ровно то, что требует её запись в контракте.
 * Экспортируется для type-level проверок: обязательность query и тела должна
 * фиксироваться тестом, а не только читаться в этом файле.
 */
export type OperationCallArgs<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = OperationArgs<OperationOf<TPaths, P, M>>;

/**
 * Подстановка path-параметров в шаблон адреса из контракта.
 *
 * Экспортируется, потому что адрес операции нужен не только запросу: аватар
 * уезжает в `src` картинки, а не в axios, и собирать его руками рядом значило бы
 * снова развести адрес и контракт.
 */
export const buildOperationPath = (
  template: string,
  params: Record<string, unknown> | undefined
): string =>
  template.replace(/\{(\w+)\}/g, (_, name: string) => {
    const value = params?.[name];
    if (value === undefined || value === null) {
      throw new Error(`Не задан path-параметр ${name} для ${template}`);
    }
    // Подстановка без экранирования — ровно как в рукописных вызовах до миграции:
    // значения path-параметров это alias вида `<team>-<номер>` и числовые id.
    return String(value);
  });

const toFormData = (fields: Record<string, File | Blob | string>): FormData => {
  const formData = new FormData();
  for (const [name, value] of Object.entries(fields)) {
    formData.append(name, value);
  }
  return formData;
};

type RuntimeArgs = {
  path?: Record<string, unknown>;
  query?: Record<string, QueryValue>;
  body?: unknown;
  multipart?: Record<string, File | Blob | string>;
};

/**
 * Возвращает функцию вызова операций одного контракта поверх готового
 * axios-инстанса: `request("/v2/reports/{aliasId}", "get", { path: { aliasId } })`.
 */
export const createOperationRequest =
  <TPaths>(instance: AxiosInstance, validateResponse?: ResponseValidator) =>
  async <P extends keyof TPaths & string, M extends MethodsOf<TPaths[P]>>(
    path: P,
    method: M,
    args: OperationArgs<OperationOf<TPaths, P, M>>
  ): Promise<OperationResult<OperationOf<TPaths, P, M>>> => {
    const { path: pathParams, query, body, multipart } = args as RuntimeArgs;

    const url = buildOperationPath(path, pathParams);

    // «Query не передан» и «query передан, но пуст» — разные адреса, и провод
    // здесь менять нельзя: рукописные вызовы списка и поиска всегда клеили
    // `?${searchParams}`, поэтому у пустых фильтров хвостовой `?` был и остаётся.
    // Ручка без query (карточка, DELETE) как раньше уходит без него вовсе.
    const search =
      query === undefined ? undefined : `?${buildQueryString(query)}`;

    const response = await instance.request({
      url: search === undefined ? url : `${url}${search}`,
      method: method as HttpMethod,
      data: multipart ? toFormData(multipart) : body,
      // Тот же заголовок, что и раньше: по нему интерсептор понимает, что тело
      // не JSON и конвертировать регистр в нём нельзя.
      ...(multipart
        ? { headers: { "Content-Type": "multipart/form-data" } }
        : {}),
    });

    validateResponse?.(response.data);
    return response.data;
  };
