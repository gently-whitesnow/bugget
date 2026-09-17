import type { paths } from "@/shared/api/generated/settings";
import { appApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";

// Ядро единственной транспортной границы `settings`: ручки — операции контракта
// (`paths` + метод), правка контракта ломает компиляцию. Прямые `appApi.*` вне
// каталога запрещены линтером (см. `transportBoundary.gate.test.ts`).
export const request = createOperationRequest<paths>(appApi);

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
