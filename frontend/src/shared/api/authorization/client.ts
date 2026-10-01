import type { paths } from "@/shared/api/generated/authorization";
import { authorizationApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

/**
 * Единственная транспортная граница модуля `authorization`: типы выведены из
 * операции контракта, префикс `/api/authorization` дописывает интерсептор
 * инстанса. Прямые `authorizationApi.post(...)` вне каталога ловит линтер.
 */
export const request = createOperationRequest<paths>(authorizationApi);

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
