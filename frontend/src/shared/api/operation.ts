import type { AxiosInstance } from "axios";
import type { Camelized } from "@/shared/lib/types";
import { buildQueryString } from "./buildQuery";
import type { QueryValue } from "./buildQuery";

// Типизированная граница «операция контракта → HTTP»: путь, метод, параметры,
// тело и ответ выведены из `paths`. Регистры — ADR-0009: тела `Camelized<T>`,
// query/path как в контракте, multipart не конвертируется.

export type HttpMethod = "get" | "post" | "put" | "patch" | "delete";

export type ResponseValidatorContext = {
  path: string;
  method: HttpMethod;
};

export type ResponseValidator = (
  data: unknown,
  context: ResponseValidatorContext
) => void;

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

// `query?:` (фильтры) и `query:` (обязательный параметр) различаются по
// `undefined` в типе свойства — иначе пропуск даст 400 в рантайме.
type QueryIsOptional<O> = "query" extends keyof ParametersOf<O>
  ? undefined extends ParametersOf<O>["query"]
    ? true
    : false
  : true;

type BodyContentOf<O> = O extends { requestBody: { content: infer C } }
  ? C
  : never;

// `[X] extends [never]` обязателен: иначе у операции без тела `infer B`
// вывел бы `unknown` вместо «тела нет». То же для ответа без тела.
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

type JsonResponseOf<O> = [SuccessResponseOf<O>] extends [never]
  ? never
  : SuccessResponseOf<O> extends { content: { "application/json": infer D } }
    ? D
    : never;

// Имена полей multipart — из схемы, как на проводе; массив — повтор поля.
type MultipartValue = File | Blob | string;
type MultipartFields<B> = {
  [K in keyof B]: NonNullable<B[K]> extends unknown[]
    ? MultipartValue[]
    : MultipartValue;
};

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

type OperationResult<O> = [JsonResponseOf<O>] extends [never]
  ? void
  : Camelized<JsonResponseOf<O>>;

export type OperationCallResult<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = OperationResult<OperationOf<TPaths, P, M>>;

export type OperationBody<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = Camelized<JsonBodyOf<OperationOf<TPaths, P, M>>>;

export type OperationResponse<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = Camelized<JsonResponseOf<OperationOf<TPaths, P, M>>>;

export type OperationQuery<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = QueryOf<OperationOf<TPaths, P, M>>;

export type OperationCallArgs<
  TPaths,
  P extends keyof TPaths,
  M extends MethodsOf<TPaths[P]>,
> = OperationArgs<OperationOf<TPaths, P, M>>;

export const buildOperationPath = (
  template: string,
  params: Record<string, unknown> | undefined
): string =>
  template.replace(/\{(\w+)\}/g, (_, name: string) => {
    const value = params?.[name];
    if (value === undefined || value === null) {
      throw new Error(`Не задан path-параметр ${name} для ${template}`);
    }
    // Без экранирования: значения — alias `<team>-<номер>` и числовые id.
    return String(value);
  });

type RuntimeMultipart = Record<
  string,
  MultipartValue | MultipartValue[] | undefined
>;

const toFormData = (fields: RuntimeMultipart): FormData => {
  const formData = new FormData();
  for (const [name, value] of Object.entries(fields)) {
    if (value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) {
      formData.append(name, item);
    }
  }
  return formData;
};

type RuntimeArgs = {
  path?: Record<string, unknown>;
  query?: Record<string, QueryValue>;
  body?: unknown;
  multipart?: RuntimeMultipart;
};

export const createOperationRequest =
  <TPaths>(instance: AxiosInstance, validateResponse?: ResponseValidator) =>
  async <P extends keyof TPaths & string, M extends MethodsOf<TPaths[P]>>(
    path: P,
    method: M,
    args: OperationArgs<OperationOf<TPaths, P, M>>
  ): Promise<OperationResult<OperationOf<TPaths, P, M>>> => {
    const { path: pathParams, query, body, multipart } = args as RuntimeArgs;

    const url = buildOperationPath(path, pathParams);

    // Пустой query даёт хвостовой `?` (как в прежних вызовах списка/поиска),
    // отсутствующий — адрес без него; провод менять нельзя.
    const search =
      query === undefined ? undefined : `?${buildQueryString(query)}`;

    const response = await instance.request({
      url: search === undefined ? url : `${url}${search}`,
      method: method as HttpMethod,
      data: multipart ? toFormData(multipart) : body,
      // По заголовку интерсептор не конвертирует регистр тела.
      ...(multipart
        ? { headers: { "Content-Type": "multipart/form-data" } }
        : {}),
    });

    validateResponse?.(response.data, { path, method: method as HttpMethod });
    return response.data;
  };
