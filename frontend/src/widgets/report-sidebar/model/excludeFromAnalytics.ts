import { createEffect } from "effector";
import { appApi } from "@/shared/api";
import type { PatchReportRequest } from "@/entities/report";

/**
 * Effector-модель тогла «исключить репорт из аналитики».
 *
 * PATCH /v2/reports/{id}. Тело пишется в camelCase, как и весь код фронта:
 * `is_excluded_from_analytics` на проводе делает интерсептор
 * (`shared/api/instances/base.ts`). Форма тела — из контракта
 * (`ReportPatchRequest`): рукописный объект здесь был бы вторым, независимым от
 * yaml представлением запроса, а опечатка в имени поля прошла бы как «не трогать
 * значение».
 */

type ToggleArgs = {
  reportId: number;
  value: boolean;
};

export const toggleExcludeFromAnalyticsFx = createEffect<ToggleArgs, void>(
  async ({ reportId, value }) => {
    const request: PatchReportRequest = { isExcludedFromAnalytics: value };
    await appApi.patch(`/v2/reports/${reportId}`, request);
  }
);

export const $isPending = toggleExcludeFromAnalyticsFx.pending;
