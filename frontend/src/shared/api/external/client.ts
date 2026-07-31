import type { paths } from "@/shared/api/generated/external";
import { appApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

/**
 * Ядро единственной транспортной границы модуля `external`.
 *
 * Ручки объявлены рядом, по ресурсам: `search.ts` (поиск по внешним источникам и
 * привязка найденного к репорту), `kaitenBoards.ts` (справочник досок). Каждая —
 * операция контракта: ключ пути из `paths` плюс метод, объявленный у этого пути.
 * Тело, query и тип ответа выведены из той же операции, поэтому правка
 * `specs/contracts/external/openapi.yaml` ломает компиляцию здесь, а не отвечает
 * 404 у заказчика.
 *
 * Прямых `appApi.get("/v1/external/...")` вне этого каталога быть не должно — за
 * этим следит правило линтера `no-restricted-syntax` (гейт `frontend-lint`),
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
