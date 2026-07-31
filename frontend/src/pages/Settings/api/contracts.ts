import type { externalApi, settingsApi } from "@/shared/api";

/**
 * Формы модуля `settings` — выведены из операций его границы
 * (`shared/api/settings`), то есть из `specs/contracts/settings/openapi.yaml`
 * вместе с путём и методом. Рукописного DTO здесь нет: второе независимое
 * представление тех же данных расходилось бы с контрактом молча.
 *
 * Регистр здесь уже camelCase: тело перекладывает интерсептор
 * (`shared/api/instances/base.ts`), и это учтено в типах операций (ADR-0009).
 *
 * Уровни настроек (workspace / team / user) описаны в контракте одной схемой
 * `Setting` и одной `SettingsSection` — трёх алиасов на один и тот же тип здесь
 * нет намеренно, они не несли информации. Уровень выбирается ручкой, а не типом.
 */

/** Ответ `GET /v1/settings-sections`: секции всех трёх уровней. */
export type SettingsSectionsResponse = settingsApi.SettingsSectionsResult;

/** Раздел настроек одного уровня. */
export type SettingsSectionView =
  SettingsSectionsResponse["workspaceSections"][number];

/** Настройка с текущим значением. `description` на проводе nullable. */
export type SettingView = SettingsSectionView["settings"][number];

/**
 * Тело PUT-ручек обновления настройки: всегда массив строк — скаляр это массив
 * из одного элемента, булева настройка — строка `true`/`false`.
 */
export type SettingValues = settingsApi.SettingValuesBody;

/**
 * Доска Kaiten: внешняя интеграция, её контракт лежит в модуле `external`, и
 * форма берётся из его операции.
 */
export type KaitenBoardResponse = externalApi.KaitenBoard;
