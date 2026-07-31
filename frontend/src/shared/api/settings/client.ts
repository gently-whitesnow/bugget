import type { paths } from "@/shared/api/generated/settings";
import { appApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

/**
 * Ядро единственной транспортной границы модуля `settings`.
 *
 * Ручки объявлены рядом (`settings.ts`) как операции контракта: ключ пути из
 * `paths` плюс метод, объявленный у этого пути. Тело и тип ответа выведены из
 * той же операции, поэтому правка `specs/contracts/settings/openapi.yaml` ломает
 * компиляцию здесь.
 *
 * До этого слайса у модуля был свой дескриптор операции (`settingsRoutes`,
 * `SettingsMethod`, `callContract`) — второе представление той же механики рядом
 * с общей границей `shared/api/operation.ts`. Два типовых пути вызова расходятся
 * молча, поэтому module-local остался ровно один: сами ручки.
 *
 * Прямых `appApi.request({ url: "/v1/settings-sections" })` вне этого каталога
 * быть не должно — за этим следит правило линтера `no-restricted-syntax`
 * (гейт `frontend-lint`), краснота которого закрыта тестом
 * `transportBoundary.gate.test.ts`.
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
