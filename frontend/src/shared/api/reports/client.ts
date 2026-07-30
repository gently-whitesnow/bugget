import type { paths } from "@/shared/api/generated/reports";
import { appApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

/**
 * Ядро единственной транспортной границы модуля `reports`.
 *
 * Ручки объявлены рядом, по ресурсам: `report.ts`, `links.ts`, `bugs.ts`,
 * `steps.ts`, `comments.ts`, `attachments.ts`. Каждая — операция контракта: ключ
 * пути из `paths` плюс метод, объявленный у этого пути. Тип тела и тип ответа
 * выведены из той же операции, поэтому смена схемы ответа, метода или пути в
 * `specs/contracts/reports/openapi.yaml` ломает компиляцию здесь, а не отвечает
 * 404 у заказчика.
 *
 * Прямых `appApi.get('/v2/reports/...')` вне этого каталога быть не должно — за
 * этим следит правило линтера `no-restricted-syntax` (гейт `frontend-lint`),
 * краснота которого закрыта тестом `transportBoundary.gate.test.ts`.
 *
 * Регистры и сериализация живут в `shared/api/operation.ts`: тела в коде
 * camelCase, на проводе snake_case; query и path — как в контракте; массив в
 * query уходит повторяющимся ключом; multipart не конвертируется.
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
