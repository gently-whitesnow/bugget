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

// Ядро единственной транспортной границы `users`: ручки — операции контракта
// (`paths` + метод), правка контракта ломает компиляцию. Прямые `usersApi.*` вне
// каталога запрещены линтером (см. `transportBoundary.gate.test.ts`).
export const request = createOperationRequest<paths>(usersApi);

export type Body<
  P extends keyof paths,
  M extends MethodsOf<paths[P]>,
> = OperationBody<paths, P, M>;

export type Result<
  P extends keyof paths,
  M extends MethodsOf<paths[P]>,
> = OperationResponse<paths, P, M>;

export type Query<
  P extends keyof paths,
  M extends MethodsOf<paths[P]>,
> = OperationQuery<paths, P, M>;

// Две формы одного ключа `paths`: короткая (сегменты аргументами) и контекстная
// (сегменты из `getAppContext`). Ручка их игнорирует — identity важнее.
const CONTEXT_PREFIX = "/v1/workspaces/{workspaceId}/teams/{teamId}";

export type ContextPath = Extract<
  keyof paths,
  `${typeof CONTEXT_PREFIX}${string}`
>;

type PathRest<T> = [keyof T] extends [never] ? object : { path: T };

type WithoutContextParams<A> = A extends { path: infer P }
  ? Omit<A, "path"> & PathRest<Omit<P, "workspaceId" | "teamId">>
  : A;

export type ContextArgs<
  P extends ContextPath,
  M extends MethodsOf<paths[P]>,
> = WithoutContextParams<OperationCallArgs<paths, P, M>>;

const contextPathParams = (): {
  workspaceId: string;
  teamId: string;
} | null => {
  const { workspaceId, teamId } = getAppContext();
  if (!workspaceId || !teamId) return null;

  return { workspaceId: String(workspaceId), teamId: String(teamId) };
};

// Легаси-адрес без сегмента контекста: в контракте его нет, но поведение менять
// нельзя — на 401 отсюда завязан редирект на логин у заказчика.
const withoutContextSegment = (path: ContextPath): string =>
  path.replace(CONTEXT_PREFIX, "/v1");

const warnNoContext = (path: string) =>
  console.warn("Users context not set, request may fail:", path);

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
    // Единственное приведение: у легаси-адреса нет записи в контракте.
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

/** Адрес операции строкой — для запросов браузера (`src` аватара). */
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
