import type { paths } from "@/shared/api/generated/users";
import {
  getAppContext,
  usersApi,
  USERS_API_PREFIX,
} from "@/shared/api/instances";
import {
  buildOperationPath,
  createOperationRequest,
} from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationCallArgs,
  OperationCallResult,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

/**
 * Ядро единственной транспортной границы модуля `users`.
 *
 * Ручки объявлены рядом, по ресурсам: `users.ts`, `avatar.ts`, `teams.ts`,
 * `teamMembers.ts`, `workspaces.ts`. Каждая — операция контракта: ключ пути из
 * `paths` плюс метод, объявленный у этого пути. Тело, query и тип ответа
 * выведены из той же операции, поэтому правка `specs/contracts/users/openapi.yaml`
 * ломает компиляцию здесь, а не отвечает 404 у заказчика.
 *
 * Прямых `usersApi.get("/api/users/v1/...")` вне этого каталога быть не должно —
 * за этим следит правило линтера `no-restricted-syntax` (гейт `frontend-lint`),
 * краснота которого закрыта тестом `transportBoundary.gate.test.ts`.
 *
 * Регистры и сериализация живут в `shared/api/operation.ts`: тела в коде
 * camelCase, на проводе snake_case; query и path — как в контракте; multipart не
 * конвертируется.
 */
export const request = createOperationRequest<paths>(usersApi);

/** Короткая запись для тела запроса операции. */
export type Body<
  P extends keyof paths,
  M extends MethodsOf<paths[P]>,
> = OperationBody<paths, P, M>;

/** Короткая запись для успешного ответа операции. */
export type Result<
  P extends keyof paths,
  M extends MethodsOf<paths[P]>,
> = OperationResponse<paths, P, M>;

/** Короткая запись для query-параметров операции. */
export type Query<
  P extends keyof paths,
  M extends MethodsOf<paths[P]>,
> = OperationQuery<paths, P, M>;

/* ── Контекстная форма адреса ──────────────────────────────────────────────── */

/**
 * У модуля две публичные формы одного и того же адреса, и слайс обязан сохранить
 * обе:
 *
 *   * короткая — рабочее пространство и команда приходят аргументами
 *     (`fetchCurrentUser(workspaceId, teamId)`, участники команды, админские
 *     ручки);
 *   * контекстная — те же сегменты берутся из контекста приложения
 *     (`getAppContext`), как это делал `usersPathWithContext`. Ручке они всё
 *     равно не нужны: рабочее пространство и команда берутся из identity, и
 *     контракт описывает эти сегменты как игнорируемые.
 *
 * Разница только в источнике двух сегментов, поэтому обе формы — один и тот же
 * ключ пути из `paths`, а не два разных адреса.
 */
const CONTEXT_PREFIX = "/v1/workspaces/{workspaceId}/teams/{teamId}";

/** Пути, у которых контекст рабочего пространства и команды стоит в адресе. */
export type ContextPath = Extract<
  keyof paths,
  `${typeof CONTEXT_PREFIX}${string}`
>;

type PathRest<T> = [keyof T] extends [never] ? object : { path: T };

/**
 * Аргументы контекстной формы — аргументы операции без двух сегментов, которые
 * подставляет контекст. Остальные (`provider`, `userId`) остаются обязательными.
 */
type WithoutContextParams<A> = A extends { path: infer P }
  ? Omit<A, "path"> & PathRest<Omit<P, "workspaceId" | "teamId">>
  : A;

export type ContextArgs<
  P extends ContextPath,
  M extends MethodsOf<paths[P]>,
> = WithoutContextParams<OperationCallArgs<paths, P, M>>;

/** Сегменты контекста для подстановки в путь, либо `null` — контекст не задан. */
const contextPathParams = (): {
  workspaceId: string;
  teamId: string;
} | null => {
  const { workspaceId, teamId } = getAppContext();
  if (!workspaceId || !teamId) return null;

  return { workspaceId: String(workspaceId), teamId: String(teamId) };
};

/**
 * Адрес без сегмента контекста: та же легаси-форма, что отдавал
 * `usersPathWithContext` при незаданном контексте. В контракте её нет —
 * бекенд ответит на неё ошибкой, — но менять поведение в этом слайсе нельзя:
 * фронт стоит в проде у заказчика, а на 401 отсюда завязан редирект на логин.
 */
const withoutContextSegment = (path: ContextPath): string =>
  path.replace(CONTEXT_PREFIX, "/v1");

const warnNoContext = (path: string) =>
  console.warn("Users context not set, request may fail:", path);

/**
 * Вызов операции в контекстной форме адреса: сегменты рабочего пространства и
 * команды берутся из контекста приложения, остальные аргументы — как у операции.
 */
export const requestInContext = <
  P extends ContextPath,
  M extends MethodsOf<paths[P]>,
>(
  path: P,
  method: M,
  args: ContextArgs<P, M>
): Promise<OperationCallResult<paths, P, M>> => {
  const context = contextPathParams();
  const { path: pathParams, ...rest } = args as { path?: object };

  if (!context) {
    warnNoContext(path);
    // Единственное приведение в границе: у легаси-адреса нет записи в контракте,
    // операция и её аргументы при этом те же — меняется только шаблон пути, из
    // которого пропали оба сегмента контекста.
    const untyped = request as unknown as (
      path: string,
      method: string,
      args: unknown
    ) => Promise<OperationCallResult<paths, P, M>>;

    return untyped(withoutContextSegment(path), method, args);
  }

  return request(path, method, {
    ...rest,
    path: { ...pathParams, ...context },
  } as unknown as OperationCallArgs<paths, P, M>);
};

/* ── Адреса, которые нужны строкой ─────────────────────────────────────────── */

/**
 * Адрес операции строкой — для случаев, где запрос делает не axios, а браузер
 * (содержимое аватара уезжает в `src` картинки). Шаблон и здесь берётся из
 * контракта, а префикс модуля — из инстанса.
 */
export const urlInContext = <P extends ContextPath>(
  path: P,
  params: Record<string, string | number> = {}
): string => {
  const context = contextPathParams();

  if (!context) {
    warnNoContext(path);
    return `${USERS_API_PREFIX}${buildOperationPath(
      withoutContextSegment(path),
      params
    )}`;
  }

  return `${USERS_API_PREFIX}${buildOperationPath(path, {
    ...params,
    ...context,
  })}`;
};
