import type { paths } from "@/shared/api/generated/analytics";
import { appApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

/**
 * Ядро единственной транспортной границы модуля `analytics`.
 *
 * Ручки объявлены рядом (`analytics.ts`) как операции контракта: ключ пути из
 * `paths` плюс метод, объявленный у этого пути. Query и тип ответа выведены из
 * той же операции, поэтому правка `specs/contracts/analytics/openapi.yaml`
 * ломает компиляцию здесь, а не отвечает 400 у заказчика.
 *
 * Detail по репорту (`/v2/reports/{id}/analytics`) — sub-resource модуля
 * `reports` и живёт в его границе; здесь он только переэкспортируется.
 *
 * Прямых `appApi.get("/v2/analytics/...")` вне этого каталога быть не должно —
 * за этим следит правило линтера `no-restricted-syntax` (гейт `frontend-lint`),
 * краснота которого закрыта тестом `transportBoundary.gate.test.ts`.
 */
export const request = createOperationRequest<paths>(appApi);

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
