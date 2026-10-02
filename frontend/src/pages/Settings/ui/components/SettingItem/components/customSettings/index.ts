import { KaitenBoardsSelect } from "./KaitenBoardsSelect";
import { ComponentType } from "react";
import type { CustomSettingProps } from "./types";

export type { CustomSettingProps };

// Тип для компонента кастомной настройки
type CustomSettingComponent = ComponentType<CustomSettingProps>;

// Реестр кастомных компонентов: ключ = "sectionId.settingId"
type CustomSettingsRegistry = Record<string, CustomSettingComponent>;

// Реестр кастомных компонентов для настроек
// Ключ: "sectionId.settingId"
export const customSettingsRegistry: CustomSettingsRegistry = {
  "kaiten.board_ids": KaitenBoardsSelect,
};

// Хелпер для получения кастомного компонента
export const getCustomSettingComponent = (
  sectionId: string,
  settingId: string
) => {
  const key = `${sectionId}.${settingId}`;
  return customSettingsRegistry[key] || null;
};
