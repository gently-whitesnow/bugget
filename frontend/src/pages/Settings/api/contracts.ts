import type { externalApi, settingsApi } from "@/shared/api";

/**
 * Формы модуля `settings` выведены из операций `shared/api/settings`
 * (`specs/contracts/settings/openapi.yaml`); регистр уже camelCase (ADR-0009).
 * Уровень (workspace / team / user) выбирается ручкой, а не типом.
 */

export type SettingsSectionsResponse = settingsApi.SettingsSectionsResult;

export type SettingsSectionView =
  SettingsSectionsResponse["workspaceSections"][number];

/** `description` на проводе nullable. */
export type SettingView = SettingsSectionView["settings"][number];

/** Всегда массив строк: скаляр — один элемент, булево — `true`/`false`. */
export type SettingValues = settingsApi.SettingValuesBody;

/** Доска Kaiten — интеграция; форма берётся из модуля `external`. */
export type KaitenBoardResponse = externalApi.KaitenBoard;
