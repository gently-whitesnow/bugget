import type { components } from "@/shared/api/generated/settings";
import type { Camelized } from "@/shared/lib/types";

/**
 * Формы модуля `settings` — выведены из контракта
 * (`specs/contracts/settings/openapi.yaml` → `shared/api/generated/settings.d.ts`).
 *
 * `Camelized` учитывает case-conversion интерсептор
 * (`shared/api/instances/base.ts`): провод — snake_case (`is_array`, `is_bool`,
 * `workspace_sections`), код читает camelCase (ADR-0009). Рукописного DTO здесь
 * больше нет: второе независимое представление тех же данных расходилось бы с
 * контрактом молча.
 *
 * Уровни настроек (workspace / team / user) описаны в контракте одной схемой
 * `Setting` и одной `SettingsSection` — трёх алиасов на один и тот же тип здесь
 * нет намеренно, они не несли информации. Уровень выбирается ручкой, а не типом.
 */

/** Настройка с текущим значением. `description` на проводе nullable. */
export type SettingView = Camelized<components["schemas"]["Setting"]>;

/** Раздел настроек одного уровня. */
export type SettingsSectionView = Camelized<
  components["schemas"]["SettingsSection"]
>;

/** Ответ `GET /v1/settings-sections`: секции всех трёх уровней. */
export type SettingsSectionsResponse = Camelized<
  components["schemas"]["SettingsSections"]
>;

/**
 * Тело PUT-ручек обновления настройки: всегда массив строк — скаляр это массив
 * из одного элемента, булева настройка — строка `true`/`false`. Имён полей внутри
 * нет, поэтому `Camelized` здесь не нужен.
 */
export type SettingValues = components["schemas"]["SettingValues"];

/**
 * Kaiten — внешняя интеграция, её контракт лежит в модуле `external` и переводится
 * отдельным слайсом. До тех пор DTO остаётся рукописным.
 */
export type KaitenBoardResponse = {
  id: number;
  title: string;
};
