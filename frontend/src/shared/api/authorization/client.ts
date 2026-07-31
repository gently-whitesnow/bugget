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
 * Ядро единственной транспортной границы модуля `authorization`.
 *
 * Ручка у модуля одна — выход из системы (`session.ts`), и она описана как
 * операция контракта: ключ пути из `paths` плюс метод, объявленный у этого пути.
 * Тип ответа выведен из той же операции, поэтому правка
 * `specs/contracts/authorization/openapi.yaml` ломает компиляцию здесь.
 *
 * Префикс модуля (`/api/authorization`) дописывает интерсептор инстанса
 * (`shared/api/instances/authorization.ts`), а не call-site: адрес на проводе
 * тот же, что и до миграции.
 *
 * Прямых `authorizationApi.post("/api/authorization/v1/...")` вне этого каталога
 * быть не должно — за этим следит правило линтера `no-restricted-syntax`
 * (гейт `frontend-lint`), краснота которого закрыта тестом
 * `transportBoundary.gate.test.ts`.
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
