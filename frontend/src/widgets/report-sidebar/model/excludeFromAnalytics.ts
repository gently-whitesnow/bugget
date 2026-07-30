import { createEffect } from "effector";
import { appApi } from "@/shared/api";

/**
 * Effector-модель тогла «исключить репорт из аналитики».
 *
 * PATCH /v2/reports/{id}. Тело пишется в camelCase, как и весь код фронта:
 * `is_excluded_from_analytics` на проводе делает интерсептор
 * (`shared/api/instances/base.ts`). Рукописный snake_case здесь означал бы
 * второе, независимое от контракта представление тела запроса.
 */

type ToggleArgs = {
  reportId: number;
  value: boolean;
};

export const toggleExcludeFromAnalyticsFx = createEffect<ToggleArgs, void>(
  async ({ reportId, value }) => {
    await appApi.patch(`/v2/reports/${reportId}`, {
      isExcludedFromAnalytics: value,
    });
  }
);

export const $isPending = toggleExcludeFromAnalyticsFx.pending;
