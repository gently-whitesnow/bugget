import type { paths } from "@/shared/api/generated/reports";
import { appApi } from "@/shared/api/instances";
import { createOperationRequest } from "@/shared/api/operation";
import type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "@/shared/api/operation";
import { validateReportsResponseEnums } from "./validateResponseEnums";

// Ядро единственной транспортной границы `reports`: ручки — операции контракта
// (`paths` + метод), смена схемы ломает компиляцию. Прямые `appApi.*` вне
// каталога запрещены линтером (см. `transportBoundary.gate.test.ts`).
export const request = createOperationRequest<paths>(
  appApi,
  validateReportsResponseEnums
);

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
