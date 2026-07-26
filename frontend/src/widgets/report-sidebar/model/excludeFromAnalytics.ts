import { createEffect } from "effector";
import { appApi } from "@/shared/api";

/**
 * Effector-модель тогла «исключить репорт из аналитики».
 *
 * PATCH /v2/reports/{id} с телом `{ is_excluded_from_analytics: boolean }`.
 * Поле в snake_case — это намеренно: интерсептор `convertObjectToSnake` всё
 * равно приведёт его в snake_case (идемпотентно), но мы держим его
 * совпадающим с контрактом (см. `shared/api/generated/reports.d.ts`).
 */

type ToggleArgs = {
  reportId: number;
  value: boolean;
};

export const toggleExcludeFromAnalyticsFx = createEffect<ToggleArgs, void>(
  async ({ reportId, value }) => {
    await appApi.patch(`/v2/reports/${reportId}`, {
      is_excluded_from_analytics: value,
    });
  }
);

export const $isPending = toggleExcludeFromAnalyticsFx.pending;
