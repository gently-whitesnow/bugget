import { createEffect } from "effector";
import { reportsApi } from "@/shared/api";

/**
 * Effector-модель тогла «исключить репорт из аналитики».
 *
 * Вызывает операцию `PATCH /v2/reports/{aliasId}` из `shared/api/reports`: путь,
 * метод и форма тела приходят из контракта. Тело пишется в camelCase, как и весь
 * код фронта, — `is_excluded_from_analytics` на проводе делает интерсептор
 * (ADR-0009).
 */

type ToggleArgs = {
  reportId: number;
  value: boolean;
};

export const toggleExcludeFromAnalyticsFx = createEffect<ToggleArgs, void>(
  async ({ reportId, value }) => {
    await reportsApi.patchReport(String(reportId), {
      isExcludedFromAnalytics: value,
    });
  }
);

export const $isPending = toggleExcludeFromAnalyticsFx.pending;
